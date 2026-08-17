# Roadmap / Open Work

Running list of things that still need to be done. Not scoped or prioritized yet — capture first, plan later.

## 1. Finish step counter migration — iOS

Android (Health Connect) is done. iOS (HealthKit) is code-complete but unverified —
see `StepCounter_HealthKit_Implementation.md` §"What's left":

- No Mac/Xcode has compiled or run any of it yet.
- No iOS Build Profile or bundle identifier exists in the project yet (Android-only so far).
- Needs: compile check in Unity → set up iOS Build Profile → build to generate Xcode
  project → confirm HealthKit capability/entitlement, framework link, and `Info.plist`
  usage string in Xcode → run on a real device (or simulator with seeded Health data) →
  verify auth sheet, `GetStepsSinceAsync`, `GetDailyStepsAsync`, and "Open Settings" all work.
- Device QA matrix and App/Play Store review prep still open (see
  `StepCounter_HealthPlatform_Migration_Scope.md` phasing — release is held until both
  platforms ship together).

## 2. Steps as currency

Idea: let accumulated steps be spent like a currency in-app.

- **Scoped** — see `StepsAsCurrency_Scope.md`. Design: a new `StepCurrency` balance
  separate from the lifetime `StepCount` total (leaderboard/race-stat meaning of
  `StepCount` stays untouched), backed by a `walletTransactions/{uid}` ledger for
  refunds. Written client-side, same trust model `StepCount` already runs under —
  no Cloud Functions layer. Known risk: a modified client could credit itself
  currency, same as it already could inflate `StepCount` today; accepted
  deliberately rather than building server-side validation infrastructure.
- Conversion 1:1, continuous accrual, no balance cap. Refunds/failure handling are
  confirmed as required but their exact mechanics wait on #3 (paid runs) being
  scoped — the balance model is designed with a transaction ledger from the start
  so refunds are possible later.
- Depends on this landing before the paid-run system below, since that system is
  described as spending steps.

## 3. Paid runs

- **Scoped** — see `PaidRuns_Scope.md`. Paid with `StepCurrency` from #2. One shared
  set of 3 tiers usable for both creating and joining a run; each tier is a fixed,
  guaranteed bonus (not a shared pot). Pay-to-win confirmed acceptable: the bonus is a
  real `SpeedPoints`/`AccelerationPoints` boost applied only within that race's
  simulation (never written back to the account's real stats).
- Refunds ride the `walletTransactions` ledger from #2: triggered by race cancellation,
  a paid participant being kicked/leaving pre-start, or a race never reaching minimum
  participants (the last one needs new auto-expiry logic — doesn't exist today).
- Still open: concrete tier costs/bonus sizes (needs balancing against
  `RaceSimulator`'s stat normalization and playtesting) and tier-selection UI.

## 4. Placement points (new leaderboard currency)

- **Scoped** — see `PlacementPoints_Scope.md`. New `users/{uid}/PlacementPoints`
  (lifetime, no seasonal reset — matches every other stat in this codebase),
  awarded in `MarkRaceWatchedAsync`'s existing "all watched → completed" hook, the
  one place a race is unambiguously finished. Ranked across all participants
  including AI filler (simplest; AI use the weakest real stats in the race so they
  rarely skew it). `HighScoreScreen` switches its sort key from `StepCount` to this.
- Interacts with #3: the pay-to-win stat bonus affects `FinishTime`, so paid runs
  can indirectly buy leaderboard position too — an accepted consequence of that
  earlier decision, not a new one.
- Still open: the actual points-per-rank table/formula, whether last place gets a
  consolation amount or zero, and how an award is shown to the player post-race.

## 5. Refer-a-friend system

- **Scoped** — see `ReferAFriend_Scope.md`. Reuses the existing `FriendCode` as the
  referral code (no new code system needed); a new "referral code" field at signup
  sets `users/{uid}/ReferredBy` once. Both sides get rewarded in `StepCurrency`,
  gated on the new account's first real step sync (not instant at signup) to raise
  the bar against disposable-email farming, since there's no other anti-multi-account
  check today.
- Key finding: since there's no Cloud Functions layer, a referred account's client
  literally cannot write to the referrer's node under current Firebase rules. Solved
  by reusing the `friendRequests`-style pattern — a new `referralRewards/{referrerUid}/{newUid}`
  node the new user writes a pending claim into, which the referrer's own client reads
  and claims on its next sync.
- Still open: reward amounts, and whether a valid referral should auto-add the friend
  relationship as a bonus.

## 6. Better error messaging

- General pass on error messages shown to the user throughout the app.
