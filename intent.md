# intent.md

## Document Purpose

This document records the requirements for restarting the requirements-analysis and implementation phases of the Consignment Operations Console.


**Requirement labels**

- **[EXPLICIT]**: Stated directly in `assignmentBrief.md`.
- **[DERIVED]**: Reasonable inference from explicit requirements.
- **[DECISION REQUIRED]**: Information not defined by the brief and requiring confirmation or a documented team decision.
- **[NOT REQUIRED BY BRIEF]**: Not required in the brief or explicitly excluded.

The brief is the authoritative requirements source. The supplied CSV is evidence of expected data shapes and data-quality problems, not the source of business rules.

---

## 1. Project Overview and Objectives

### 1.1 Overview

**[EXPLICIT]** TV builds transport management software for Australian carriers. The existing platform uses .NET and SQL Server, while the next-generation platform uses PostgreSQL and React/TypeScript. This assignment sits between those platforms.

The deliverable is a small, end-to-end consignment operations console demonstrating:

- A .NET API.
- A database.
- A React/TypeScript web application.
- Dispatchers for two or more carriers.

**[EXPLICIT]** The primary goal is a workable, live demo rather than a code sample. It must run end-to-end and allow dispatchers to track consignments and record delivery outcomes.

### 1.2 Objectives

**[EXPLICIT]**

1. Import legacy consignment data reliably.
2. Prevent tenants from viewing or modifying one another’s data.
3. Preserve and enforce the supplied consignment status lifecycle.
4. Record status history.
5. Prevent stale updates from overwriting newer changes.
6. Provide a usable dispatcher console.
7. Demonstrate database, API, and frontend operation together.
8. Provide tests for essential lifecycle, isolation, concurrency, and frontend behavior.
9. Explain migration from SQL Server to PostgreSQL.
10. Provide clear setup documentation and a runnable demo.

**[DERIVED]** The system should make failures visible and actionable rather than silently discarding records or hiding validation problems.

---

## 2. Functional Scope

### 2.1 Consignments

**[EXPLICIT]** The system stores consignments with at least the data represented in the CSV:

- Tenant code.
- Consignment number.
- Customer name.
- Receiver name.
- Receiver suburb.
- Receiver state.
- Receiver postcode.
- Item count.
- Weight in kilograms.
- Service type.
- Status.
- Created timestamp.

**[DERIVED]** The system should allow dispatchers to retrieve consignments and display their identifying and delivery information.

### 2.2 Dispatcher Activities

**[EXPLICIT]** Dispatchers must be able to:

- List consignments.
- Filter consignments by status.
- Search by consignment number or receiver.
- Navigate paged results.
- View one consignment.
- View its status history.
- Change its status.
- Provide outcome details when changing to delivered or failed_delivery.

### 2.3 In Scope

**[EXPLICIT]**

- CSV import and import reporting.
- Tenant isolation.
- Consignment retrieval.
- Status transitions.
- Status history.
- Optimistic concurrency.
- React/TypeScript frontend.
- Database setup.
- Automated backend tests.
- One meaningful frontend test.
- README instructions.
- AI-usage note.
- Migration design.
- A live demo.


### 2.4 Out of Scope

**[EXPLICIT]**

- Authentication (may be skipped; request-scoped tenant identity may be represented by an `X-Tenant` header or another lightweight mechanism).
- Deployment.
- Migration implementation (migration note is design-only).
- Anything not listed in the assignment requirements.

**[DECISION REQUIRED]** Whether any additional administrative, user-management, reporting, carrier-management, or editing features are required. No such features should be inferred as requirements.

---

## 3. CSV Import and Validation Requirements

### 3.1 Import Behavior

**[EXPLICIT]** The system must load the CSV into the database.

**[EXPLICIT]** Good rows must be imported.

**[EXPLICIT]** Bad rows must be rejected.

**[EXPLICIT]** Rejected rows must have a reason in an import report.

**[EXPLICIT]** Bad rows must not be silently dropped.

**[EXPLICIT]** A bad row must not cause the entire import process to crash.

**[EXPLICIT]** Running the import twice must not create duplicates.

**[DERIVED]** Import processing should continue after individual row validation failures.

**[DERIVED]** The import report should distinguish successful rows, rejected rows, and possibly already-imported or duplicate rows.

**[DECISION REQUIRED]** The exact import report format and whether it must be visible through the API, a file, the console, or all of those.


### 3.2 CSV Data Characteristics and Duplicate/Conflict Clarification

**[EXPLICIT]** The CSV may contain data-quality problems.

The supplied data contains approximately 70 consignments and has the expected columns.

Observed characteristics include:

- **Negative weight:** `CN100032` has `weight_kg = -3.5`.
- **Missing required-looking value:** `CN200031` has no postcode.
- **Unknown status:** `CN100031` has status `DELIVRD` instead of `delivered`.
- **Nonstandard date format:** `CN200032` uses `17/11/2026 9:30` rather than an ISO-style timestamp.
- **Duplicate data:** An identical-looking Swiftline row for `CN100008` appears twice.
- **Repeated consignment number within a tenant:** `CN200011` appears twice under Coastal Couriers with different row details.
- **Three tenant codes:** `swiftline-freight`, `coastal-couriers`, and the additional `northern-haulage`.
- **Cross-tenant number reuse:** For example, `CN100017` appears under both Coastal Couriers and Swiftline Freight.

**Clarification of duplicate/conflict cases:**

- **Identical duplicate rows:** Same tenant, consignment number, and all other fields are identical. These should be treated as idempotent (importing again does not create a new record).
- **Conflicting rows (same tenant and consignment number, different data):** These represent a potential data conflict. The brief does not specify whether to reject, update, or report these; this must be decided.
- **Identical consignment numbers across different tenants:** Explicitly allowed by the brief. The same consignment number can exist under two tenants.

These observations describe the supplied data only. They do not establish that all such cases should be rejected or accepted.

### 3.3 Validation Requirements

**[DERIVED]** Validation should occur before a row is persisted.

**[EXPLICIT]** The status value must conform to the lifecycle’s defined status values.

**[DERIVED]** The import should validate required CSV fields and data types.

**[DERIVED]** Invalid values should produce a reason-specific report entry identifying the affected row.

**[DECISION REQUIRED]** Exact required/optional field rules. The brief does not state which fields are mandatory or whether the following are valid:

- Zero or positive item counts.
- Zero or positive weights.
- Any permitted states or provinces.
- Postcode format.
- Maximum field lengths.
- Whether service type is restricted to a predefined set.
- Whether tenant codes must belong to a known tenant list.
- Whether duplicate consignment numbers within the same tenant are forbidden.
- Whether a repeated row represents an idempotency key match or a conflicting import record.
- Whether `DELIVRD` should be corrected as a typo or rejected.
- Whether the non-ISO timestamp should be accepted through a parser or rejected.
- Whether CSV import should run automatically on startup or be explicitly triggered.

### 3.4 Import Idempotency

**[EXPLICIT]** Re-running the import must not create duplicates.

**[DERIVED]** Import matching must at least prevent duplication of an identical existing record.

**[DECISION REQUIRED]** The uniqueness strategy. The supplied data indicates that a consignment number may need to be unique only within a tenant, but this remains an implementation decision that must align with the tenant-isolation requirement.

**[DECISION REQUIRED]** The behavior for:

- An exact duplicate row.
- The same tenant and consignment number with changed values.
- A row that matches an existing record but has a different representation or formatting.
- A partial repeat caused by an interrupted import.

### 3.5 Import Report

**[DERIVED]** The report should provide enough information to determine:

- How many source rows were processed.
- How many rows were imported.
- How many rows were rejected.
- The source row number or another stable identifier.
- The tenant code and consignment number, where available.
- One or more clear reasons for rejection.

**[DECISION REQUIRED]** Whether duplicate rows are reported as “skipped,” “accepted,” or “rejected,” and whether the report is retained between runs.

---

## 4. Tenant Isolation Requirements

### 4.1 Request Tenant

**[EXPLICIT]** Every request must carry a tenant.

**[EXPLICIT]** `X-Tenant` is an acceptable tenant request mechanism.

**[DERIVED]** The tenant value should scope all consignment operations.

**[DECISION REQUIRED]** Exact tenant header name and behavior for missing or unknown tenant values.

### 4.2 Isolation

**[EXPLICIT]** A tenant must never see another tenant’s consignments.

**[EXPLICIT]** A tenant must never change another tenant’s consignments.

**[EXPLICIT]** Attempts using guessed IDs or consignment numbers must be answered as if the record does not exist.

**[EXPLICIT]** The same consignment number may exist under two tenants.

**[DERIVED]** Tenant isolation must apply to:

- Listing consignments.
- Filtering and searching.
- Pagination.
- Get-by-ID operations.
- Get-by-consignment-number operations.
- Status changes.
- Status history retrieval.
- Concurrency checks.

**[DERIVED]** IDs or consignment numbers alone must not identify a record across tenants.

**[DECISION REQUIRED]** Whether tenant isolation is guaranteed by composite keys, tenant-qualified queries, database constraints, or a combination.

**[DECISION REQUIRED]** Response behavior for missing tenant header or unknown tenant. It must remain distinct from successful tenant-scoped operations but should not disclose cross-tenant existence.

---

## 5. Consignment Status Lifecycle Requirements

### 5.1 Explicit Lifecycle

The brief defines the following lifecycle:

| Current status | Allowed next status | Required details |
|---|---|---|
| `booked` | `picked_up`, `cancelled` | None specified |
| `picked_up` | `in_transit` | None specified |
| `in_transit` | `out_for_delivery` | None specified |
| `out_for_delivery` | `delivered`, `failed_delivery` | Delivery proof or failure reason |
| `delivered` | None | Final state |
| `failed_delivery` | `out_for_delivery`, `returned` | None specified |
| `returned` | None | Final state |
| `cancelled` | None | Final state |

### 5.2 Transition Details

**[EXPLICIT]** A transition to `delivered` requires proof of delivery:

- Name of the person who signed.
- Delivery time.

**[EXPLICIT]** A transition to `failed_delivery` requires a reason:

- `no_one_home`
- `address_incorrect`
- `refused`
- `access_issue`

**[EXPLICIT]** Rule for status changing from `failed_delivery` to `out_for_delivery` or `returned`: Re-attempt or return to sender.

**[EXPLICIT]** Invalid transitions must be rejected with clear errors.

**[EXPLICIT]** Missing proof-of-delivery details or failure details must be rejected with clear errors.

**[DERIVED]** Rejected transitions must not change the current status or create status-history entries.

**[DECISION REQUIRED]** Exact field names and representation for proof-of-delivery and failure details.

**[DECISION REQUIRED]** Whether proof and failure details should be retained in a separate history record or on the consignment.

**[DECISION REQUIRED]** How to validate delivery time relative to the consignment’s current timestamp or other timeline rules. The brief does not specify a clock rule.

**[DECISION REQUIRED]** Whether signer names and failure reasons can be changed after a final status has been reached.

No additional transitions or business rules may be inferred.

---

## 6. Status History Requirements

### 6.1 Explicit Requirements

**[EXPLICIT]** Get-one-consignment must include its status history.

**[DERIVED]** A status-history entry should represent a status transition that occurred for the consignment.

**[DERIVED]** The history view should allow dispatchers to understand how and when the status changed.

### 6.2 Decision Required

The brief does not define the required history fields. The following decisions are unresolved:

- Whether imported consignments receive an initial history entry.
- Which fields each history entry contains.
- Whether actor or dispatcher identity is required.
- Whether history includes:
  - Previous status.
  - New status.
  - Timestamp.
  - Proof-of-delivery details.
  - Failure reason.
  - Reason for an invalid or failed update.
  - Version number.
- Whether history entries are immutable.
- Ordering, such as oldest-first or newest-first.
- Whether tenant information is stored directly in history or inherited through the consignment.

**[DERIVED]** A successful status change should create or append one history entry if history is mandatory for every successful transition.

**[DECISION REQUIRED]** Whether the same status can be written as a no-op or whether this is an invalid transition.

---

## 7. Optimistic Concurrency Requirements

**[EXPLICIT]** Two dispatchers may update the same consignment concurrently.

**[EXPLICIT]** A stale update must not silently overwrite a newer update.

**[EXPLICIT]** Stale updates must be rejected.

**[EXPLICIT]** A version or ETag mechanism may be used.

**[DERIVED]** The update request should carry the version or ETag from the representation used by the dispatcher.

**[DERIVED]** A mismatch must cause a conflict response rather than a successful status change.

**[DERIVED]** A conflict response should provide the latest state, or enough information for the frontend to retrieve it.

**[EXPLICIT]** Example conflict status is HTTP 409.

**[DECISION REQUIRED]** Whether concurrency is represented by:

- A numeric database row version.
- An entity version column.
- An ETag.
- A timestamp.
- A combination.

**[DECISION REQUIRED]** Exact conflict response body and header format.

**[DERIVED]** Conflict checking must be tenant-qualified so a guessed version for one tenant cannot affect another tenant’s record.

---

## 8. API Requirements

### 8.1 ASP.NET Core

**[EXPLICIT]** The API must use ASP.NET Core.

**[EXPLICIT]** The target framework must be .NET 8 or later.

### 8.2 Required Operations

**[EXPLICIT]** The API must support:

1. Listing consignments.
2. Filtering by status.
3. Searching by consignment number or receiver.
4. Pagination.
5. Getting one consignment with its status history.
6. Changing status.

**[EXPLICIT]** The API must reject invalid transitions and missing outcome details with clear errors.

**[DERIVED]** Cross-tenant requests must return the same not-found-style result as an absent record.

**[DECISION REQUIRED]** Endpoint paths and HTTP methods.

**[DECISION REQUIRED]** Page size limits and page metadata format.

**[DECISION REQUIRED]** Search behavior:

- Case sensitivity.
- Partial versus exact match.
- Combining status and text filters.
- Search field precedence.

**[DECISION REQUIRED]** Whether consignment list results include a summary plus the full proof/failure details.

### 8.3 API Error Handling

**[DERIVED]** Error responses should distinguish:

- Validation failures.
- Invalid lifecycle transitions.
- Missing proof or failure details.
- Tenant-not-found or record-not-found outcomes.
- Optimistic-concurrency conflicts.
- Unexpected server errors.

**[EXPLICIT]** Invalid transitions and missing details must produce clear errors.

**[DERIVED]** Internal error details and sensitive implementation information should not be disclosed to clients.

**[DECISION REQUIRED]** Error response schema and field naming.

**[DECISION REQUIRED]** Exact HTTP statuses for missing tenant, unknown tenant, invalid tenant resource access, and malformed CSV import-trigger requests.

---

## 9. Frontend Requirements

### 9.1 Technology

**[EXPLICIT]** The frontend must use React and TypeScript.

### 9.2 Required Screens and Interactions

**[EXPLICIT]** The dispatcher console must include:

1. A consignment list.
2. Status filtering.
3. Search.
4. Paging.
5. A detail view.
6. The status history.
7. Status-change actions.
8. Proof-of-delivery collection when delivered is selected.
9. Failure-reason collection when failed_delivery is selected.
10. Clear validation errors.
11. A tenant switcher to demonstrate isolation.

### 9.3 Conflict Behavior

**[EXPLICIT]** When saving encounters a conflict:

- Tell the dispatcher a conflict occurred.
- Show the latest state.
- Keep what the dispatcher typed.

**[DERIVED]** The UI should not discard unsaved signer or failure data unless the dispatcher explicitly chooses to replace or discard it.

**[DERIVED]** The UI should provide an explicit recovery or retry action after the latest state is loaded.

**[DECISION REQUIRED]** Whether draft details are preserved indefinitely or only until navigation or refresh.

### 9.4 Usability

**[DERIVED]** Loading, empty, success, and error states should be clear.

**[DERIVED]** Only valid next actions should be presented where practical, but server validation must remain authoritative.

**[DERIVED]** Tenant-switching behavior must prevent stale views from one tenant being shown while viewing or changing another tenant.

**[DECISION REQUIRED]** Routing and layout details.

**[DECISION REQUIRED]** Whether paging, filtering, and search persist across navigation.

---

## 10. Database Requirements

**[EXPLICIT]** The database may be SQL Server or PostgreSQL.

**[EXPLICIT]** The README must explain why the chosen database was selected.

**[DERIVED]** The database should enforce referential and uniqueness constraints appropriate to the modeled data.

**[DERIVED]** The implementation should support reliable transaction boundaries for status changes and history updates.

**[DECISION REQUIRED]** Database choice.

**[DECISION REQUIRED]** Data-access approach.

**[DECISION REQUIRED]** Migration, seeding, and local database startup method.

**[DECISION REQUIRED]** Whether to model imported proof, failure details, and status history as separate entities or embedded fields.

---

## 11. Non-Functional Requirements

### 11.1 Demonstrability

**[EXPLICIT]** The database, API, and web app must start using clearly documented commands.

**[EXPLICIT]** A single command is ideal.

**[EXPLICIT]** Docker Compose is acceptable.

**[EXPLICIT]** The CSV must be imported during setup or startup.

**[EXPLICIT]** The import report must be visible.

### 11.2 Maintainability

**[DERIVED]** The project should be understandable and easy to extend for the live interview.

**[DERIVED]** Setup should be deterministic enough for a fresh environment to run the demo.

**[DERIVED]** The solution should avoid unnecessary dependencies and complexity.

### 11.3 Reliability

**[DERIVED]** Import failures must be isolated to individual rows.

**[DERIVED]** Status updates must be atomic with history updates.

**[DERIVED]** Concurrent updates must be rejected predictably.

**[DERIVED]** Tenant-qualified operations must be applied consistently.

### 11.4 Performance

**[DERIVED]** CSV import and normal console operations should be usable for a small demo dataset.

**[DECISION REQUIRED]** Pagination defaults and maximum page size.

**[DECISION REQUIRED]** Search or import performance targets beyond workable demo behavior.

### 11.5 Security

**[EXPLICIT]** Real authentication is out of scope.

**[DERIVED]** Tenant isolation is a core correctness requirement despite authentication being skipped.

**[DERIVED]** No secrets or credentials should be exposed in source, logs, or documentation.

**[DECISION REQUIRED]** Whether tenant header values should be allowlisted.

### 11.6 Timebox

**[EXPLICIT]** The main effort is timeboxed to 4 hours.

**[EXPLICIT]** Delivery is due within two days.

**[EXPLICIT]** A third day may be requested.

**[EXPLICIT]** Work stopped at the timebox must be documented in “what I would do next.”

**[EXPLICIT]** Unfinished but well-reasoned work is preferable to over-engineered polish.

---

## 12. Technical Constraints

**[EXPLICIT]**

- Backend language and framework: C# and ASP.NET Core .NET 8 or later.
- Frontend language/framework: React and TypeScript.
- Database choice: SQL Server or PostgreSQL.
- Authentication and deployment are excluded.
- AI tools may be used and are encouraged.
- The UI needs to be functional and clear rather than visually polished.

**[EXPLICIT]** Data-access approach, libraries, project layout, and local runtime tooling are discretionary and must be selected deliberately.

**[DECISION REQUIRED]** Database choice and rationale.

**[DECISION REQUIRED]** Data-access choice and rationale.

**[DECISION REQUIRED]** Frontend state-management approach.

**[DECISION REQUIRED]** Testing frameworks within the accepted choices.

**[DECISION REQUIRED]** Container and local runtime tooling.

---

## 13. Migration Requirements

**[EXPLICIT]** A migration design, approximately one page, must describe how to move this module:

- From the current .NET / SQL Server platform.
- To PostgreSQL.
- With tenant isolation enforced in PostgreSQL using row-level security.
- For side-by-side operation for a pilot carrier.

**[EXPLICIT]** This is design-only; implementation is not required.

### 13.1 Required Migration Topics

**[DERIVED]** The migration note must cover:

1. Data export and transformation.
2. Import into PostgreSQL.
3. Tenant and key mapping.
4. Constraint and index planning.
5. PostgreSQL tenant context setup.
6. Row-level-security policy design at a conceptual level.
7. Side-by-side operation and routing for the pilot carrier.
8. Consistency, reconciliation, and rollback considerations.
9. Cutover and validation.
10. Known limitations and rollout risks.

**[DECISION REQUIRED]** Actual PostgreSQL migration sequence and synchronization mechanism.

---

## 14. Testing Requirements

### 14.1 Backend Tests

**[EXPLICIT]** Backend tests must cover at minimum:

- Transition rules.
- Tenant isolation.
- Concurrency checks.

**[EXPLICIT]** Tests may use xUnit, NUnit, or MSTest.

### 14.2 Frontend Tests

**[EXPLICIT]** At least one meaningful frontend test is required.

**[DERIVED]** Meaningful frontend coverage should test application behavior rather than only static rendering.

**[DECISION REQUIRED]** Specific frontend test framework and test scenarios.

Potential suitable scenarios, subject to confirmation:

- Tenant switch resets or isolates displayed data.
- Validation detail appears for an invalid status change.
- Conflict response preserves typed data and exposes latest state.
- Search/filter request parameters are represented correctly.

No scenario should be considered required unless explicitly listed in the brief or confirmed.

---

## 15. Deliverables

**[EXPLICIT]** The repository deliverable must contain:

- Full source code.
- The demo application.
- README documentation.

**[EXPLICIT]** README requirements:

- Exact commands for running the demo.
- Key decision explanations.
- Database choice and reason.
- Data-access choice and reason.
- Frontend choice and reason.
- “What I would do next” list.

**[EXPLICIT]** The AI-usage note must identify:

- Which AI tools were used.
- What was generated versus handwritten.
- At least one thing the AI got wrong and how it was caught.

**[EXPLICIT]** The migration note must be in the README or a separate file.

**[EXPLICIT]** The database/API/web app must run together in a demo.

**[EXPLICIT]** Submission may be a Git repository link or zip.

---


## 16. Acceptance Criteria

The following criteria are direct testable interpretations of the brief and are consolidated to avoid repetition with earlier sections. All must be satisfied for acceptance:

- CSV import: Valid rows are imported, invalid rows are rejected with reasons, import is idempotent, and the process is robust to bad data.
- Tenant isolation: Tenants cannot see or modify each other's consignments; cross-tenant consignment numbers are allowed.
- Status lifecycle: Only allowed transitions per the lifecycle table; delivered and failed_delivery require the specified details; invalid transitions and missing details are rejected.
- Status history: Status changes are recorded and retrievable; rejected transitions do not alter history.
- Concurrency: Stale updates are rejected (e.g., 409); the latest state is shown after a conflict.
- API: .NET 8+, supports list, filter, search, paging, get-one, status-change, and error handling as specified.
- Frontend: React+TypeScript, supports all required screens and interactions, including conflict handling and tenant switching.
- Demo/setup: One-command startup (ideally), import report visible, demo works end-to-end.
- Testing/documentation: Backend tests for lifecycle, isolation, concurrency; one meaningful frontend test; README and migration note as specified.

## 17. Explicit Assumptions

No business assumptions should be silently introduced. The only assumptions identified for planning purposes are:

1. **[ASSUMED FOR DEMO ONLY]** “About 70” means the supplied file is small enough for a local demo and does not require bulk-import infrastructure.
2. **[ASSUMED FOR DEMO ONLY]** The CSV is the seed dataset and is not intended to be the sole source of future runtime data.
3. **[ASSUMED FOR DEMO ONLY]** A lightweight tenant header is sufficient because authentication is explicitly excluded.
4. **[ASSUMED FOR DEMO ONLY]** The implementation will have a local database and local frontend/API processes or containers.
5. **[DERIVED]** The consignment number may repeat across tenants.

These assumptions must not be treated as confirmed business rules.

---


## 18. Unresolved Questions and Decisions

The following decisions must be made before core implementation begins (all others may be documented in spec.md):

1. Which CSV fields are mandatory?
2. What is valid for item count and weight?
3. Are `standard` and `express` the only service types?
4. Is `DELIVRD` rejected or normalized to `delivered`?
5. How should non-ISO timestamps be parsed or reported?
6. Is duplicate consignment number within a tenant permitted?
7. How should two different CSV rows using the same tenant and consignment number be handled?
8. How should exact and near-exact repeated rows be distinguished during import?
9. What tenant header name and missing-tenant behavior should be used?
10. How is an unknown tenant represented to the caller?
11. What constitutes a unique consignment identifier internally?
12. What proof-of-delivery and failure-reason data shape is required?
13. What status-history fields are required?
14. Should import-created records receive initial status-history entries?
15. Which concurrency mechanism will be used?
16. What is the exact 409 conflict response contract?
17. How will the frontend preserve typed details after a conflict?
18. How will PostgreSQL row-level security map to the tenant context in the migration design?
19. What does “side by side for a pilot carrier” mean operationally?

Other technical choices (e.g., API routes, pagination contract, data-access library, test framework, container tooling, error schema, etc.) may be documented in spec.md and do not block core implementation.

## 19. Risks and Scope Boundaries

### 19.1 Identified Risks

- CSV validation rules that are not stated in the brief could accidentally become invented business rules.
- Unique consignment-number behavior could be modeled incorrectly across tenants.
- An application-only tenant check may leave future data-access paths vulnerable.
- Concurrent update checks may be omitted or implemented inconsistently.
- Status updates may succeed without creating history or storing required outcome details.
- A demo setup may depend on undocumented local state or an external database.
- The supplied dataset may contain rows that intentionally expose validator weaknesses.
- UI-only lifecycle restrictions could allow invalid API calls.

### 19.2 Scope Boundaries

**[NOT REQUIRED BY BRIEF]** The brief does not require:

- Authentication.
- Authorization beyond lightweight tenant isolation.
- Deployment.
- Production-grade observability.
- High-availability infrastructure.
- Multi-region operation.
- Real-time notifications.
- Full audit management beyond status history.
- Migration implementation.
- Gold-plated UI polish.

**[DERIVED]** Any of these may be considered future work, but should not be confused with current acceptance criteria.

---


# [Section removed: Implementation recommendations belong in spec.md, not intent.md.]
