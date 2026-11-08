# ADR-005 — Signed development-only synthetic identity

Accepted, 8 November 2026; planning decision D-03.

Use synthetic seeded accounts and official `dotnet user-jwts` one-hour tokens. JWT bearer authentication validates signature, issuer, audience, expiry and a GUID `sub`. Signing keys live in user-secrets, outside Git. Swagger uses HTTP bearer Authorize. No token issuance API or arbitrary identity headers are provided.

SQL is authoritative for current account, membership and Host approval on every operation; token roles are insufficient. The application refuses to start outside Development, ensuring this demo configuration cannot silently become a live authentication system. Normal startup does not seed or migrate; an explicit development `--seed` utility performs those tasks.

Only synthetic meeting-point labels and account records are seeded. No real identity evidence is stored. Log safe classifications and correlation, not token/request bodies or exception internals.
