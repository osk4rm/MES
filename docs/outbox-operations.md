# Transactional outbox operations: inspect, fix, replay

This runbook covers day-2 operations for the transactional outbox
(slices 1–3, issues #258/#259/#260): staged `shared."OutboxMessages"` rows,
the background relay, and poison-message recovery. Every query below runs
against the documented schema (migration `AddOutboxMessages`):

| Column | Type | Meaning |
|--------|------|---------|
| `"Id"` | uuid | Row id (also the log correlation key). |
| `"TenantId"` | uuid | Owning tenant; relay dispatches under this tenant's scope. |
| `"IdempotencyKey"` | varchar(64), unique | One GUID per staged event. |
| `"Type"` | varchar(1000) | Assembly-qualified CLR event type. |
| `"Payload"` | text | JSON snapshot of the event. |
| `"OccurredOnUtc"` | timestamptz | When the source transaction staged the row. |
| `"Dispatched"` | boolean | `true` = delivered exactly once by the relay. |
| `"RetryCount"` | integer | Attempts so far; pinned to the attempt budget when parked. |

Relay configuration (`Outbox` section, defaults in parentheses): `Enabled`
(true), `PollIntervalSeconds` (10, clamped 1–3600), `BatchSize` (100, clamped
1–500), `MaxAttempts` (5, clamped 1–100).

A row whose `"RetryCount"` reaches `MaxAttempts` is **parked as poison**: the
relay query excludes it, so it is never refetched. `Dispatched = true` always
means "delivered", never "parked" (no schema change was made for poison
state). Raising `MaxAttempts` re-enlists parked rows automatically.

## 1. Inspect: list parked (poison) rows

Run against the plant database. The select lists type, tenant and age but
**never the payload** — payloads carry business data (e.g. the tenant admin's
password hash) and must not land in tickets or chat logs.

```sql
SELECT "Id",
       "TenantId",
       "Type",
       "OccurredOnUtc",
       "RetryCount"
FROM shared."OutboxMessages"
WHERE "Dispatched" = false
  AND "RetryCount" >= 5          -- replace 5 with the live Outbox:MaxAttempts
ORDER BY "OccurredOnUtc";
```

Healthy system: zero rows. Any row here was logged at `Error` level with its
outbox id and event type — correlate with the application logs first:

```text
Outbox {OutboxId} of type {EventType} parked as poison after {Attempts} attempts
Outbox {OutboxId} of type {EventType} parked as poison: {Reason}
```

Also useful — undispatched backlog per tenant (should drain within a few poll
intervals):

```sql
SELECT "TenantId",
       count(*) AS undispatched
FROM shared."OutboxMessages"
WHERE "Dispatched" = false
  AND "RetryCount" < 5           -- replace 5 with the live Outbox:MaxAttempts
GROUP BY "TenantId"
ORDER BY undispatched DESC;
```

## 2. Fix: decide per parked row

| Symptom (log `Reason` / row content) | Meaning | Fix |
|--------------------------------------|---------|-----|
| `unknown event type '...'` | The staged CLR type no longer resolves — the event was renamed/removed without a replay adapter. | Deploy a type adapter (or re-stage the event under its new type), then replay (step 3). |
| `payload cannot be deserialized as '...'` | Corrupt or hand-edited payload. | Inspect the single row's payload (`SELECT "Payload" ... WHERE "Id" = '...'`) in a restricted session, correct the JSON, then replay. |
| `payload of N characters exceeds the maximum` | Row predates the size cap. | Split the source write or raise the cap in code; then replay or drop. |
| `parked as poison after N attempts` with a handler exception | The handler (e.g. RBAC provisioning, admin-user insert) kept failing — downstream bug or constraint violation. | Fix the downstream cause first (migrations applied? unique conflict on admin email?), then replay. |
| Tenant row missing for a `TenantCreatedEvent` row | Tenant save was removed out-of-band while its event survived. | Safe to drop (step 4): the listener is idempotent, but there is no tenant to provision under. |

Never edit `"Type"` or `"Payload"` to another tenant's values: the relay
dispatches strictly under the row's `"TenantId"`, and the
`TenantCreatedEvent` listener binds provisioning to the payload tenant id —
cross-tenant edits would provision under the wrong tenant.

## 3. Replay: re-enlist a fixed row

Resetting the retry counter below the budget re-enlists the row; the next
relay poll (≤ `PollIntervalSeconds`) picks it up oldest-first. Replay is safe
to repeat: `TenantCreatedEvent` handling is idempotent (RBAC rows reconcile by
explicit tenant id, an existing admin email is skipped), and every other
consumer must tolerate at-least-once delivery.

```sql
-- Re-enlist one fixed row (single-row WHERE: never reset the whole table).
UPDATE shared."OutboxMessages"
SET "RetryCount" = 0
WHERE "Id" = '00000000-0000-0000-0000-000000000000'  -- the parked outbox id
  AND "Dispatched" = false;
```

Verify delivery:

```sql
SELECT "Id", "Dispatched", "RetryCount"
FROM shared."OutboxMessages"
WHERE "Id" = '00000000-0000-0000-0000-000000000000';
```

Expect `"Dispatched" = true` after the next poll. If the row parks again
(`"RetryCount"` back at the budget), the downstream cause is not fixed —
return to step 2. To force an immediate cycle instead of waiting for the poll
interval, restart the `api`/`api-prod` container (the relay loop is
delay-first, so the first cycle runs one interval after start).

Tenant-signup gap (slice 3): tenant creation commits the tenant row first and
stages the `TenantCreatedEvent` row second. If the process dies between the
two commits, the tenant exists with no staged event and the admin is never
provisioned. Recover by re-staging exactly one row for the affected tenant
(the listener provisions idempotently, so a duplicate is harmless):

```sql
-- Inspect first: a live tenant with no undispatched tenant-created row.
SELECT t."Id", t."Name", t."ContactEmail"
FROM multitenancy."Tenants" t
WHERE t."IsActive" = true
  AND NOT EXISTS (SELECT 1
                  FROM shared."OutboxMessages" o
                  WHERE o."TenantId" = t."Id");
```

Re-stage through the application (preferred — keeps payload shape stable):
re-run the signup for the same tenant name fails on the unique name, so
instead relay the tenant explicitly via the `OutboxRelayService` entry point
or re-stage with the writer; raw `INSERT` is a last resort and must reuse the
exact `TenantCreatedEvent` JSON shape (`Id`, `Email`, `HashedPassword`).

## 4. Drop: discard an unfixable row

Only for rows that can never be delivered and whose business effect was
achieved out-of-band (document the reason in the incident log). Deleting a
parked row is permanent — the relay never refetches it either way, so
deletion only clears the backlog listing.

```sql
DELETE FROM shared."OutboxMessages"
WHERE "Id" = '00000000-0000-0000-0000-000000000000'
  AND "Dispatched" = false
  AND "RetryCount" >= 5;         -- safety: only parked rows
```

## 5. Raising the attempt budget

`Outbox:MaxAttempts` can be raised (up to the 100 hard cap) to give a
struggling downstream more attempts: rows parked under the old budget
automatically become eligible again because the relay query compares
`"RetryCount" < MaxAttempts` at runtime — no replay `UPDATE` needed. Lowering
the budget parks more rows sooner; it never deletes anything.
