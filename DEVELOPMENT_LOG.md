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

## Current Status

- Completed: repository setup, premature scaffold, requirements reset, `intent.md` revisions, architecture planning, independent review, specification revisions, and approved design decisions.
- Not completed: implementation of the approved specification, automated and runtime verification, and delivery of the working demo.

## Planned Implementation Placeholder

- Implement the approved API, PostgreSQL schema/migrations, import flow, and React console.
- Replace or correct any preliminary implementation that differs from `spec.md`.
- Add and run the backend and frontend tests specified by the architecture.

## Planned Verification Placeholder

- Record build results only after commands are run.
- Record test results only after test suites execute.
- Record Docker/demo startup, tenant isolation, CSV import, status, and conflict demonstrations only after observed runtime verification.

## Final Stop-Time Report Placeholder

- Add the actual stop-time date/time and timebox status.
- Add completed items with evidence, deferred items, known limitations, and exact next steps.
- Do not claim demo readiness until the documented startup and core workflows are verified.
