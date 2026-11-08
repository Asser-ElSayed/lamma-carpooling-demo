# ADR-002 — Curated ordered-node matching

Accepted, 8 November 2026; planning decision D-02.

Nodes belong to one corridor/direction/order. Requests require active pickup before drop-off. Offers must contain that segment and permit intermediate stops, or match exact endpoints. Future departure, seat availability and current Host membership/approval are required; own offers are excluded.

Implementation defaults: maximum two-hour UTC window, 20 results; closest to window midpoint at millisecond resolution, then departure UTC, then SQL Server GUID ordering. SQL applies all candidate filters, ordering and TOP before materialization. `RideMatcher` verifies policy and supplies factual reason codes. No arbitrary candidate cutoff can hide a better match.

The catalogue index covers corridor/direction/order; ride indexes cover origin/status/time and Host/status/time. Data is synthetic; this is not a detour estimator or road routing system. A seat is occupied for the entire ride.
