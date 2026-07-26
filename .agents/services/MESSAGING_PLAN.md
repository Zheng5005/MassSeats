# MassSeats — Plan de Implementación de la Mensajería (RabbitMQ + Saga)

> Fase final del backend: integrar los 4 microservicios con RabbitMQ crudo
> (`RabbitMQ.Client`), usando **coreografía** (sin orquestador central),
> **Outbox** para no perder eventos e **Inbox/idempotencia** para no duplicarlos.
>
> Complemento de `BACKEND_PLAN.md`. Los 4 servicios ya funcionan solos
> (CRUD + DB, probados). Este documento cubre SOLO la capa de mensajería.

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
| `EventCreated` | Event | Booking |
| `EventUpdated` | Event | Booking |
| `EventCancelled` | Event | Booking |
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

### 🔲 Falta construir (el grueso de este plan)

1. **`BuildingBlocks.Messaging.RabbitMQ`** — proyecto nuevo con la implementación
   concreta de `IEventBus`/`IEventPublisher`, gestión de conexión, topología y
   el host de consumo. Hoy NO hay implementación de RabbitMQ en ningún lado.
2. **Outbox pattern** — tabla + interceptor de EF + worker publicador, por servicio.
3. **Inbox pattern** — tabla + dedup por `messageId`, por servicio consumidor.
4. **Consumers concretos** — cada servicio implementa `IEventConsumer<>` de lo que
   le importa.
5. **Deuda bloqueante en EventService** (ver sección 5).

---

## 2. Topología RabbitMQ

Un **topic exchange** durable. Cada servicio tiene UNA cola durable, bindeada a
las routing keys que le interesan. Cada cola tiene su **DLQ** vía dead-letter
exchange.

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

## 4. Componentes a construir

### 4.1 `BuildingBlocks.Messaging.RabbitMQ` (proyecto nuevo)

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

### 4.2 Outbox pattern (reutilizable, por servicio)

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

### 4.3 Inbox pattern (reutilizable, por servicio consumidor)

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
>   Necesita su PROPIA tabla (`processed_stripe_events`) en el borde de Payment.
>   Hoy Payment usa idempotencia por ESTADO (guard `EnsurePending`); esto lo
>   endurece para el caso de eventos Stripe que no mapean a transición de estado.

### 4.4 Traducción domain event → integration event (por servicio)

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

> Los campos ya alinean (por eso agregué `Reason` a `PaymentFailedDomainEvent`).
> Los de Event hay que CREARLOS (ver sección 5).

### 4.5 Consumers por servicio

| Servicio | Consume | Acción (caso de uso que YA existe o falta) |
|----------|---------|--------------------------------------------|
| Payment | `SeatReserved` | `InitiateAsync` ✅ (ya existe) |
| Booking | `PaymentSucceeded` | `ConfirmAsync` ✅ (ya existe) |
| Booking | `PaymentFailed` | `CancelAsync` ✅ (ya existe) |
| Event | `SeatReserved` | `DecrementAvailability` 🔲 (falta, sección 5) |
| Event | `ReservationCancelled` / `ReservationExpired` | `ReleaseSeat` 🔲 (falta) |
| Event | `ReservationConfirmed` | confirmar ocupación (no-op o marca) 🔲 |
| Booking | `EventCreated`/`Updated`/`Cancelled` | opcional: réplica local de eventos (fase tardía) |

> 🎉 Buena noticia: los casos de uso de la saga en Booking y Payment
> (`InitiateAsync`, `ConfirmAsync`, `CancelAsync`) **ya están implementados**.
> Los consumers solo los invocan. El grueso del trabajo nuevo es Event + el
> plumbing de RabbitMQ/Outbox/Inbox.

---

## 5. Deuda bloqueante a resolver ANTES de la saga (EventService)

EventService usa `AggregateRoot` pero **no levanta domain events, no tiene
`AvailableSeats`, ni comportamiento para ajustarlo**. Sin esto, no puede ni
publicar `EventCreated` ni reflejar disponibilidad. A resolver primero:

1. **Campo `AvailableSeats`** en `Event` (int), inicializado a `TotalSeats` en
   `Create`. Columna nueva → **migración**.
2. **Comportamiento de disponibilidad** (protege invariantes, no anémico):
   - `DecrementAvailability()` — al consumir `SeatReserved` (valida `> 0`).
   - `ReleaseSeat()` — al consumir `ReservationCancelled`/`ReservationExpired`
     (no supera `TotalSeats`).
3. **Domain events** (carpeta `Events/`): `EventCreatedDomainEvent`,
   `EventUpdatedDomainEvent`, `EventCancelledDomainEvent` + `RaiseDomainEvent(...)`
   en `Create`/`UpdateDetails`/(nuevo) `Cancel()`.
4. **`Cancel()`** en `Event` para poder emitir `EventCancelled`.
5. **Consumers** de Event (sección 4.5) + Inbox en la DB de Event.

> Nota de consistencia eventual: `AvailableSeats` en Event es un **reflejo
> informativo**. La verdad de disponibilidad vive en Booking (unique constraint).
> Que se desincronice unos segundos es aceptable y esperado.

---

## 6. Fases de implementación (orden incremental, verificable)

Cada fase deja algo **probable**. No avanzar sin verificar la anterior.

```
Fase 0  Deuda EventService (AvailableSeats + domain events + Cancel)   🎓 vos
   ▼
Fase 1  BuildingBlocks.Messaging.RabbitMQ (conexión + topología +      juntos
        publisher + consumer host). Prueba: publicar/consumir un ping.
   ▼
Fase 2  Outbox en Booking (interceptor + worker + tabla + mapper).     juntos
        Prueba: POST /reservations → ver SeatReserved en RabbitMQ UI.
   ▼
Fase 3  Inbox + consumer en Payment (SeatReserved → InitiateAsync).    juntos
        Prueba: la reserva dispara la creación del PaymentIntent.
   ▼
Fase 4  Outbox en Payment (PaymentSucceeded/Failed) + consumers en     juntos
        Booking (Confirm/Cancel). Prueba: Stripe CLI → confirma reserva.
   ▼
Fase 5  Event: consumers (SeatReserved/Reservation*) + su Outbox       🎓 vos
        (EventCreated...). Prueba: availableSeats sube/baja.
   ▼
Fase 6  Retry + DLQ + hardening de mensajes envenenados.               juntos
   ▼
Fase 7  Prueba end-to-end de la saga completa (happy path + fallo +    juntos
        expiración).
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
- [ ] Proyecto `BuildingBlocks.Messaging.RabbitMQ` (conexión, opciones, topología).
- [ ] `RabbitMqEventBus`, `RabbitMqConsumerHostedService`, registro DI.
- [ ] Outbox reutilizable (interceptor base, worker, entidad, config EF).
- [ ] Inbox reutilizable (entidad, config EF, helper de dedup).

**EventService** (más trabajo — deuda + saga)
- [ ] `AvailableSeats` + `DecrementAvailability`/`ReleaseSeat`/`Cancel` + migración.
- [ ] Domain events + mapper a `EventCreated/Updated/Cancelled`.
- [ ] Outbox (interceptor + worker + tabla).
- [ ] Consumers `SeatReserved`, `Reservation*` + Inbox.

**BookingService**
- [ ] Outbox (interceptor + worker + tabla + mapper → `SeatReserved`, `Reservation*`).
- [ ] Consumers `PaymentSucceeded` (→Confirm), `PaymentFailed` (→Cancel) + Inbox.
- [ ] El worker de expiración ya emite el domain event; solo enchufar al Outbox.

**PaymentService**
- [ ] Consumer `SeatReserved` (→InitiateAsync) + Inbox.
- [ ] Outbox (interceptor + worker + tabla + mapper → `PaymentSucceeded/Failed`).
- [ ] Tabla `processed_stripe_events` para dedup de webhooks por `StripeEventId`.

---

## 11. TODOs arrastrados (cerrarlos en esta fase)

- **Payment `Reason` persistido**: hoy el motivo viaja en el domain event pero no
  se guarda como columna. Con el Outbox el `Reason` llega al `PaymentFailed`, pero
  `GET /payments/{id}` no lo muestra. Decidir si se persiste (columna + migración).
- **Inbox por `StripeEventId`**: la tabla `processed_stripe_events` de la sección 4.3.
- **Race en `InitiateAsync`**: al consumir `SeatReserved` dos veces concurrentes,
  el unique constraint en `booking_id` rebota con `DbUpdateException` (23505 crudo →
  500). Atraparlo y devolver el pago existente (como hace el repo de Booking con
  `SeatAlreadyReservedException`).
- **Booking réplica de eventos**: decidir si Booking consume `EventCreated/Updated/
  Cancelled` para mantener una copia local del catálogo, o si le alcanza con los
  datos que vienen en el request de reserva (fase tardía / opcional).
```
