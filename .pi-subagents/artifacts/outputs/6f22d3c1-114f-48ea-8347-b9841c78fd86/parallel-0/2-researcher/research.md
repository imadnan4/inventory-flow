# Research: Initial B2B inventory catalog API and EF Core relational model

## Summary
Use a stable surrogate `ProductId` as the product’s primary key and enforce business SKU uniqueness in the database with a unique composite index scoped to the owning workspace. Model stock and money as `decimal` with explicitly selected, domain-driven precision/scale; archive rather than delete with a global filter; and make catalog writes conditional on an optimistic-concurrency token. For a first B2B release, a shared database with `WorkspaceId` on every tenant-owned table and a context-bound global filter is the lowest operational-cost tenant model, but it needs authorization and write-path safeguards in addition to the filter.

## Findings
1. **Product identity and SKU — high priority.** Use an opaque, immutable primary key (`ProductId`, usually generated `Guid`/numeric) for all foreign keys; keep `Sku` as a mutable business identifier. EF Core says a primary key uniquely identifies an entity, while an alternate key adds relationship-target semantics; when uniqueness alone is wanted it recommends a **unique index** rather than an alternate key. For tenant-local SKUs configure a required, bounded normalized SKU and `HasIndex(p => new { p.WorkspaceId, p.NormalizedSku }).IsUnique()`. Do not make SKU globally unique unless the business contract truly says it is. [Keys](https://learn.microsoft.com/en-us/ef/core/modeling/keys) · [Indexes](https://learn.microsoft.com/en-us/ef/core/modeling/indexes)
   - API: never accept `WorkspaceId` as trusted client authority; derive it from authenticated membership/route authorization. `POST /products` should return 201 and the immutable ID; translate database unique-constraint violations to a deterministic 409 conflict.
   - Normalize deliberately before persistence (e.g., trim and apply an agreed invariant case policy) and align the unique index’s database collation with that policy. EF Core documents that collation controls text comparison/ordering, so case behavior is provider/database-dependent. [Entity properties—collations](https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties#column-collations)

2. **Decimal quantities and currency — high priority.** Persist counts that can be fractional (`OnHandQuantity`, reorder thresholds, pack conversion factors) as `decimal`, never `float`/`double`; choose precision and scale explicitly with `HasPrecision(precision, scale)` / `[Precision]`. EF Core defines precision as total digits and scale as decimal places, and warns EF does not validate either before sending data to the provider. Example starting contracts (must be confirmed against business limits): quantities `decimal(18,4)`, unit price `decimal(19,4)`, and totals `decimal(19,4)` or a documented calculation/rounding policy. Add database check constraints such as quantity >= 0 where negative inventory is not part of the chosen domain model. [Precision and scale](https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties#precision-and-scale) · [Check constraints](https://learn.microsoft.com/en-us/ef/core/modeling/indexes#check-constraints)
   - Model money as an amount plus required ISO-4217-style `CurrencyCode` (bounded string, normally 3 characters); do not store a bare decimal price. Store the currency on each price/transaction snapshot even if the workspace has a default, so historic meaning survives a default change. Rounding, tax inclusivity, and whether prices may have more than two minor-unit decimals are business rules, not supplied by EF Core.

3. **Archival/soft delete — medium priority.** Prefer `ArchivedAtUtc` nullable (and optionally `ArchivedByUserId`) rather than hard deletion for catalog products referenced by orders or movements. Apply `HasQueryFilter(p => p.ArchivedAtUtc == null)` so ordinary reads hide archived rows; EF Core documents this as the soft-delete pattern and permits exceptional administrative reads with `IgnoreQueryFilters()`. Intercept/override delete operations only if the API exposes delete semantics; otherwise expose an explicit archive command. [Global query filters—soft deletion](https://learn.microsoft.com/en-us/ef/core/querying/filters#basic-example---soft-deletion)
   - Decide SKU reuse explicitly: the safest audit/identity choice is an unfiltered unique `(WorkspaceId, NormalizedSku)` index, so an archived SKU cannot silently identify a new product. A filtered unique index for active rows allows reuse but makes historical references ambiguous unless they retain `ProductId`. EF supports filtered indexes where the provider does. [Indexes—filter](https://learn.microsoft.com/en-us/ef/core/modeling/indexes#index-filter)

4. **Optimistic concurrency — high priority.** Add a version property to each mutable aggregate that is edited through the catalog API. For SQL Server, map `byte[] RowVersion` with `[Timestamp]`/`IsRowVersion()`; EF Core includes the original token in update/delete predicates and throws `DbUpdateConcurrencyException` when zero rows are affected. Return the token (base64 or ETag) on reads and require it on update/archive requests; catch the exception and return 409 (or 412 if using HTTP conditional request semantics), requiring refresh/merge rather than last-write-wins. [Handling concurrency conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
   - `rowversion` is SQL Server-specific; SQLite and other providers may require an application-managed token (e.g., regenerated GUID). Verify the selected provider and protect stock mutation separately—an inventory movement/ledger plus transaction may be more appropriate than updating a product’s `OnHand` directly. [Handling concurrency conflicts—native/application-managed tokens](https://learn.microsoft.com/en-us/ef/core/saving/concurrency#native-database-generated-concurrency-tokens)

5. **Workspace ownership / tenancy trade-off — high priority.** Recommended initial shape: `Workspace` table; each owned aggregate (`Product`, later `InventoryLocation`, `Price`, `InventoryMovement`) has non-null `WorkspaceId` FK; child tables should either contain `WorkspaceId` and enforce composite tenant-consistent relationships or be reached only through a tenant-owned parent. In one shared database, construct the `DbContext` with the current trusted workspace ID and use a global `WorkspaceId == _workspaceId` filter. EF Core specifically identifies a discriminator column plus global query filter as its supported shared-database approach, intended to prevent accidental cross-tenant query access. [EF Core multi-tenancy](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy) · [Global query filters—multi-tenancy](https://learn.microsoft.com/en-us/ef/core/querying/filters#using-context-data---multi-tenancy)
   - Trade-off: shared DB/column discriminator is simplest and supports global filtering; database-per-tenant offers stronger physical isolation and per-tenant restore/customization, but adds connection provisioning, migrations, reporting, and operational cost. EF Core lists database-per-tenant as supported by configuration, but says schema-per-tenant is not directly supported/recommended. Global filters are defense-in-depth, not authorization: privileged code can call `IgnoreQueryFilters()`, and writes/foreign-key lookups still require server-side workspace authorization. [EF Core multi-tenancy](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy)

## Recommended first schema/API contract
- `Workspace(Id, …)`; `Product(Id PK, WorkspaceId FK NOT NULL, Sku, NormalizedSku, Name, Description?, UnitOfMeasureCode, DefaultCurrencyCode?, ArchivedAtUtc?, RowVersion, CreatedAtUtc, UpdatedAtUtc)`.
- Unique index: `(WorkspaceId, NormalizedSku)`; add query indexes based on actual list endpoint filters such as `(WorkspaceId, ArchivedAtUtc, Name)` rather than indexing every field. Composite index order matters for the leading filtered columns. [Indexes—composite indexes](https://learn.microsoft.com/en-us/ef/core/modeling/indexes#composite-index)
- Keep price as a separate time-effective `ProductPrice` if price history is required: it carries `ProductId`, `Amount decimal(p,s)`, `CurrencyCode`, effective dates, and version. Keep stock as a location/ledger concern rather than a single catalog fact when multi-location or auditability is expected.
- Endpoints: workspace-scoped list/get/create/update/archive; no hard-delete initially. Include product `id`, SKU, archive state, and version in representations. Validate DTO bounds, scale, currency code and SKU normalization before `SaveChanges`, but retain database constraints as the final integrity boundary.

## Sources
- Kept: [EF Core Keys](https://learn.microsoft.com/en-us/ef/core/modeling/keys) — primary guidance distinguishing primary, alternate, and unique business identifiers.
- Kept: [EF Core Indexes](https://learn.microsoft.com/en-us/ef/core/modeling/indexes) — primary unique/composite/filtered index and check-constraint guidance.
- Kept: [EF Core Entity Properties](https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties) — primary precision/scale, nullability, and collation guidance.
- Kept: [EF Core Handling Concurrency Conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) — primary optimistic-concurrency behavior and provider caveats.
- Kept: [EF Core Global Query Filters](https://learn.microsoft.com/en-us/ef/core/querying/filters) — primary soft deletion and tenant-filter behavior/caveats.
- Kept: [EF Core Multi-tenancy](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy) — primary comparison of discriminator, database-per-tenant, and unsupported schema-per-tenant approaches.
- Dropped: general blogs/ORM tutorials — not needed; official EF Core sources directly cover the requested framework behavior.

## Gaps
- The database provider, EF Core version, expected quantity granularity, currency/rounding policy, inventory valuation, and whether SKUs may be reused after archival were not specified. Precision values above are safe starting examples, not authoritative business limits; confirm maximum quantities/prices and currency minor-unit requirements before migration.
- The current official EF Core query-filter page documents named multiple filters as EF Core 10+ preview. For earlier/stable versions, combine the archive and workspace predicates in one `HasQueryFilter`; do not rely on named-filter selective disabling without version verification.

## Acceptance report
```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Concrete authoritative findings and severity-tagged recommendations are written to /home/adnan/Projects/Inventory-Flow/.pi-subagents/artifacts/outputs/6f22d3c1-114f-48ea-8347-b9841c78fd86/parallel-0/2-researcher/research.md."
    }
  ],
  "changedFiles": [
    "/home/adnan/Projects/Inventory-Flow/.pi-subagents/artifacts/outputs/6f22d3c1-114f-48ea-8347-b9841c78fd86/parallel-0/2-researcher/research.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "Focused official-source retrieval via web_search and fetch_content",
      "result": "passed",
      "summary": "Retrieved current EF Core documentation for keys, indexes, concurrency, properties, filters, and multi-tenancy; search-provider configuration was unavailable, so direct official URLs were fetched."
    }
  ],
  "validationOutput": [
    "Artifact written at the required path.",
    "review-findings and residual-risks are included below."
  ],
  "residualRisks": [
    "Precision/scale and rounding cannot be finalized without quantity, price, and currency business limits.",
    "SQL Server rowversion guidance is provider-specific; confirm the production EF Core provider.",
    "Shared-database tenant filters do not replace authorization or controlled use of IgnoreQueryFilters()."
  ],
  "noStagedFiles": true,
  "diffSummary": "No project files edited; created only the required research artifact.",
  "reviewFindings": [
    "high: SKU uniqueness must be database-enforced as (WorkspaceId, NormalizedSku), not application-only or globally by default.",
    "high: mutable catalog writes need a concurrency token and conflict response.",
    "high: workspace ID must be trusted server-side; query filters are defense-in-depth rather than authorization.",
    "medium: archival/SKU-reuse semantics must be explicitly chosen before creating the unique index."
  ],
  "manualNotes": "No project files were edited. Official-source search API was unavailable; direct current Microsoft Learn pages were fetched instead."
}
```