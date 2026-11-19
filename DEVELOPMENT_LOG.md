# Development Log

This log separates observed work from planned work and does not claim unobserved implementation or verification results.

## Completed Through Architecture Review

### Repository and Environment Setup

- The repository was initialized with a .NET solution structure (`ConsignmentConsole.sln` and `.slnx`), `global.json`, the assignment files, and `consignments.csv`.
- An ASP.NET Core API project was created under `src/Api`, with existing controller, data, domain, model, and service areas.
- A .NET test project was created under `tests/Api.Tests`.
- A React/TypeScript Vite application was created under `web`.
- Setup commands and runtime verification are not documented as having been completed here.

### Initial Copilot Scaffold

- GitHub Copilot initially generated project scaffolding and preliminary implementation before the requirements baseline was finalized.
- The preliminary implementation included the solution/API/test structure and initial API, EF Core, domain, DTO, and React starter areas.
- This work was identified as premature because implementation started ahead of a requirements baseline.

### Stop and Requirements Reset

- Development was stopped to establish the requirements baseline instead of continuing against an unapproved implementation.
- `AGENTS.md`, `assignmentBrief.md`, and the supplied CSV were read.
- `intent.md` was created from the assignment brief and revised to label explicit, derived, unresolved, and out-of-scope requirements; it also clarified CSV duplicate/conflict observations and separated requirements from technical design choices.
- The assignment brief remained the authoritative source of requirements, and existing implementation was not treated as a requirements source.

### Architecture Planning

- `spec.md` was created from the approved requirements baseline and the existing project structure.
- The specification selected PostgreSQL 16, EF Core, ASP.NET Core controllers, React/TypeScript with Vite, and Docker Compose as the intended demo startup approach.
- It defined the domain and schema, tenant-qualified access, lifecycle transitions, CSV import behavior, API contracts, frontend behavior, testing, migration design, and four-hour priorities.
- The developer approved the resulting architecture decisions.

### Independent Architecture Review and Revisions

- A separate architecture review was conducted after the initial specification draft.
- Main issues identified included:
  - The PostgreSQL project had been specified with SQL Server-style `RowVersion` / `IsRowVersion()` semantics, which was not an appropriate PostgreSQL concurrency design.
  - Tenant ownership and status history needed a composite `(TenantId, ConsignmentId)` foreign-key relationship, rather than independent ownership references.
  - Status updates needed an explicit positive integer version and a database-level tenant-and-version conditional update, with history inserted in the same transaction.
  - The lifecycle, proof/failure validation, `409` conflict response, CSV duplicate/conflict behavior, and tenant qualification needed more precise contracts.
- The specification was revised to use an explicit integer `Version`, incremented only by the successful conditional update, and an affected-row count as the conflict check.
- Additional approved revisions established `(TenantId, ConsignmentNumber)` uniqueness, composite tenant-owned history keys, tenant-qualified list/detail/update queries, exact-duplicate/import-conflict rules, and migration-only RLS design.
- PostgreSQL migration implementation and RLS enforcement remain migration design only for this phase.

## Backend Foundation (2026-11-19)

### Status

- Partially verified: domain/schema configuration and the initial EF Core migration were generated.
- PostgreSQL application was not run. No PostgreSQL service, `psql`, or Docker executable was available, and `localhost:5432` was unreachable.
- No CSV import, API endpoint, or frontend work was performed in this phase.

### Files Created or Modified

- Modified `src/Api/Domain/Models.cs`: replaced SQL Server-style `uint RowVersion` with positive `int Version` initialized to `1`; added `StatusHistory.TenantId`.
- Modified `src/Api/Data/ConsignmentDbContext.cs`: configured PostgreSQL table/column names, types, keys, foreign keys, checks, and indexes.
- Modified `src/Api/Program.cs` and `src/Api/appsettings.Development.json`: registered Npgsql using the `ConsignmentDatabase` connection string.
- Modified `src/Api/Services/ConsignmentService.cs` and `src/Api/Models/Dtos.cs` only to align existing version references/types with `int Version`. Status-update behavior remains unfinished.
- Modified `tests/Api.Tests/Api.Tests.csproj`; added `tests/Api.Tests/DatabaseModelTests.cs` for composite ownership-key and version/unique-index metadata checks; removed the empty `tests/Api.Tests/UnitTest1.cs` placeholder.
- Generated `src/Api/Data/Migrations/20261119063728_InitialBackendFoundation.cs`, its designer file, and `src/Api/Data/Migrations/ConsignmentDbContextModelSnapshot.cs`.
- Updated this development log. `AI_USAGE.md` was not changed.

### Schema and Migration Decisions

- Used the existing .NET 8, EF Core 8.0.11, and Npgsql 8.0.11 project; no second project or solution was created.
- Persisted lifecycle values as constrained strings, matching the approved `varchar` schema.
- Added the tenant-scoped unique index on `(tenant_id, consignment_number)`, the `(tenant_id, id)` alternate key, and the single composite history foreign key `(tenant_id, consignment_id)`.
- Added database checks for valid statuses and transitions, nonnegative item count/weight, positive version, required nonblank values, and outcome-specific history details.
- Added the specified tenant/status/created-at and history indexes, including descending timestamp columns.
- The generated migration uses PostgreSQL types (`uuid`, `character varying`, `integer`, `numeric(10,2)`, and `timestamp with time zone`). Inspection found no SQL Server rowversion type or configuration.

### Verification

- `dotnet restore ConsignmentConsole.sln`: succeeded after adding the EF Core reference required by the model-metadata tests.
- `dotnet build ConsignmentConsole.sln --no-restore`: succeeded with 0 warnings and 0 errors.
- EF migration command (Development configuration): build succeeded and `InitialBackendFoundation` was generated.
- `dotnet test ConsignmentConsole.sln --no-restore`: passed, 2 passed and 0 failed. An earlier test run exposed an assertion against EF's optimized runtime model; the assertion was narrowed to metadata available at runtime, then the suite passed.
- Migration and model snapshot were inspected for keys, checks, indexes, PostgreSQL types, and absence of SQL Server-specific configuration.
- `dotnet ef database update` and all other database-application commands were deliberately not run. Schema creation against PostgreSQL remains unverified.

### Known Issues and Remaining Work

- The existing status-update service is outside this phase and still needs its transaction/conditional version increment and history tenant assignment implemented before status updates can be considered correct with this schema.
- PostgreSQL schema application and runtime constraint verification remain pending until a PostgreSQL database is available.
- Remaining API behavior, frontend behavior, and end-to-end demo verification remain for later phases.

## Current Status

- Completed: repository setup, requirements reset, architecture planning/review, approved design decisions, Backend Foundation implementation plus migration generation, CSV import implementation, and Status Lifecycle/API implementation.
- Partially verified: backend builds and automated tests pass; PostgreSQL migration application, generated update SQL, and runtime behavior remain unverified.
- Not completed: frontend behavior, full integration/runtime verification, and delivery of the working demo.

## Planned Implementation

- Implement the React console and frontend test in a later phase.
- Apply the migration and verify schema creation when PostgreSQL is available.

## Planned Verification

- Record Docker/demo startup, tenant isolation, CSV import, status, and conflict demonstrations only after observed runtime verification.

## Final Stop-Time Report Placeholder

- Add the actual stop-time date/time and timebox status.
- Add completed items with evidence, deferred items, known limitations, and exact next steps.
- Do not claim demo readiness until the documented startup and core workflows are verified.

## CSV Import (2026-11-19)

### Status

- Implemented the CSV import phase and startup/report integration.
- Build and automated tests passed as recorded below.
- PostgreSQL was unavailable and the initial migration remains unapplied; database runtime behavior, including the unique-constraint race path, was not verified.

### Files Changed

- `src/Api/Data/CsvImportService.cs`: row parsing and validation, normalized tenant resolution, canonical duplicate/conflict handling, per-row persistence, and report types.
- `src/Api/Program.cs`: register the import/report services, run the import once during startup, and log totals and row issues.
- `src/Api/Controllers/ImportsController.cs`: expose the latest startup report at `GET /api/imports/latest`.
- `tests/Api.Tests/Api.Tests.csproj`: add EF Core InMemory for service-level tests.
- `tests/Api.Tests/CsvImportServiceTests.cs`: add 15 focused import tests, including a run over the supplied CSV.
- `DEVELOPMENT_LOG.md`: record implementation and verification.

### Import Behavior

- Validates required fields, database field limits, integer item count, non-negative weight within `numeric(10,2)`, known lifecycle status, and supported timestamps.
- Parses ISO 8601 timestamps with explicit offsets and interprets `dd/MM/yyyy H:mm` as UTC.
- Trims input fields, normalizes tenant codes and statuses case-insensitively, and compares parsed numeric values and timestamp instants.
- Resolves/creates tenant codes using the normalized lowercase code. Duplicate matching is scoped by tenant and consignment number.
- Skips exact canonical duplicates; rejects conflicting duplicates without updating the first/existing record; allows the same number for different tenants.
- Persists each consignment separately so row validation/data-constraint failures do not roll back earlier rows. Handles the named PostgreSQL unique constraints by reloading and comparing the existing tenant-scoped record. This race handling is implemented but has not been exercised against PostgreSQL.
- Reports processed/imported/skipped/rejected totals and row-specific issues through startup logs and the latest-report endpoint.
- Does not run or apply an EF migration. Startup import requires the existing database schema to have been applied separately.

### Verification

- `dotnet restore ConsignmentConsole.sln`: succeeded.
- `dotnet build ConsignmentConsole.sln --no-restore`: succeeded with 0 warnings and 0 errors.
- `dotnet test ConsignmentConsole.sln --no-restore`: passed, 17 passed and 0 failed (15 CSV-import tests and 2 existing model tests).
- Import service tests use EF Core InMemory. They verify validation, continuation after invalid/short rows, timestamps, canonical duplicate behavior, conflicts, cross-tenant reuse, idempotency, preservation of API-modified records, report contents, and the supplied CSV.
- The supplied 68-row CSV fixture produced 63 imported, 1 skipped, and 4 rejected rows under the service-level test; the known quality/duplicate cases were asserted by consignment number without relying on fixed row numbers.
- These tests do not verify PostgreSQL constraints or concurrent insert behavior. PostgreSQL startup/import runtime was not tested, and `dotnet ef database update` was not run.

### Known Limitations

- The PostgreSQL migration must be applied before startup import can run against a real database.
- PostgreSQL persistence, database constraint enforcement, tenant creation races, and the consignment unique-constraint race path remain unverified.
- Malformed CSV file structure and database connectivity failures remain fatal startup/import errors; they are not reported as ordinary row validation failures.

## CSV Import Failure Handling and Timestamp Coverage (2026-11-19)

### Changes

- `src/Api/Data/CsvImportService.cs`: CsvHelper header/parser exceptions during record enumeration now throw a `CsvImportFileException` with a file-level diagnostic instead of returning a partial report. Ordinary row validation and persistence handling remain unchanged; database connectivity exceptions are not caught or converted to row issues.
- `src/Api/Program.cs`: logs fatal CSV file-level import failures with the input path and exception, then rethrows so startup cannot continue with an incomplete report. No migrations or startup architecture changes were made.
- `tests/Api.Tests/CsvImportServiceTests.cs`: covers an invalid header, a malformed quoted field, and timestamp parsing for a negative offset and fractional seconds, plus rejection of an offsetless ISO timestamp. The existing test for importing a valid row after an ordinary invalid row continues to cover that behavior.
- `DEVELOPMENT_LOG.md`: records these implementation changes, verification results, and the PostgreSQL runtime limitation.

### Verification

- `dotnet build ConsignmentConsole.sln --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test ConsignmentConsole.sln --no-restore`: passed, 22 passed and 0 failed.
- The malformed quoted-field test confirms CsvHelper reports the input as a parser failure with the current configuration; no normal import report is returned.
- PostgreSQL runtime behavior remains unverified because no PostgreSQL instance was available for this validation. These tests use EF Core InMemory, and no PostgreSQL import/startup execution was performed.

## Status Lifecycle and API (2026-11-19)

### Changes

- Added `GET /api/consignments` with tenant-scoped status/search filtering, result counts, and pagination; invalid status and paging inputs return structured `400 VALIDATION` errors.
- Added `GET /api/consignments/{id}` returning tenant-scoped detail and status history, and `PATCH /api/consignments/{id}/status` for lifecycle changes.
- Required and resolved `X-Tenant` for each consignment operation. Missing/blank headers return `400 TENANT_REQUIRED`; unknown tenants and cross-tenant IDs return `404 NOT_FOUND`.
- Kept tenant predicates in the list/count, detail/history, and update queries. Search by consignment number or receiver is applied only after scoping to the resolved tenant.
- Completed lifecycle/outcome validation and standard API exception responses. Conflict responses include freshly reloaded tenant-scoped detail.
- Updated status and version with EF Core's conditional concurrency check. The primary key plus original `TenantId` and `Version` are used in the update predicate; `Version` is advanced once. A concurrency failure is not retried.
- Wrapped the status update and history insertion in one transaction. The history insert runs only after the conditional consignment update succeeds, and includes the owning `TenantId`.
- Preserved CSV import behavior. No migration or database-schema change was made; `TenantId` concurrency configuration is EF model metadata only.
- Added lifecycle, validation, tenant-isolation, list/search/paging, tenant-header, detail-history, and stale-update tests. Updated the model test to verify tenant and version concurrency-token configuration.

### Verification

- `dotnet build ConsignmentConsole.sln --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test ConsignmentConsole.sln --no-restore`: passed, 44 passed and 0 failed. This includes the existing CSV import tests.
- EF InMemory tests verify lifecycle and service behavior; they do not prove relational transaction atomicity or PostgreSQL concurrency behavior.

### Limitations

- PostgreSQL was unavailable, so no database-backed request, transaction, or concurrency test was run and Npgsql-generated update SQL was not captured.
- The conditional update was reviewed through the EF Core concurrency-token configuration and service flow: ID is the primary-key predicate, `TenantId` and `Version` are original-value concurrency predicates, and EF reports a zero-row update as `DbUpdateConcurrencyException`. The PostgreSQL provider's emitted SQL still requires runtime verification.
- No migration or schema change was needed.

## Phase 3 Completion and Targeted Review (2026-11-19)

- Implemented the consignment list API with tenant-scoped search, status filtering, and pagination; detail with history; and status updates.
- Tenant resolution uses `X-Tenant`; missing/unknown tenants and cross-tenant resources return the specified 400/404 responses.
- Lifecycle transitions, delivery proof, and failed-delivery reasons are validated. Successful updates increment `Version`; both `TenantId` and `Version` are EF Core concurrency tokens.
- Verification: solution build passed with 0 warnings and 0 errors; all 44 tests passed.
- Targeted review confirmed both concurrency-token configurations. The stale-update test exercises the explicit `Version` mismatch check, not a real concurrent `DbUpdateConcurrencyException`.
- The transaction encloses the consignment update and history insertion, but PostgreSQL rollback behavior has not been verified. PostgreSQL was unavailable; generated Npgsql SQL and real database concurrency remain unverified.

## React Dispatcher Frontend (2026-11-19)

### Changes

- Replaced the Vite starter screen with a dispatcher console for tenant selection, server-side search/status filtering/pagination, consignment detail, status history, and status updates.
- Added a typed API client for the existing list, detail, status-update, and latest-import-report contracts. Consignment requests include `X-Tenant`; the import report request does not.
- Added the three demo tenant codes present in the supplied CSV. No tenant-discovery or lifecycle-discovery endpoint exists, so tenant choices and the small valid-next-status selector map are frontend configuration; API validation remains authoritative.
- Displayed status history in the order returned by the API (oldest first). On a `409`, the UI uses the response's `latest` detail, shows its status and `Version`, preserves the form draft, and only submits again after an explicit user action.
- Added a compact, collapsed import-report section, Vite development proxy configuration, frontend test setup, and web-specific setup/check instructions.
- Added `web/src/App.test.tsx`, which verifies that a failed-delivery draft survives a mocked conflict, displays the latest state, sends `X-Tenant`, and is not retried automatically.
- Added Vitest, jsdom, and React Testing Library as development dependencies. `npm install` created `web/package-lock.json`; it completed with 0 reported vulnerabilities.
- No backend source, schema, migrations, or CSV import files were changed.

### Verification

- `npm test` (from `web`): passed, 1 test file and 1 test.
- `npm run build` (from `web`): passed; TypeScript project build and Vite production build completed.
- `npm run lint` (from `web`): passed with no lint warnings or errors.
- The frontend test uses mocked API responses. No API/database process was started; live API and PostgreSQL integration remain unverified.

### Limitations and Deferred Verification

- Demo tenant options are the three CSV tenant codes because the API does not expose tenant discovery.
- The frontend's short transition map mirrors the current backend lifecycle because there is no endpoint or shared package to supply it. The API remains authoritative for accepted updates.
- The Vite proxy targets the API's existing HTTP development address (`http://localhost:5020`) by default and can be overridden with `VITE_API_PROXY_TARGET`. A running API/database demo and production hosting/proxy configuration were not verified in this phase.

## Local PostgreSQL Setup (2026-11-19)

### Changes

- Added a minimal PostgreSQL 16 Compose service, bound to `127.0.0.1:5432`, with a health check and named `postgres-data` volume. No prior Compose file, PostgreSQL service, Docker container, or Docker volume was present.
- Added `.env.example`; created a local `.env` with a generated development-only password. The existing `.gitignore` already excludes `.env`, so it was not changed. The existing ignored `appsettings.Development.json` was left untouched.
- Updated the root README with prerequisites and PowerShell commands for starting/stopping PostgreSQL, applying migrations, starting the API, and starting the Vite frontend.
- The first real API import attempt exposed that Npgsql requires UTC `DateTimeOffset` values for `timestamp with time zone`, while ISO CSV timestamps could retain non-UTC offsets. `CsvImportService` now normalizes parsed ISO timestamps to UTC without changing their instant; a focused assertion verifies the stored offset is UTC.

### Environment and Verification

- Docker CLI 29.9.3, Docker Desktop, and Compose v5.7.0 were available and the Docker engine was responsive. No local PostgreSQL Windows service or `psql` installation was detected; port 5432 was free.
- `docker compose config --quiet` passed. The PostgreSQL service became healthy; `pg_isready` accepted connections and `localhost:5432` was reachable. The `consignments` database and `app` role were confirmed.
- `dotnet ef database update --project src/Api --startup-project src/Api` applied the existing `20261119063728_InitialBackendFoundation` migration. The `tenants`, `consignments`, and `status_history` tables and all expected indexes were present. No migration was generated or modified.
- The first CSV import completed with 68 processed, 63 imported, 1 skipped, and 4 rejected. `GET /api/imports/latest` returned that report. A second joint API/Vite run successfully served the frontend and proxied the import report; the idempotent second import reported 0 imported, 64 skipped, and 4 rejected. API startup logs contained no database errors during the successful runs.
- The focused CSV import tests passed (20/20); the full solution test suite passed (44/44). PostgreSQL remains running and healthy; the API and Vite processes used for verification were stopped afterward.
