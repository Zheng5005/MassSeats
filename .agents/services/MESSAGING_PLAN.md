# MassSeats — Plan de Implementación de la Mensajería (RabbitMQ + Saga)

> Fase final del backend: integrar los 4 microservicios con RabbitMQ crudo
> (`RabbitMQ.Client`), usando **coreografía** (sin orquestador central),
> **Outbox** para no perder eventos e **Inbox/idempotencia** para no duplicarlos.
>
> Complemento de `BACKEND_PLAN.md`. Los 4 servicios ya funcionan solos
> (CRUD + DB, probados). Este documento cubre SOLO la capa de mensajería.
>
> **Estado (actualizado 2026-08-02):** fases 0–6 IMPLEMENTADAS, fase 7
> verificada con la suite de tests automatizados (36/36 ✅). Solo queda la
> prueba manual end-to-end con Stripe CLI (ver sección 6, fase 7).

---

## 0. Objetivo y alcance

Conectar el flujo de negocio distribuido: una reserva dispara un cobro, el
cobro confirma o cancela la reserva, y el catálogo refleja la disponibilidad
de asientos — todo por eventos asincrónicos, con **consistencia eventual** y
sin transacciones ACID que crucen servicios.

**Regla de oro (de BACKEND_PLAN.md):** por el bus viajan SOLO contratos de
mensajes y utilidades técnicas puras. NUNCA lógica de negocio. Cada dominio
es soberano.

---

## 1. Estado actual — qué YA existe y qué falta

### ✅ Ya construido (BuildingBlocks.Messaging)

**Contratos (integration events)** — todos definidos, heredan de
`IntegrationEvent` (que ya trae `Id` para idempotencia y `OccurredOn`):

| Contrato | Publica | Consumen |
|----------|---------|----------|
| `EventCreated` | Event | Booking *(opcional, réplica tardía — NO implementado)* |
| `EventUpdated` | Event | Booking *(opcional, réplica tardía — NO implementado)* |
| `EventCancelled` | Event | Booking *(opcional, réplica tardía — NO implementado)* |
| `SeatReserved` | Booking | Payment, Event |
| `ReservationConfirmed` | Booking | Event |
| `ReservationCancelled` | Booking | Event |
| `ReservationExpired` | Booking | Event |
| `PaymentSucceeded` | Payment | Booking |
| `PaymentFailed` | Payment | Booking |

**Abstracciones** (`BuildingBlocks.Messaging.Abstractions`):
- `IEventPublisher.PublishAsync<TEvent>(...)`
- `IEventBus : IEventPublisher` + `SubscribeAsync<TEvent>(...)`
- `IEventConsumer<in TEvent>.HandleAsync(...)`

**Domain events (in-process) listos para traducir a integration events:**
- Booking: `ReservationCreatedDomainEvent`, `ReservationConfirmedDomainEvent`,
  `ReservationCancelledDomainEvent`, `ReservationExpiredDomainEvent`.
- Payment: `PaymentSucceededDomainEvent`, `PaymentFailedDomainEvent`
  (+ `PaymentInitiatedDomainEvent`, que es interno y NO sale del servicio).

### ✅ Construido (implementación completa, fases 0–6)

1. **`BuildingBlocks.Messaging.RabbitMQ`** — implementación concreta de
   `IEventBus`/`IEventPublisher`, gestión de conexión, topología (exchange +
   colas + bindings + retry + DLQ) y el host de consumo
   (`RabbitMqConsumerHostedService`). Tests de integración reales en
   `BuildingBlocks.Messaging.RabbitMQ.Tests` (ping, retry, DLQ) ✅.
2. **Outbox pattern** — `OutboxSaveChangesInterceptor` + `OutboxPublisherWorker`
   + entidad/config EF, por servicio publicador (Booking, Payment, Event) ✅.
3. **Inbox pattern** — entidad + dedup por `messageId`, por servicio consumidor
   (Booking, Payment, Event) ✅.
4. **Consumers concretos** — cada servicio implementa `IEventConsumer<>` de lo
   que le importa (ver sección 4.5) ✅.
5. **Deuda bloqueante en EventService** — resuelta (ver sección 5) ✅.

---

## 2. Topología RabbitMQ

Un **topic exchange** durable. Cada servicio tiene UNA cola durable, bindeada a
las routing keys que le interesan. Cada cola tiene su **retry exchange**
(`massseats.events.retry`) y su **DLQ** vía dead-letter exchange
(`massseats.events.dead-letter`). ✅ Implementado.

```
                     ┌────────────────────────────┐
   publishers ──────▶│  exchange: massseats.events │ (topic, durable)
                     └───────────┬────────────────┘
        ┌───────────────┬────────┼────────────────┬──────────────┐
        ▼               ▼        ▼                 ▼              ▼
  event.created    seat.reserved  reservation.*   payment.*   (routing keys)
        │               │            │               │
        ▼               ▼            ▼               ▼
  ┌───────────┐   ┌───────────┐  ┌───────────┐  ┌───────────┐
  │booking.q  │   │payment.q  │  │ event.q   │  │booking.q  │
  └─────┬─────┘   └─────┬─────┘  └─────┬─────┘  └───────────┘
        │ (falla N veces)                │
        ▼                                ▼
  ┌───────────┐                    ┌───────────┐
  │booking.dlq│  ...               │ event.dlq │   (dead-letter queues)
  └───────────┘                    └───────────┘
```

### Routing keys (convención: minúsculas, punto)

| Contrato | Routing key |
|----------|-------------|
| `EventCreated` / `EventUpdated` / `EventCancelled` | `event.created` / `event.updated` / `event.cancelled` |
| `SeatReserved` | `seat.reserved` |
| `ReservationConfirmed` / `ReservationCancelled` / `ReservationExpired` | `reservation.confirmed` / `reservation.cancelled` / `reservation.expired` |
| `PaymentSucceeded` / `PaymentFailed` | `payment.succeeded` / `payment.failed` |

### Colas y bindings

| Cola | Bindea (routing keys) | Consumidor |
|------|-----------------------|------------|
| `payment.queue` | `seat.reserved` | PaymentService |
| `booking.queue` | `payment.succeeded`, `payment.failed`, `event.*` (opcional) | BookingService |
| `event.queue` | `seat.reserved`, `reservation.confirmed`, `reservation.cancelled`, `reservation.expired` | EventService |

> UserService NO participa de la saga (no publica ni consume). Queda afuera.

---

## 3. La saga completa (flujo end-to-end, coreografía)

```
 Usuario     Booking          RabbitMQ         Payment          Stripe        Event
   │ POST      │                  │                │              │             │
   │──reserva─▶│ crea Pending     │                │              │             │
   │           │ (unique constr.) │                │              │             │
   │           │──outbox─▶SeatReserved──┬──────────▶│ InitiatePayment            │
   │           │                  │     └───────────────────────────────────────▶│ decrementa
   │           │                  │                │──crea PaymentIntent─▶│       │ availableSeats
   │           │                  │                │              │       │       │
   │           │                  │       ...webhook Stripe (firma verificada)...  │
   │           │                  │                │◀─payment_intent.succeeded─────│
   │           │                  │                │ Succeed()    │              │
   │           │◀─PaymentSucceeded─┤◀──outbox───────│              │              │
   │           │ Confirm()        │                │              │              │
   │           │──outbox─▶ReservationConfirmed──────────────────────────────────▶│ confirma
   │           │                  │                │              │              │
   │  (si falla: PaymentFailed → Booking.Cancel → ReservationCancelled → Event libera)
   │  (si expira: worker → ReservationExpired → Event libera)                     │
```

**Las tres "patas" del saga:**
1. **Booking → Payment**: `SeatReserved` inicia el cobro.
2. **Payment → Booking**: `PaymentSucceeded`/`PaymentFailed` confirma/cancela.
3. **Booking/Payment → Event**: `SeatReserved` + `Reservation*` ajustan `availableSeats`.

---

## 4. Componentes construidos

### 4.1 `BuildingBlocks.Messaging.RabbitMQ` ✅ implementado

Implementación concreta y reutilizable. Contiene:

- **`RabbitMqConnection`** — conexión/canal singleton, resiliente (reconexión).
- **`RabbitMqOptions`** — host, port, user, pass, vhost, exchange name (bind de config).
- **`TopologyInitializer`** — declara exchange, colas, bindings y DLQ (idempotente,
  se corre al arrancar cada servicio para SUS colas).
- **`RabbitMqEventBus : IEventBus`** — `PublishAsync` serializa el evento a JSON,
  publica al exchange con la routing key correspondiente y el `MessageId` en las
  propiedades AMQP (`messageId`, `type`, `content-type: application/json`, persistente).
- **`RabbitMqConsumerHostedService`** — `BackgroundService` que consume de la cola
  del servicio, crea un **scope de DI por mensaje**, resuelve el `IEventConsumer<>`
  correcto (por `type`), pasa por el Inbox (dedup) y hace **ack manual** al terminar
  (o `nack` → DLQ si falla).
- **Registro DI**: `AddRabbitMqMessaging(config)` + `AddEventConsumer<TEvent, THandler>()`.

> 🎓 Concepto: `PublishAsync` NO habla con RabbitMQ desde el caso de uso —
> el caso de uso escribe al **Outbox**. Es el worker del Outbox el que llama a
> `IEventPublisher`. Así la publicación es atómica con el cambio de negocio.

### 4.2 Outbox pattern ✅ implementado (reutilizable, por servicio)

**Tabla `outbox_messages`** (en la DB de cada servicio publicador):

| Columna | Tipo | Nota |
|---------|------|------|
| `id` | uuid PK | = `IntegrationEvent.Id` |
| `type` | text | nombre del contrato (ej. `SeatReserved`) |
| `content` | jsonb | evento serializado |
| `occurred_on` | timestamptz | orden de publicación |
| `processed_on` | timestamptz null | null = pendiente |
| `attempts` | int | reintentos de publicación |
| `error` | text null | último error |

**Mecanismo (2 piezas):**

1. **`OutboxSaveChangesInterceptor`** (EF Core): antes de guardar, drena
   `ChangeTracker.Entries<AggregateRoot>()` → junta `DomainEvents` → los traduce a
   integration events (mapper por servicio, sección 4.4) → inserta filas en
   `outbox_messages` **en la MISMA transacción** que el cambio de negocio → limpia
   los domain events. **Atomicidad garantizada.**
2. **`OutboxPublisherWorker`** (`BackgroundService`): cada N segundos lee filas con
   `processed_on IS NULL` (orden por `occurred_on`), publica cada una vía
   `IEventPublisher`, marca `processed_on`. Si falla, incrementa `attempts` y guarda
   `error` (reintenta el próximo tick).

> 🎓 Por qué NO publicar directo desde el caso de uso: si guardás en la DB y
> justo se cae antes de publicar a RabbitMQ, **perdés el evento** y el saga queda
> colgado. El Outbox convierte "guardar + publicar" en una sola operación atómica
> (guardar), y desacopla la publicación real.

### 4.3 Inbox pattern ✅ implementado (reutilizable, por servicio consumidor)

**Tabla `inbox_messages`** (en la DB de cada servicio consumidor):

| Columna | Tipo | Nota |
|---------|------|------|
| `message_id` | uuid PK | = `IntegrationEvent.Id` entrante |
| `type` | text | contrato |
| `processed_on` | timestamptz | cuándo se procesó |

**Mecanismo:** el consumer, dentro de una transacción del DbContext:
1. ¿`message_id` ya está en `inbox_messages`? → sí: **ack y salir** (duplicado).
2. No: ejecutar el handler (cambio de negocio) + insertar fila inbox + `SaveChanges`
   (todo en la misma transacción) → **ack**.
3. Excepción → rollback → **nack** (reintento/DLQ).

> ⚠️ **Dos idempotencias distintas, no confundir:**
> - **Inbox (bus)**: dedup por `IntegrationEvent.Id` para mensajes de RabbitMQ.
> - **Webhook Stripe**: dedup por `StripeEventId`, es HTTP (no pasa por el bus).
>   Tiene su PROPIA tabla (`processed_stripe_events`) en el borde de Payment ✅
>   (`StripeWebhookProcessor` + migración `AddProcessedStripeEvents`), además de
>   la idempotencia por ESTADO (guard `EnsurePending`).

### 4.4 Traducción domain event → integration event ✅ implementado (por servicio)

Un mapper por servicio (ej. `IntegrationEventFactory`) que el interceptor usa:

| Servicio | Domain event | Integration event |
|----------|--------------|-------------------|
| Booking | `ReservationCreatedDomainEvent` | `SeatReserved` |
| Booking | `ReservationConfirmedDomainEvent` | `ReservationConfirmed` |
| Booking | `ReservationCancelledDomainEvent` | `ReservationCancelled` |
| Booking | `ReservationExpiredDomainEvent` | `ReservationExpired` |
| Payment | `PaymentSucceededDomainEvent` | `PaymentSucceeded` |
| Payment | `PaymentFailedDomainEvent` | `PaymentFailed` |
| Payment | `PaymentInitiatedDomainEvent` | *(interno, no se traduce)* |
| Event | `EventCreatedDomainEvent` | `EventCreated` |
| Event | `EventUpdatedDomainEvent` | `EventUpdated` |
| Event | `EventCancelledDomainEvent` | `EventCancelled` |

> Los campos alinean (por eso se agregó `Reason` a `PaymentFailedDomainEvent`,
> y ahora está persistido como `FailureReason`). Los mappers por servicio ya
> existen: `BookingIntegrationEventFactory`, `PaymentIntegrationEventFactory`,
> `EventIntegrationEventFactory`.

### 4.5 Consumers por servicio

| Servicio | Consume | Acción (caso de uso) | Estado |
|----------|---------|----------------------|--------|
| Payment | `SeatReserved` | `InitiateAsync` | ✅ implementado |
| Booking | `PaymentSucceeded` | `ConfirmAsync` | ✅ implementado |
| Booking | `PaymentFailed` | `CancelAsync` | ✅ implementado |
| Event | `SeatReserved` | `DecrementAvailability` | ✅ implementado |
| Event | `ReservationCancelled` / `ReservationExpired` | `ReleaseSeat` | ✅ implementado |
| Event | `ReservationConfirmed` | confirmar ocupación (no-op o marca) | ✅ implementado (no-op con registro de inbox) |
| Booking | `EventCreated`/`Updated`/`Cancelled` | opcional: réplica local de eventos | 🔲 pendiente (fase tardía / opcional) |

> 🎉 Los consumers de la saga en los 3 servicios ya están implementados y
> probados (Booking `PaymentConsumersTests`, Payment `SeatReservedConsumerTests`
> + integración real, Event `EventMessagingTests`). El único consumer que sigue
> abierto es la réplica opcional del catálogo en Booking.

---

## 5. Deuda bloqueante en EventService — ✅ RESUELTA

EventService usaba `AggregateRoot` pero **no levantaba domain events, no tenía
`AvailableSeats`, ni comportamiento para ajustarlo**. Resuelto en las fases 0/5:

1. ✅ **Campo `AvailableSeats`** en `Event` (int), inicializado a `TotalSeats` en
   `Create`. Migraciones: `AddEventAvailability`, `AddAvailableSeats`.
2. ✅ **Comportamiento de disponibilidad** (protege invariantes, no anémico):
   - `DecrementAvailability()` — al consumir `SeatReserved` (valida `> 0`).
   - `ReleaseSeat()` — al consumir `ReservationCancelled`/`ReservationExpired`
     (no supera `TotalSeats`).
3. ✅ **Domain events** (carpeta `Events/`): `EventCreatedDomainEvent`,
   `EventUpdatedDomainEvent`, `EventCancelledDomainEvent` + `RaiseDomainEvent(...)`
   en `Create`/`UpdateDetails`/`Cancel()`.
4. ✅ **`Cancel()`** en `Event` para poder emitir `EventCancelled`.
5. ✅ **Consumers** de Event (sección 4.5) + Inbox en la DB de Event
   (migración `AddEventMessaging`).

> Nota de consistencia eventual: `AvailableSeats` en Event es un **reflejo
> informativo**. La verdad de disponibilidad vive en Booking (unique constraint).
> Que se desincronice unos segundos es aceptable y esperado.

---

## 6. Fases de implementación (orden incremental, verificable)

Cada fase deja algo **probable**. No avanzar sin verificar la anterior.

```
Fase 0  Deuda EventService (AvailableSeats + domain events + Cancel)   ✅ HECHO
   ▼
Fase 1  BuildingBlocks.Messaging.RabbitMQ (conexión + topología +      ✅ HECHO
        publisher + consumer host). Prueba: publicar/consumir un ping.
        → RabbitMqPingTests (integración real, 4/4).
   ▼
Fase 2  Outbox en Booking (interceptor + worker + tabla + mapper).     ✅ HECHO
        Prueba: POST /reservations → SeatReserved en RabbitMQ UI.
        → cubierto por PaymentConsumersTests + factory de Booking.
   ▼
Fase 3  Inbox + consumer en Payment (SeatReserved → InitiateAsync).    ✅ HECHO
        Prueba: la reserva dispara la creación del PaymentIntent.
        → SeatReservedMessagingTests (RabbitMQ real + Postgres, 1/1).
   ▼
Fase 4  Outbox en Payment (PaymentSucceeded/Failed) + consumers en     ✅ HECHO
        Booking (Confirm/Cancel). Prueba: Stripe CLI → confirma reserva.
        → PaymentOutboxTests + PaymentConsumersTests; la prueba con
        Stripe CLI real queda para la fase 7 manual.
   ▼
Fase 5  Event: consumers (SeatReserved/Reservation*) + su Outbox       ✅ HECHO
        (EventCreated...). Prueba: availableSeats sube/baja.
        → EventMessagingTests + EventService.Domain.Tests.
   ▼
Fase 6  Retry + DLQ + hardening de mensajes envenenados.               ✅ HECHO
        → RabbitMqPingTests: retry hasta éxito + DLQ al agotar budget.
   ▼
Fase 7  Prueba end-to-end de la saga completa (happy path + fallo +    ⚠️ PARCIAL
        expiración). → Suite automatizada 36/36 ✅. Falta la prueba
        manual con Stripe CLI real (webhook payment_intent.*).
```

---

## 7. Decisiones técnicas y tradeoffs

| Decisión | Elección | Por qué |
|----------|----------|---------|
| Cliente | `RabbitMQ.Client` crudo | Aprender exchanges/colas/DLQ a fondo (decisión de BACKEND_PLAN) |
| Exchange | 1 topic durable (`massseats.events`) | Routing flexible por wildcards; un solo punto |
| Colas | 1 por servicio | Simple; el consumer despacha por `type` interno |
| Serialización | System.Text.Json | Estándar, sin dependencias extra |
| Publicación | Outbox + worker (polling) | Atomicidad; no perder eventos. Optimización futura: LISTEN/NOTIFY de Postgres |
| Entrega | At-least-once | RabbitMQ no garantiza exactly-once → consumers idempotentes (Inbox) obligatorio |
| Ack | Manual, post-proceso | Nunca perder un mensaje por ack prematuro |
| Retry | N reintentos → DLQ | Aísla mensajes envenenados sin frenar la cola |

---

## 8. Manejo de errores: retry, DLQ, idempotencia

- **Publicación (Outbox)**: fila queda `processed_on IS NULL`; el worker reintenta.
  `attempts`/`error` para diagnóstico; alertar tras N intentos.
- **Consumo**: `try` handler → éxito: `basicAck`. Error transitorio: `basicNack`
  (requeue limitado) o cabecera de reintento. Tras N fallos → a la **DLQ**
  (cola declarada con `x-dead-letter-exchange`).
- **Idempotencia**: Inbox por `messageId` (bus) + dedup por `StripeEventId` (webhook).
- **Poison messages**: quedan en la DLQ para inspección manual (ops).

---

## 9. Cómo probar

- **`docker-compose`** con RabbitMQ + management UI (puerto 15672) y los 4 Postgres.
- **Stripe CLI**: `stripe listen --forward-to localhost:<PORT>/payments/webhook`
  para recibir webhooks reales de test y obtener el `whsec_...`.
- **Flujo manual**:
  1. `POST /reservations` → ver `SeatReserved` en la RabbitMQ UI y en `outbox_messages`.
  2. Payment crea el PaymentIntent (log / `GET /payments/booking/{id}`).
  3. `stripe trigger payment_intent.succeeded` → `PaymentSucceeded` → reserva `Confirmed`.
  4. Verificar `availableSeats` decrementado en Event.
  5. Probar fallo (`payment_intent.payment_failed`) y expiración (esperar el hold).

---

## 10. Checklist por servicio

**BuildingBlocks**
- [x] Proyecto `BuildingBlocks.Messaging.RabbitMQ` (conexión, opciones, topología).
- [x] `RabbitMqEventBus`, `RabbitMqConsumerHostedService`, registro DI.
- [x] Outbox (interceptor, worker, entidad, config EF — replicado por servicio).
- [x] Inbox (entidad, config EF, helper de dedup).

**EventService** (más trabajo — deuda + saga)
- [x] `AvailableSeats` + `DecrementAvailability`/`ReleaseSeat`/`Cancel` + migración.
- [x] Domain events + mapper a `EventCreated/Updated/Cancelled`.
- [x] Outbox (interceptor + worker + tabla).
- [x] Consumers `SeatReserved`, `Reservation*` + Inbox.

**BookingService**
- [x] Outbox (interceptor + worker + tabla + mapper → `SeatReserved`, `Reservation*`).
- [x] Consumers `PaymentSucceeded` (→Confirm), `PaymentFailed` (→Cancel) + Inbox.
- [x] El worker de expiración emite el domain event y el Outbox lo traduce
      (`ReservationExpired`).

**PaymentService**
- [x] Consumer `SeatReserved` (→InitiateAsync) + Inbox.
- [x] Outbox (interceptor + worker + tabla + mapper → `PaymentSucceeded/Failed`).
- [x] Tabla `processed_stripe_events` para dedup de webhooks por `StripeEventId`.

---

## 11. TODOs arrastrados — estado 2026-08-02

- [x] **Payment `Reason` persistido**: resuelto. `FailureReason` como columna en
  `Payment` (migración `AddPaymentFailureReason`) y expuesto en `GET /payments/{id}`.
  El `Reason` llega completo al `PaymentFailed`.
- [x] **Inbox por `StripeEventId`**: resuelto. Tabla `processed_stripe_events`
  (migración `AddProcessedStripeEvents`) + `StripeWebhookProcessor` con dedup,
  probado en `StripeWebhookProcessorTests`.
- [x] **Race en `InitiateAsync`**: resuelto. Al consumir `SeatReserved` concurrente,
  el `DbUpdateException` (23505) se atrapa y devuelve el pago existente.
  Probado en `PaymentInitiateRaceTests`.
- [ ] **Booking réplica de eventos**: sigue abierto (fase tardía / opcional).
  Booking NO consume `EventCreated/Updated/Cancelled` hoy; le alcanza con los
  datos que vienen en el request de reserva.
```
