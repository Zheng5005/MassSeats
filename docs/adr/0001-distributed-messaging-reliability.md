# ADR-0001: Adopt choreographed sagas with reliable RabbitMQ messaging

- Status: Accepted
- Date: 2026-08-04
- Decision owners: MassSeats backend team
- Scope: BookingService, PaymentService, and EventService

## Context

A reservation crosses three independently deployed services:

1. Booking holds a seat.
2. Payment processes the charge.
3. Event updates its availability projection.

These services own separate databases. A transaction cannot atomically update
business state and publish a message across PostgreSQL and RabbitMQ.

RabbitMQ provides at-least-once delivery, so messages can be delivered more
than once. Consumers may also fail temporarily or receive malformed messages.

The architecture must therefore:

- avoid coupling service availability through synchronous request chains;
- prevent committed business changes from losing their integration events;
- process duplicate messages safely;
- isolate poison messages;
- preserve business logic inside the service that owns the domain.

## Decision

MassSeats coordinates the reservation lifecycle through a **choreographed
saga** using RabbitMQ integration events.

```text
┌─────────┐  SeatReserved   ┌─────────┐
│ Booking │────────────────▶│ Payment │
└────┬────┘                 └────┬────┘
     │                           │
     │ Reservation*              │ PaymentSucceeded
     │                           │ PaymentFailed
     ▼                           ▼
┌─────────┐                 ┌─────────┐
│  Event  │                 │ Booking │
└─────────┘                 └─────────┘
```

### Messaging topology

- Use raw `RabbitMQ.Client`.
- Use one durable topic exchange: `massseats.events`.
- Give each consuming service one durable queue.
- Route messages with lowercase dotted routing keys.
- Keep integration contracts and technical messaging utilities in
  `BuildingBlocks.Messaging`.
- Keep business decisions and domain transitions inside each service.

### Delivery guarantees

- Treat delivery as **at least once**, not exactly once.
- Acknowledge messages manually only after successful processing.
- Store every outgoing integration event in a transactional **Outbox** beside
  the originating business change.
- Publish pending Outbox records asynchronously.
- Record consumed message IDs in a transactional **Inbox** beside the
  consumer's business change.
- Treat duplicate Inbox message IDs as successfully processed.

### Failure handling

- Retry transient consumer failures a bounded number of times.
- Use durable quorum retry queues with delayed dead-letter routing.
- Route messages that exhaust their retry budget to a service-specific DLQ.
- Use publisher confirms when transferring messages between retry and
  dead-letter stages.
- Retain Outbox publication failures for later retry and diagnostics.

### Consistency model

- Accept eventual consistency between services.
- Booking remains the source of truth for seat ownership.
- Event's `AvailableSeats` is an informational projection.
- Competing terminal transitions use optimistic concurrency so only one
  transition and its associated Outbox event can commit.

## Consequences

### Positive

- Services remain independently deployable and own their data.
- A database commit cannot silently lose its integration event.
- Duplicate RabbitMQ deliveries do not repeat business effects.
- Temporary failures do not immediately become permanent failures.
- Poison messages cannot block the main service queue indefinitely.
- The complete message lifecycle is inspectable through Outbox, Inbox, retry
  queues, and DLQs.

### Negative

- The system is eventually consistent; reads can temporarily show stale data.
- Each participating service owns additional tables, workers, and migrations.
- Operators must monitor Outbox backlog, retry queues, and DLQs.
- Message contracts require compatibility discipline.
- Raw `RabbitMQ.Client` provides control and learning value but requires more
  infrastructure code than MassTransit or another messaging framework.
- Exactly-once business behavior depends on application idempotency and
  database constraints, not RabbitMQ alone.

## Alternatives considered

### Synchronous HTTP orchestration

Rejected because it couples service availability, creates long failure chains,
and still cannot provide a transaction across service databases.

### Central saga orchestrator

Not selected because the current workflow is small and naturally represented
by domain events. An orchestrator would add another owner and runtime component.
This decision should be revisited if the saga gains many branches, deadlines,
or manual compensation steps.

### Publish directly after saving business state

Rejected because a process failure between the database commit and RabbitMQ
publication would permanently lose the event.

### Exactly-once messaging

Rejected because RabbitMQ does not provide end-to-end exactly-once business
processing. At-least-once delivery with transactional idempotency gives a
clearer and more reliable contract.

### MassTransit or another messaging framework

Not selected because the project intentionally uses raw `RabbitMQ.Client` to
make topology, acknowledgements, retries, and DLQ behavior explicit.

## Operational requirements

Monitor and alert on:

- unprocessed Outbox record age and count;
- repeated Outbox publication failures;
- retry queue depth;
- DLQ depth;
- consumer processing failures;
- growing Inbox tables and their retention policy.

## Revisit this decision when

- a workflow requires centralized visibility or manual intervention;
- choreography becomes difficult to understand or debug;
- message volume makes polling Outbox workers inadequate;
- contract evolution requires schema governance;
- operational cost justifies adopting a messaging framework;
- additional regions or brokers require stronger ordering guarantees.

## References

- [Messaging implementation plan](../../.agents/services/MESSAGING_PLAN.md)
- [`BuildingBlocks.Messaging.RabbitMQ`](../../services/BuildingBlocks/src/BuildingBlocks.Messaging.RabbitMQ)
- [Integration event contracts](../../services/BuildingBlocks/src/BuildingBlocks.Messaging/Contracts)
