# Attachments and audit trail

Attachments carry the files (drawings, certificates, photos) and
the audit trail carries the history: together they answer "what
evidence backs this record, and who changed it when".

## Attachments

Attachments are polymorphic: one endpoint serves every owner type
(orders, machines, lots and the rest) through an
`ownerType` + `ownerId` pair. Listing and downloading need the
`attachments.read` permission plus the owner module's scope
permission; uploading and deleting need `attachments.write`
(`403` without them).

| Action | API | Notes |
|--------|-----|-------|
| List | `GET /api/attachments?ownerType=…&ownerId=…` (`200`) | All files on one owner entity |
| Upload | `POST /api/attachments` multipart form (`200`) | Fields `ownerType`, `ownerId`, `file`, optional `description` |
| Download | `GET /api/attachments/{id}/download` | Forced download with a sanitized file name |
| Delete | `DELETE /api/attachments/{id}` (`204`) | Removes the row |

Upload validation, in order:

1. A file is required — an empty upload returns `400`.
2. The declared size must fit the configured app cap, else `400`
   naming the limit; oversized payloads are already rejected at
   the 11 MiB edge before the handler buffers anything.
3. The MIME type must pass the allowlist plus sniffed-type
   verification — a renamed executable does not pass.
4. The per-tenant quota must have room.

Safe download: the response forces a download disposition
(`Content-Disposition: attachment`), serves only allowlisted
content types as-is and everything else as
`application/octet-stream`, and sets
`X-Content-Type-Options: nosniff` — so an uploaded HTML or SVG
payload can never execute in another user's browser.

Procedure:

1. Open the owner record and list its attachments to see what
   evidence already exists.
2. Upload with the owner type, owner id, file and a short
   description (revision, certificate number, photo context).
3. Download to verify the stored bytes before referencing the
   file in a handover or a quality record.
4. Delete superseded revisions; the audit trail below still shows
   that a file existed and when it left.

Blob storage is the database itself — attachments persist as
rows — so the database backup in
[Deploy, backup and operations](25-deploy-backup-ops.md) is a full
system backup including files.

## Audit trail

`GET /api/audit-events` (paged browse, signed-in, no permission
required) returns the append-only history — one row per create,
update and delete of Production Orders, Machines and Production
Confirmations — in descending time order. Each row carries the
actor, the action, the entity and the timestamp.

There are no write endpoints on purpose: history rows are written
by the system as side effects of the operations above and are
never edited through the API.

Procedure:

1. Open the record whose past you need (or query the browse with
   its entity filter) to see every change newest first.
2. Identify the actor and time of a suspicious edit, then confirm
   the current values on the record itself.
3. Production-order history is also reachable per order as page 1
   of size 50 (`GET /api/production-orders/{id}/history`) — see
   [Production Orders](07-production-orders.md).

## Tenant isolation

Attachments and audit rows are tenant-scoped: listing an
owner id from another tenant returns nothing (or `404` on direct
ids), downloads of foreign files resolve as `404`, and the audit
browse never mixes tenants.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `attachments.read` / `attachments.write` (plus owner scope) | `403` |
| Missing file on upload | `400` |
| File over the size cap, blocked type, or quota exhausted | `400` |
| Unknown attachment id, or foreign id | `404` |

## Next steps

- Orders whose history you will read: [Production Orders](07-production-orders.md).
- Files referenced at shift change: [Shift handover](13-shift-handover.md).
- Backing up the files with everything else: [Deploy, backup and operations](25-deploy-backup-ops.md).
- Tracing an error report to its logs: [Health, correlation and observability](24-observability-health.md).
