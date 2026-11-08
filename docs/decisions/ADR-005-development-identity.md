# ADR-005 — Signed development-only synthetic identity

Accepted, 8 November 2026; planning decision D-03.

Use synthetic seeded accounts and official `dotnet user-jwts` one-hour tokens. JWT bearer authentication validates signature, issuer, audience, expiry and a GUID `sub`. Signing keys live in user-secrets, outside Git. Swagger uses HTTP bearer authorization. Arbitrary identity headers are not accepted.

Reviewer-access revision, 8 November 2026: at the user's explicit request to remove hourly reviewer token renewal, `New-ReviewerAccess.ps1` defaults to 30-day signed tokens for the existing synthetic accounts. A bounded `-ValidityDays` parameter supports 1–30 days. Ordinary `Get-DemoToken.ps1` developer tokens remain one hour. This changes only the review credential window; it does not grant permanent credentials, change account permissions, or provide independent server hosting.

Self-service revision, 8 November 2026: the user explicitly requested that JWTs always be available to reviewers on the live Swagger page. An opt-in account selector now requests fresh one-hour signed JWTs from `POST /swagger/reviewer-token` and applies them through Swagger's bearer authorization. A same-origin request interceptor renews the selected account's token before requests when expiry approaches. The helper accepts only Host, Rider1–4, UnapprovedHost and InactiveRider, and is anonymous intentionally so visitors to the synthetic demo can try those accounts. It never accepts arbitrary user IDs or returns signing keys. Responses use `Cache-Control: no-store`; browser credentials remain in memory. The helper and its JavaScript are unavailable unless `ReviewerAccess:Enabled` is true in Development, and are excluded from the three-operation OpenAPI document. This is demo documentation tooling, not production login or user onboarding. This revision supersedes the earlier requirement to distribute token sheets for normal reviewer use.

SQL is authoritative for current account, membership and Host approval on every operation; token roles are insufficient. The application refuses to start outside Development, ensuring this demo configuration cannot silently become a live authentication system. Normal startup does not seed or migrate; an explicit development `--seed` utility performs those tasks.

Only synthetic meeting-point labels and account records are seeded. No real identity evidence is stored. Log safe classifications and correlation, not token/request bodies or exception internals.
