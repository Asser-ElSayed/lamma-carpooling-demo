# Validation evidence

Implementation baseline: planning v1.1, decisions D-01–D-08, 8 November 2026. Validation uses .NET SDK 10.0.202 / runtime 10.0.6 and real local SQL Server Express. Each integration run migrates a new uniquely named test database and drops only that database afterward.

Complete reviewed suite: **49 passed, 0 failed, 0 skipped**. Recorded command:

```powershell
dotnet test Lamma.sln --no-restore --logger 'console;verbosity=minimal' --logger trx --results-directory artifacts/test-results
```

Generated `.trx` evidence stays in ignored `artifacts/test-results`. The live smoke script also passed with official `dotnet user-jwts` tokens: Swagger/OpenAPI 200, publishing 201 with three seats, intermediate-stop search 200, booking 201, identical replay 200, third-seat fill, fourth Rider 409, and full ride absent from search. Tokens were omitted from script output.

| Acceptance | Automated evidence |
|---|---|
| AC-01 | `PublishingTests`: approved Host 201; unapproved Host and ordinary Rider 403; unknown/inactive account denied |
| AC-02 | `PublishingTests` and `DomainTests`: invalid direction/order/corridor/time, unknown nodes, missing/malformed/unknown fields; fixed capacity |
| AC-03 | `SearchTests` and `DomainTests`: exact vs opted-in segment matching, reverse direction, Host eligibility, own ride, time bounds, Full exclusion, deterministic order, 20-result limit and empty 200 |
| AC-04 | `BookingTests`: self-booking/inactive membership denied; one seat each; third booking Full; fourth rejected |
| AC-05 | Five independent `Two_riders_racing_for_the_third_seat` cases against SQL Server: one 201, one 409, zero seats and three bookings |
| AC-06 | `BookingTests`: identical replay before/after Full and eight concurrent identical attempts: same booking, one debit |
| AC-07 | `BookingTests`: key reused across rides, duplicate Rider/Ride, different keys raced on one ride, same key raced on different rides; rollback preserves seats |
| AC-08 | `SchemaTests`: clean migration, idempotent fixtures, new context reads persisted ride, no pending model changes; SQL CHECK constraints reject invalid state |
| AC-09 | `FoundationTests`: actual Swagger assets and OpenAPI load; exactly three paths, named/tagged operations, bearer requirements, required header and every documented response status. Chrome visual inspection confirms all operations render |
| AC-10 | Signed-JWT tests (no fake auth), SQL tests, explicit failed-INSERT rollback and sanitized Problem Details; production startup refused. `EligibilityTests` checks live permission revocation, inactive nodes, departed rides with a controlled clock, and case-sensitive/128-character keys |

The failed-insert test injects an exception into the SQL command immediately after the guarded decrement; a fresh context then observes no booking and unchanged seats. This tests transaction rollback rather than a happy-path simulation.

The test suite uses EF Core's SQL Server provider throughout. It does not use EF InMemory or SQLite to make SQL Server concurrency claims. Re-run the documented commands on a fresh checkout for current evidence.

NuGet audit (`dotnet list Lamma.sln package --vulnerable --include-transitive`) reported no vulnerable packages in either project using the configured sources. Explicit patched OpenAPI/PKCS references avoid vulnerable versions inherited by other packages.

Fresh GitHub clone verification: restored the local EF tool and NuGet dependencies, then ran all **49 tests successfully** from `.local/fresh-checkout`. The clone used a new disposable SQL database and the committed solution/migrations, without generated build artifacts. The original working tree was clean and local/remote `main` matched after publication.
