# MassSeats

Ticket-platform style seat reservation system for mass events. Backend is a set
of microservices following Clean Architecture, frontend is Angular.

## Project Structure
```
├── frontend/                # Angular project
├── API_gateway/             # Client endpoint for micros-services
├── infra/                   # Configuration/Containers
├── services/
│   ├── BookingService/
│   ├── EventService/
│   ├── PaymentService/
│   ├── UserService/
```

## Architecture

- **UserService** — user registration, profile and JWT authentication (password
  hashing with ASP.NET Identity PBKDF2).
- **EventService** — event catalog (events, venues, categories) plus seat
  availability; publishes catalog events over RabbitMQ.
- **BookingService** — seat reservations with a state machine; owns the seat
  inventory and drives the reservation lifecycle (expiration included).
- **PaymentService** — Stripe integration and webhooks that confirm or cancel
  reservations.
- **API Gateway** — YARP reverse proxy with JWT auth, the single HTTP entry
  point for all services.
- **RabbitMQ** — the services coordinate through a choreographed saga (no
  central orchestrator) using Outbox + Inbox patterns for reliable, at-least-
  once delivery.

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) (all services target
  `net10.0`)
- [Docker](https://www.docker.com/) (with Docker Compose) — for Postgres,
  RabbitMQ and the services
- [Node.js](https://nodejs.org/) + npm (frontend)

## Quick start (development)

Start the infrastructure and all services with the dev compose override (it
runs the services in Development mode with hot reload):

```bash
docker compose -f infra/docker-compose.yml -f infra/docker-compose.override.yml up -d
```

On first boot each service migrates its own database automatically and seeds
demo data. This only happens in Development — see the production note below.

Frontend:

```bash
cd frontend
npm install
npm start
```

### Demo credentials

| Email | Password |
|-------|----------|
| `demo@massseats.dev` | `Demo123!` |

### Seeded catalog

The event service seeds 3 categories, 2 venues and 3 future events (relative
dates) so the catalog is browsable and bookable right away.

## Tests

Frontend:

```bash
cd frontend
npm test
```

Backend — per service, from the repo root (services that ship test projects):

```bash
dotnet test services/EventService
dotnet test services/BookingService
dotnet test services/PaymentService
dotnet test services/BuildingBlocks
```

## Production note

Migrations are an **explicit deploy step**. Generate an idempotent script per
service and apply it before releasing the new version — never rely on startup
auto-migration in production:

```bash
dotnet ef migrations script --idempotent -o migrate.sql
```

Seed data is **dev-only by design**: the seeder only runs when the environment
is Development **and** `SeedData:Enabled` is `true` (set only in
`appsettings.Development.json`), and it only seeds empty tables. Outside
Development it never evaluates seed logic, so production databases are never
touched.
