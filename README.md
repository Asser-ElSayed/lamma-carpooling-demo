# Lamma — Credosis backend demonstration

Three business endpoints implement the approved [planning baseline v1.1](Lamma_Backend_Planning_Document_v1.1.md): an approved Host publishes a **three-passenger-seat** ride, an active Rider searches an ordered corridor, and a Rider confirms **one seat** without overbooking. Swagger UI is the demo client.

ASP.NET Core 10 Minimal API route groups, C#, EF Core and SQL Server. One application, one database, one test project. Services use `AppDbContext` directly. The future Lamma product roadmap is outside this executable.

## Prerequisites

- .NET 10 SDK (the repository selects 10.0.202 or a later 10.0 feature band).
- SQL Server Express/Developer, or another SQL Server instance where your development account can create a database. The default is Windows integrated authentication to `.\SQLEXPRESS`.
- PowerShell for the optional token and smoke-test scripts.

## Build, migrate, seed and run

Run from the repository root:

```powershell
dotnet restore Lamma.sln
dotnet tool restore
dotnet build Lamma.sln --no-restore
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/Lamma.Api --no-launch-profile -- --seed
dotnet run --project src/Lamma.Api --launch-profile http
```

Open **http://localhost:5234/swagger/index.html**. The JSON specification is **http://localhost:5234/openapi/v1.json**. The launch profile also supports HTTPS at `https://localhost:7235` after your normal `dotnet dev-certs https --trust` setup. Local HTTP is sufficient for this synthetic demonstration.

`--seed` explicitly applies committed EF migrations and adds missing synthetic fixture rows; it does not reset rides, bookings or changed account permissions. Seeding does not run during normal startup. To apply only the schema:

```powershell
dotnet ef database update --project src/Lamma.Api -- --environment Development
```

For a different SQL instance, set the connection string in the current shell before seed/run. Use user-secrets or environment variables for credentials; never commit them:

```powershell
$env:ConnectionStrings__Lamma = 'Server=YOUR_SERVER;Database=LammaDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
```

The checked-in Express setting disables encryption only for the local synthetic demo. SQL Server tests use the actual SQL Server provider and transactions. There is no EF InMemory/SQLite substitute.

## Development tokens and Swagger Authorize

Generate a one-hour token in a second terminal:

```powershell
dotnet user-jwts create --project src/Lamma.Api --name 10000000-0000-0000-0000-000000000001 --audience lamma-demo --valid-for 1h --output token
```

The official tool stores the signing key outside the repository in the user-secrets store. If the API was already running when the first signing key was generated, restart it. Click **Authorize** in Swagger and paste the token **without** the `Bearer` prefix. Replace the authorization token when switching accounts; click Logout first if Swagger already shows an authorized entry.

Convenience wrapper (process-scoped execution policy, without changing machine policy):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Get-DemoToken.ps1 -Account Host
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Get-DemoToken.ps1 -Account Rider1
```

The application refuses startup outside `Development`; the seeded identity strategy is deliberately development-only. It validates signature, issuer, audience, lifetime and a GUID subject. Every operation rechecks account/capability state in SQL Server. An arbitrary role or user-ID header does not grant permission.

| Account | Synthetic user ID | Fixture permission |
|---|---|---|
| Host | `10000000-0000-0000-0000-000000000001` | Active member, approved Host |
| UnapprovedHost | `10000000-0000-0000-0000-000000000002` | Active member, unapproved Host |
| Rider1 | `10000000-0000-0000-0000-000000000011` | Active member |
| Rider2 | `10000000-0000-0000-0000-000000000012` | Active member |
| Rider3 | `10000000-0000-0000-0000-000000000013` | Active member |
| Rider4 | `10000000-0000-0000-0000-000000000014` | Active member |
| InactiveRider | `10000000-0000-0000-0000-000000000015` | Inactive account |

Every active member may ride, including an approved Host, but nobody can book their own ride.

## Curated meeting nodes

These are synthetic public meeting-point labels, not real route measurements. Node order describes one demo corridor, not road routing.

| Node ID suffix | Label | Corridor / direction / order |
|---|---|---|
| `...000001` | Community gate | Demo-corridor / University-bound / 1 |
| `...000002` | Library meeting point | Demo-corridor / University-bound / 2 |
| `...000003` | Station meeting point | Demo-corridor / University-bound / 3 |
| `...000004` | University gate | Demo-corridor / University-bound / 4 |
| `...000005`–`...000008` | Reverse-direction catalogue | Demo-corridor / Community-bound / 1–4 |
| `...000009` | Other corridor gate | Other-corridor / University-bound / 1 |

Each full node ID starts `20000000-0000-0000-0000-` and ends in the twelve-digit suffix, e.g. `20000000-0000-0000-0000-000000000001`.

## The three endpoints

| Operation | Identity / input | Successful result |
|---|---|---|
| `POST /rides` | Approved Host; origin/destination nodes, future departure, stop opt-in | `201`, Published, capacity/availability 3 |
| `GET /rides/search` | Active member; pickup/dropoff nodes, `fromUtc`, `toUtc` | `200`, up to 20 matches or `[]` |
| `POST /bookings` | Active member; `rideId` and required `Idempotency-Key` header | `201` Confirmed; identical replay `200` |

Publish example — **replace the timestamp with a future UTC time**:

```json
{
  "originNodeId": "20000000-0000-0000-0000-000000000001",
  "destinationNodeId": "20000000-0000-0000-0000-000000000004",
  "departureAtUtc": "2030-01-01T12:00:00Z",
  "allowsIntermediateStops": true
}
```

Search the same date/time with `pickupNodeId=20000000-0000-0000-0000-000000000002`, `dropoffNodeId=20000000-0000-0000-0000-000000000003`, `fromUtc=2030-01-01T11:30:00Z`, `toUtc=2030-01-01T12:30:00Z`. The maximum window is two hours. Offers must contain the requested ordered segment, use the same corridor/direction, and allow intermediate stops; otherwise only exact endpoints match. Ranking uses distance from the window midpoint at millisecond resolution, then departure UTC and SQL Server Ride ID order. Future departure, available seats and current Host permissions are hard filters. The Rider's own offers are excluded.

Booking body (copy a real ride ID from publish/search):

```json
{ "rideId": "REPLACE_WITH_RETURNED_RIDE_ID" }
```

Supply a fresh UUID string in `Idempotency-Key` for a new logical attempt. Keys are Rider-scoped, case-sensitive, 1–128 characters, with no surrounding whitespace. Reuse the same key for a network retry. A retry returns the original booking, including after the final seat has been taken. A different ride under the same key is `409 IDEMPOTENCY_KEY_REUSED`; a different key for the same Rider/Ride is `409 DUPLICATE_BOOKING`.

Ride capacity, booking quantity and user identities cannot be supplied in JSON. Unknown JSON fields and undocumented/repeated search parameters are rejected. No resource lookup endpoints are implemented, so `201` responses do not invent a `Location` URL.

## Transaction and errors

One short SQL transaction performs the conditional seat decrement, atomic `Full` transition at zero, and uniquely keyed booking INSERT. A zero-row update creates no booking. Unique-key collisions roll back the debit and resolve the committed winner. Other failures also roll back. Each passenger occupies one seat for the whole journey; seats are not reused by route segment.

Failures use `application/problem+json` with `type`, `title`, `status`, safe `detail`, `code` and `traceId`. Swagger documents the applicable `400`, `401`, `403`, `404`, `409`, `500` responses. Logs carry method/path/outcome/correlation; token contents, raw request bodies and exception/SQL details are omitted.

## Tests and live smoke check

```powershell
dotnet test Lamma.sln --logger 'console;verbosity=normal'
```

The integration fixture creates and drops only its own `LammaTests_<random GUID>` SQL database. The configured user therefore needs create/drop-database permission. It does not delete the demo database. To point tests at another SQL Server, set the **server connection** (its database name is replaced by the fixture):

```powershell
$env:LAMMA_TEST_SQL = 'Server=YOUR_SERVER;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test Lamma.sln
```

Run only last-seat evidence:

```powershell
dotnet test Lamma.sln --filter FullyQualifiedName~Two_riders_racing_for_the_third_seat --logger 'console;verbosity=normal'
```

With the demo API running:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Smoke-Test.ps1
```

The smoke script uses official developer JWTs, checks Swagger/OpenAPI, publishes, searches, books, retries, fills the ride and verifies a fourth Rider's `409`. It adds a synthetic ride/bookings to the demo database. [Validation evidence](docs/validation.md) maps tests to AC-01–AC-10. [Reviewer walkthrough](docs/demo-walkthrough.md) provides a 3–5 minute sequence.

## Structure and decisions

`Features/Users`, `Features/Rides` (including Geography/Matching), `Features/Bookings`, `Data`, `Common/ErrorHandling`. Endpoints translate HTTP, focused services coordinate use cases, domain methods preserve invariants, and SQL constraints preserve integrity. See [ADRs](docs/decisions/ADR-001-feature-first-monolith.md).

No CQRS, MediatR, generic repositories, microservices, mobile screens, cancellation, Host acceptance, live routes, payments, notifications, university verification, real identity collection or AI infrastructure is included. Those remain in the planning document's separate product roadmap.
