# Reviewer walkthrough — 3–5 minutes

Recording status: the walkthrough script is ready. A video file has not been produced: Windows Snipping Tool returned to its launcher instead of exposing usable recording-selection controls on both attempts in this session. The API, automated SQL Server evidence and live Swagger requests are verified separately.

Prepare: migrate/seed, run the API, generate Host and Rider1–4 one-hour tokens. Run the full SQL Server test suite and keep the final-seat test output ready. Use an uncluttered browser tab at http://localhost:5234/swagger/index.html. Do not show token-generation output/signing keys in a published recording.

| Time | Action | Evidence to explain |
|---|---|---|
| 0:00–0:30 | Show the Rides/Bookings tags and three operations; open Authorize | Minimal API, signed synthetic accounts, no extra business APIs |
| 0:30–1:10 | Authorize as Host; execute POST /rides with nodes 1→4, future UTC departure, stop opt-in | 201 Published, capacity 3, availability 3; copy the returned ID |
| 1:10–1:50 | Switch authorization to Rider1; search nodes 2→3 around that departure, window ≤2 hours | 200, created ride, factual corridor/time/stop reasons; snapshot availability |
| 1:50–2:30 | POST /bookings with the ride ID and a fresh UUID Idempotency-Key | 201 Confirmed, exactly one seat; no user/seat fields |
| 2:30–2:55 | Execute the exact same request/key again | 200 with the same booking ID; no second debit |
| 2:55–3:35 | Rider2 and Rider3 book with their own keys; Rider4 tries | Third seat fills the ride; fourth Rider receives 409 SEATS_UNAVAILABLE |
| 3:35–4:00 | Show an unapproved Host's POST /rides or attempted capacity field | 403 HOST_NOT_APPROVED or 400 VALIDATION_FAILED, Problem Details with traceId |
| 4:00–4:30 | Show the automated final-seat race test and passing suite | Two independent Riders compete after two bookings: one 201, one 409, exactly three persisted bookings. Swagger alone does not prove concurrency |

Use the example bodies and full seed IDs in README. Replace the example departure timestamp if it is no longer in the future. When switching Swagger tokens, open Authorize, Logout the old token, paste the new JWT without `Bearer`, Authorize, Close.

Explain the boundary: node/corridor matching, immediate confirmation and development JWTs are implemented. Live detours, university verification, Host acceptance, cancellation, payments, notifications and AI are deferred. A working Swagger assessment is not a real-user pilot readiness claim.
