# MassSeats — Frontend Implementation Plan

> Angular application for the MassSeats seat-reservation platform.
> This plan covers **only what is buildable against the current backend state**:
> all 4 services CRUD + JWT auth + API Gateway (YARP) + **full messaging saga complete**
> (fases 0–6 de MESSAGING_PLAN: RabbitMQ + Outbox/Inbox + Event consumers + retry/DLQ).
> La fase 7 (prueba manual end-to-end con Stripe CLI) está verificada con la suite
> automatizada 36/36 ✅; solo resta la validación manual con webhook real, que no
> bloquea el frontend.
>
> **Estado del proyecto frontend (2026-08-02):** existe el scaffold base en
> `frontend/` (Angular 22 + SSR + Tailwind 4 + vitest, solo `ng new` sin features).
> NINGUNA fase de este plan está implementada todavía.

---

## 1. System context

```
 Browser (Angular 22, Tailwind 4, SSR)
      │  HTTPS/JSON  (Authorization: Bearer <jwt>)
      ▼
 ╭───────────────╮   http://localhost:8080
 │  API Gateway  │  YARP reverse proxy + JWT validation + claim forwarding
 ╰───────┬───────╯
    ┌────┼────┬─────────┬──────────┐
    ▼    ▼    ▼         ▼          ▼
 users   events/venues/categories  booking  payments   (upstream services)
```

| Component | Local port (dev) | Notes |
|-----------|------------------|-------|
| Gateway | 8080 | Single entry point for the frontend. Validates JWT, forwards `X-User-Id/Email/Name`. |
| UserService | 5026 | CRUD users + `POST /users/login` (JWT). |
| EventService | 5144 | CRUD events/venues, read-only categories. |
| BookingService | 5281 | Reservations under `/booking/**` (gateway strips prefix). |
| PaymentService | 5002 | Payments, Stripe webhook. |
| PostgreSQL / RabbitMQ | 5432 / 5673 | RabbitMQ expuesto localmente en 5673 (ver `.env`); docker-compose interno usa 5672. |

> **The frontend must always talk to the Gateway (`http://localhost:8080`), never to services directly.**

---

## 2. Current API surface (what the frontend can call TODAY)

### Public (no JWT required)

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/users` | Register a user |
| POST | `/users/login` | Login → `{ token, user }` |
| POST | `/payments/webhook` | Stripe webhook (never called by the frontend) |

### Authenticated (JWT required)

| Method | Path | Purpose | Notes |
|--------|------|---------|-------|
| GET | `/events` | List events | |
| GET | `/events/{id}` | Event detail | |
| POST | `/events` | Create event (admin) | No role enforcement yet — see §6 |
| PUT | `/events/{id}` | Update event (admin) | |
| PUT | `/events/{id}/pricing` | Update price (admin) | |
| DELETE | `/events/{id}` | Delete event (admin) | |
| GET | `/venues` | List venues | |
| GET | `/venues/{id}` | Venue detail | |
| POST | `/venues` | Create venue (admin) | |
| PUT | `/venues/{id}` | Update venue (admin) | |
| DELETE | `/venues/{id}` | Delete venue (admin) | |
| GET | `/categories` | List categories | Read-only; no create/update endpoint |
| GET | `/users/{id}` | Get user profile | Own profile only — no role check |
| PUT | `/users/{id}` | Update profile | |
| DELETE | `/users/{id}` | Delete user | |
| POST | `/booking/reservations` | Create reservation | `userId` comes from `X-User-Id`, NOT from the body |
| GET | `/booking/reservations/{id}` | Reservation detail | |
| DELETE | `/booking/reservations/{id}` | Cancel reservation | |
| GET | `/payments/{id}` | Payment detail | Find via `reservation.paymentId` |
| GET | `/health` | Gateway health | |

> Gateway auth rule: every path except `/users/login` and `/payments/webhook` requires a
> valid Bearer JWT. There is **no public catalog**: even reading events requires a token.

---

## 3. API contracts → TypeScript models

Source of truth: the C# records in each service's `Application/DTOs` folder.
`DateTimeOffset` arrives as ISO-8601 (`2026-07-31T12:00:00+00:00`); `decimal` arrives as a JSON number.

```ts
// auth
export interface LoginRequest { email: string; password: string; }
export interface LoginResponse { token: string; user: User; }

// users
export interface CreateUserRequest {
  firstName: string; lastName?: string | null; email: string;
  password: string; nationalId?: string | null; phone?: string | null;
}
export interface UpdateUserRequest {
  firstName: string; lastName?: string | null; phone?: string | null; profileImage?: string | null;
}
export interface User {
  id: string; firstName: string; lastName?: string | null; email: string;
  nationalId?: string | null; profileImage?: string | null; phone?: string | null;
  createdAt: string; updatedAt: string;
}

// events
export interface CreateEventRequest {
  title: string; description?: string | null; categoryId: string; venueId: string;
  eventDate: string; ticketPrice: number; totalSeats: number; bannerImage?: string | null;
}
export interface UpdateEventRequest {
  title: string; description?: string | null; categoryId: string;
  venueId: string; eventDate: string;
}
export interface UpdateEventPricingRequest { ticketPrice: number; }
export interface Event {
  id: string; title: string; description?: string | null; categoryId: string; venueId: string;
  eventDate: string; ticketPrice: number; totalSeats: number; availableSeats: number;
  bannerImage?: string | null; createdAt: string; updatedAt: string;
}

// categories / venues
export interface Category { id: string; name: string; description?: string | null; createdAt: string; updatedAt: string; }
export interface VenueRequest { name: string; address: string; city: string; country: string; capacity: number; }
export interface Venue { id: string; name: string; address: string; city: string; country: string; capacity: number; createdAt: string; updatedAt: string; }

// bookings
export interface CreateReservationRequest {
  eventId: string; seatSection: string; seatRow: string; seatNumber: number; price: number;
}
export type ReservationStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'Expired';
export interface Reservation {
  id: string; userId: string; eventId: string;
  seatSection: string; seatRow: string; seatNumber: number;
  price: number; status: ReservationStatus; paymentId?: string | null;
  reservedAt: string; expiresAt: string;
}

// payments
export type PaymentStatus = 'Pending' | 'Succeeded' | 'Failed' | 'Cancelled' | string;
export interface Payment {
  id: string; bookingId: string; stripePaymentIntentId: string;
  amount: number; currency: string; paymentMethod?: string | null;
  status: PaymentStatus; createdAt: string; updatedAt?: string | null; failureReason?: string | null;
}
```

---

## 4. What is NOT possible yet (explicitly out of scope)

| Capability | Why blocked | Unblocked by |
|------------|-------------|--------------|
| ~~**In-browser Stripe checkout**~~ ✅ | ~~No endpoint returning a `client_secret`.~~ | Resolved 2026-08-07: `GET /payments/{bookingId}/client-secret` (PaymentService) + Stripe Payment Element "Pay now" flow in `reservation-detail` (see §3.3 of the checkout plan). |
| ~~**"My reservations" list**~~ ✅ | ~~No list-by-user endpoint.~~ | Resolved 2026-08-07: `GET /reservations` (user-scoped via `X-User-Id`) + `/reservations` page with nav link. |
| **Category management** | EventService exposes only `GET /categories`. | New create/update/delete endpoints. |
| **Role-based admin** | JWT carries no role claim; gateway authorizes any authenticated user for every mutation. | Add role claim to JWT + gateway policy. For now, gate "admin" pages behind auth only (documented limitation). |
| **Reservation confirm action** | Confirmation is **event-driven** (`PaymentSucceeded` → Booking confirms). No manual confirm endpoint exists or is planned. | Nothing needed: UI only reflects the status. |
| **Retry/DLQ observability (admin tooling)** | The messaging retry/DLQ is implemented (Phase 6 ✅) but it lives in RabbitMQ management UI; there is no API surface for the frontend to inspect it. | Ops tooling later (out of frontend scope for now). |

> ✅ **Live `availableSeats` is now possible**: the messaging saga is complete
> (fases 0–6), so EventService consumes `SeatReserved` / `Reservation*` and
> `availableSeats` updates over time. It is an **eventually consistent**
> informational reflection, not transactional truth — the UI should still show
> a small caveat (see §7 Phase 2 and §8).

---

## 5. Angular project structure

> Estado actual del repo: `frontend/` tiene el scaffold oficial generado por
> `ng new` (Angular 22, SSR con `@angular/ssr`, Tailwind 4 vía PostCSS, vitest
> como runner de tests, `src/main.ts` + `src/main.server.ts` + `src/server.ts`).
> `src/app/app.ts` es el componente raíz placeholder; NO existe todavía la
> estructura de features de abajo. Esta sección es la estructura objetivo.

```
frontend/src/app/
├── core/
│   ├── api/                     # HTTP plumbing
│   │   ├── api.config.ts        # base URL (environment or injection token)
│   │   ├── api-client.ts        # typed HttpClient wrapper (auth header, error mapping)
│   │   └── error.interceptor.ts # ProblemDetails → user-facing errors
│   ├── auth/
│   │   ├── auth.service.ts      # token persistence (localStorage), login/register/logout
│   │   ├── auth.guard.ts        # route guard: redirect to /login
│   │   └── token.interceptor.ts # attach Authorization: Bearer <token>
│   └── shell/
│       ├── header/ footer/      # app shell
│       └── app-shell.component
├── features/
│   ├── catalog/                 # public-ish browsing (still behind auth)
│   │   ├── pages/event-list/ event-detail/ venue-list/ venue-detail/
│   │   └── catalog.api.ts       # GET /events, /venues, /categories
│   ├── account/
│   │   ├── pages/register/ login/ profile/
│   │   └── account.api.ts       # POST /users, /users/login, GET/PUT /users/{id}
│   ├── admin/
│   │   ├── pages/event-manage/ venue-manage/
│   │   └── admin.api.ts         # POST/PUT/DELETE events + venues
│   └── bookings/
│       ├── pages/reservation-create/ reservation-detail/
│       ├── booking.api.ts       # POST/GET/DELETE /booking/reservations
│       └── payment.api.ts       # GET /payments/{id}
├── shared/
│   ├── models/                  # §3 interfaces
│   ├── ui/                      # buttons, badges, cards, forms, empty states
│   └── utils/                   # date formatting (ISO-8601 → locale), price formatting
└── app.routes.ts                # route table (§6)
```

Conventions:
- **Feature modules** own pages + feature-level API services; `core` owns cross-cutting plumbing.
- All HTTP goes through `api-client.ts` so auth/error handling stays in one place.
- Components are container/presentational: pages fetch data, presentational components render.

---

## 6. Routes and navigation

```
/                    → Event list (catalog)            [auth]
/events/:id          → Event detail                    [auth]
/venues              → Venue list                      [auth]
/venues/:id          → Venue detail                    [auth]
/login               → Login                           [public]
/register            → Register                        [public]
/profile             → Profile (view/edit)             [auth]
/admin/events        → Event management                [auth]  (role limitation, §4)
/admin/events/new    → Create event                    [auth]
/admin/events/:id    → Edit event / pricing            [auth]
/admin/venues        → Venue management                [auth]
/admin/venues/new    → Create venue                    [auth]
/admin/venues/:id    → Edit venue                      [auth]
/events/:id/book     → Reservation create              [auth]
/reservations/:id    → Reservation detail + payment status [auth]
```

- `auth.guard` on all non-public routes.
- After login, redirect to the originally requested URL (store returnUrl).
- 401 responses from the gateway → clear token → redirect to `/login`.

---

## 7. Implementation phases

Each phase is independently verifiable against the running stack (`docker compose up -d`).

### Phase 1 — Foundation (core plumbing)

- `api.config.ts`: base URL from `API_BASE_URL` (default `http://localhost:8080`).
- `api-client.ts`: wraps `HttpClient`; attaches Bearer token; maps errors.
- `error.interceptor.ts`: parse `application/problem+json` from the gateway (status, title, detail).
- `auth.service.ts` + `token.interceptor.ts` + `auth.guard.ts`.
- App shell: header (nav + login/logout state), footer, placeholder routes.
- Health check: call `GET /health` on boot; show "services offline" banner on failure.

**Verify:** `ng serve` → login against a running stack → token stored → header shows the user.

### Phase 2 — Catalog (read-only browsing)

- Models: `Event`, `Category`, `Venue` (§3).
- `catalog.api.ts`: `GET /events`, `GET /events/{id}`, `GET /venues`, `GET /venues/{id}`, `GET /categories`.
- Event list page: cards with title, date, venue name (resolve via venue id), price, seats.
- Event detail page: banner, description, venue/category info, price, seat info.
  - `availableSeats` now updates over time (saga complete ✅) but is **eventually
    consistent**; show a small "approximate / updates from booking events" caveat (§4).
- Venue list/detail pages, category chips on the event list.

**Verify:** browse events/venues; detail pages render real data.

### Phase 3 — Account (register, login, profile)

- `account.api.ts`: `POST /users` (register), `POST /users/login`, `GET/PUT /users/{id}`, `DELETE /users/{id}`.
- Register page: validations (email format, password min length, required fields per `CreateUserRequest`).
- Login page: store `token` + `user` from `LoginResponse`; redirect via returnUrl.
- Profile page: view + edit (`UpdateUserRequest`); display errors from domain exceptions (e.g., duplicate email).

**Verify:** register → login → edit profile → refresh keeps session.

### Phase 4 — Admin CRUD (events + venues)

- `admin.api.ts`: `POST /events`, `PUT /events/{id}`, `PUT /events/{id}/pricing`, `DELETE /events/{id}`; same for venues.
- Event form: title, description, category dropdown (from `/categories`), venue dropdown (from `/venues`), date picker, price, total seats, banner URL.
- Venue form: name, address, city, country, capacity.
- Management lists with delete confirmations.
- **Documented limitation:** any authenticated user can reach these pages today (no roles). Keep the guard as auth-only and mark the UI as "admin" for when roles arrive.

**Verify:** create/edit/delete an event and a venue; catalog reflects the change.

### Phase 5 — Reservation flow (current backend capability)

- `booking.api.ts`: `POST /booking/reservations` (body has no `userId` — it comes from the JWT), `GET /booking/reservations/{id}`, `DELETE /booking/reservations/{id}`.
- `payment.api.ts`: `GET /payments/{id}`.
- Event detail → "Book seat": seat section/row/number inputs + price prefilled from event.
- After create: show reservation page (status, seat, expiresAt countdown).
- Reservation detail shows status badge: Pending / Confirmed / Cancelled / Expired.
  - When `status === 'Pending'`, show "payment pending" + cancel button.
  - When `status === 'Confirmed'`, show confirmation (paymentId linked to payment status via `GET /payments/{paymentId}`).
- Cancel button calls `DELETE /booking/reservations/{id}`.
- Because the saga is complete, status will actually advance on its own
  (`PaymentSucceeded` → `Confirmed`, `PaymentFailed`/expiry → `Cancelled`/`Expired`).
  Recommend a small poll or manual refresh on the reservation page.

**What the UI cannot do yet:** in-browser payment (no `client_secret` endpoint) — show status and explanatory copy instead (§4).

---

## 8. Design decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| API access | Only via Gateway :8080 | YARP routes + JWT in one place; services stay unreachable from the browser |
| Token storage | `localStorage` + `Authorization: Bearer` | Matches the gateway's JWT bearer validation with zero backend change |
| HTTP wrapper | Single typed `api-client.ts` | One place for token/error handling |
| State management | Angular signals + services; no NgRx | App is small; signals suffice |
| Date handling | Keep ISO-8601 strings; format in `shared/utils` | Avoid timezone bugs from naive `Date` mutation |
| Availability copy | "Approximate — updates from booking events" | Honest about eventual consistency; the saga is live but not transactional truth |
| Naming | PascalCase JSON kept as-is in TS models | Matches backend records 1:1, less mapping code |

---

## 9. Open questions / backend dependencies (NOT in this plan)

These block future frontend work but are explicitly **not** part of this plan:

1. ~~**Stripe checkout UX**~~ ✅ Resolved 2026-08-07: `GET /payments/{bookingId}/client-secret` endpoint + frontend `@stripe/stripe-js` Payment Element checkout (`STRIPE_PUBLISHABLE_KEY` token in `api.config.ts`, default empty → fallback copy).
2. ~~**"My reservations" list**~~ ✅ Resolved 2026-08-07: `GET /reservations` (X-User-Id scoped) + `/reservations` page and nav link.
3. **Role claims** — needs a role claim in the JWT + gateway policies before "admin" means anything.
4. **Category admin** — needs create/update/delete endpoints on EventService.

> ✅ **Resolved since this plan was written (2026-08-02):** "Live availability" is no
> longer blocked — messaging phases 5–6 are implemented (Event consumers + retry/DLQ),
> so `availableSeats` updates through the saga. The only remaining messaging gap is the
> manual end-to-end verification with Stripe CLI (fase 7), which does not block the UI.

---

## 10. Checklist

> Estado 2026-08-02: el scaffold Angular 22 existe en `frontend/` (SSR + Tailwind 4 +
> vitest, componente raíz placeholder). Ninguna fase está implementada. **Siguiente
> paso: Phase 1 — Foundation** (el backend no bloquea nada de esta fase).

**Phase 1 — Foundation**
- [ ] `api.config.ts` base URL from env
- [ ] `api-client.ts` typed wrapper + token attach
- [ ] `error.interceptor.ts` ProblemDetails mapping
- [ ] `auth.service.ts` + token interceptor + route guard
- [ ] App shell (header/footer) + `/health` banner

**Phase 2 — Catalog**
- [ ] TS models (Event, Category, Venue)
- [ ] `catalog.api.ts` reads
- [ ] Event list + detail pages
- [ ] Venue list + detail pages
- [ ] Category chips

**Phase 3 — Account**
- [ ] Register page
- [ ] Login page + returnUrl redirect
- [ ] Profile view/edit

**Phase 4 — Admin CRUD**
- [ ] `admin.api.ts` (events + venues)
- [ ] Event create/edit/pricing forms
- [ ] Venue create/edit forms
- [ ] Management lists + delete confirmations

**Phase 5 — Reservation flow**
- [ ] `booking.api.ts` + `payment.api.ts`
- [ ] Seat selection UI on event detail
- [ ] Reservation detail + status badge
- [ ] Cancel reservation
- [ ] Payment status link (`GET /payments/{paymentId}`)
