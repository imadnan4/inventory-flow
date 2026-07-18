# Code Context

## Files Retrieved
1. `backend/src/InventoryFlow.Api/Controllers/InventoryController.cs` (lines 10-55) — authenticated receipt/issue/balance API already exists.
2. `backend/src/InventoryFlow.Infrastructure/Inventory/EfInventoryLedger.cs` (lines 10-43) — serializable, execution-strategy-wrapped atomic ledger writer.
3. `backend/src/InventoryFlow.Infrastructure/Inventory/InventoryLedgerWriter.cs` (lines 9-42) — validates active product/warehouse, applies signed balance, and enforces movement idempotency.
4. `backend/src/InventoryFlow.Infrastructure/Purchases/EfPurchaseReceiptService.cs` (lines 11-64) — exact template for a posted document plus one linked ledger movement in one transaction.
5. `backend/src/InventoryFlow.Domain/Entities/PurchaseReceipt.cs` (lines 6-34) — small immutable document model to mirror for a fulfillment.
6. `backend/src/InventoryFlow.Infrastructure/Persistence/Configurations/InventoryMovementConfiguration.cs` (lines 13-24) — movement idempotency is unique per workspace, not per movement type.
7. `backend/src/InventoryFlow.Domain/Entities/WorkspaceMember.cs` (lines 6-28), `WorkspaceMemberRole.cs` (lines 1-9), and `Infrastructure/Tenancy/CurrentWorkspaceResolver.cs` (lines 11-24) — roles are Owner-only and workspace resolution explicitly selects the one Owner membership.
8. `backend/tests/InventoryFlow.IntegrationTests/Api/PurchaseReceiptEndpointsTests.cs` (lines 40-185) — reusable acceptance-test pattern: tenancy, archival/no side effects, ordering, replay, and concurrent replay.
9. `backend/tests/InventoryFlow.IntegrationTests/Api/InventoryEndpointsTests.cs` (lines 29-56, 122-197) — proves issue/overdraft semantics and archival concurrency already work.
10. `frontend/src/features/purchases/pages/PurchasesPage.tsx` (lines 30-104, 107-245) — reusable form/history/retry/query-invalidation UI pattern.
11. `frontend/src/app/router/index.tsx` (lines 52-72) — sales, reports, and users remain placeholders.

## Key Code

### Recommendation: **single-line sales fulfillment** (not sales orders/customers)
Add an immutable `SalesFulfillment` document that atomically creates exactly one existing `InventoryMovementType.Issue`; expose `POST`/`GET` at `/api/sales/fulfillments`; replace the `/sales` placeholder with a product/warehouse/quantity form and fulfillment history.

This is the smallest *next operational vertical slice*: it closes the outbound counterpart to the just-added supplier receipt while reusing all material inventory correctness. No Customer, SalesOrder, pricing, tax, reservation, partial-fulfillment, or multi-line scope is necessary.

Existing reusable issue path:
```csharp
// InventoryController.cs:31-47
await sender.Send(new RecordIssueCommand(workspace.Id, request.WarehouseId,
    request.ProductId, request.Quantity, idempotencyKey ?? request.IdempotencyKey), cancellationToken);
```

The ledger already provides the critical behavior: serializable transaction (`EfInventoryLedger.cs:18-31`), active source checks (`InventoryLedgerWriter.cs:22-27`), and a signed balance update (`InventoryLedgerWriter.cs:37`). Its domain balance rejects an overdraft; existing integration coverage expects `409 Conflict` (`InventoryEndpointsTests.cs:41-55`).

### Exact implementation prerequisites
1. Add `SalesFulfillment` entity with only `Id, WorkspaceId, WarehouseId, ProductId, Quantity, IdempotencyKey, InventoryMovementId, FulfilledAtUtc`; retain the same nonempty-ID, UTC, positive decimal(18,4), and normalized key rules as `PurchaseReceipt.cs:8-23`.
2. Add DbSet, entity configuration, migration, document-idempotency unique index `(WorkspaceId, IdempotencyKey)`, workspace cascade FK, and restricted warehouse/product FKs. Do **not** make the ledger movement key equal the document key: movement keys are unique workspace-wide (`InventoryMovementConfiguration.cs:18-21`), so use a deterministic internal key such as fulfillment ID `N` format, as receipts do.
3. Add application request/command/query/response/service/validator and handlers; add an infrastructure fulfillment service copied from `EfPurchaseReceiptService.cs:11-64`, but omit supplier validation and call `InventoryLedgerWriter.RecordAsync(... Issue, ...)` inside the same serializable transaction.
4. Add authorized controller `POST /api/sales/fulfillments` and `GET /api/sales/fulfillments`, resolving workspace exclusively via `ICurrentWorkspace` as purchases do. Register the service in infrastructure DI.
5. Add frontend `sales-api.ts`, types, Zod schema, and `SalesPage.tsx`, modeled on Purchases: warehouse/product selectors, decimal quantity, generated UUID retained for retry, history, and invalidation of sales history and inventory balances. Lazy-load it in router in place of the placeholder.

### Comparison

| Candidate | Incremental scope | Why not next / result |
|---|---|---|
| **Sales fulfillment** | One immutable document + one existing issue movement; product/warehouse UI data and all nonnegative/idempotent mechanics exist. | **Recommended.** Smallest new end-to-end business operation after receipts; mirrors established pattern without new master data. |
| Warehouse transfer | One document but **two** linked movements and two balances, source/destination validation, same-warehouse guard, both-side atomicity/replay, and internal movement-key derivation. | Larger and materially riskier; source issue must succeed before destination receipt, while no partial state can commit. Defer. |
| Roles/invitations | Requires mutable multi-role membership, invitation lifecycle/token/email policy, authorization policies, and selecting/switching a workspace. | Large foundation change: current domain forbids every role other than Owner (`WorkspaceMember.cs:14`) and resolver only accepts Owner (`CurrentWorkspaceResolver.cs:18-23`). |
| Reporting | Can read balances now, but meaningful movement/receipt/fulfillment reporting needs query projections, filters/time semantics, and outbound documents; it does not create the missing operational counterpart. | Technically small only as a dashboard read model, but lower-value and likely reworked after fulfillment. Defer until both inbound/outbound documents exist. |

## Architecture

`SalesPage` -> sales API client -> authenticated `SalesFulfillmentsController` -> MediatR command -> fulfillment service -> one serializable EF transaction containing `SalesFulfillment`, `InventoryMovement(Issue)`, and updated `InventoryBalance`. The single client idempotency key identifies/replays the fulfillment document; a separate internal key identifies its ledger row. GET queries documents scoped by the server-resolved workspace. This follows the receipt route/service/document flow exactly while relying on the already-tested issue ledger for overdraft prevention.

## Acceptance

A proposed fulfillment slice is done only when all are true:
- Unauthenticated POST/GET return 401; authenticated cross-workspace, archived product, and archived warehouse inputs return 404 with no fulfillment/movement/balance side effect.
- Valid POST returns 201, persists one fulfillment linked to one `Issue` movement, and lowers only that warehouse/product balance by the requested valid decimal quantity.
- Insufficient stock returns 409 and persists neither fulfillment nor movement nor balance change.
- Same idempotency key replays 201 with the original fulfillment; concurrent identical requests leave exactly one fulfillment and one Issue movement.
- GET returns only current-workspace fulfillments, ordered newest-first.
- `/sales` replaces its placeholder with a retry-safe form/history; success invalidates the sales-history and inventory-balance queries.
- Backend migration applies; backend solution tests/build/format and frontend typecheck/lint/build/prettier pass.

## Review Findings

- **High — transfer is not a safe “small” reuse of the receipt service.** `InventoryLedgerWriter` supports a single movement/balance only (`backend/src/InventoryFlow.Infrastructure/Inventory/InventoryLedgerWriter.cs:9-42`), while transfers must atomically issue and receive. Implementing transfer through two public calls can create partial stock movement.
- **High — roles/invitations cannot be layered on current tenancy without changing its central contract.** Membership construction rejects all non-Owner roles (`backend/src/InventoryFlow.Domain/Entities/WorkspaceMember.cs:10-15`) and request resolution filters Owner membership (`backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs:16-24`).
- **Medium — a sales page that merely calls existing `/api/inventory/issues` is not auditable fulfillment.** The generic issue endpoint exists (`backend/src/InventoryFlow.Api/Controllers/InventoryController.cs:31-47`) but creates no sales document/history, unlike receipt document linkage (`backend/src/InventoryFlow.Infrastructure/Purchases/EfPurchaseReceiptService.cs:44-49`).

## Start Here

Open `backend/src/InventoryFlow.Infrastructure/Purchases/EfPurchaseReceiptService.cs`: it is the shortest authoritative template for the recommended document-plus-ledger atomic transaction. Then mirror its boundary in `PurchaseReceiptModels.cs`, handlers, controller, and `PurchaseReceiptEndpointsTests.cs` before building the Sales UI.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Concrete comparative recommendation, exact paths/line ranges, severity-rated review findings, prerequisites, and acceptance are documented above."
    }
  ],
  "changedFiles": [
    ".pi-subagents/artifacts/outputs/a85ef940-7c5b-4f33-a2fe-46559f2b537c/parallel-0/0-scout/context.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "git log --oneline -12 && git status --short",
      "result": "passed",
      "summary": "HEAD is 0b95b48 feat(purchases): add atomic purchase receipts; only untracked .pi-subagents runtime files were present."
    },
    {
      "command": "find/read targeted backend and frontend feature files",
      "result": "passed",
      "summary": "Confirmed existing issue ledger, receipt atomic template, Owner-only tenancy, and sales/reports/users placeholders."
    }
  ],
  "validationOutput": [
    "Inspection-only task; no product code was edited and no build/test suite was run."
  ],
  "residualRisks": [
    "A sales fulfillment without customer/order fields is intentionally a direct stock-out; adding reservations, partial fulfillment, or customer attribution later requires a new slice.",
    "The recommended document service must keep its own idempotency lookup before writing its distinct internal ledger key to preserve correct replay behavior."
  ],
  "noStagedFiles": true,
  "diffSummary": "No product-code diff; wrote the required scouting artifact only.",
  "reviewFindings": [
    "high: backend/src/InventoryFlow.Infrastructure/Inventory/InventoryLedgerWriter.cs:9-42 - transfer cannot safely be composed from two independent movements.",
    "high: backend/src/InventoryFlow.Domain/Entities/WorkspaceMember.cs:10-15 and backend/src/InventoryFlow.Infrastructure/Tenancy/CurrentWorkspaceResolver.cs:16-24 - roles/invitations require tenancy-contract redesign.",
    "medium: backend/src/InventoryFlow.Api/Controllers/InventoryController.cs:31-47 - generic issue lacks an auditable sales fulfillment document."
  ],
  "manualNotes": "Recommended smallest safe next vertical slice: single-line direct sales fulfillment backed by one atomic Issue ledger movement."
}
```