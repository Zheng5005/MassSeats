# MassSeats — Seed Data Plan (startup seeder, Opción A)

> Goal: a fresh clone should boot with **migrated databases and demo data** so
> anyone can try the app without manually creating categories/venues/events or a
> user. The seeder must be **safe in production**: it never runs outside
> Development, and migrations are an explicit deploy step there.
>
> **Status (2026-08-07):** planned only — nothing implemented. Verified current
> state: no `HasData`, no seeder, no SQL init scripts, and no `MigrateAsync` in
> any `Program.cs` (databases are migrated manually today).

---

## 1. Why this design (the production-safety contract)

The user raised a valid concern: an auto-migration + seeder on startup could be
dangerous if a service restarts in production. The design answers it with two
separate responsibilities:

| Responsibility | Development | Production |
|----------------|-------------|------------|
| **Schema evolution (migrations)** | `Database.MigrateAsync()` at startup (convenient) | **Explicit deploy step only** — `dotnet ef migrations script` output applied by the pipeline, or a one-shot migration job before rollout. Never auto-migrate at startup. |
| **Data bootstrap (seed)** | Startup seeder with **two guards** | Seeder is **disabled** — never evaluates seed logic |

Key facts that make this safe:

1. **`MigrateAsync()` is idempotent by design.** EF Core tracks applied
   migrations in `__EFMigrationsHistory` and only applies pending ones. A
   crash-restart does NOT re-run migrations. The only migrations that can damage
   data are new destructive ones — and those are applied once, deliberately, at
   deploy time (not on restart).
2. **The real risk is the seeder**, not the migration. Guarding the seeder only
   with "table is empty" is insufficient (a legitimately empty table in prod
   could re-seed). So the seeder requires **both**:
   - `SeedData:Enabled = true` in configuration, set **only** in
     `appsettings.Development.json`; AND
   - `app.Environment.IsDevelopment()` check at runtime; AND
   - idempotency: seed only when the relevant tables are empty.
   Three layers. If any fails, the seeder does nothing.

---

## 2. Data to seed

### 2.1 UserService — one demo user

| Field | Value |
|-------|-------|
| FirstName / LastName | `Demo` / `User` |
| Email | `demo@massseats.dev` |
| Password | `Demo123!` (hashed at runtime via `IPasswordHasher.Hash` — the real PBKDF2 implementation, never a hardcoded hash) |

Why through the service: `User.Create(...)` needs a `passwordHash`; the only
correct way to get one is `PasswordHasher` (ASP.NET Identity PBKDF2). The seeder
resolves `IPasswordHasher` from DI and calls `Hash("Demo123!")` at runtime.

### 2.2 EventService — categories, venues, events

**Categories** (read-only — no create endpoint exists, so DB seeding is the ONLY
way to get them):

| Name | Description |
|------|-------------|
| Concert | Live music performances |
| Theater | Plays, musicals and stage shows |
| Sports | Sporting events and matches |

**Venues** (via `Venue.Create`):

| Name | Address | City | Country | Capacity |
|------|---------|------|---------|----------|
| Grand Hall | 100 Main St | Springfield | US | 500 |
| Open Air Arena | 250 Riverside Dr | Springfield | US | 2000 |

**Events** (via `Event.Create`, referencing the seeded categories/venues, dates
in the future so they are browsable/booking-able):

| Title | Category | Venue | EventDate | TicketPrice | TotalSeats |
|-------|----------|-------|-----------|-------------|------------|
| Summer Symphony | Concert | Grand Hall | now + 14 days | 45.00 | 500 |
| The Glass Menagerie | Theater | Grand Hall | now + 21 days | 30.00 | 200 |
| City Cup Final | Sports | Open Air Arena | now + 30 days | 60.00 | 2000 |

> Event dates are computed relative to `DateTimeOffset.UtcNow` at seed time so
> demo data never goes stale in a freshly-created database.

### 2.3 BookingService / PaymentService

No seed. Reservations and payments are created by real usage (the demo user
books a seat, the saga drives the rest). Seeding fake reservations would require
RabbitMQ + Stripe plumbing at boot and add nothing to the demo.

---

## 3. Implementation steps

### Step 1 — UserService seeder

**Files:**
- NEW `services/UserService/src/UserService.Infrastructure/Seeding/UserDbSeeder.cs`
- EDIT `services/UserService/src/UserService.Infrastructure/DependencyInjection.cs`
- EDIT `services/UserService/src/UserService.API/Program.cs`
- EDIT `services/UserService/src/UserService.API/appsettings.Development.json`

**Behavior:**
1. `UserDbSeeder` (scoped) receives `UserDbContext`, `IUserRepository`,
   `IPasswordHasher`, `IConfiguration`.
2. `SeedAsync()`:
   - Reads `SeedData:Enabled`; returns immediately if not `true`.
   - Checks `Users` table is empty (`!await _context.Users.AnyAsync()`); returns if not.
   - Hashes `Demo123!`, creates `User.Create("Demo", "User", "demo@massseats.dev", hash)`, persists.

**Registration:** an `IHostedService` (or a startup hook in `Program.cs`) runs
the seeder after `MigrateAsync` **only when `app.Environment.IsDevelopment()`**.

### Step 2 — EventService seeder

**Files:**
- NEW `services/EventService/src/EventService.Infrastructure/Seeding/EventDbSeeder.cs`
- EDIT `services/EventService/src/EventService.Infrastructure/DependencyInjection.cs`
- EDIT `services/EventService/src/EventService.API/Program.cs`
- EDIT `services/EventService/src/EventService.API/appsettings.Development.json`

**Behavior:**
1. `EventDbSeeder` (scoped) receives `EventDbContext`, `IConfiguration`.
2. `SeedAsync()`:
   - Returns immediately unless `SeedData:Enabled` is `true`.
   - If `Categories` table empty → `Category.Create(...)` for the 3 categories.
   - If `Venues` table empty → `Venue.Create(...)` for the 2 venues.
   - If `Events` table empty → `Event.Create(...)` for the 3 events using the
     seeded category/venue ids.
   - Single `SaveChangesAsync` at the end (or per entity group — choose clarity).
3. Same dev-only registration as UserService.

### Step 3 — Startup wiring (both services)

In each `Program.cs` (dev-only, after `builder.Build()`):

```csharp
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<XxxDbContext>();
    await db.Database.MigrateAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<XxxDbSeeder>();
    await seeder.SeedAsync();
}
```

- `SeedData:Enabled` set to `true` in `appsettings.Development.json` for both
  services (explicit config guard in addition to the environment check).
- **Production runs do NOT hit this block** (environment + config guards), and
  even if they did, the seeder's empty-table + enabled-flag checks are no-ops.

### Step 4 — Production migration guidance (docs only)

Add a section to `Readme.md`:

- **Dev:** `docker compose up` — services auto-migrate and seed.
- **Prod:** migrations are applied by the deployment pipeline:
  `dotnet ef migrations script --idempotent -o migrate.sql` (per service), apply
  `migrate.sql` as a release step BEFORE starting the new version. Never rely on
  startup auto-migration in prod. Seed data is dev-only by design.

### Step 5 — Verification

- Fresh environment (`docker compose down -v && docker compose up -d`):
  1. `docker compose ps` → all services healthy.
  2. Login with `demo@massseats.dev` / `Demo123!` → token works.
  3. `GET /events`, `GET /venues`, `GET /categories` return seeded data.
  4. Book a seat on a seeded event → reservation flow works.
- Restart test (simulating the user's concern): `docker compose restart
  massseats-event` → service comes back, **no duplicate categories/venues/events**
  (idempotency proven), `__EFMigrationsHistory` unchanged.
- Production-safety smoke test: run the service with
  `ASPNETCORE_ENVIRONMENT=Production` → logs show no seed/migrate activity.

---

## 4. Files touched (summary)

| File | Action |
|------|--------|
| `services/UserService/src/UserService.Infrastructure/Seeding/UserDbSeeder.cs` | NEW |
| `services/UserService/src/UserService.Infrastructure/DependencyInjection.cs` | EDIT (register seeder) |
| `services/UserService/src/UserService.API/Program.cs` | EDIT (dev migrate + seed) |
| `services/UserService/src/UserService.API/appsettings.Development.json` | EDIT (`SeedData:Enabled`) |
| `services/EventService/src/EventService.Infrastructure/Seeding/EventDbSeeder.cs` | NEW |
| `services/EventService/src/EventService.Infrastructure/DependencyInjection.cs` | EDIT (register seeder) |
| `services/EventService/src/EventService.API/Program.cs` | EDIT (dev migrate + seed) |
| `services/EventService/src/EventService.API/appsettings.Development.json` | EDIT (`SeedData:Enabled`) |
| `Readme.md` | EDIT (dev vs prod migration/seed guidance) |

No changes to BookingService, PaymentService, Gateway, or frontend.

---

## 5. Checklist

**UserService**
- [ ] `UserDbSeeder` — flag guard + empty-table guard + real `PasswordHasher`
- [ ] DI registration
- [ ] Dev-only startup wiring (`IsDevelopment` + `MigrateAsync` + `SeedAsync`)
- [ ] `SeedData:Enabled` in `appsettings.Development.json`

**EventService**
- [ ] `EventDbSeeder` — categories, venues, events (relative future dates)
- [ ] DI registration
- [ ] Dev-only startup wiring
- [ ] `SeedData:Enabled` in `appsettings.Development.json`

**Docs**
- [ ] `Readme.md` — dev auto-migrate+seed vs prod explicit migration deploy step

**Verification**
- [ ] Fresh `docker compose down -v && up -d` → demo login + seeded catalog
- [ ] `docker compose restart` → no duplicates (idempotency)
- [ ] Production env smoke test → no seed/migrate activity
