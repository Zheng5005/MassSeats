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

## Estado actual

- ✅ **UserService**: ya reconstruido con Clean Architecture (Guid, PostgreSQL, servicios clásicos, Minimal API, migración inicial). Es el molde de referencia.
- 🔲 **EventService**, **BookingService**, **PaymentService**: todavía con el template inicial. A reconstruir.

---

## Building Blocks compartidos (proyecto previo)

Carpeta `services/BuildingBlocks/` con dos proyectos:

- **`BuildingBlocks.Domain`**: clases base `Entity`, `AggregateRoot`, `IDomainEvent`, `DomainException` base. Evita copiar/pegar lo mismo en 4 servicios.
- **`BuildingBlocks.Messaging`**: contratos de los mensajes que viajan por RabbitMQ (`SeatReserved`, `PaymentSucceeded`, etc.) + abstracción del bus (`IEventBus`, `IEventPublisher`, `IEventConsumer`) + helpers de RabbitMQ (conexión, topología, outbox/inbox).

> ⚠️ **Regla de oro**: compartir SOLO contratos de mensajes y utilidades técnicas puras. **NUNCA** lógica de negocio. Cada dominio es soberano.

---

# Servicio 1 — EventService (arrancar por acá: el más simple)

Catálogo. CRUD puro, sin saga. Ideal para afianzar el patrón de UserService.

### Dominio
- **`Event`** (aggregate root): título, descripción, `categoryId`, `venueId`, fecha, precio, `totalSeats`, `availableSeats`, banner, timestamps.
- **`Venue`** (aggregate root independiente): nombre, dirección, ciudad, país, capacidad.
- **`Category`** (aggregate root independiente): nombre.

> Recordatorio: `availableSeats` en Event es solo **reflejo informativo** (eventual consistency). La verdad de disponibilidad vive en BookingService.

### Capas
- **Domain**: las 3 entidades encapsuladas (factory + behavior, como `User`), `IEventRepository`, `IVenueRepository`, `ICategoryRepository`, excepciones (`EventNotFoundException`, etc.).
- **Application**: DTOs (Create/Update/Response de cada una), `IEventService` / `IVenueService` / `ICategoryService` (servicios clásicos), mapping manual, validación.
- **Infrastructure**: `EventDbContext`, configuraciones EF (snake_case), 3 repositorios, migración inicial, design-time factory.
- **API**: Minimal API con grupos `/events`, `/venues`, `/categories`. Endpoint clave: `GET /events` con **paginación y filtros** (categoría, ciudad, rango de fechas).

### Mensajería
- **Publica**: `EventCreated`, `EventUpdated`, `EventCancelled` (para que Booking conozca qué eventos existen).
- **Consume**: `SeatReserved` / `SeatReleased` → ajusta `availableSeats` (reflejo informativo).

### 🎓 Para vos
Este servicio entero. Es CRUD, ya tenés el molde de User. Practicás **paginación y filtros** con EF Core.

---

# Servicio 2 — BookingService (el corazón: concurrencia + dueño del inventario)

El problema más difícil del sistema: **dos usuarios no pueden reservar el mismo asiento**. Concurrencia distribuida. Además, **este servicio es dueño del inventario** (decisión confirmada).

### Dominio
- **`Reservation`** (aggregate root): id (Guid), `userId` (Guid), `eventId` (Guid), datos del asiento (sección, fila, número), precio, **status**, `paymentId`, `reservedAt`, **`expiresAt`**.
- **Estados** (máquina de estados): `Pending → Confirmed` / `Pending → Expired` / `Pending → Cancelled`.

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

### El problema de concurrencia (lo más importante)
- Garantizar atomicidad al reservar un asiento. **Solución elegida: unique constraint** en `(event_id, seat_section, seat_row, seat_number)` → la DB rechaza el segundo intento. Robusto y simple.
- Una reserva `Pending` "ocupa" el asiento temporalmente hasta que expira.
- Alternativas descartadas (mencionadas para contexto): optimistic concurrency (rowversion), pessimistic locking (`SELECT FOR UPDATE`).

### Inventario (porque Booking es el dueño)
- Booking mantiene la verdad sobre qué asientos están ocupados/libres por evento.
- Cuando se crea/confirma/libera una reserva, **publica eventos** para que Event refleje `availableSeats`.
- Decisión pendiente de detalle: ¿Booking guarda el mapa completo de asientos de cada evento (proyección de capacidad) o solo las reservas? Recomendado: guardar las reservas + conocer `totalSeats`/layout del evento (replicado desde Event vía `EventCreated`).

### Capas
- **Domain**: `Reservation` con máquina de estados (métodos `Confirm()`, `Expire()`, `Cancel()`), `IReservationRepository`, excepciones (`SeatAlreadyReservedException`, `ReservationExpiredException`).
- **Application**: casos de uso `CreateReservation`, `ConfirmReservation`, `CancelReservation`, `GetReservation`. Acá vive la coordinación de la saga.
- **Infrastructure**: `BookingDbContext` con el **unique constraint**, repositorio, **tabla outbox**, y un **background worker** que expira reservas vencidas.
- **API**: `/reservations` (POST crear, GET consultar, DELETE cancelar).

### Mensajería (saga de reserva)
- **Publica**: `SeatReserved` (→ Payment inicia cobro, Event decrementa), `ReservationConfirmed`, `ReservationCancelled` / `ReservationExpired` (→ Event libera).
- **Consume**: `PaymentSucceeded` (→ Confirm), `PaymentFailed` (→ Cancel).
- **Background job**: expirar `Pending` vencidas y publicar `ReservationExpired`.

### 🎓 Para vos (lo más jugoso)
El **unique constraint** + el manejo de la excepción de DB cuando dos reservas chocan. Y el **background worker** de expiración (`BackgroundService` / `IHostedService`). Acá aprendés concurrencia de verdad.

---

# Servicio 3 — PaymentService (integración externa: Stripe)

### Dominio
- **`Payment`** (aggregate root): id (Guid), `bookingId` (Guid), `stripePaymentIntent`, amount, currency, paymentMethod, **status**, timestamps.
- **Estados**: `Pending → Succeeded` / `Pending → Failed`.

### Lo crítico: idempotencia
- Stripe envía **webhooks que pueden llegar duplicados o desordenados**. Procesar dos veces = cobrar/confirmar doble. Necesitás **idempotencia** (Inbox pattern o chequear si ya procesaste ese evento de Stripe).

### Capas
- **Domain**: `Payment` encapsulado, `IPaymentRepository`, excepciones.
- **Application**: `InitiatePayment` (crea PaymentIntent en Stripe), `HandleStripeWebhook` (procesa el resultado), puerto `IPaymentGateway` (abstracción de Stripe).
- **Infrastructure**: `PaymentDbContext`, repositorio, **implementación de `IPaymentGateway` con el SDK de Stripe**, tabla de idempotencia/inbox.
- **API**: `POST /payments/webhook` (lo llama **Stripe**, no RabbitMQ — verificar firma del webhook), `GET /payments/{id}`.

### Mensajería
- **Consume**: `SeatReserved` (→ inicia el PaymentIntent) — o recibe comando explícito `InitiatePayment`.
- **Publica**: `PaymentSucceeded`, `PaymentFailed`.

### 🎓 Para vos
El puerto `IPaymentGateway` y entender por qué Stripe va detrás de una abstracción (testeo sin tocar Stripe real). El webhook con **verificación de firma** es un buen ejercicio de seguridad.

---

# Fase final — Comunicación RabbitMQ (la saga completa)

Recién cuando los 4 servicios funcionen solos (CRUD + DB), se integra la mensajería.

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
1. BuildingBlocks (Entity base, Result, contratos)   ← cimientos
        ▼
2. EventService (CRUD)        🎓 lo hacés vos (replicás User)
        ▼
3. BookingService (concurrencia + inventario)   🎓 mitad y mitad
        ▼
4. PaymentService (Stripe)
        ▼
5. RabbitMQ + Saga (integra los 4)   ← lo más complejo, al final
```

---

## Reparto (vos aprendés, yo acelero)

| Parte | Quién | Por qué |
|-------|-------|---------|
| EventService completo | 🎓 **Vos** | Es CRUD, ya tenés el molde de User |
| BuildingBlocks | Yo (o juntos) | Boilerplate técnico |
| Booking: unique constraint + worker de expiración | 🎓 **Vos** | El concepto clave de concurrencia |
| Booking: capas Domain/App/Infra | Juntos | Saga es complejo |
| Payment: puerto `IPaymentGateway` + webhook | 🎓 **Vos** | Aprendés abstracción de externos |
| Stripe SDK integración | Yo | Boilerplate del SDK |
| Outbox + Inbox + RabbitMQ topología | Juntos | Lo más difícil, mejor en pareja |

---

## Recordatorios técnicos (aprendidos en UserService)

- **.NET 10**, solución en formato nuevo `.slnx`.
- Estructura: `.slnx` en la raíz del servicio, 4 proyectos bajo `src/`.
- Referencias: API → (Application + Infrastructure); Infrastructure → Application; Application → Domain; Domain → nada.
- **Mapeo snake_case** vive en las configuraciones EF (Infrastructure), NUNCA en la entidad. Entidades en PascalCase de C#.
- Entidades **ricas, no anémicas**: setters privados, constructor privado, factory `Create()` con validación de invariantes, métodos de comportamiento.
- **Design-time factory** (`IDesignTimeDbContextFactory`) en Infrastructure para que la API quede libre de la dependencia EF.Design.
- Manejo de errores de dominio → `IExceptionHandler` que mapea a `ProblemDetails` (RFC 7807).
- Connection strings y secretos: **fuera del repo** (user-secrets / variables de entorno) para producción.
- EF tools: alinear versiones de paquetes EF Core para evitar warnings de conflicto.
```

