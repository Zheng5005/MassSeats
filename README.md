# MassSeats

**A distributed ticket-platform seat reservation system** built as a set of
microservices following Clean Architecture, coordinated through a choreographed
saga over RabbitMQ, with an Angular frontend and in-browser Stripe checkout.

MassSeats is a portfolio-grade, production-shaped reference implementation of a
real-world event booking flow: users browse a catalog, reserve seats, pay with
Stripe, and track their reservations — while the backend keeps every service
independently deployable, fault-tolerant and consistent through messaging.

---

## Features

- **Event catalog** — browse events, venues and categories; admin management for
  events, pricing and venues.
- **Seat reservations** — reserve seats against a per-event inventory with a
  state-machine lifecycle (reserved → paid / expired / cancelled).
- **Payments** — in-browser Stripe checkout (Payment Intents), webhook-driven
  confirmation, and per-user payment scoping.
- **My reservations** — the authenticated user's booking history with status
  tracking.
- **JWT authentication** — registration, login and profile management with
  ASP.NET Identity PBKDF2 password hashing.
- **Choreographed saga** — Booking, Payment and Event services coordinate the
  reservation lifecycle through RabbitMQ with **Outbox + Inbox** patterns for
  reliable, at-least-once delivery (see [ADR-0001](docs/adr/0001-distributed-messaging-reliability.md)).
- **API Gateway** — a single HTTP entry point (YARP reverse proxy) with JWT
  validation, routing to the internal services.
- **Demo seed data** — on first development boot, each service migrates its own
  database and seeds a demo catalog and user.

## Architecture

```text
                     ┌─────────────────┐
                     │   Angular 22    │
                     │   (frontend)    │
                     └────────┬────────┘
                              │ HTTP / JWT
                     ┌────────▼────────┐
                     │   API Gateway   │  YARP reverse proxy :8080
                     └───┬────┬────┬───┘
                         │    │    │
              ┌──────────▼┐ ┌─▼──────────┐ ┌──────────▼─────┐
              │ UserService│ │EventService│ │ PaymentService │
              │   :5026    │ │   :5144    │ │     :5002      │
              └──────────┬─┘ └─┬──────────┘ └─┬──────────────┘
                         │     │              │
              ┌──────────▼─────▼──────────────▼──────┐
              │            BookingService            │
              │                :5281                 │
              └──────────────────┬───────────────────┘
                                 │ PostgreSQL 17 (one DB per service)
                   ┌─────────────▼──────────────┐
                   │   RabbitMQ 4 (saga bus)    │
                   │   massseats.events exchange │
                   └────────────────────────────┘
```

### Services

| Service | Responsibility |
|---|---|
| **UserService** | User registration, profile and JWT authentication (PBKDF2 hashing). |
| **EventService** | Event catalog (events, venues, categories), seat availability; publishes catalog changes over RabbitMQ. |
| **BookingService** | Seat reservations with a state machine; owns the seat inventory and drives the reservation lifecycle (including expiration). |
| **PaymentService** | Stripe integration and webhooks that confirm or cancel reservations. |
| **API Gateway** | YARP reverse proxy with JWT auth — the single HTTP entry point for all services. |
| **RabbitMQ** | Choreographed saga bus (no central orchestrator) with Outbox + Inbox patterns for at-least-once delivery. |

Every service owns its own PostgreSQL database and follows Clean Architecture:
`Domain` → `Application` → `Infrastructure` → `API`. Integration between services
happens exclusively through events; there are no cross-service synchronous calls.

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10 (C#), ASP.NET Core minimal APIs |
| Architecture | Clean Architecture, CQRS-free vertical slices, Domain-Driven building blocks |
| Messaging | RabbitMQ 4, choreographed saga, Outbox + Inbox, dead-letter handling |
| Data | PostgreSQL 17, EF Core 10 |
| Frontend | Angular 22, Tailwind CSS 4, RxJS |
| Payments | Stripe Payment Intents + Webhooks |
| Gateway | YARP reverse proxy |
| Infra | Docker + Docker Compose |

## Repository Structure

```text
├── frontend/                  # Angular application
├── gateway/                   # YARP reverse proxy (API gateway)
├── infra/                     # Dockerfiles, compose files
├── scripts/                   # Developer tooling (e.g. setup-local-db.sh)
├── docs/adr/                  # Architecture Decision Records
└── services/
    ├── BookingService/        # Seat reservations + inventory
    ├── BuildingBlocks/        # Shared messaging primitives (Outbox/Inbox)
    ├── EventService/          # Catalog: events, venues, categories
    ├── PaymentService/        # Stripe payments + webhooks
    └── UserService/           # Auth, users, JWT
```

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) — all services and the
  gateway target `net10.0`
- [Docker](https://www.docker.com/) with Docker Compose — PostgreSQL, RabbitMQ
  and the services
- [Node.js](https://nodejs.org/) 20+ with npm — the frontend (`npm@11` is
  pinned via `packageManager`)

## Quick Start (development)

### Option A — Full stack with Docker Compose (recommended)

The dev override runs every service in Development mode with hot reload
(`dotnet watch`) and publishes the infrastructure ports to your host:

```bash
docker compose -f infra/docker-compose.yml -f infra/docker-compose.override.yml up -d
```

On first boot, each service **migrates its own database automatically** and
**seeds demo data** (Development only — see [Production](#production) below).

Start the frontend:

```bash
cd frontend
npm install
npm start
```

Open [http://localhost:4200](http://localhost:4200).

### Option B — Local services with `dotnet watch` (outside Docker)

Prefer this when you want to iterate on the backend without rebuilding
containers. Infrastructure still runs in Docker; services run on your host.

1. **Configure local settings** (copies `.env.example` and propagates connection
   strings to every service):

   ```bash
   cp .env.example .env
   ./scripts/setup-local-db.sh
   ```

2. **Start only the infrastructure** (database + message broker):

   ```bash
   docker compose -f infra/docker-compose.yml -f infra/docker-compose.override.yml up -d massseats-db massseats-rabbitmq
   ```

3. **Run the services you need** from the repo root, each in its own terminal:

   ```bash
   dotnet run --project gateway
   dotnet run --project services/UserService/src/UserService.API
   dotnet run --project services/EventService/src/EventService.API
   dotnet run --project services/BookingService/src/BookingService.API
   dotnet run --project services/PaymentService/src/PaymentService.API
   ```

4. **Frontend**:

   ```bash
   cd frontend && npm install && npm start
   ```

> **Note:** `setup-local-db.sh` rewrites the `ConnectionStrings` in each
> service's `appsettings.json` to point at `localhost`. It also configures the
> design-time EF factories, so `dotnet ef` commands work out of the box.

### Ports

| Service | URL |
|---|---|
| API Gateway (all API traffic) | http://localhost:8080 |
| Angular app | http://localhost:4200 |
| UserService (direct) | http://localhost:5026 |
| EventService (direct) | http://localhost:5144 |
| BookingService (direct) | http://localhost:5281 |
| PaymentService (direct) | http://localhost:5002 |
| RabbitMQ Management UI | http://localhost:15672 (`guest` / `guest`) |
| PostgreSQL | localhost:5432 (`postgres` / `postgres`) |

### Demo credentials

| Email | Password |
|---|---|
| `demo@massseats.dev` | `Demo123!` |

### Seeded catalog

On first boot the event service seeds 3 categories, 2 venues and 3 upcoming
events (relative to the current date) so the catalog is browsable and bookable
immediately.

### Stripe test payments

Payments run against **Stripe test mode**. Provide your test keys via
environment variables before starting the stack (the compose file reads them
with a safe placeholder default):

```bash
export STRIPE_SECRET_KEY="sk_test_..."
export STRIPE_WEBHOOK_SECRET="whsec_..."
```

In the checkout you can use Stripe's test card **`4242 4242 4242 4242`** with
any future expiry date and any CVC.

## Tests

```bash
# Frontend (unit + component tests)
cd frontend && npm test

# Backend — per service, from the repo root
dotnet test services/EventService
dotnet test services/BookingService
dotnet test services/PaymentService
dotnet test services/BuildingBlocks
```

## Production

- **Migrations are an explicit deploy step.** Generate an idempotent script per
  service and apply it before releasing — never rely on startup auto-migration
  in production:

  ```bash
  dotnet ef migrations script --idempotent -o migrate.sql
  ```

- **Seed data is dev-only by design.** The seeder runs only when the environment
  is `Development` **and** `SeedData:Enabled` is `true` (set only in
  `appsettings.Development.json`), and it only seeds empty tables. Outside
  Development the seed logic is never evaluated.

- **Secrets.** Replace the `Jwt__SecretKey` placeholder and set real Stripe keys
  via a secrets manager or CI/CD variables — never commit real credentials.

## Architecture Decision Records

- [ADR-0001: Adopt choreographed sagas with reliable RabbitMQ messaging](docs/adr/0001-distributed-messaging-reliability.md)
  — why the reservation lifecycle is an event-driven saga with Outbox/Inbox and
  dead-letter handling instead of synchronous coupling.

## Roadmap / Ideas

- Seat picker with visual layout and held-seat timeout
- Event search, filters and pagination
- Idempotent retry UI with reservation expiration countdown
- Admin dashboard with sales analytics
- Kubernetes manifests (Helm) and CI/CD pipeline

## License

Not yet licensed. Contact the author before reusing this repository.
