# Consignment Operations Console
### Overview
TV builds transport management software for carriers across Australia. Our current platform runs on .NET and SQL Server; our next-generation platform runs on PostgreSQL with a React/TypeScript front end. This role starts on the current platform and moves to the new one, so this assignment sits between the two.
Build a small, working slice of a consignment operations console: a .NET API, a database and a React web app that dispatchers at two different carriers use to track consignments and record delivery outcomes. We care most that it runs end-to-end as a workable demo — real, running software, not a code sample.

## What we provide
- `consignments.csv` — about 70 consignments exported from a legacy system, with columns: `tenant_code`, `consignment_number`, `customer_name`, `receiver_name`, `receiver_suburb`, `receiver_state`, `receiver_postcode`, `item_count`, `weight_kg`, `service_type`, `status`, `created_at`. Each tenant is a different carrier using the platform. A handful of rows have the data-quality problems you'd expect from a legacy export.
- The consignment status lifecycle below.

|From|Allowed next status|Rule|
| --- | --- | --- |
|`booked`|`picked_up`, `cancelled`||
|`picked_up`|`in_transit`||
|`in_transit`|`out_for_delivery`||
|`out_for_delivery`|`delivered`, `failed_delivery`|Delivered needs proof of delivery: the name of the person who signed and the delivery time. Failed needs a reason: no_one_home, address_incorrect, refused or access_issue.|
|`failed_delivery`|`out_for_delivery`, `returned`|Re-attempt or return to sender.|
|`delivered`, `returned`, `cancelled`|none|Final states.|

## Requirements
1. Import: load the CSV into your database. Good rows go in; bad rows are rejected with a reason in a report, not silently dropped and not a crash. Running the import twice must not create duplicates.
2. Tenant isolation: every request carries a tenant (an `X-Tenant` header is fine — skip real authentication). A tenant must never see or change another tenant's consignments, even with a guessed ID or consignment number; answer as if the record doesn't exist. The same consignment number can exist under two tenants.
3. API (ASP.NET Core, .NET 8 or later): list consignments (filter by status, search by consignment number or receiver, paged); get one consignment with its status history; change its status. Invalid transitions and missing proof-of-delivery or failure details are rejected with clear errors.
4. Concurrency: two dispatchers may update the same consignment at once. A stale update must not silently overwrite a newer one — reject it (e.g. with 409) using a version or ETag.
5. Web UI (React + TypeScript): a dispatcher console with (a) the consignment list with filters, search and paging, (b) a detail view with the status history, and (c) a status-change action that collects proof of delivery or a failure reason when needed. Show validation errors clearly. When a save hits a conflict, tell the dispatcher, show the latest state and keep what they typed. Add a tenant switcher so we can see isolation working.
6. Database: SQL Server (what our current platform runs on) or PostgreSQL (what our new platform runs on) — your choice; say why in the README.
7. Tests: at minimum, the transition rules, tenant isolation and the concurrency check (xUnit, NUnit or MSTest), plus at least one meaningful frontend test.
8. Migration note (about a page): imagine this module lives in our current .NET / SQL Server platform and has to move to the new platform on PostgreSQL, where tenant isolation is enforced in the database with row-level security. How would you move the data, enforce isolation in the database rather than only in application code, and run both platforms side by side for a pilot carrier? Design only, no implementation.
9. Workable demo: the database, API and web app start with clearly documented commands (a single command is ideal — docker compose works well), with the CSV imported and the import report visible.

## Ground rules
- Timebox: 4 hours of effort, due within 2 days of receiving this brief (a 3rd day is available on request). Stop at the timebox and write down what you would do next — unfinished-but-well-reasoned beats gold-plated. Keep the UI functional and clear; visual polish is not the point.
- AI tools (Claude Code, Copilot, Cursor, etc.) are allowed and encouraged — we work AI-first. Include a short AI-usage note: which tools, what was generated vs. hand-written, and at least one thing the AI got wrong and how you caught it.
- Tooling is your call — languages are fixed (C# / .NET backend, React + TypeScript frontend), but the data-access approach (EF Core, Dapper, etc.), libraries, project layout, and how you run things locally are up to you. Choose deliberately; we will ask why. Skip authentication, deployment, and anything not listed in the requirements.
- You will demo this live (about 5 minutes) at the start of your final interview and extend it during the session — build something you can talk about.

## What to submit
- A Git repository link (or zip) with the full source.
- README.md: how to run the demo (the exact commands), key decisions — including your database, data-access and frontend choices and why — and your “what I'd do next” list.
- Your AI-usage note and the migration note (in the README or separate files).

## How we evaluate
- The demo is the deliverable: if we cannot run your project with the documented commands, we will ask you once to fix the setup; if it still doesn't run, we can't progress the application. The demo matters more than any individual feature.
- We look at: correctness of the lifecycle rules · tenant isolation · import robustness · concurrency handling · data modeling · API design · frontend structure, state handling and UX judgment (especially errors and conflicts) · tests · setup and tooling choices · the clarity of your migration note · judgment in how you used AI tools.
- In the final interview you'll demo the app, walk us through your decisions and migration note, and extend it live with your AI tools.