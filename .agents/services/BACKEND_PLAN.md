# MassSeats — Plan de Backend (Microservicios)

> Sistema de reserva de asientos para eventos masivos (estilo Ticketmaster).
> Este documento es la guía de arquitectura e implementación del backend.
> **Frontend queda fuera de alcance por ahora.**

---

## Decisiones transversales (CONFIRMADAS)

| # | Decisión | Elección | Notas |
|---|----------|----------|-------|
| 1 | Tipo de ID global | **Guid** | En todos los servicios. Las FK cross-service son referencias lógicas (no FK físicas). |
| 2 | Mensajería | **RabbitMQ crudo** (`RabbitMQ.Client`) | Elegido para aprender los conceptos a fondo (exchanges, colas, routing, DLQ) en vez de abstraerlos con MassTransit. |
| 3 | Dueño del inventario de asientos | **BookingService** | El que reserva es dueño de la verdad sobre disponibilidad. Event solo guarda `totalSeats` informativo y refleja `availableSeats` por eventos (eventual consistency). |
| 4 | Arquitectura por servicio | **Clean Architecture** | Domain → Application → Infrastructure → API (molde de UserService). |
| 5 | Base de datos | **PostgreSQL, database-per-service** | Cada micro tiene su propia DB. Sin JOINs cross-service. |
| 6 | Consistencia de mensajes | **Outbox + Inbox pattern** | No perder eventos (outbox) ni procesar duplicados (inbox/idempotencia). |
| 7 | Estilo de saga | **Coreografía** | Cada servicio reacciona a eventos; sin orquestador central (al menos al inicio). |

---

## Principios que atraviesan TODO

1. **Database-per-service**: cada micro tiene su propio Postgres. Nadie toca la DB de otro. Referencias cross-service = Guids lógicos sin FK física.
2. **Clean Architecture en los 4**: dependencias apuntando hacia adentro. Domain no referencia a nadie.
3. **Síncrono (HTTP)** para queries con respuesta inmediata (vía API Gateway). **Asíncrono (RabbitMQ)** para eventos de dominio y comandos entre servicios.
4. **Cada servicio es dueño de sus datos**. Si Booking necesita datos de Event, los pide por HTTP o mantiene una copia local sincronizada por eventos.

---

## Diagrama del sistema

```
                      ╭──────────────╮
                      │ API Gateway  │  (HTTP síncrono hacia afuera)
                      ╰──────┬───────╯
          ┌──────────────┬───┴────────┬──────────────┐
          ▼              ▼            ▼              ▼
   ╭────────────╮ ╭────────────╮ ╭────────────╮ ╭────────────╮
   │   User     │ │   Event    │ │  Booking   │ │  Payment   │
   │  Service   │ │  Service   │ │  Service   │ │  Service   │
   ╰─────┬──────╯ ╰─────┬──────╯ ╰─────┬──────╯ ╰─────┬──────╯
         │ DB           │ DB           │ DB           │ DB
         ▼              ▼              ▼              ▼
      (postgres)   (postgres)     (postgres)     (postgres)
         │              │              │              │
         └──────────────┴──────┬───────┴──────────────┘
                               ▼
                        ╭──────────────╮
                        │   RabbitMQ   │  (eventos/comandos async)
                        ╰──────────────╯
```

---

## Estado actual (actualizado 2026-08-02)

- ✅ **UserService**: Clean Architecture completa. CRUD probado y funcionando. Guid, PostgreSQL, Minimal API, migración aplicada. **Molde de referencia.**
- ✅ **EventService**: Clean Architecture completa. 3 entidades (Event, Venue, Category), repositorios, migración, endpoints CRUD. Domain events + `AvailableSeats` + saga implementados. **Testeado (Domain 7/7, Infra 6/6).**
- ✅ **BookingService**: Clean Architecture completa. Reservation con máquina de estados, unique constraint, background worker de expiración, migración. Outbox + consumers de la saga implementados. **Testeado (Infra 4/4).**
- ✅ **PaymentService**: Clean Architecture completa. Payment entity rica, Stripe SDK integrado, webhook endpoint, migración. Inbox/Outbox + saga implementados, dedup webhook y race resueltos. **Testeado (Infra 15/15, incluye integración real RabbitMQ+Postgres).**
- ✅ **RabbitMQ + Saga**: Implementado (fases 0–6 de MESSAGING_PLAN). Suite de tests 36/36 ✅. Falta la prueba manual con Stripe CLI real.
- ✅ **API Gateway**: Implementado con YARP (reverse proxy) + JWT auth, rutas a los 4 servicios.
- ✅ **Docker Compose**: `infra/docker-compose.yml` con Postgres, RabbitMQ, los 4 servicios y el gateway.

### Lo que queda (priorizado)

1. **Prueba manual end-to-end con Stripe CLI real** (fase 7 manual): instalar Stripe CLI, configurar test keys y correr `stripe trigger payment_intent.succeeded` / `payment_failed` / expiración contra la infra completa.
2. **Configurar Stripe test keys reales** (hoy `sk_test_replace_me` / `whsec_replace_me` en appsettings — solo placeholders).
3. **Paginación y filtros en `GET /events`** (categoría, ciudad, rango de fechas).
4. **Endpoint `PUT /reservations/{id}/confirm`** en Booking (testing directo sin RabbitMQ).
5. **(Opcional) Réplica local del catálogo en Booking** vía `EventCreated/Updated/Cancelled`.

---

## Building Blocks compartidos (proyecto previo)

Carpeta `services/BuildingBlocks/` con tres proyectos:

- **`BuildingBlocks.Domain`**: clases base `Entity`, `AggregateRoot`, `IDomainEvent`, `DomainException` base. Evita copiar/pegar lo mismo en 4 servicios.
- **`BuildingBlocks.Messaging`**: contratos de los mensajes que viajan por RabbitMQ (`SeatReserved`, `PaymentSucceeded`, etc.) + abstracción del bus (`IEventBus`, `IEventPublisher`, `IEventConsumer`).
- **`BuildingBlocks.Messaging.RabbitMQ`**: ✅ **implementado** — conexión/topología, `RabbitMqEventBus`, `RabbitMqConsumerHostedService`, retry y DLQ. Probado en `BuildingBlocks.Messaging.RabbitMQ.Tests` (integración real, 4/4).

> ⚠️ **Regla de oro**: compartir SOLO contratos de mensajes y utilidades técnicas puras. **NUNCA** lógica de negocio. Cada dominio es soberano.

---

# Servicio 1 — EventService ✅ CRUD + saga completos

Catálogo. CRUD + participación en la saga (disponibilidad). Implementado y testeado.

### Dominio (✅ implementado)
- **`Event`** (aggregate root): título, descripción, `categoryId`, `venueId`, fecha, precio, `totalSeats`, `availableSeats`, banner, timestamps.
- **`Venue`** (aggregate root independiente): nombre, dirección, ciudad, país, capacidad.
- **`Category`** (aggregate root independiente): nombre.
- **Comportamiento de saga** ✅: `DecrementAvailability()`, `ReleaseSeat()`, `Cancel()`, domain events (`EventCreated/Updated/CancelledDomainEvent`).

> Recordatorio: `availableSeats` en Event es solo **reflejo informativo** (eventual consistency). La verdad de disponibilidad vive en BookingService.

### Capas (✅ implementadas)
- **Domain**: las 3 entidades encapsuladas (factory + behavior, como `User`), `IEventRepository`, `IVenueRepository`, excepciones (`EventNotFoundException`, `DuplicateVenueException`, etc.).
- **Application**: DTOs (Create/Update/Response de Event y Venue), `IEventService` / `IVenueService` (servicios clásicos), mapping manual, validación.
- **Infrastructure**: `EventDbContext`, configuraciones EF (snake_case), 2 repositorios (Event, Venue), migraciones (initial + availability + seats + messaging), design-time factory.
- **API**: Minimal API con grupos `/events` (CRUD + `GET /categories`) y `/venues` (CRUD). `GET /events` retorna todos (paginación pendiente).

### Mensajería (✅ implementada)
- **Publica**: `EventCreated`, `EventUpdated`, `EventCancelled` (Outbox + worker).
- **Consume**: `SeatReserved` (→ decrementa `availableSeats`), `ReservationConfirmed/Cancelled/Expired` (→ confirma/libera) con Inbox/dedup.

### 🔲 Pendiente
- Paginación y filtros en `GET /events` (categoría, ciudad, rango de fechas).

---

# Servicio 2 — BookingService ✅ Dominio + App + Infra + saga

El corazón del sistema: **concurrencia + dueño del inventario**. Implementado y testeado.

### Dominio (✅ implementado)
- **`Reservation`** (aggregate root): id (Guid), `userId` (Guid), `eventId` (Guid), datos del asiento (sección, fila, número), precio, **status**, `paymentId`, `reservedAt`, **`expiresAt`**.
- **Estados** (máquina de estados): `Pending → Confirmed` / `Pending → Expired` / `Pending → Cancelled`.
- **Domain Events**: `ReservationCreated`, `ReservationConfirmed`, `ReservationCancelled`, `ReservationExpired`.
- **Excepciones**: `SeatAlreadyReservedException`, `InvalidReservationStateException`, `ReservationNotFoundException`, `DomainValidationException`.

```
   crear reserva           pago OK
  ╭─────────╮  ──────▶  ╭───────────╮  ──────▶  ╭────────────╮
  │ (nuevo) │           │  Pending  │           │ Confirmed  │
  ╰─────────╯           ╰─────┬─────╯           ╰────────────╯
                              │ timeout / pago falla
                              ▼
                        ╭───────────╮
                        │  Expired  │ ──▶ libera asiento (evento)
                        ╰───────────╯
```

### El problema de concurrencia (✅ resuelto)
- **Unique constraint** en `(event_id, seat_section, seat_row, seat_number)` → la DB rechaza el segundo intento. Robusto y simple.
- Una reserva `Pending` "ocupa" el asiento temporalmente hasta que expira.
- `ReservationRepository.SaveChangesAsync()` maneja `PostgresException` (23505) → `SeatAlreadyReservedException` (HTTP 409).

### Capas (✅ implementadas)
- **Domain**: `Reservation` con máquina de estados (`Confirm()`, `Expire()`, `Cancel()`), `IReservationRepository`, excepciones, domain events.
- **Application**: `ReservationAppService` con casos de uso `CreateAsync`, `GetByIdAsync`, `ConfirmAsync`, `CancelAsync`, `ExpireDueReservationsAsync`. DTOs (`CreateReservationRequest`, `ConfirmReservationRequest`, `ReservationResponse`). `ReservationOptions` (HoldDuration, ExpirationSweepInterval).
- **Infrastructure**: `BookingDbContext` con **unique constraint**, `ReservationRepository` (con manejo de UniqueViolation), `ReservationConfiguration` (snake_case, partial unique index), `ReservationExpirationWorker` (BackgroundService con PeriodicTimer), migración inicial, design-time factory.
- **API**: Minimal API con grupo `/reservations` (POST crear, GET por ID, DELETE cancelar).

### Background Worker (✅ implementado)
- `ReservationExpirationWorker` usa `PeriodicTimer` con `ExpirationSweepInterval` (default: 1 min).
- Abre scope fresco por tick (resuelve `IReservationService` Scoped desde un Singleton).
- Llama `ExpireDueReservationsAsync()` que busca reservas Pending vencidas y las expira en batch.
- Error handling: un fallo no mata al worker, reintenta en el próximo tick.

### Mensajería (✅ implementada)
- **Publica**: `SeatReserved` (→ Payment inicia cobro, Event decrementa), `ReservationConfirmed`, `ReservationCancelled` / `ReservationExpired` (→ Event libera), vía Outbox + worker.
- **Consume**: `PaymentSucceeded` (→ Confirm), `PaymentFailed` (→ Cancel), con Inbox/dedup.
- **Background job**: el worker de expiración expira `Pending` vencidas; el domain event viaja por el Outbox como `ReservationExpired`.

### 🔲 Pendiente
- Endpoint `PUT /reservations/{id}/confirm` (para testing directo sin RabbitMQ).
- *(Opcional)* Réplica local del catálogo consumiendo `EventCreated/Updated/Cancelled`.

---

# Servicio 3 — PaymentService ✅ Dominio + App + Infra + saga

Integración externa con Stripe. Implementado y testeado.

### Dominio (✅ implementado)
- **`Payment`** (aggregate root): id (Guid), `bookingId` (Guid), `stripePaymentIntentId` (string), `amount` (decimal), `currency`, `paymentMethod`, **`status`**, `createdAt`, `updatedAt`.
- **Estados**: `Pending → Succeeded` / `Pending → Failed`.
- **Domain Events**: `PaymentInitiated`, `PaymentSucceeded`, `PaymentFailed`.
- **Excepciones**: `DomainValidationException`, `PaymentNotFoundException`, `InvalidPaymentStateException`.

### Lo crítico: idempotencia (✅ implementada)
- **En InitiateAsync**: si ya existe un pago para ese `bookingId`, retorna el existente (no crea segundo PaymentIntent en Stripe).
- **En HandleWebhookAsync**: si el pago ya salió de Pending, retorna el estado actual (no re-aplica Succeed/Fail).
- **En el endpoint**: verifica firma del webhook ANTES de llegar a la Application. Firma inválida → 400 directo.

### Capas (✅ implementadas)
- **Domain**: `Payment` encapsulado (factory `Create()`, `Succeed()`, `Fail()`), `IPaymentRepository` (con `GetByStripePaymentIntentIdAsync`), domain events, excepciones.
- **Application**: `PaymentAppService` con `InitiateAsync`, `GetByIdAsync`, `GetByBookingIdAsync`, `HandleWebhookAsync`. DTOs (`InitiatePaymentRequest`, `PaymentResponse`, `StripeWebhookRequest`). Puerto `IPaymentGateway` (abstracción de Stripe). `StripeWebhookResult` (resultado parseado del webhook).
- **Infrastructure**: `PaymentDbContext`, `PaymentRepository`, `PaymentConfiguration` (snake_case), `StripePaymentGateway` (SDK de Stripe), `StripeOptions` (config), migración inicial, design-time factory.
- **API**: Minimal API con grupo `/payments` (GET por ID, POST `/webhook` para Stripe).

### 🎓 Conceptos clave implementados
- **Puerto `IPaymentGateway`**: abstracción de Stripe para poder testear sin tocar Stripe real.
- **Webhook con verificación de firma**: `EventUtility.ConstructEvent()` valida la firma HMAC-SHA256 contra el `WebhookSecret`.
- **Stripe SDK**: `StripeClient`, `PaymentIntentService`, conversión de montos a centavos.

### Mensajería (✅ implementada)
- **Consume**: `SeatReserved` (→ inicia el PaymentIntent) con Inbox/dedup.
- **Publica**: `PaymentSucceeded`, `PaymentFailed` vía Outbox + worker.
- **Webhook dedup**: tabla `processed_stripe_events` por `StripeEventId` + `StripeWebhookProcessor`.
- **Race resuelto**: `InitiateAsync` concurrente atrapa el unique constraint (23505) y devuelve el pago existente.
- **Reason persistido**: `FailureReason` en `Payment` (migración `AddPaymentFailureReason`), visible en `GET /payments/{id}`.

### 🔲 Pendiente
- Configurar Stripe test keys reales (hoy placeholders `sk_test_replace_me` / `whsec_replace_me`).
- Probar webhook con Stripe CLI (`stripe trigger payment_intent.succeeded`) — fase 7 manual.

---

# Fase final — Comunicación RabbitMQ (la saga completa) ✅

**Estado: Implementada y testeada (fases 0–6 de MESSAGING_PLAN, suite 36/36).**
Todos los servicios funcionan solos (CRUD + DB) y comunicados por eventos.
Falta solo la prueba manual end-to-end con Stripe CLI real (fase 7 manual).

### Flujo end-to-end de una reserva (coreografía)

```
 Usuario          Booking            RabbitMQ          Payment           Event
   │  POST /reserv   │                   │                │                │
   │────────────────▶│ crea Pending      │                │                │
   │                 │ (unique constraint)│                │                │
   │                 │──SeatReserved─────▶│                │                │
   │                 │                   │──SeatReserved─▶│ crea PaymentIntent
   │                 │                   │                │ (Stripe)       │
   │                 │                   │   ...Stripe webhook...          │
   │                 │                   │◀─PaymentSucceeded               │
   │                 │◀──PaymentSucceeded─│                │                │
   │                 │ Reservation       │                │                │
   │                 │ → Confirmed       │──ReservationConfirmed──────────▶│ decrementa asientos
   │                 │                   │                │                │
   │   (si falla o expira: ReservationExpired → Event libera asiento)     │
```

### Componentes de mensajería construidos (con RabbitMQ crudo)
1. ✅ **Outbox pattern** (productor): el caso de uso guarda el evento en tabla `outbox_messages` en la MISMA transacción que el cambio de negocio; `OutboxPublisherWorker` lo publica a RabbitMQ. No se pierden eventos.
2. ✅ **Inbox / idempotencia** (consumidor): `inbox_messages` por `messageId` + dedup de webhooks por `StripeEventId`.
3. ✅ **Topología RabbitMQ**: exchange topic `massseats.events`, colas por servicio, routing keys por tipo de evento, retry exchange + dead-letter queues (DLQ).
4. ✅ **Retry + DLQ**: reintentos con backoff y cola de mensajes muertos (probado en `RabbitMqPingTests`).

### Orden de integración ejecutado
```
1. ✅ Outbox table + publisher worker en BookingService
   (publica SeatReserved, ReservationConfirmed, etc.)
        ▼
2. ✅ Inbox table + consumer en PaymentService
   (consume SeatReserved → InitiatePayment)
        ▼
3. ✅ Outbox en PaymentService
   (publica PaymentSucceeded, PaymentFailed)
        ▼
4. ✅ Inbox en BookingService
   (consume PaymentSucceeded → Confirm, PaymentFailed → Cancel)
        ▼
5. ✅ Outbox en EventService (consume SeatReserved/Reservation*)
   + domain events del catálogo (EventCreated/Updated/Cancelled)
        ▼
6. ✅ DLQ + retry en todos los servicios
        ▼
7. ⚠️ Prueba end-to-end: suite automatizada 36/36 ✅;
   falta manual con Stripe CLI real
```

### 🎓 Para vos (concepto fundamental)
Estudiá **eventual consistency** y por qué no podés usar una transacción ACID que abarque varios servicios. La saga + outbox + idempotencia es la respuesta de la industria a eso.

---

## Catálogo de eventos de mensajería (contratos)

| Evento | Publicado por | Consumido por | Propósito | Estado |
|--------|---------------|---------------|-----------|--------|
| `EventCreated` | Event | Booking *(opcional)* | Booking conoce el evento y su capacidad/layout | ✅ publicado; consumo 🔲 opcional |
| `EventUpdated` | Event | Booking *(opcional)* | Sincronizar cambios | ✅ publicado; consumo 🔲 opcional |
| `EventCancelled` | Event | Booking *(opcional)* | Cancelar reservas asociadas | ✅ publicado; consumo 🔲 opcional |
| `SeatReserved` | Booking | Payment, Event | Iniciar cobro / decrementar asientos | ✅ |
| `ReservationConfirmed` | Booking | Event | Confirmar ocupación | ✅ |
| `ReservationCancelled` | Booking | Event | Liberar asiento | ✅ |
| `ReservationExpired` | Booking | Event | Liberar asiento por timeout | ✅ |
| `PaymentSucceeded` | Payment | Booking | Confirmar reserva | ✅ |
| `PaymentFailed` | Payment | Booking | Cancelar reserva | ✅ |

---

## Orden de implementación ejecutado

```
1. BuildingBlocks (Entity base, Result, contratos)   ✅ COMPLETADO
        ▼
2. UserService (CRUD)   ✅ COMPLETADO Y TESTEADO
        ▼
3. EventService (CRUD + saga)   ✅ COMPLETADO Y TESTEADO
        ▼
4. BookingService (concurrencia + inventario + saga)   ✅ COMPLETADO Y TESTEADO
        ▼
5. PaymentService (Stripe + saga)   ✅ COMPLETADO Y TESTEADO
        ▼
6. RabbitMQ + Outbox/Inbox   ✅ COMPLETADO
        ▼
7. Saga coreografía completa   ✅ COMPLETADO
        ▼
8. API Gateway + Docker Compose   ✅ COMPLETADO
```

---

## Reparto (completado / pendiente)

| Parte | Estado | Notas |
|-------|--------|-------|
| BuildingBlocks | ✅ Completado | Entity, AggregateRoot, IDomainEvent, DomainException, Messaging, Messaging.RabbitMQ |
| UserService completo | ✅ Completado y testeado | Molde de referencia |
| EventService completo | ✅ Completado y testeado | CRUD + saga (disponibilidad) |
| BookingService completo | ✅ Completado y testeado | Unique constraint + worker + saga |
| PaymentService completo | ✅ Completado y testeado | Stripe SDK + webhook + saga |
| Outbox + Inbox + RabbitMQ topología | ✅ Completado | En los 3 servicios de la saga |
| API Gateway | ✅ Completado | YARP + JWT, rutas a los 4 servicios |
| Docker Compose | ✅ Completado | Postgres + RabbitMQ + servicios + gateway |
| Prueba manual end-to-end (Stripe CLI) | 🔲 Pendiente | Fase 7 manual: happy path + fallo + expiración con webhook real |
| Stripe test keys reales | 🔲 Pendiente | Hoy placeholders en appsettings |
| Paginación `GET /events` | 🔲 Pendiente | Categoría, ciudad, rango de fechas |
| `PUT /reservations/{id}/confirm` | 🔲 Pendiente | Testing directo sin RabbitMQ |
| Réplica local de catálogo en Booking | 🔲 Opcional | Consumir `EventCreated/Updated/Cancelled` |

---

## Recordatorios técnicos (aprendidos)

- **.NET 10**, solución en formato nuevo `.slnx`.
- Estructura: `.slnx` en la raíz del servicio, 4 proyectos bajo `src/`.
- Referencias: API → (Application + Infrastructure); Infrastructure → Application; Application → Domain; Domain → nada.
- **Mapeo snake_case** vive en las configuraciones EF (Infrastructure), NUNCA en la entidad. Entidades en PascalCase de C#.
- Entidades **ricas, no anémicas**: setters privados, constructor privado, factory `Create()` con validación de invariantes, métodos de comportamiento.
- **Design-time factory** (`IDesignTimeDbContextFactory`) en Infrastructure para que la API quede libre de la dependencia EF.Design.
- Manejo de errores de dominio → `IExceptionHandler` que mapea a `ProblemDetails` (RFC 7807).
- Connection strings y secretos: **fuera del repo** (user-secrets / variables de entorno) para producción.
- EF tools: alinear versiones de paquetes EF Core para evitar warnings de conflicto.
- `Microsoft.EntityFrameworkCore.Design` con `PrivateAssets=all` → ejecutar `dotnet ef` desde el proyecto Infrastructure, no desde la API.
- `dotnet-tools.json` en la raíz del servicio para `dotnet-ef` local (no global).

