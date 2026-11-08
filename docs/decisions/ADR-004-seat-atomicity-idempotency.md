# ADR-004 — Atomic seats and Rider-scoped idempotency

Accepted, 8 November 2026; planning decision D-06.

One read-committed SQL Server transaction conditionally updates a Published, future, currently eligible ride with positive availability. The same SQL statement decrements one seat and derives Full from the old final-seat value. The transaction then inserts the Confirmed booking and commits. No decrement is committed alone.

Unique `(RiderId, RideId)` and `(RiderId, IdempotencyKey)` indexes enforce duplicates and retries. The key uses binary case-sensitive collation. Its stored SHA-256 request fingerprint covers the operation, canonical ride GUID and fixed quantity one. Keys must be nonempty, at most 128 characters and have no surrounding whitespace; UUID strings are recommended.

Committed identical replay is resolved before availability. Zero-row updates and unique insert collisions roll back before rereading the winner. First commit returns 201, identical replay 200, key misuse or duplicate Rider/Ride 409. All other failures roll back on transaction disposal. SQL Server integration tests prove final-seat races, concurrent same-key attempts, different-ride key collisions and failed-insert rollback.
