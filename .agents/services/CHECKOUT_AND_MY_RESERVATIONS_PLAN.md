# MassSeats — "My Reservations" + In-Browser Checkout Plan

> Two follow-up features that close the two open frontend gaps documented in
> `FRONTEND_PLAN.md` (§4 / §9): the missing "My reservations" list (needs a
> query endpoint on BookingService) and the missing in-browser Stripe checkout
> (needs a `client_secret` path on PaymentService).
>
> **Status (2026-08-04):** planned only — nothing implemented. Frontend Phase 5
> (reservation flow) is committed; this plan builds on top of it.
>
> **Chosen design:** Opción A for the checkout — persist the Stripe
> `client_secret` on the Payment aggregate and expose it via a dedicated
> endpoint (see §3.2 for why).

---

## 1. Problem statement

| Gap | Why it exists today | This plan |
|-----|---------------------|-----------|
| **"My reservations" list** | BookingService exposes only `POST /reservations`, `GET /reservations/{id}`, `DELETE /reservations/{id}`. There is no list-by-user query. The frontend currently tracks created reservation ids in `localStorage` (best effort, from Phase 5). | New `GET /reservations` endpoint (user scoped via `X-User-Id`) + a "My reservations" page. |
| **In-browser checkout** | PaymentService creates the Stripe PaymentIntent server-side on `SeatReserved`, but discards the `client_secret` (returns only `intent.Id`) and creates it without `automatic_payment_methods`. The browser has no way to render Stripe Elements. | Persist the `client_secret`, expose it through a scoped endpoint, and render a Stripe Payment Element checkout in the frontend. |

Both are **backend-first**: the endpoints must exist, be tested, and run before
any frontend work starts.

---

## 2. Current state (verified 2026-08-04)

### 2.1 BookingService (my reservations)

- `IReservationRepository` (`Domain/Interfaces`): `GetByIdAsync`, `GetExpiredPendingAsync`, `AddAsync`, `Update`, `SaveChangesAsync`. **No list-by-user method.**
- `ReservationAppService` (`Application/Services`): `CreateAsync`, `GetByIdAsync`, `ConfirmAsync`, `CancelAsync`, `ExpireDueReservationsAsync`.
- `ReservationEndpoints` (`API/Endpoints`): `MapPost("/")`, `MapGet("/{id:guid}")`, `MapDelete("/{id:guid}")`.
- `POST /` reads the user id from the `X-User-Id` header injected by the gateway — **not** from the body. The new list endpoint must follow the same pattern.
- Gateway route `booking-route` matches `/booking/{**catch-all}` and strips the `/booking` prefix → no gateway change needed.

### 2.2 PaymentService (checkout)

- `SeatReservedConsumer` (`Infrastructure/Messaging`) → `IPaymentService.InitiateAsync` (`Application/Services/PaymentAppService`).
- `PaymentAppService.InitiateAsync` calls `IPaymentGateway.CreatePaymentIntentAsync` **before** persisting the aggregate (external side effect first), then `Payment.Create(bookingId, stripePaymentIntentId, amount, currency)`.
- `StripePaymentGateway.CreatePaymentIntentAsync` (`Infrastructure/Gateways`):
  - returns **only** `intent.Id` (the `client_secret` from Stripe's response is dropped);
  - builds `PaymentIntentCreateOptions` with only `Amount` (cents) + `Currency` — `AutomaticPaymentMethods` is **not enabled**, so the intent cannot be confirmed from the browser with Stripe Elements;
  - already sets an idempotency key (`payment-intent:{bookingId:N}`) — keep it.
- `Payment` aggregate (`Domain/Entities`): `StripePaymentIntentId`, `Amount`, `Currency`, `PaymentMethod`, `FailureReason`, `Status`, timestamps. **No `ClientSecret` property.**
- `PaymentResponse` (`Application/DTOs`) mirrors the aggregate — **no client secret** (correct: never in the general read model).
- `PaymentEndpoints` (`API/Endpoints`): `GET /payments/{id}` and `POST /payments/webhook`. **No client-secret endpoint.**
- `StripeOptions` (`Infrastructure/Configuration`): `SecretKey` + `WebhookSecret` only — the **publishable key** will be added for the frontend (secret key never leaves the backend).
- Migrations live in `PaymentService.Infrastructure/Persistence/Migrations/` (existing: `AddPaymentFailureReason`, `AddProcessedStripeEvents`, etc.). New column → new migration.
- Gateway route `payments-route` matches `/payments/{**catch-all}` → no gateway change needed.

---

## 3. Design

### 3.1 "My reservations" (BookingService)

**Security decision (important):** the user id comes from the `X-User-Id` header
injected by the API Gateway — **never** from a `?userId=` query parameter. A
query param would let any authenticated user list anyone else's reservations.

Changes, bottom-up:

| Layer | File (current) | Change |
|-------|----------------|--------|
| Domain | `IReservationRepository` | Add `Task<IReadOnlyList<Reservation>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)`. |
| Infrastructure | `ReservationRepository` | Implement: `Where(r => r.UserId == userId).OrderByDescending(r => r.ReservedAt).ToListAsync(ct)`. |
| Application | `IReservationService` + `ReservationAppService` | Add `Task<IReadOnlyList<ReservationResponse>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)`; map via existing `ToResponse()`. |
| API | `ReservationEndpoints` | Add `group.MapGet("/", ...)` reading `X-User-Id` (same guard as `POST`: empty/unparseable → `Results.Unauthorized()`), returning `Results.Ok(list)`. |

**Response shape:** `ReservationResponse[]` (reuse the existing record; the
frontend already models it as `Reservation`). No pagination for MVP — note it as
a future consideration in §6.

### 3.2 In-browser checkout (PaymentService) — **Opción A: persist `client_secret`**

Why A over B (fetch from Stripe on demand): the `client_secret` is the key the
browser will use to confirm; we want the exact secret generated for the intent
we created, not a possibly-regenerated one. It also avoids an extra Stripe round
trip per checkout and keeps the value stable across page refreshes while
`Pending`.

Changes, bottom-up:

| Layer | File (current) | Change |
|-------|----------------|--------|
| Application | `IPaymentGateway` | Change `CreatePaymentIntentAsync` to return a `PaymentIntentResult(string Id, string ClientSecret)` record (new, in `Application/DTOs` or `Interfaces`). |
| Infrastructure | `StripePaymentGateway` | Return the full result: `return new PaymentIntentResult(intent.Id, intent.ClientSecret);`. **Also** enable `AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true }` in `PaymentIntentCreateOptions` (required for browser confirmation with Elements). Keep the idempotency key. |
| Domain | `Payment` | Add `public string ClientSecret { get; private set; }`; extend the private ctor and `Payment.Create(...)` signature to accept it; add a non-empty guard (like the existing `StripePaymentIntentId` guard). |
| Infrastructure | new EF migration | `AddPaymentClientSecret` — nullable-safe non-null column; backfill is not needed (all rows were created without it → set as required with existing data handling as per repo convention). |
| Application | `IPaymentService` + `PaymentAppService` | Add `Task<string?> GetClientSecretAsync(Guid bookingId, CancellationToken ct = default)` returning the secret **only when `Status == Pending`** (after `Succeeded`/`Failed` there is nothing to confirm — return null → 404/410). |
| API | `PaymentEndpoints` | Add `GET /payments/{bookingId}/client-secret` → `GetByBookingIdAsync` → if payment missing or not `Pending`, `Results.NotFound()`; else `Results.Ok(new { clientSecret = ..., paymentIntentId = ... })`. |

**PaymentResponse stays clean** — the client secret is never included in
`GET /payments/{id}`.

### 3.3 Frontend

| Area | Change |
|------|--------|
| `features/bookings/booking.api.ts` | Add `listReservations(): Observable<Reservation[]>` → `GET /booking/reservations`. |
| New page | `features/bookings/pages/my-reservations/` — table/cards of the user's reservations: event id, seat, price, status badge, `expiresAt`, link to `/reservations/{id}`. Follow the existing page pattern (signals, `isPlatformBrowser` guard, `errorMessage`). |
| Route | Add `{ path: 'reservations', component: MyReservations, canActivate: [authGuard] }` (exact path — coexists with `/reservations/:id`). |
| Shell/nav | Add a "My reservations" link. |
| New dependency | `@stripe/stripe-js` (dev-dependency of the app bundle). Publishable key from `StripeOptions`-style env config on the frontend (`API_BASE_URL`-like pattern in `api.config.ts`); the secret key stays in PaymentService only. |
| `features/bookings/payment.api.ts` | Add `getClientSecret(bookingId: string): Observable<{ clientSecret: string; paymentIntentId: string }>` → `GET /payments/${bookingId}/client-secret`. |
| Checkout UI | In `reservation-detail`, when `status === 'Pending'`, replace the static "payment pending" copy with a "Pay now" flow: load the client secret, mount Stripe Elements Payment Element, `confirmPayment({ clientSecret, elements })`. SSR-safe: Stripe.js initializes only in the browser. After confirmation the existing webhook + saga advance the status → the existing 5s poll picks up `Confirmed`. |
| Fallback copy | If the client secret cannot be loaded (payment already resolved), keep the current explanatory copy and hide the pay form. |

### 3.4 Gateway

No gateway changes: `/booking/**` and `/payments/**` routes already exist and
forward to the right clusters; both new endpoints fall under them.

---

## 4. Security notes

1. **`X-User-Id` header, not query param** for `GET /reservations` — prevents cross-user data exposure (see §3.1).
2. **Client secret exposure:** only returned while the payment is `Pending` and only via a booking-scoped endpoint. Still worth an explicit **ownership check** follow-up: today `GET /payments/{id}` does not verify the payment belongs to the JWT user. Ideal: verify `bookingId` ownership before returning the secret (via BookingService or a lightweight check). Flagged as a known debt — see §6.
3. **Publishable key only** on the frontend; `SecretKey` and `WebhookSecret` remain server-side.

---

## 5. Implementation order (each step verifiable)

```
Step 1  BookingService: GetByUserIdAsync (domain → infra → app → endpoint) + tests
   ▼
Step 2  PaymentService: PaymentIntentResult + AutomaticPaymentMethods + Payment.ClientSecret
        + migration AddPaymentClientSecret + GetClientSecretAsync + endpoint + tests
   ▼
Step 3  Frontend: listReservations + My Reservations page + route + nav
   ▼
Step 4  Frontend: Stripe Elements checkout on reservation-detail (pay now flow)
   ▼
Step 5  Docs: update FRONTEND_PLAN.md §4/§9 → both gaps resolved
```

### How to verify

- **Step 1:** `GET /booking/reservations` with a valid JWT returns only the caller's reservations, newest first. Unauthenticated/malformed header → 401. Unit + integration tests in `BookingService.Infrastructure.Tests` (mirror `PaymentConsumersTests` style).
- **Step 2:** after `POST /booking/reservations` the PaymentIntent in Stripe has `automatic_payment_methods.enabled = true`; `GET /payments/{bookingId}/client-secret` returns a secret while `Pending` and 404 after the webhook resolves it. Tests in `PaymentService.Infrastructure.Tests` + `StripePaymentGateway` behavior.
- **Step 3:** `/reservations` page renders real data from the backend.
- **Step 4:** manual end-to-end with Stripe CLI (`stripe listen --forward-to .../payments/webhook`, `stripe trigger payment_intent.succeeded`) — reservation flips Pending → Confirmed and the UI reflects it.

---

## 6. Open items / known debt

- **Pagination** for the reservations list (out of scope for MVP).
- **Ownership check** on payment-scoped endpoints (`GET /payments/{id}`, `GET /payments/{bookingId}/client-secret`): today they do not verify the payment belongs to the JWT user. Recommended before public rollout.
- **Frontend routing collision check:** `/reservations` (exact) vs `/reservations/:id` — the more specific route must come after (or the exact one uses `pathMatch: 'full'`) to avoid swallowing the detail route.

---

## 7. Checklist

**BookingService — "My reservations"**
- [ ] `IReservationRepository.GetByUserIdAsync` (Domain)
- [ ] `ReservationRepository` implementation (Infrastructure)
- [ ] `IReservationService` + `ReservationAppService.GetByUserIdAsync` (Application)
- [ ] `GET /reservations` endpoint reading `X-User-Id` (API)
- [ ] Tests (unit + integration)

**PaymentService — in-browser checkout (Opción A)**
- [ ] `PaymentIntentResult` record (Application)
- [ ] `IPaymentGateway` + `StripePaymentGateway`: return `Id` + `ClientSecret`, enable `AutomaticPaymentMethods` (Infrastructure)
- [ ] `Payment.ClientSecret` property + `Create` signature + guard (Domain)
- [ ] Migration `AddPaymentClientSecret` (Infrastructure)
- [ ] `IPaymentService.GetClientSecretAsync` — Pending-only (Application)
- [ ] `GET /payments/{bookingId}/client-secret` endpoint (API)
- [ ] Tests (gateway, service, endpoint)

**Frontend**
- [ ] `BookingService.listReservations()` + spec
- [ ] My Reservations page + route + nav
- [ ] `@stripe/stripe-js` + publishable key config
- [ ] `PaymentService.getClientSecret()` + spec
- [ ] Stripe Elements "Pay now" flow on reservation-detail (SSR-safe)
- [ ] Keep fallback copy when checkout is unavailable

**Docs**
- [ ] Update `FRONTEND_PLAN.md` §4 / §9 (both gaps resolved)
