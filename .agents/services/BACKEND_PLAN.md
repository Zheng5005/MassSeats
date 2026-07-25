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

## Estado actual (actualizado 2026-07-25)

- ✅ **UserService**: Clean Architecture completa. CRUD probado y funcionando. Guid, PostgreSQL, Minimal API, migración aplicada. **Molde de referencia.**
- ✅ **EventService**: Clean Architecture completa. 3 entidades (Event, Venue, Category), repositorios, migración, endpoints CRUD. **Compila pero no testeado.**
- ✅ **BookingService**: Clean Architecture completa. Reservation con máquina de estados, unique constraint, background worker de expiración, migración. **Compila pero no testeado.**
- ✅ **PaymentService**: Clean Architecture completa. Payment entity rica, Stripe SDK integrado, webhook endpoint, migración. **Compila pero no testeado (requiere Stripe test keys).**
- 🔲 **RabbitMQ + Saga**: No implementado todavía. Ningún servicio tiene mensajería.

### Lo que queda (priorizado)

1. **Testear EventService y BookingService** individualmente (como se hizo con UserService).
2. **Testear PaymentService** con Stripe test keys.
3. **RabbitMQ + Outbox/Inbox**: Integrar mensajería en los 4 servicios.
4. **Saga coreografía**: Conectar los servicios vía eventos (SeatReserved → Payment → Confirm → Event).
5. **API Gateway**: HTTP síncrono hacia afuera.
6. **Docker Compose**: Postgres + RabbitMQ + servicios.

---

## Building Blocks compartidos (proyecto previo)

Carpeta `services/BuildingBlocks/` con dos proyectos:

- **`BuildingBlocks.Domain`**: clases base `Entity`, `AggregateRoot`, `IDomainEvent`, `DomainException` base. Evita copiar/pegar lo mismo en 4 servicios.
- **`BuildingBlocks.Messaging`**: contratos de los mensajes que viajan por RabbitMQ (`SeatReserved`, `PaymentSucceeded`, etc.) + abstracción del bus (`IEventBus`, `IEventPublisher`, `IEventConsumer`) + helpers de RabbitMQ (conexión, topología, outbox/inbox).

> ⚠️ **Regla de oro**: compartir SOLO contratos de mensajes y utilidades técnicas puras. **NUNCA** lógica de negocio. Cada dominio es soberano.

---

# Servicio 1 — EventService ✅ CRUD completo (sin mensajería)

Catálogo. CRUD puro, sin saga. Ya implementado.

### Dominio (✅ implementado)
- **`Event`** (aggregate root): título, descripción, `categoryId`, `venueId`, fecha, precio, `totalSeats`, `availableSeats`, banner, timestamps.
- **`Venue`** (aggregate root independiente): nombre, dirección, ciudad, país, capacidad.
- **`Category`** (aggregate root independiente): nombre.

> Recordatorio: `availableSeats` en Event es solo **reflejo informativo** (eventual consistency). La verdad de disponibilidad vive en BookingService.

### Capas (✅ implementadas)
- **Domain**: las 3 entidades encapsuladas (factory + behavior, como `User`), `IEventRepository`, `IVenueRepository`, excepciones (`EventNotFoundException`, `DuplicateVenueException`, etc.).
- **Application**: DTOs (Create/Update/Response de Event y Venue), `IEventService` / `IVenueService` (servicios clásicos), mapping manual, validación.
- **Infrastructure**: `EventDbContext`, configuraciones EF (snake_case), 2 repositorios (Event, Venue), migración inicial, design-time factory.
- **API**: Minimal API con grupos `/events` (CRUD + `GET /categories`) y `/venues` (CRUD). `GET /events` retorna todos (paginación pendiente).

### Mensajería (🔲 pendiente)
- **Publica**: `EventCreated`, `EventUpdated`, `EventCancelled` (para que Booking conozca qué eventos existen).
- **Consume**: `SeatReserved` / `SeatReleased` → ajusta `availableSeats` (reflejo informativo).

### 🔲 Pendiente
- Paginación y filtros en `GET /events` (categoría, ciudad, rango de fechas).
- Integrar mensajería (outbox + RabbitMQ).
- Testear endpoints individualmente.

---

# Servicio 2 — BookingService ✅ Dominio + App + Infra (sin mensajería)

El corazón del sistema: **concurrencia + dueño del inventario**. Ya implementado.

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

### Mensajería (🔲 pendiente)
- **Publica**: `SeatReserved` (→ Payment inicia cobro, Event decrementa), `ReservationConfirmed`, `ReservationCancelled` / `ReservationExpired` (→ Event libera).
- **Consume**: `PaymentSucceeded` (→ Confirm), `PaymentFailed` (→ Cancel).
- **Background job**: expirar `Pending` vencidas y publicar `ReservationExpired`.

### 🔲 Pendiente
- Tabla outbox para domain events (garantizar publicación atómica).
- Integrar mensajería (outbox + RabbitMQ).
- Endpoint `PUT /reservations/{id}/confirm` (para testing directo sin RabbitMQ).
- Testear endpoints y worker individualmente.

---

# Servicio 3 — PaymentService ✅ Dominio + App + Infra (sin mensajería)

Integración externa con Stripe. Ya implementado.

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

### Mensajería (🔲 pendiente)
- **Consume**: `SeatReserved` (→ inicia el PaymentIntent).
- **Publica**: `PaymentSucceeded`, `PaymentFailed`.

### 🔲 Pendiente
- Configurar Stripe test keys (user-secrets: `Stripe:SecretKey`, `Stripe:WebhookSecret`).
- Testear webhook con Stripe CLI (`stripe trigger payment_intent.succeeded`).
- Integrar mensajería (inbox para `SeatReserved` + outbox para `PaymentSucceeded`/`PaymentFailed`).

---

# Fase final — Comunicación RabbitMQ (la saga completa) 🔲

**Estado: No implementado.** Todos los servicios funcionan solos (CRUD + DB). Falta integrar la mensajería.

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

### Componentes de mensajería a construir (con RabbitMQ crudo)
1. **Outbox pattern** (productor): guardar el evento en tabla `outbox` dentro de la MISMA transacción que el cambio de negocio; un worker lo publica a RabbitMQ después. Garantiza no perder eventos.
2. **Inbox / idempotencia** (consumidor): registrar los `messageId` ya procesados para no duplicar.
3. **Topología RabbitMQ**: exchanges (topic), colas por servicio, routing keys por tipo de evento, dead-letter queues (DLQ).
4. **Retry + DLQ**: reintentos con backoff y cola de mensajes muertos.

### Orden de integración recomendado
```
1. Outbox table + publisher worker en BookingService
   (publica SeatReserved, ReservationConfirmed, etc.)
        ▼
2. Inbox table + consumer en PaymentService
   (consume SeatReserved → InitiatePayment)
        ▼
3. Outbox en PaymentService
   (publica PaymentSucceeded, PaymentFailed)
        ▼
4. Inbox en BookingService
   (consume PaymentSucceeded → Confirm, PaymentFailed → Cancel)
        ▼
5. Outbox en EventService (consume SeatReserved/Released)
   + Outbox en BookingService (consume EventCreated)
        ▼
6. DLQ + retry en todos los servicios
```

### 🎓 Para vos (concepto fundamental)
Estudiá **eventual consistency** y por qué no podés usar una transacción ACID que abarque varios servicios. La saga + outbox + idempotencia es la respuesta de la industria a eso.

---

## Catálogo de eventos de mensajería (contratos)

| Evento | Publicado por | Consumido por | Propósito |
|--------|---------------|---------------|-----------|
| `EventCreated` | Event | Booking | Booking conoce el evento y su capacidad/layout |
| `EventUpdated` | Event | Booking | Sincronizar cambios |
| `EventCancelled` | Event | Booking | Cancelar reservas asociadas |
| `SeatReserved` | Booking | Payment, Event | Iniciar cobro / decrementar asientos |
| `ReservationConfirmed` | Booking | Event | Confirmar ocupación |
| `ReservationCancelled` | Booking | Event | Liberar asiento |
| `ReservationExpired` | Booking | Event | Liberar asiento por timeout |
| `PaymentSucceeded` | Payment | Booking | Confirmar reserva |
| `PaymentFailed` | Payment | Booking | Cancelar reserva |

---

## Orden de implementación recomendado

```
1. BuildingBlocks (Entity base, Result, contratos)   ✅ COMPLETADO
        ▼
2. UserService (CRUD)   ✅ COMPLETADO Y TESTEADO
        ▼
3. EventService (CRUD)   ✅ COMPLETADO (pendiente testing)
        ▼
4. BookingService (concurrencia + inventario)   ✅ COMPLETADO (pendiente testing)
        ▼
5. PaymentService (Stripe)   ✅ COMPLETADO (pendiente testing con Stripe test keys)
        ▼
6. RabbitMQ + Outbox/Inbox   🔲 PRÓXIMO PASO
        ▼
7. Saga coreografía completa   🔲 DESPUÉS DEL 6
        ▼
8. API Gateway + Docker Compose   🔲 AL FINAL
```

---

## Reparto (completado / pendiente)

| Parte | Estado | Notas |
|-------|--------|-------|
| BuildingBlocks | ✅ Completado | Entity, AggregateRoot, IDomainEvent, DomainException |
| UserService completo | ✅ Completado y testeado | Molde de referencia |
| EventService completo | ✅ Completado | Pendiente testing individual |
| BookingService completo | ✅ Completado | Unique constraint + background worker |
| PaymentService completo | ✅ Completado | Stripe SDK + webhook verification |
| Outbox + Inbox + RabbitMQ topología | 🔲 Próximo | Integrar mensajería en los 4 servicios |
| API Gateway | 🔲 Pendiente | HTTP síncrono hacia afuera |
| Docker Compose | 🔲 Pendiente | Postgres + RabbitMQ + servicios |

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

