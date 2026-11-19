# AI Usage

## Tools Used

- **GitHub Copilot:** Used for repository setup assistance, initial scaffolding and preliminary implementation, requirements-document drafting/revision, architecture specification work, and project review assistance.
- **ChatGPT:** Used for project analysis and independent architecture review of the requirements and proposed design.

## Generated and Assisted Work

- **AI-generated before requirements were finalized:** GitHub Copilot generated or helped generate the initial solution and project structure, including the ASP.NET Core API scaffold, test project scaffold, React/TypeScript scaffold, and preliminary data/domain/service files.
- **AI-assisted requirements work:** Copilot and ChatGPT assisted with reading `assignmentBrief.md`, identifying requirements and unresolved decisions, drafting `intent.md`, and refining its requirement labels and duplicate/conflict clarifications.
- **AI-assisted architecture work:** Copilot and ChatGPT assisted with reviewing the repository, drafting `spec.md`, defining data/API/frontend/testing approach, and revising the design after independent review.
- The premature implementation/scaffolding was produced before a requirements baseline had been approved.

## Developer Work and Decisions

- The developer supplied the assignment brief, repository guidance, and project constraints.
- The developer stopped implementation, restarted requirements analysis, and directed the refinement of `intent.md`.
- The developer decided to proceed with the approved PostgreSQL/EF Core architecture recorded in `spec.md`.
- The developer is responsible for final human review and approval of requirements, architecture, source changes, tests, and runtime behavior.

## Observed AI Mistakes

### Premature implementation before requirements baseline

- **Mistake:** GitHub Copilot began scaffolding and preliminary implementation before requirements analysis and a baseline were complete.
- **How caught:** The developer stopped the implementation and required a requirements restart. `AGENTS.md`, `assignmentBrief.md`, and the supplied CSV were used to establish `intent.md` rather than treating the existing code as requirements.
- **Handling:** Development was reset to requirements and architecture planning. The premature work remains subject to review against the approved specification.

### PostgreSQL concurrency design mistake

- **Mistake:** The implementation and proposed design used SQL Server-style `RowVersion` / `IsRowVersion()` semantics for a PostgreSQL project.
- **How caught:** An independent architecture review identified that this EF pattern was not the approved PostgreSQL concurrency mechanism and could not by itself provide the required reliable conditional update semantics.
- **Handling:** The review established the correction: use an explicit positive integer `Version`, increment it on a successful status change, and perform a database-level conditional update qualifying on `id`, `tenant_id`, and `expected_version`. Affected-row count zero must return `409`; one affected row must insert the history row in the same transaction.
- **Approved status:** The corrected design is recorded in `spec.md`.
- **Pending implementation:** The integer version, conditional update, transaction/history behavior, conflict response, and runtime behavior have **not** yet been implemented or verified.
- **Pending verification:** PostgreSQL behavior and stale-update tests for this correction are pending.

## Human Review and Verification

- **Pending:** Human review of each future implementation change.
- **Pending:** Developer review and interpretation of build/test output.
- **Pending:** Runtime verification of tenant isolation, import behavior, lifecycle transitions, conflict handling, and the demo startup.
- No build, test, or runtime/demo result is claimed in this record.

## Future AI-Generated Code and Review Placeholder

- Record future AI-generated code by component and work item as it is actually added.
- Record which generated code was accepted, revised, or rejected by the developer.
- Record build/test/demo commands and observed outcomes only after execution.
- Record additional genuine AI mistakes only when observed and supported by evidence.
