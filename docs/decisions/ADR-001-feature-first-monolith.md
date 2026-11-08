# ADR-001 — Feature-first monolith

Accepted, 8 November 2026; planning decision D-04.

Use one ASP.NET Core application and one SQL Server database. Minimal API groups provide the three public paths with named operations, feature tags, schemas and bearer security. Endpoints own HTTP mapping; feature services coordinate use cases and depend directly on `AppDbContext`. Shared result/error mapping is a small concrete utility.

No strict Clean Architecture assemblies, mediator, generic repository, message bus, or interfaces without a substitution need. This keeps the assessment reviewable while retaining feature boundaries.

Related: [matching](ADR-002-matching.md), [booking](ADR-003-booking.md), [atomicity](ADR-004-seat-atomicity-idempotency.md), [identity](ADR-005-development-identity.md).
