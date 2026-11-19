# AI Usage

## Tools and Roles

- **GitHub Copilot** generated or substantially assisted repository setup, requirements and specification drafting, implementation, testing, CSV import handling, and code review.
- **ChatGPT** assisted with requirements analysis, architecture discussions, technical design review, implementation planning, and independent review.
- **The developer** provided the assignment brief and constraints, participated in technical and domain decisions, reviewed AI-generated designs and code, and verified the resulting implementation through tests and runtime checks.

## Work Generated or Assisted by AI

AI substantially assisted with:

- Initial solution structure and React/.NET scaffolding.
- Drafting `intent.md` and `spec.md` from the assignment brief.
- Technical proposals covering the database, API, data model, frontend, testing, CSV import, and migration design.
- Implementation and revision of backend and frontend components.
- Test creation, code review, troubleshooting, and documentation.

AI-generated output was treated as a proposal rather than an authoritative implementation or specification. Technical and domain decisions were reviewed against the assignment requirements and actual application behavior.

## Developer Contributions and Review

The developer actively participated in design and implementation decisions rather than relying on AI-generated defaults.

Key contributions included:

- **Technical stack and architecture:** Evaluated and confirmed key technology choices, including PostgreSQL as the required database, EF Core with Npgsql, the .NET backend, and the React/TypeScript frontend. Reviewed database mapping, persistence, and tenant-isolation decisions against the assignment constraints.
- **Domain model and lifecycle:** Defined and reviewed consignment status lifecycle rules, including valid transitions, terminal states, delivery proof requirements, and failed-delivery reason requirements. Checked that API validation, persistence constraints, and status history behavior remained consistent with these rules.
- **Data formats and validation:** Reviewed API request/response formats, status-update payloads, version handling, timestamps, and the relationship between delivery outcomes and their required fields.
- **CSV import business rules:** Participated in decisions about how imported consignments should be classified and handled, including valid records, malformed or invalid rows, duplicate records, conflicting records, and tenant-specific behavior. Reviewed the distinction between imported, skipped, and rejected records and verified the resulting import report.
- **Tenant isolation and concurrency:** Reviewed tenant-scoped query behavior, optimistic concurrency using the consignment Version, and conflict handling to ensure stale updates do not silently overwrite newer state.
- **Verification and acceptance:** Compared generated work against the original assignment brief, reviewed implementation details, ran builds and tests, and verified PostgreSQL startup, migration application, CSV import, and frontend/API integration.

The developer accepted, revised, or rejected AI-generated proposals based on explicit requirements, domain rules, code inspection, and observed runtime behavior.

## Incident: Incorrect Database Provider in the Initial Spec

### What AI got wrong

The initial AI-generated `spec.md` incorrectly referred to MySQL instead of the PostgreSQL database required by the assignment brief.

### How it was detected

The discrepancy was found during requirements/specification review by comparing `spec.md` with the original `assignmentBrief.md`, which explicitly required PostgreSQL. It was a source-requirements mismatch, not a runtime discovery.

### Correction

Before implementation, the specification was corrected to PostgreSQL 16 and the intended .NET provider was aligned to EF Core with Npgsql. MySQL was not implemented or used in the project.

### Lesson

AI-generated specifications must be checked against explicit source requirements before implementation. Architecture documents can otherwise introduce a database choice that conflicts with the brief.

## Incident: Npgsql Timestamp Compatibility

### How it was discovered

During actual PostgreSQL startup and CSV import verification, startup/import failed for timestamp values because Npgsql rejected a timestamp representation incompatible with PostgreSQL `timestamp with time zone`.

### Technical cause

The CSV import code needed to normalize timestamps consistently for Npgsql and PostgreSQL `timestamptz` storage. ISO timestamps with explicit offsets and legacy timestamps without offsets required explicit, consistent UTC handling.

### Correction

`src/Api/Data/CsvImportService.cs` was corrected to:

- Convert ISO timestamps with explicit offsets to the equivalent UTC instant.
- Interpret legacy timestamps without offsets explicitly as UTC.
- Produce UTC `DateTimeOffset` values for both forms.
- Preserve the normalized instant for duplicate comparison.

The corresponding CSV import tests were updated or added to cover offset, legacy, fractional-second, and duplicate-comparison behavior.

### Verification

A focused read-only review found no concrete issues. The full backend suite passed 44/44, and subsequent real PostgreSQL startup and CSV import verification succeeded. The successful import demonstrated database-provider behavior that in-memory tests alone did not cover.

### Lesson

Successful compilation and in-memory tests do not replace testing against the actual database provider. Provider-specific validation and real startup/import behavior must be verified against PostgreSQL.