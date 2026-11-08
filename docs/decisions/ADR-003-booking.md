# ADR-003 — Immediate one-seat confirmation

Accepted, 8 November 2026; planning decisions D-01, D-05, D-07.

Every ride starts Published with exactly three passenger seats, excluding the Host. A successful booking is immediately Confirmed and occupies one seat. At zero available seats the ride is Full. Self-booking, inactive Riders, revoked Hosts and departed/inactive offers are rejected.

There are no cancellation, acceptance, completion, expiry or rebooking transitions. SQL CHECK constraints enforce capacity, availability, one-seat bookings and allowed persisted states. No seat quantity or user identity is accepted from the client.
