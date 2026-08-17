# Steps as Currency — Scoping

Roadmap item #2. This covers what exists today, the shape of the proposed
design, and what has to be true before it can ship safely. Not implemented
yet — scoping only.

## Why

Roadmap items #3 (paid runs) and #4 (placement points) both assume there's
something spendable. Steps are the obvious candidate — the app already
tracks them continuously — but "spendable" changes their risk profile, so
this needs to be scoped deliberately rather than bolted on.

## Current state (as implemented today)

- `users/{uid}/StepCount` (`DatabaseManager.cs`, schema in
  `DataCollections.cs`) is a lifetime, never-reset total. It feeds the
  `leaderboard/{uid}` mirror directly and, via `StepsPerPoint = 2000`,
  `SpeedPoints`/`AccelerationPoints` — the stats `RaceSimulator` uses to
  place a player in a race. `dailySteps/{date}` are a separate resettable
  view for the activity graph only.
- Sync path: `SyncFromProviderAsync` reads new steps from the platform
  provider (Health Connect/HealthKit), anchored per-device
  (`deviceSync/{deviceId}`), capped at 30 days of backlog
  (`DeviceSyncMaxBacklogMillis`), clamped to 20,000 steps/sync
  (`MaxPlausibleStepsPerSync`) — then `WriteStepsToFirebaseAsync` adds the
  delta to `StepCount` and updates the leaderboard mirror. All of this is
  **client-side logic run on the player's own device.**
- No currency/wallet/points/purchase system exists anywhere in the project
  today (no `com.unity.purchasing`, no IAP, no `Wallet`/`Currency` classes).
  This is a from-scratch, purely virtual currency — no real-money payment
  plumbing involved at any point.
- **Security gap (see `RealtimeDatabase.rules.json`), accepted as-is:** the
  `users/{uid}` write rule only requires `auth.uid == $uid` plus a shape
  check (`UserID`/`UserLevel` present and consistent) — nothing constrains
  what value `StepCount` (or any other numeric field) is written as. The
  30-day cap and 20k/sync clamp mentioned above are enforced by the Unity
  client code, not by the database — a modified client, or a direct
  authenticated write to the REST/SDK endpoint, can set `StepCount` (or
  `StepCurrency`) to anything. **Decision: no Cloud Functions layer for this.**
  `StepCurrency` and `walletTransactions` will be written client-side, same
  trust model `StepCount` already runs under today — this is a known,
  deliberately accepted risk, not an oversight. Revisit only if abuse is
  actually observed in practice.

## Decisions made

- **Balance is a separate field from `StepCount`.** Proposed:
  `users/{uid}/StepCurrency` (name TBD), accrued alongside `StepCount` in
  the same sync write rather than derived from it later. Spending it does
  **not** decrement `StepCount`, and does not touch `SpeedPoints`/
  `AccelerationPoints` — those keep their current meaning untouched. Steps
  effectively mint currency as they're synced; the lifetime total keeps
  meaning "total steps ever taken," independent of how much currency has
  since been spent.
- **No Cloud Functions layer — client writes the wallet directly, same as
  `StepCount` today.** Considered a server-side (Cloud Functions) validation
  layer for `StepCurrency`/`walletTransactions` writes; decided against it.
  It would have been a real new piece of infrastructure (no Cloud Functions
  exist anywhere in this project today — no `functions/` directory, no
  Functions module in the Firebase Unity SDK, and Cloud Functions require
  upgrading the Firebase project to the paid Blaze plan). The resulting
  exploit surface (a modified client crediting itself currency) is accepted
  as a known risk rather than engineered away right now.

## Open questions — resolved

- **Conversion rate: 1 step = 1 currency unit.** No scaling.
- **Accrual timing: continuous.** `StepCurrency` accrues in the same write as
  `StepCount`, on the same sync cadence (every 60s per `FirebaseSyncMs`,
  directly from `WriteStepsToFirebaseAsync`) — no separate "collect" action.
- **No cap on balance.** Grows unbounded, same as `StepCount` does today.
- **Refunds/failure handling: required.** Confirmed needed — exact mechanics
  (e.g. a paid run that never fills and never starts) are part of the #3
  paid-runs design, since a refund is really "reverse a specific spend
  transaction," which only makes sense once that spend exists. Noted here so
  the balance model is built to support it from day one:
  - Every debit against `StepCurrency` needs to be a discrete, identifiable
    transaction (e.g. `walletTransactions/{uid}/{txId}` with `type`,
    `amount`, `relatedRaceId`, `status`), not just a bare balance
    decrement — a refund needs something to point back at and reverse.
  - Refunds are written client-side too, same as everything else in this
    doc — no server authority to route them through, per the decision above.
  - Failure modes to cover once #3 is scoped: run cancelled before start,
    run fails to reach minimum participants, payment succeeds but
    create/join itself fails (network/crash mid-flow), and double-spend
    protection (the same debit can't be refunded twice).

## Suggested phasing

1. Add the `StepCurrency` field, accrual, and a `walletTransactions/{uid}`
   ledger (earns included, not just future spends/refunds) to
   `WriteStepsToFirebaseAsync`/`DataCollections.cs`, written directly by the
   client.
2. Update `RealtimeDatabase.rules.json` with a `.validate` rule for
   `StepCurrency`/`walletTransactions` shape (still no server-side amount
   authority, just structural validation — e.g. required fields present,
   `type` is one of the known values).
3. Wire a balance display into the UI (profile screen is the natural home,
   next to the existing step total).
4. Only then build spending (#3 paid runs) against the ledger, with refunds
   designed in from the start rather than retrofitted.
