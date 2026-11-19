# Consignment Operations Console Architecture Specification

## 1. Scope and Authority

This specification implements the supplied assignment. `assignmentBrief.md` is authoritative, and `intent.md` is the approved requirements baseline.

The target is a working 4-hour demonstration: a .NET 8 API, PostgreSQL database, and React/TypeScript web application. Prefer the existing scaffold and straightforward EF Core patterns over new abstractions or infrastructure.

The implementation is a demo, not a production authentication or administration system. `X-Tenant` is the request-scoped tenant identity.

## 2. Architecture and Existing Structure

### 2.1 Technology Choices

- PostgreSQL 16: the supported next-generation database and target for the later PostgreSQL migration design.
- EF Core: data access, migrations, tenant-qualified LINQ, and database transactions.
- ASP.NET Core controllers: conventional REST endpoints and Swagger.
- React/TypeScript with Vite: dispatcher UI.
- Docker Compose: intended demo startup, subject to verification.

Keep dependencies and libraries to those needed for the demo. No state-management library or component framework is required.

### 2.2 Actual Repository Structure

Use the existing repository layout:

```text
ConsignmentConsole/
  src/Api/
    Controllers/
    Services/
    Domain/
    Data/
    Models/
  tests/Api.Tests/
  web/
    src/
  consignments.csv
  ConsignmentConsole.sln
  ConsignmentConsole.slnx
  README.md
  AI_USAGE.md
  DEVELOPMENT_LOG.md
  MIGRATION.md
  spec.md
```

Add files only where they directly support the working slice. Replace or remove the existing WeatherForecast placeholder. Remove unused Vite starter assets if convenient. Do not create parallel domain, test, or frontend projects solely for architectural symmetry.

EF Core migrations belong under `src/Api/Data/Migrations/`. The compose file, if added, belongs at the repository root.

### 2.3 Runtime Flow

```text
React tenant switcher
  -> X-Tenant header
  -> ASP.NET request handling
  -> resolved tenant ID
  -> tenant-qualified EF Core query or transaction
  -> PostgreSQL
```

Startup CSV import is a system operation and is not an HTTP tenant operation. It does not require or use `X-Tenant`.

## 3. Domain Model

### 3.1 Tenant

- `Id`: UUID primary key.
- `Code`: unique, nonblank demo code.
- `Name`: display name.
- `Consignments`: one-to-many collection.

Tenant codes from `consignments.csv` are created or resolved during startup import.

### 3.2 Consignment

- `Id`: UUID primary key.
- `TenantId`: tenant foreign key.
- `ConsignmentNumber`: nonblank string.
- `CustomerName`: nonblank string.
- `ReceiverName`: nonblank string.
- `ReceiverSuburb`: nonblank string.
- `ReceiverState`: nonblank string.
- `ReceiverPostcode`: nonblank string.
- `ItemCount`: integer, non-negative.
- `WeightKg`: decimal(10,2), non-negative.
- `ServiceType`: nonblank string.
- `Status`: one of the lifecycle statuses.
- `CreatedAt`: timestamp with time zone.
- `Version`: explicit positive integer, initially `1`.
- `History`: status-history collection.

The unique key `(TenantId, ConsignmentNumber)` allows reuse of a consignment number across tenants while preventing reuse within one tenant.

### 3.3 StatusHistory

Every successful status transition creates one history record:

- `Id`: UUID primary key.
- `TenantId`: tenant ownership key.
- `ConsignmentId`: consignment key.
- `FromStatus`: lifecycle status.
- `ToStatus`: lifecycle status.
- `ChangedAt`: timestamp with time zone.
- `SignedBy`: nullable.
- `DeliveryTime`: nullable.
- `FailureReason`: nullable.

No dispatcher identity or synthetic initial imported record is required.

## 4. PostgreSQL Schema

### 4.1 Tables

```text
tenants
  id uuid primary key
  code varchar(100) not null unique
  name varchar(200) not null

consignments
  id uuid primary key
  tenant_id uuid not null references tenants(id)
  consignment_number varchar(100) not null
  customer_name varchar(200) not null
  receiver_name varchar(200) not null
  receiver_suburb varchar(200) not null
  receiver_state varchar(100) not null
  receiver_postcode varchar(20) not null
  item_count integer not null
  weight_kg numeric(10,2) not null
  service_type varchar(100) not null
  status varchar(30) not null
  created_at timestamptz not null
  version integer not null
  unique (tenant_id, consignment_number)
  unique (tenant_id, id)

status_history
  id uuid primary key
  tenant_id uuid not null
  consignment_id uuid not null
  from_status varchar(30) not null
  to_status varchar(30) not null
  changed_at timestamptz not null
  signed_by varchar(200) null
  delivery_time timestamptz null
  failure_reason varchar(40) null
  foreign key (tenant_id, consignment_id)
    references consignments (tenant_id, id) on delete cascade
```

### 4.2 Constraints

EF Core migrations should include practical database constraints:

- `Consignment.ItemCount >= 0`.
- `Consignment.WeightKg >= 0`.
- `Consignment.Version >= 1`.
- `Consignment.Status` is one of the eight lifecycle values.
- `StatusHistory.FromStatus` and `StatusHistory.ToStatus` are known lifecycle values.
- `StatusHistory.FailureReason` is null except for `to_status = 'failed_delivery'`.
- `StatusHistory.FailureReason` is one of `no_one_home`, `address_incorrect`, `refused`, or `access_issue` when present.
- `SignedBy` and `DeliveryTime` are both present or both null.
- When `ToStatus = 'delivered'`, `FromStatus = 'out_for_delivery'`, and both delivery fields are present.
- When `ToStatus = 'failed_delivery'`, `FromStatus = 'out_for_delivery'`, and the failure reason is present.
- Other history outcomes have null `SignedBy`, `DeliveryTime`, and `FailureReason`.
- `Consignment` must have a unique alternate key on `(TenantId, Id)` in addition to its primary key on `Id`.
- `StatusHistory` must have one composite foreign key `(TenantId, ConsignmentId)` referencing `Consignment(TenantId, Id)`. This is the enforceable tenant-ownership invariant; two independent foreign keys are not equivalent protection.
- Configure the composite alternate key and composite EF Core relationship explicitly, and ensure the generated migration creates the unique constraint and composite foreign key. Do not model independent `TenantId` and `ConsignmentId` foreign keys as a substitute.

Lifecycle transition validation remains in application code as well as in database constraints.

### 4.3 Indexes

- Unique `tenants.code`.
- Unique `(tenant_id, consignment_number)`.
- `(tenant_id, status, created_at DESC)` for tenant filtering and paging.
- `(consignment_id, changed_at DESC)` for history.
- `(tenant_id, consignment_id, changed_at DESC)` for tenant-owned history reads.

No trigram, full-text index, search service, or additional search infrastructure is required for approximately 70 rows. Case-insensitive `ILIKE` over the consignment number and receiver name is sufficient.

## 5. Tenant Isolation

### 5.1 Request Isolation

- Consignment list, detail, update, and history access require `X-Tenant`.
- Missing or blank `X-Tenant` returns `400 TENANT_REQUIRED`.
- Unknown tenant codes return `404 NOT_FOUND`.
- Middleware or a focused tenant resolver resolves the header to a server-known tenant ID.
- Controllers pass only that resolved tenant ID to services.
- List and count queries include `TenantId == tenantId`.
- Detail and history queries include both record ID and tenant ID.
- Status updates include tenant ID in both lookup and conditional update.
- History reads are reached only through a tenant-scoped consignment.
- Cross-tenant IDs and consignment numbers return `404 NOT_FOUND`, never a permission or existence disclosure.
- Tenant codes and tenant IDs are never accepted as replacement record IDs in request bodies.

Paging uses the tenant-scoped filtered query: status filter, search, count, and `Skip/Take` all operate on the same tenant-qualified query.

### 5.2 Startup Import Isolation

Startup import reads each CSV row's `tenant_code`, resolves or creates that tenant, and performs duplicate/conflict matching by `(TenantId, ConsignmentNumber)`. It is system-level work and has no `X-Tenant` header.

### 5.3 RLS Migration Design Only

RLS is not implemented in the demo.

For migration, set a server-validated tenant context at the start of each tenant transaction, such as `app.tenant_id`, from the tenant code or resolved tenant ID:

1. Reconcile and migrate tenant mappings and consignment ownership.
2. Backfill history with the owning tenant ID and verify no mismatches.
3. Enable RLS on `consignments` and `status_history` with tenant predicates.
4. Force RLS for application roles and route EF commands through tenant context.
5. Keep application tenant predicates as defense in depth.
6. Pilot one carrier by routing a selected slice of reads and writes to PostgreSQL while SQL Server remains the reconciliation source.
7. Reconcile tenant, consignment, status, and history counts before cutover.
8. Roll back routing and tenant-context deployment if reconciliation or pilot checks fail.

## 6. Status Lifecycle

| From | Allowed next status |
|---|---|
| `booked` | `picked_up`, `cancelled` |
| `picked_up` | `in_transit` |
| `in_transit` | `out_for_delivery` |
| `out_for_delivery` | `delivered`, `failed_delivery` |
| `failed_delivery` | `out_for_delivery`, `returned` |
| `delivered`, `returned`, `cancelled` | none |

Status updates:

1. Resolve the tenant.
2. Begin a database transaction.
3. Reload the consignment with `Id == id && TenantId == tenantId`.
4. Compare the request version with the stored positive `Version`.
5. Validate the normalized target status and transition.
6. For `delivered`, require a nonblank signer and delivery time.
7. For `failed_delivery`, require exactly one recognized failure reason.
8. Execute the tenant-and-version conditional update.
9. Insert exactly one outcome-valid history row only if the conditional update affected one row.
10. Commit both operations together.

Unknown statuses, invalid transitions, missing proof, and missing or unknown failure reasons return `400` validation errors. Delivery time has no brief-defined relationship to current time or creation time. Re-applying the current status is invalid rather than a no-op.

### 6.1 Atomic Update and Transaction Sequence

The status and version update must be a database-level conditional operation equivalent to:

```sql
UPDATE consignments
SET status = @target_status,
    version = version + 1
WHERE id = @id
  AND tenant_id = @tenant_id
  AND version = @expected_version;
```

The affected-row count is the concurrency result. The operation must run with the history insert in the same database transaction:

1. Open a transaction.
2. Load the tenant-scoped consignment for lifecycle validation.
3. Validate target status and outcome fields without mutating entities.
4. Execute the conditional update for exactly `id`, `tenant_id`, and `expected_version`.
5. If affected rows are `0`, roll back the transaction. Neither status nor history is changed.
6. After rollback, issue a fresh tenant-scoped query using the same `tenantId` to reload the latest consignment detail and its history; do not construct the response from a potentially stale tracked entity. Return HTTP `409 CONFLICT` with that latest detail.
7. If affected rows are `1`, insert the status-history row with the old status, target status, new outcome fields, and owning tenant ID.
8. Commit the transaction. Commit failure rolls back both operations.
9. If any validation or database exception occurs after the update before commit, roll back and return no status or history change.

EF Core change tracking may use the conditional update pattern only when it also verifies and enforces the affected row count. Raw SQL or a service helper must not perform a separate unconditional update. The stale request must not modify the consignment or history.

## 7. CSV Import

### 7.1 Startup Behavior

The API startup/import service reads the configured `consignments.csv` once per process startup. It continues after row-level errors and prints or exposes a visible startup report. It must be safe to rerun or restart.

Manual CSV upload and a manual/rerun endpoint are optional and deferred. Import report tables and downloadable import reports are optional and deferred.

A successful run can expose totals through `GET /api/imports/latest`; startup logs must also show totals and row issues. A simple in-memory latest report is sufficient for the demo. This endpoint does not represent tenant data and does not require `X-Tenant`.

### 7.2 Row Validation

Required fields:

- `tenant_code`
- `consignment_number`
- `customer_name`
- `receiver_name`
- `receiver_suburb`
- `receiver_state`
- `receiver_postcode`
- `item_count`
- `weight_kg`
- `service_type`
- `status`
- `created_at`

Demo assumptions, not extra assignment requirements:

- Item count and weight must be non-negative.
- Missing postcode, negative weight, and unknown status such as `DELIVRD` are rejected.
- States and postcode formats are not otherwise enumerated or pattern-validated.
- Service type is not restricted to a predefined set.
- `created_at` accepts ISO 8601 offset timestamps.
- The supplied nonstandard `dd/MM/yyyy H:mm` format is accepted. It is interpreted as UTC in the demo because the CSV row provides no timezone. Store the parsed value as `timestamptz` UTC.

Row validation and parsing failures produce a row-specific issue and do not stop later rows.

### 7.3 Duplicate and Conflict Behavior

- Exact same-tenant duplicate: import returns `skipped`; no existing row or history is changed.
- Same tenant and consignment number with any differing canonical field: import returns `conflict/rejected`; the existing record remains unchanged.
- Cross-tenant reuse of the same consignment number: allowed; use `(TenantId, ConsignmentNumber)` for matching.
- Canonical comparison trims fields, normalizes status to lowercase, parses numeric fields and timestamps, and compares the resulting values.
- Existing API-modified records are never overwritten by import, even if their canonical fields differ from the original CSV.
- The first valid row for a tenant/number wins within a new import. Later conflicting rows are reported.
- Interrupted startup runs may leave committed valid rows and can safely rerun.
- The database composite unique constraint `(tenant_id, consignment_number)` is the final protection against concurrent duplicate inserts. If an insert loses a unique-constraint race, identify that constraint, report the row as a duplicate or conflict based on a fresh same-tenant comparison, and leave the existing record unchanged.

Imported consignments receive status and created timestamp from the CSV but no synthetic status-history record.

### 7.4 Report

The startup report includes:

- processed/source row count;
- imported count;
- skipped duplicate count;
- rejected count;
- for each issue: source row, tenant code, consignment number, and reason.

An exact import report is logged and, for the simple latest-report endpoint, returned as visible demo state.

## 8. API Contracts

### 8.1 Tenant Header

Header: `X-Tenant`.

Required for list, detail, and status-update operations. It is not required for startup import or the latest startup report.

### 8.2 Endpoints

| Method | Path | Tenant | Purpose |
|---|---|---|---|
| GET | `/api/consignments` | Required | Tenant-scoped list/filter/search/page |
| GET | `/api/consignments/{id}` | Required | Tenant-scoped detail and history |
| PATCH | `/api/consignments/{id}/status` | Required | Tenant-scoped atomic status update |
| GET | `/api/imports/latest` | Not required | Latest startup import report |

Manual import is deferred. The exact route casing is an implementation choice.

### 8.3 List Query

- `status`: optional exact lifecycle status.
- `search`: optional partial match against consignment number or receiver name.
- `page`: positive integer, default `1`.
- `pageSize`: positive integer, default `20`, maximum `100`.

Invalid status filters and invalid paging values return `400 VALIDATION`. Unknown status values are not silently ignored.

### 8.4 DTO Shape

Consignment list item:

```json
{
  "id": "uuid",
  "consignmentNumber": "CN100020",
  "customerName": "Redgum Electrical",
  "receiverName": "Henry Harris",
  "receiverSuburb": "Penrith",
  "receiverState": "NSW",
  "receiverPostcode": "2750",
  "itemCount": 10,
  "weightKg": "26.90",
  "serviceType": "standard",
  "status": "in_transit",
  "createdAt": "2026-11-10T11:37:00+11:00",
  "version": 1
}
```

Paged response:

```json
{
  "items": [],
  "total": 70,
  "page": 1,
  "pageSize": 20
}
```

Detail adds an always-present history array:

```json
{
  "id": "uuid",
  "consignmentNumber": "CN100020",
  "status": "in_transit",
  "createdAt": "2026-11-10T11:37:00+11:00",
  "version": 1,
  "history": [
    {
      "id": "uuid",
      "fromStatus": "picked_up",
      "toStatus": "in_transit",
      "changedAt": "2026-11-16T10:00:00Z",
      "signedBy": null,
      "deliveryTime": null,
      "failureReason": null
    }
  ]
}
```

The DTO should include all list fields in detail, not only the fields shown above for brevity.

Status update request:

```json
{
  "status": "delivered",
  "version": 2,
  "signedBy": "Pat Taylor",
  "deliveryTime": "2026-11-19T09:30:00+08:00",
  "failureReason": null
}
```

For `failed_delivery`, send `signedBy: null`, `deliveryTime: null`, and an allowed `failureReason`.

Conflict response:

```json
{
  "code": "CONFLICT",
  "message": "This consignment changed after it was opened.",
  "latest": {
    "id": "uuid",
    "consignmentNumber": "CN100020",
    "status": "in_transit",
    "version": 3,
    "history": []
  }
}
```

`latest` is the normal consignment detail contract. Numeric `Version` is the only concurrency contract; ETag is deferred.

Standard errors:

```json
{
  "code": "VALIDATION",
  "message": "Proof of delivery or failure details are missing.",
  "errors": {
    "signedBy": ["Signed-by name is required."]
  }
}
```

Codes include `TENANT_REQUIRED`, `NOT_FOUND`, `VALIDATION`, `INVALID_TRANSITION`, `CONFLICT`, and `IMPORT_FAILED`.

## 9. Frontend

### 9.1 Components

- Tenant switcher for the demo tenant codes.
- Consignment list with status filter, search, pagination, and loading/empty/error states.
- Detail view with identifying fields, current status, version, and ordered history.
- Status-change panel with only allowed target states and conditional proof/failure fields.
- Conflict banner with latest status/version.
- Startup import report panel with totals and row issues.

One `App` plus small components is sufficient. React Router and state-management libraries are unnecessary.

### 9.2 Conflict Behavior

On `409 CONFLICT`, refresh the latest tenant-scoped detail and replace only server-owned base state such as status and version.

Preserve the user's selected target status and entered signer, delivery time, and failure reason while showing the latest state and a clear retry instruction. Do not silently retry or overwrite the draft.

Tenant changes clear or reset tenant-owned list/detail state. The UI sends `X-Tenant` on every consignment request.

## 10. Error and Concurrency Handling

- Map tenant and resource not-found cases to `404 NOT_FOUND`.
- Map missing tenant to `400 TENANT_REQUIRED`.
- Map validation and lifecycle failures to `400`.
- Map stale or affected-row-zero updates to `409 CONFLICT` with latest detail.
- Map unexpected exceptions to generic `500` responses and server logs; do not expose SQL or stack traces.
- Status update validation, conditional update, history insertion, and commit occur in one transaction.
- Startup import row failures remain visible as report issues.

## 11. Testing and Verification

### 11.1 Backend

Small xUnit suites should cover:

- every allowed and invalid lifecycle transition;
- delivered and failed_delivery outcome requirements;
- tenant-scoped list, detail, update, and history behavior;
- cross-tenant consignment IDs and numbers not being exposed;
- stale expected version returns `409` and makes no status/history change;
- successful status update increments the version and appends history;
- exact duplicate, conflicting duplicate, and cross-tenant CSV behavior;
- negative weight, missing postcode, and unknown status import rejection.

Use a real local/PostgreSQL test database if practical. Testcontainers is optional and deferred; EF InMemory is not sufficient for conditional update or constraint verification.

### 11.2 Frontend

One meaningful Vitest/React Testing Library test should verify conditional proof/failure fields and that a conflict preserves the draft target and entered values.

### 11.3 Demo Verification

Before completion:

- the solution/build succeeds;
- relevant backend tests execute;
- frontend build and meaningful test execute;
- Docker Compose or the documented startup command succeeds;
- startup report is visible;
- tenant switch demonstrates isolation;
- status validation and conflict behavior are demonstrated.

Do not claim unexecuted verification.

## 12. Local Demo Startup

The repository should provide a clear root-level startup command, ideally Docker Compose:

```text
docker compose up --build
```

PostgreSQL starts, migrations apply, the API starts, startup CSV import completes, and the web app starts. The README must record actual ports and URLs after verification.

At minimum the README commands identify:

- PostgreSQL/API/web startup;
- database migration method;
- web and Swagger URLs;
- two demo tenant codes;
- how to observe the startup import report;
- how to demonstrate conflict and cross-tenant isolation.

## 13. Deliverables and Checklist

Required deliverables:

- [ ] README.md with exact run commands, database/data-access/frontend choices and rationale, key design decisions, migration summary, and "what I'd do next".
- [ ] AI_USAGE.md with tools, generated versus reviewed work, one AI mistake, and how it was caught.
- [ ] DEVELOPMENT_LOG.md with observed work, test/build/demo evidence, and final stop-time status.
- [ ] MIGRATION.md with PostgreSQL RLS tenant-context, ownership/reconciliation, pilot routing, and rollback design.
- [ ] Working API, PostgreSQL schema/migrations, startup CSV import/report, and React console.
- [ ] Backend and frontend verification evidence.
- [ ] Final four-hour stop-time report listing completed work and deferred next steps.

The migration note may be a separate MIGRATION.md or summarized in README, but it must exist as a clear deliverable.

## 14. Four-Hour Priorities

### 0:00-1:00: Database and API Foundation

- Replace or adjust the existing domain and DbContext.
- Add explicit positive `Version`.
- Add EF migration, constraints, composite uniqueness, and tenant/history indexes.
- Implement tenant resolution and tenant middleware/service.

### 1:00-2:05: Import and Start-Up Slice

- Implement startup row validation and UTC interpretation for the nonstandard timestamp.
- Implement exact duplicate, conflict, and cross-tenant reuse behavior.
- Expose or log the startup report.
- Verify repeat startup import safety.

### 2:05-3:05: Status and Concurrency End-to-End

- Implement lifecycle/outcome validation.
- Implement the atomic conditional update plus history transaction.
- Add list/detail/update DTOs and centralized errors.
- Demonstrate tenant detail and `409` behavior.

### 3:05-3:40: React Working Slice

- Tenant switcher and list/filter/search/paging.
- Detail/history view.
- Conditional status form.
- Conflict refresh with preserved draft fields.
- Visible startup report.

### 3:40-4:00: Verification and Stop Report

- Run the smallest build/tests/checks covering core behavior.
- Smoke the documented demo startup.
- Update README, AI_USAGE.md, DEVELOPMENT_LOG.md, MIGRATION.md, and stop-time report.
- Stop promptly and document deferred work rather than extending infrastructure.

Optional/deferred scope: import history tables, manual CSV upload/rerun endpoint, ETag, Testcontainers, advanced search, React Router, state-management libraries, full tenant administration, and downloadable reports.

## 15. Assumptions

- Missing postcode, negative weight, and unknown status such as `DELIVRD` are rejected as documented demo assumptions.
- Item count and weight are non-negative.
- Missing X-Tenant is `400` and unknown/cross-tenant resources are `404`.
- Cross-tenant reuse of a consignment number is allowed.
- Same-tenant exact duplicates are skipped and conflicting rows are rejected without changing existing records.
- The nonstandard CSV timestamp is parsed as UTC.
- Imported consignments have no synthetic history record.
- Writing the current status is invalid.
- Numeric version is the concurrency contract.
- RLS remains migration design only.

## 16. Remaining Assumptions or Implementation Choices

- `weightKg` may be serialized as a decimal string to avoid JSON decimal ambiguity.
- Demo tenant display names may be derived from CSV tenant codes.
- History is returned newest first for console readability.
- The startup report may be in memory plus logs; no import-history tables are required.

There are no unresolved requirements decisions that block implementation.
