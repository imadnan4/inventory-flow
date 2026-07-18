# Research: Atomic inventory transfers, locking, idempotency, and ledger audit

## Summary
For PostgreSQL, make the source decrement, destination increment, transfer/idempotency record, and two immutable movement entries part of **one short transaction**. Lock the two `(item_id, location_id)` balance rows in a canonical sorted order before validating and changing them; PostgreSQL explicitly identifies inconsistent multi-object lock order as the principal deadlock defense and requires retries after deadlock/serialization aborts.

## Findings
1. **[Critical] Atomic two-row transfer** — Within one transaction, identify the two predetermined balance rows, lock both using `SELECT ... FOR UPDATE ... ORDER BY location_id` (or a canonical composite balance key), verify `source.on_hand >= qty`, then decrement source and increment destination. `FOR UPDATE` blocks concurrent writers/lockers on those rows until transaction end; rollback makes neither balance change nor its corresponding ledger/idempotency writes durable. PostgreSQL specifically uses the analogous two-account transfer as a valid predetermined-row Read Committed example. [Explicit locking](https://www.postgresql.org/docs/current/explicit-locking.html) · [Transaction isolation](https://www.postgresql.org/docs/current/transaction-iso.html)
2. **[Critical] Deterministic locking and retry** — Never acquire source then destination in caller-supplied direction. Sort the two unique balance keys and acquire locks in that same order in every transfer/adjustment/reservation path. PostgreSQL documents row-lock deadlocks with two account updates, says consistent order is the best defense, and says deadlock victims must be retried. Keep transactions short; locks otherwise wait indefinitely. [PostgreSQL explicit locking](https://www.postgresql.org/docs/current/explicit-locking.html)
3. **[High] Isolation choice** — A simple transfer over two fixed rows can use the above locking transaction under Read Committed. If correctness also depends on predicates or aggregates beyond the two locked rows (for example, a cross-location allocation rule), use `SERIALIZABLE` and retry the **entire** transaction on SQLSTATE `40001`; PostgreSQL says successful Serializable transactions act as a serial execution but applications must handle retries. [PostgreSQL transaction isolation](https://www.postgresql.org/docs/current/transaction-iso.html)
4. **[Critical] Idempotency is a database invariant, not only an HTTP convention** — Store a client/request idempotency key with a `UNIQUE` constraint in a `transfer` (or request) table, with request fingerprint and final result/status. In the same transfer transaction, insert it and use `INSERT ... ON CONFLICT` to return/reuse the existing completed transfer rather than apply movements again. PostgreSQL guarantees an atomic insert-or-update outcome for `ON CONFLICT DO UPDATE`; Stripe’s official guidance confirms the general retry model: retain first result and reject a reused key with mismatched parameters. Do not treat a key as permanently complete before the transaction commits. [PostgreSQL INSERT](https://www.postgresql.org/docs/current/sql-insert.html) · [Stripe idempotent requests](https://docs.stripe.com/api/idempotent_requests)
5. **[High] Ledger/audit design** — Write two append-only movement rows (negative source, positive destination) with `transfer_id`, item, location, quantity delta, actor, reason, and timestamp in the same transaction as balances. Use PK/FK/NOT NULL/CHECK constraints (e.g., nonzero delta) and enforce exactly two opposite-signed rows per transfer in service logic or a deferred constraint trigger; PostgreSQL warns that `CHECK` cannot safely enforce cross-row/table invariants. An `AFTER` trigger can write audit records transactionally; a separate trigger/privilege policy should reject `UPDATE`/`DELETE` on the ledger for append-only behavior. [PostgreSQL constraints](https://www.postgresql.org/docs/current/ddl-constraints.html) · [PostgreSQL trigger behavior](https://www.postgresql.org/docs/current/trigger-definition.html)

### Recommended transfer shape
```sql
BEGIN;
-- Canonical order: lock source + destination balance records by one stable key.
SELECT * FROM inventory_balance
WHERE (item_id, location_id) IN ((:item, :source), (:item, :destination))
ORDER BY item_id, location_id
FOR UPDATE;

-- fail/rollback if either row is absent, source == destination, or source lacks quantity
UPDATE inventory_balance SET on_hand = on_hand - :qty
 WHERE item_id = :item AND location_id = :source;
UPDATE inventory_balance SET on_hand = on_hand + :qty
 WHERE item_id = :item AND location_id = :destination;
INSERT INTO inventory_movement (...) VALUES (..., -:qty), (..., :qty);
-- transfer idempotency row is inserted/claimed in this same transaction
COMMIT;
```

Retry only transient database failures (deadlock and `40001`) with bounded backoff, reusing the *same* idempotency key. Do not retry business rejections such as insufficient stock.

## Sources
- Kept: [PostgreSQL: Explicit Locking](https://www.postgresql.org/docs/current/explicit-locking.html) — primary guidance for row locks, deadlocks, consistent ordering, and short transactions.
- Kept: [PostgreSQL: Transaction Isolation](https://www.postgresql.org/docs/current/transaction-iso.html) — primary guidance for fixed-row transfers, Serializable semantics, and SQLSTATE `40001` retry.
- Kept: [PostgreSQL: INSERT](https://www.postgresql.org/docs/current/sql-insert.html) — primary `ON CONFLICT` atomicity guarantee for idempotency records.
- Kept: [PostgreSQL: Constraints](https://www.postgresql.org/docs/current/ddl-constraints.html) — limitations of cross-row `CHECK` and appropriate constraint building blocks.
- Kept: [PostgreSQL: Trigger Behavior](https://www.postgresql.org/docs/current/trigger-definition.html) — transactional trigger behavior and audit propagation.
- Kept: [Stripe: Idempotent requests](https://docs.stripe.com/api/idempotent_requests) — official external API retry/idempotency semantics; analogy, not PostgreSQL-specific implementation guidance.
- Dropped: PostgreSQL historical-version documentation — redundant with current official manuals.

## Gaps
No repository schema, database engine/version, or current transfer implementation was provided, so this brief cannot attest that the required composite unique key, balance-row creation policy, grants, trigger ownership, or retry classification exists. Reconcile balances against movement sums periodically; database triggers alone do not prevent privileged roles from altering an audit table.