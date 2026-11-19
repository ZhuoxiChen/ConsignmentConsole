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
- CSV import, remaining API behavior, frontend behavior, and end-to-end demo verification remain for later phases.

## Current Status

- Completed: repository setup, requirements reset, architecture planning/review, approved design decisions, and Backend Foundation implementation plus migration generation.
- Partially verified: Backend Foundation model tests and build passed; PostgreSQL migration application was not verified.
- Not completed: CSV import, remaining API behavior, frontend, full integration/runtime verification, and delivery of the working demo.

## Planned Implementation

- Implement CSV import and reporting in a later phase.
- Complete tenant-scoped API and status update behavior in later phases.
- Implement the React console and frontend test in a later phase.
- Apply the migration and verify schema creation when PostgreSQL is available.

## Planned Verification

- Record Docker/demo startup, tenant isolation, CSV import, status, and conflict demonstrations only after observed runtime verification.

## Final Stop-Time Report Placeholder

- Add the actual stop-time date/time and timebox status.
- Add completed items with evidence, deferred items, known limitations, and exact next steps.
- Do not claim demo readiness until the documented startup and core workflows are verified.
