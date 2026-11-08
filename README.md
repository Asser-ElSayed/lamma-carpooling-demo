# Carpooling MVP —  backend demonstration

## What has been built

Three business endpoints implement the approved [planning baseline v1.1](docs/planning/README.md): an approved Host publishes a **three-passenger-seat** ride, an active Rider searches an ordered corridor, and a Rider confirms **one seat** without overbooking. Swagger UI is the demo client.

ASP.NET Core 10 Minimal API route groups, C#, EF Core and SQL Server. One application, one database, one test project. Services use `AppDbContext` directly. The future Lamma product roadmap is outside this executable.

## Implemented features

| Feature | Implemented behavior |
|---|---|
| Ride publishing | Approved Hosts publish future rides between active ordered meeting nodes; the server assigns exactly three seats |
| Corridor search | Same corridor/direction, ordered segment containment, explicit intermediate-stop opt-in, bounded UTC window and deterministic ranking |
| Immediate booking | Active Riders confirm one seat per ride; self-booking and duplicate Rider/Ride reservations are rejected |
| Capacity protection | One SQL transaction guards the seat decrement, Full transition and booking insert; failed inserts roll back the debit |
| Retry protection | Required Rider-scoped Idempotency-Key; identical retries return the same booking without another seat debit |
| Authentication and permissions | Signed, expiring development JWTs with current SQL account/membership/Host permission checks |
| Persistent database | Committed EF Core migrations, SQL constraints/indexes and repeatable synthetic fixture seeding |
| Swagger UI | All three business operations expose editable inputs, bearer authorization, response schemas and documented error codes |
| Error handling and verification | Correlated Problem Details, safe structured logs, domain tests and real-SQL integration/concurrency tests |

## Access instructions

The demo can be reviewed without downloading the project using the [temporary live Swagger UI](https://productive-str-comment-affected.trycloudflare.com/swagger/index.html). At the top, use **Try the demo — choose an account** to select **Host** or **Rider1–4**. Swagger obtains a fresh signed JWT and authorizes the selected account automatically. Tokens renew before requests when nearing expiry, so reviewers can keep testing without requesting replacement tokens or downloading a token sheet. **Copy JWT** is available for reviewers who want the token itself. The link requires the owner's laptop, internet connection, API and tunnel to remain running; it is not a persistent hosted deployment.

For local development, clone the private repository using a GitHub account with access:

```powershell
git clone https://github.com/Asser-ElSayed/lamma-carpooling-demo.git
cd lamma-carpooling-demo
```

There are **no username/password credentials** to distribute for this synthetic demo. The Swagger account selector provides reviewer access; the three business operations still require signed JWTs and current SQL permissions. The demo-only token helper is opt-in, only runs in Development, and accepts only the seven fixed seeded account names. It is documentation tooling, excluded from the OpenAPI business-operation list.

The supplied launch profiles enable the reviewer selector. For a custom launch without a profile, set `$env:ReviewerAccess__Enabled = 'true'`. Without that explicit setting the helper and its script are unavailable. The API continues to reject non-Development startup.

### Optional offline access sheets

The account selector is the primary reviewer workflow. If an offline token sheet is useful, the **demo owner** can still generate one:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/New-ReviewerAccess.ps1 -BaseUrl https://productive-str-comment-affected.trycloudflare.com
```

Send `.local/Reviewer-Access.html` or `.local/Reviewer-Access.txt` to the reviewers with the Swagger link. The HTML sheet includes copy buttons, an idempotency-key generator, account purposes and an explicit expiry time. It includes Host, Rider1–4, UnapprovedHost and InactiveRider tokens. **Reviewer tokens default to 30 days**, so reviewers can reuse the sheet throughout the review period. Set `-ValidityDays` to a shorter duration when desired (1–30 days). The exact expiry appears in the sheet. These are synthetic demo accounts; current SQL permissions are still checked on every operation. Generated credentials are ignored by Git; the signing key is never included. Longer token validity does not keep the API online: this temporary link still requires the owner's computer and internet connection.

## Prerequisites

- .NET 10 SDK (the repository selects 10.0.202 or a later 10.0 feature band).
- SQL Server Express/Developer, or another SQL Server instance where your development account can create a database. The default is Windows integrated authentication to `.\SQLEXPRESS`.
- PowerShell for the optional token and smoke-test scripts.
- Node.js 18+ only to run the JavaScript reviewer-flow tests; reviewers do not need it.

## Build, migrate, seed and run

Run from the repository root:

```powershell
dotnet restore Lamma.sln
dotnet tool restore
dotnet build Lamma.sln --no-restore
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/Lamma.Api --no-launch-profile -- --seed
dotnet user-jwts create --project src/Lamma.Api --name 10000000-0000-0000-0000-000000000001 --audience lamma-demo --valid-for 1h --output token
dotnet run --project src/Lamma.Api --launch-profile http
```

Copy the generated Host token, keep the API terminal running, and open **[Swagger UI](http://localhost:5234/swagger/index.html)**. The [OpenAPI JSON](http://localhost:5234/openapi/v1.json) describes the same three operations. The launch profile also supports HTTPS at `https://localhost:7235` after your normal `dotnet dev-certs https --trust` setup. Local HTTP is sufficient for this synthetic demonstration.

`--seed` explicitly applies committed EF migrations and adds missing synthetic fixture rows; it does not reset rides, bookings or changed account permissions. Seeding does not run during normal startup. To apply only the schema:

```powershell
dotnet ef database update --project src/Lamma.Api -- --environment Development
```

For a different SQL instance, set the connection string in the current shell before seed/run. Use user-secrets or environment variables for credentials; never commit them:

```powershell
$env:ConnectionStrings__Lamma = 'Server=YOUR_SERVER;Database=LammaDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
```

The checked-in Express setting disables encryption only for the local synthetic demo. SQL Server tests use the actual SQL Server provider and transactions. There is no EF InMemory/SQLite substitute.

## Reviewer authorization and optional developer tokens

**Reviewers:** select an account from the green panel above the operations. Selecting **Host** authorizes publishing; selecting **Rider1–4** authorizes searching and booking. Select **UnapprovedHost** or **InactiveRider** for the documented negative tests. **Get fresh token / authorize** forces a new token whenever desired. No terminal commands or token files are required. Clearing the selection signs out.

The browser holds the token in memory and obtains another when needed before an endpoint request. Tokens expire individually, but reviewers do not need to track their expiry or contact the owner. Signing keys stay on the server.

**Developers or other API clients:** manual JWT generation remains available. Generate a one-hour token in a second terminal:

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

## Test every feature through Swagger UI

1. At the top of Swagger, select **Host** in **Demo account**. Wait for **Authorized as Host**; no token copy/paste is needed.
2. Expand **Rides → POST /rides**. The editable example contains real seeded node IDs and a future departure. Click **Execute** and expect `201`, `status: Published`, `seatCapacity: 3`, `availableSeats: 3`. Copy the returned `id` and departure time.
3. Change **Demo account** to **Rider1** and wait for the authorization confirmation.
4. Expand **Rides → GET /rides/search**. Enter pickup node 2, drop-off node 3, and a UTC window around the ride's departure, at most two hours wide. Click **Execute**. Expect `200` containing the ride and its factual match reasons. Searching as the publishing Host excludes that Host's own offer.
5. Expand **Bookings → POST /bookings**. Paste the returned ride ID into the JSON body. Enter a fresh UUID in the required **Idempotency-Key** header field; for a first attempt, `8c54bd03-cfd8-4f71-9eb4-4f225f8b249d` is an example. Click **Execute**. Expect `201`, `status: Confirmed`, `seats: 1`.
6. Execute the exact same body and key again. Expect `200` and the same booking ID. Search again to see availability reduced by one, not two.
7. Select Rider2 and Rider3 in turn and book the same ride, each with a fresh key. The third successful booking fills the ride. Select Rider4 and try: expect `409 SEATS_UNAVAILABLE`; the Full ride disappears from search.
8. To inspect validation/authorization, try publishing as UnapprovedHost (`403`), add `seatCapacity` to the publish JSON (`400`), or reuse an already committed key with another ride (`409 IDEMPOTENCY_KEY_REUSED`). Expand each operation's Responses section for its schema and documented errors.

Swagger enables editing by default. If an operation is not editable, select **Try it out**. Request examples are starting points: reuse the actual ride ID and keep publish/search timestamps consistent. API response bodies and headers are shown beneath **Server response** after execution.

## How the key features work

**Publishing:** the API derives the Host from the validated token and checks current approval in SQL. It validates future departure and the node sequence, then persists a Published ride with server-owned capacity and availability set to three.

**Matching:** SQL filters eligible future rides by corridor/direction, time, seat availability and current Host permission. Exact endpoints match directly; shorter segments require the Host's intermediate-stop opt-in. SQL ranks and limits the candidates before `RideMatcher` verifies the rules and returns factual explanations. Search does not reserve a seat.

**Booking and retries:** the API checks current Rider permission and existing idempotency/duplicate records. One conditional SQL update and booking insert share a transaction. Only a successful commit consumes a seat. The final seat atomically marks the ride Full. A retry returns the committed booking; competing or invalid attempts cannot create extra seats or bookings.

### Request examples and business rules

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

The repository contains **63 .NET test cases and seven JavaScript tests**, including the real SQL Server race/rollback checks and the self-service reviewer flow. Test source is kept so another reviewer can verify the behavior; generated results, build output, local tokens and temporary verification clones are ignored by Git. The unused template `UnitTest1.cs` has been removed.

Stop the running API with **Ctrl+C before rebuilding or running `dotnet test` on Windows**, so its executable is not locked. Restart the API afterward for the Swagger walkthrough or live smoke script.

| Test file / support | Purpose |
|---|---|
| `DomainTests.cs` | Domain invariants and corridor/stop rules |
| `PublishingTests.cs` | Publish contracts, current Host approval and signed-token failures |
| `SearchTests.cs` | SQL matching, ordering, window bounds and result limits |
| `BookingTests.cs` | Capacity, duplicates, retries, concurrent final-seat races and rollback |
| `EligibilityTests.cs` | Current permission revocation, inactive nodes, departed rides and key boundaries |
| `SchemaTests.cs` | Clean migrations, persistence, fixtures and SQL constraints |
| `FoundationTests.cs` | Swagger/OpenAPI contracts and Development-only startup |
| `ReviewerAccessTests.cs` | Opt-in reviewer helper, allowlisted identities, signed token issuance, fresh tokens and unchanged SQL permission checks |
| `tests/swagger-reviewer-access.test.cjs` | Browser account switching, automatic renewal, concurrent renewal, sign-out, errors and same-origin credential handling |
| `DemoFactory.cs`, `SqlFixture.cs` | Shared signed-JWT API host and isolated disposable SQL Server database needed by the integration tests |

```powershell
dotnet test Lamma.sln --logger 'console;verbosity=normal'
node --test tests/swagger-reviewer-access.test.cjs
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

## Troubleshooting

| Symptom | Action |
|---|---|
| Swagger cannot open | Keep `dotnet run` running; check the listening URL in its output; use `/swagger/index.html` on that same computer |
| `401 UNAUTHENTICATED` | Choose an account in the reviewer panel or use Get fresh token / authorize. For manual clients, generate a fresh token for this project |
| Reviewer panel is absent | Owner: use a supplied launch profile or set `ReviewerAccess__Enabled=true`, restart the API, and refresh Swagger |
| Reviewer panel reports setup unavailable | Owner: run the seed command and configure a developer signing key with the documented `dotnet user-jwts` command, then restart |
| `403 HOST_NOT_APPROVED` | Publishing needs the approved Host fixture, not a Rider or UnapprovedHost token |
| `400 VALIDATION_FAILED` | Check the required fields, seeded node IDs, future departure and two-hour maximum search window; remove extra seat/user fields |
| `200 []` search | Use the Rider token, matching date/time and ordered nodes; Full, departed and own offers are excluded |
| `409` booking | Read the stable `code`: full ride, duplicate Rider/Ride, or a key already committed for another payload |
| SQL connection/table error | Start SQL Server, configure `ConnectionStrings__Lamma` if needed, and run the explicit `--seed` migration/fixture command |
| Build reports that `Lamma.Api.exe` is locked | Stop the API with Ctrl+C, run the build/tests, then start it again |

## Structure and decisions

`Features/Users`, `Features/Rides` (including Geography/Matching), `Features/Bookings`, `Data`, `Common/ErrorHandling`. Endpoints translate HTTP, focused services coordinate use cases, domain methods preserve invariants, and SQL constraints preserve integrity. See [ADRs](docs/decisions/ADR-001-feature-first-monolith.md).

No CQRS, MediatR, generic repositories, microservices, mobile screens, cancellation, Host acceptance, live routes, payments, notifications, university verification, real identity collection or AI infrastructure is included. Those remain in the planning document's separate product roadmap.
