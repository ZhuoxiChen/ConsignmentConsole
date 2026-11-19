# SQL Server to PostgreSQL Migration Note

This is a proposed migration design for the existing .NET/SQL Server module, not a description of RLS or migration features implemented in the assignment demo.

## Migration Approach

Inventory the SQL Server schema, data types, nullability, constraints, indexes, foreign keys, triggers, stored procedures, functions, jobs, query patterns, and application dependencies. Map them explicitly to PostgreSQL: for example, identity keys to sequences or generated keys, `datetime2`/`datetimeoffset` to `timestamp`/`timestamptz`, and SQL Server procedural or T-SQL-specific behavior to PostgreSQL functions or application code. Preserve business invariants and tenant ownership while accounting for SQL Server and PostgreSQL query-plan differences.

Create the PostgreSQL schema and migrations from reviewed inventory, then migrate tenant mappings and consignments before status history or dependent data. Quiesce or capture a consistent source cutoff, bulk-load referentially complete data, and apply a bounded final delta. Before cutover, reconcile row counts, tenant ownership, key uniqueness, lifecycle/history relationships, checksums or field-level samples, and aggregate status counts; do not cut over until discrepancies are explained or corrected.

## Database-Enforced Tenant Isolation

Enable PostgreSQL RLS on tenant-owned tables such as `consignments` and `status_history`, using tenant predicates on `tenant_id`. Policies must apply to normal application roles; `FORCE ROW LEVEL SECURITY` prevents table owners or the application role from accidentally bypassing the policy. Keep privileged migration roles separate from the application role and grant only the operations that normal application code requires.

At the start of each tenant request/transaction, resolve the untrusted `X-Tenant` value to a server-known tenant ID and set a validated PostgreSQL context, such as `app.tenant_id`, on that transaction's connection. With a pooled connection, set and clear the context before reuse, and prefer a transaction-scoped setup plus no persistent session state. If context is missing or invalid, policies must reject data access with a fail-closed result rather than returning all rows. Application `TenantId` predicates remain required as defense in depth and for predictable not-found behavior, but they are not the only isolation boundary.

## Parallel Rollout and Pilot

Migrate a reviewed read-only snapshot of the two pilot carriers' data first, with SQL Server retaining authoritative ownership during the initial pilot. Route those carriers' application requests to PostgreSQL using a feature or routing configuration while traffic for other carriers stays on SQL Server. Start with read-only or shadow traffic and then allow controlled writes only after reconciliation and RLS cross-tenant tests pass. Choose an explicit data ownership rule: for each carrier, either SQL Server or PostgreSQL is authoritative, rather than casually permitting unrestricted dual writes. If a short rollback window requires bidirectional synchronization, use scoped, idempotent change capture with conflict ownership and reconciliation; log and reconcile every divergence, status transition, and history record.

Monitor request errors, 404/403 behavior, tenant leakage tests, RLS policy violations, latency, database locks, query plans, migration lag, and row-count or checksum drift. Expand only when a carrier is stable for a defined observation period, reconciliation is within an agreed zero-drift threshold, and no cross-tenant access or data-loss defect remains. Roll back by restoring routing to SQL Server and disabling PostgreSQL writes; investigate and re-reconcile before retrying. The rollback criterion should include reconciliation failures, tenant leakage, unacceptable latency or errors, or loss of write durability.

## Risks and Operations

Key risks are a false reconciliation result, lost or duplicated records, inconsistent status/history transitions, and stale data during cutover. Validate constraints and reconcile field-level data, not only totals. PostgreSQL query plans may differ from SQL Server; load-test representative list/search/paging queries and verify that indexes support tenant and status filters. Test RLS with positive and negative cases, including guessed IDs and consignment numbers for different tenants and attempted direct SQL access.

Operate backup and restore for PostgreSQL, test point-in-time recovery, retain SQL Server rollback backups or snapshots for the controlled pilot window, and add tenant- and operation-tagged observability plus audit records for routing, migration, reconciliation, and failures.
