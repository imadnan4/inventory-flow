# Research: Purchase-receipt idempotency and transactional inventory-ledger posting

## Summary
Make receipt posting a one-way document transition performed in one database transaction: claim an immutable, non-null idempotency/business-receipt key under a database `UNIQUE` constraint; create the receipt, lines, and all stock-ledger movements; then mark it `POSTED` and commit. Retries must return the already-persisted receipt/result, never repeat ledger inserts; cancellation should post a linked compensating movement rather than mutate or delete the original ledger.

## Findings
1. **Enforce duplicate prevention in the database, not with “select then insert.”** Give every logical receipt a server-scoped immutable key: normally `tenant_id + source_system + external_receipt_id`; for an API command also store a client-generated `idempotency_key` and a canonical request hash. Make the fields `NOT NULL` and enforce `UNIQUE` (or a primary key). PostgreSQL documents that a multicolumn unique constraint enforces uniqueness of the *combination*, and that ordinary unique constraints allow multiple nulls—hence nullable receipt keys do not provide deduplication. A primary key supplies both uniqueness and not-nullness. [PostgreSQL Constraints](https://www.postgresql.org/docs/current/ddl-constraints.html)

2. **Use the unique constraint as the concurrent idempotency arbiter.** In PostgreSQL, issue `INSERT ... ON CONFLICT` against that unique key as the first durable step; on conflict, fetch the existing receipt/operation and compare its immutable request hash. Return its recorded result when equal; reject the same key with different payload (do not overwrite a posted receipt). `ON CONFLICT DO UPDATE` guarantees an atomic insert-or-update outcome under high concurrency, while `DO NOTHING` can skip an insert because of an unobservable concurrent transaction—so a `DO NOTHING` implementation must subsequently read/wait for the canonical row rather than treat “zero rows” as an error or permission to post again. [PostgreSQL INSERT](https://www.postgresql.org/docs/current/sql-insert.html) [PostgreSQL transaction isolation](https://www.postgresql.org/docs/current/transaction-iso.html)

3. **Persist and replay the operation outcome, including an uncertain request response.** Store `operation_key`, `request_hash`, `receipt_id`, status (`PROCESSING`/`POSTED`/`REJECTED`), and a stable response/result reference in the same database. The external API precedent is to return the first result for later same-key attempts and to reject same-key different parameters; Stripe specifically notes that its idempotency layer stores the first status/body (including failures after execution begins). For receipt posting, retain the idempotency record at least as long as upstream retries and integrations can recur—unlike a short cache, a business receipt number should ordinarily remain unique indefinitely. [Stripe idempotent requests](https://docs.stripe.com/api/idempotent_requests)

4. **Commit document state and inventory effects atomically.** One `BEGIN`/`COMMIT` block must: validate a `DRAFT`/eligible receipt; insert immutable receipt lines; insert every stock-ledger movement keyed to `receipt_id` (and, where appropriate, `(receipt_id, line_id, movement_type)` unique); update any materialized on-hand balance; persist the idempotent success result; and transition the receipt to `POSTED`. On any validation, constraint, or stock failure, `ROLLBACK` the whole unit. PostgreSQL states that transaction steps are all-or-nothing, invisible until completion, and durable before completion is reported—precisely the guarantee needed to prevent a posted receipt without stock movements or stock movements without a receipt. [PostgreSQL Transactions](https://www.postgresql.org/docs/current/tutorial-transactions.html)

5. **Make the lifecycle explicit and one-way: `DRAFT → POSTED → CANCELLED/REVERSED`; do not repost the original.** Once posted, treat receipt identity, lines, quantities, warehouse, and valuation inputs as immutable. A correction should be a new/amended receipt or a cancellation with a separate reversal movement linked to the original; require a unique reversal relationship so an original receipt is reversed once. ERPNext’s primary product documentation describes immutable stock/accounting ledgers: cancellation adds reverse entries rather than deleting linked entries, and it restricts backdated stock posts because they would alter later FIFO valuations. [ERPNext Immutable Ledger](https://docs.erpnext.com/docs/user/manual/en/immutable-ledger-in-erpnext)

6. **Choose a concurrency strategy for cross-row inventory rules and retry complete transactions.** A row-local `CHECK` is useful for quantities/status shape, and foreign keys should bind ledger rows to receipts and receipt lines, but PostgreSQL warns that `CHECK` cannot safely enforce conditions involving other rows. For rules such as available capacity, a receipt’s remaining quantity, or a balance table, either use `SERIALIZABLE` for every relevant read/write and retry the entire transaction on SQLSTATE `40001`, or lock the exact control/balance rows with `SELECT ... FOR UPDATE` before validating and updating. PostgreSQL explicitly says Serializable execution is equivalent to some serial ordering but can abort and must be retried; it also recommends serializable transactions or explicit locks for application-level consistency. [PostgreSQL Constraints](https://www.postgresql.org/docs/current/ddl-constraints.html) [PostgreSQL application-level consistency](https://www.postgresql.org/docs/current/applevel-consistency.html) [PostgreSQL transaction isolation](https://www.postgresql.org/docs/current/transaction-iso.html)

## Practical relational pattern

```text
purchase_receipt(
  receipt_id PK,
  tenant_id NOT NULL,
  source_system NOT NULL,
  external_receipt_id NOT NULL,
  status NOT NULL CHECK (status IN ('DRAFT','POSTED','CANCELLED')),
  ...,
  UNIQUE (tenant_id, source_system, external_receipt_id)
)
receipt_operation(
  tenant_id NOT NULL,
  idempotency_key NOT NULL,
  request_hash NOT NULL,
  receipt_id NULL REFERENCES purchase_receipt,
  status NOT NULL,
  response_ref ...,
  PRIMARY KEY (tenant_id, idempotency_key)
)
stock_ledger_entry(
  entry_id PK, receipt_id NOT NULL REFERENCES purchase_receipt,
  receipt_line_id NOT NULL REFERENCES purchase_receipt_line,
  movement_type NOT NULL, quantity_delta NOT NULL CHECK (quantity_delta <> 0),
  reversal_of_entry_id NULL REFERENCES stock_ledger_entry,
  UNIQUE (receipt_id, receipt_line_id, movement_type)
)
```

The posting transaction should claim/create `receipt_operation` first, validate same-key payload equality on conflict, serialize the receipt/control row as required, write receipt+ledger+balance effects, set `POSTED`, record the response, and commit. Do not publish events before commit; use a transactional outbox written in that same transaction if downstream integration is required. A relay can then retry delivery independently with an event ID unique at consumers.

## Sources
- Kept: [PostgreSQL: Transactions](https://www.postgresql.org/docs/current/tutorial-transactions.html) — authoritative atomicity, visibility, rollback, and durability semantics.
- Kept: [PostgreSQL: INSERT](https://www.postgresql.org/docs/current/sql-insert.html) — authoritative `ON CONFLICT` concurrency and atomic UPSERT behavior/caveats.
- Kept: [PostgreSQL: Constraints](https://www.postgresql.org/docs/current/ddl-constraints.html) — primary details on composite uniqueness, null behavior, PK/FK, and cross-row `CHECK` limitation.
- Kept: [PostgreSQL: Transaction Isolation](https://www.postgresql.org/docs/current/transaction-iso.html) — authoritative isolation guarantees and whole-transaction retry requirement.
- Kept: [PostgreSQL: Application-Level Consistency](https://www.postgresql.org/docs/current/applevel-consistency.html) — explicit serializable-vs-locking guidance.
- Kept: [Stripe: Idempotent requests](https://docs.stripe.com/api/idempotent_requests) — primary implementation precedent for key/result/payload-match replay semantics.
- Kept: [ERPNext: Immutable Ledger](https://docs.erpnext.com/docs/user/manual/en/immutable-ledger-in-erpnext) — primary ERP evidence for compensating cancellation and immutable stock history.
- Dropped: SQL Server secondary idempotency articles — non-primary and unnecessary for database-agnostic principles.
- Dropped: generic ledger blog posts/repositories — weaker than the kept ERP product documentation.

## Gaps
- No repository code/schema was inspected, so this brief cannot identify whether the required uniqueness constraints, state guards, transactions, ledger keys, or outbox already exist.
- The exact DDL and conflict SQL depend on the target RDBMS; the concrete syntax above is PostgreSQL-oriented. SQL Server, MySQL, and Oracle need equivalent unique-index, transaction, locking/isolation, and retry implementations.
- Distributed side effects (supplier acknowledgements, message brokers, labels) are not atomically committed by a local relational transaction alone; use a transactional outbox and idempotent consumers, or an explicitly designed distributed protocol.

## Review findings
1. **Severity: high — repository-wide / no source file inspected.** Receipt posting is unsafe if duplicate prevention is application-only, receipt keys can be null, or a retry can use the same key with a different payload. Add durable unique constraints plus payload comparison.
2. **Severity: high — repository-wide / no source file inspected.** Receipt status changes, ledger writes, balances, and idempotent result persistence must share one transaction; otherwise partial commits create irreconcilable inventory/accounting state.
3. **Severity: medium — repository-wide / no source file inspected.** Editing/deleting posted ledger entries weakens auditability and can invalidate FIFO history; use linked compensating reversals and prevent repeat reversal.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Concrete, primary-source-backed findings, severities, and the output file path are provided in this artifact."
    }
  ],
  "changedFiles": [
    "/home/adnan/Projects/Inventory-Flow/.pi-subagents/artifacts/outputs/d2aa83fa-dc83-4865-a542-05aa3ff0ff01/parallel-0/2-researcher/research.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "Focused primary-source web research (PostgreSQL, Stripe, ERPNext)",
      "result": "passed",
      "summary": "Retrieved and reviewed authoritative documentation relevant to uniqueness, atomic commit, isolation, idempotency, and immutable reversal."
    }
  ],
  "validationOutput": [
    "Research artifact written to the required path.",
    "review-findings and residual-risks are included."
  ],
  "residualRisks": [
    "No application code or schema was reviewed; actual implementation gaps are unknown.",
    "External side effects require an outbox/idempotent-consumer design beyond a single local database transaction."
  ],
  "noStagedFiles": true,
  "diffSummary": "Created the requested research artifact only; no application files were edited.",
  "reviewFindings": [
    "high: repository-wide / no source file inspected - application-only deduplication or nullable receipt keys allow duplicate posting.",
    "high: repository-wide / no source file inspected - non-atomic receipt, ledger, balance, and operation-result writes permit partial inventory state.",
    "medium: repository-wide / no source file inspected - mutable/deleted posted ledger rows reduce auditability and can disrupt valuation history."
  ],
  "manualNotes": "No code was edited; this is a primary-source research brief."
}
```