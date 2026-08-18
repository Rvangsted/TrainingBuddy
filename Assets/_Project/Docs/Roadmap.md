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
  `RaceSimulator`'s stat normalization and playtesting).
- **Tier-selection UI now scoped** — see `PaidRunsUI_Scope.md`. Turned out
  to be the biggest gap of anything on the roadmap: zero UI anywhere lets a
  player actually reach a paid tier today (every real race is created/joined
  with `tier: None`, hardcoded). Not implemented yet.

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
- **Implemented** — points-per-rank table, last-place handling, and post-race UI
  are resolved and built; see `PlacementPoints_Scope.md` "Resolved". Compiles
  clean; not yet playtested end-to-end on a real device.

## 5. Refer-a-friend system

- **Implemented** — see `ReferAFriend_Scope.md`. Reuses the existing `FriendCode` as the
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
- Reward amounts (referrer 1000 / new user 500 StepCurrency) and friend auto-add
  are resolved and built — see `ReferAFriend_Scope.md` "Resolved". Not yet
  playtested end-to-end on a real device.

## 6. Better error messaging

- **Implemented** — see `ErrorMessaging_Scope.md`. Fixing gaps in an existing mechanism,
  not building a new one: `UniversalOverlay`/`ShowMessage` already exists but is
  reused ad hoc, several user-initiated actions (join/leave race, login) currently
  fail completely silently, some places show raw `ex.Message` verbatim (works today
  by accident, not by design), and copy is a genuine Danish/English mix in a couple
  of spots (`DeleteAccountAsync`).
- Scope: user-initiated actions first (join/leave race, register, login, cancel,
  kick, delete account) — passive background fetches (leaderboard, friends list)
  stay logging-only for now, deferred as a separate lower-priority pass.
- Design: one shared helper (`DatabaseManager.ShowError`) that only ever shows a
  pre-written user-safe message or one shared generic fallback — never
  raw/technical/English exception text again. Turned out ~40 of the app's own
  exception messages were English, not Danish as assumed — all translated and
  centralized in `UserMessages.cs`. See `ErrorMessaging_Scope.md` "Resolved".
  Not yet playtested end-to-end on a real device.

## 7. Design/UI follow-ups on #3–#6

Found by auditing what's actually reachable in the app vs. what only exists
in the backend, after #2–#6 above landed. Scoped, not implemented, in
priority order:

1. **Paid-runs tier UI** (`PaidRunsUI_Scope.md`) — **implemented, wired, and
   compiles**: new Create Race screen (tier picker + race-name input) and a
   join-confirm modal on `FindLobbyScreen`. Not yet playtested end-to-end on
   a real device. The pre-existing, separate join-request/host-approval flow
   gap (`SubmitJoinRequestAsync` etc. have no UI at all) remains explicitly
   deferred, not bundled in.
2. ~~Paid-participant badge~~ — **explicitly deferred, not wanted for now**
   (pay-to-win itself is still fine; just not the visual "who paid"
   indicator). See `PaidRunsUI_Scope.md`. Revisit only if asked.
3. **Notification toast component** (`NotificationToast_Scope.md`) —
   **implemented**: new `ToastNotification` control + `UIManager.ShowToast`,
   wired to both refer-a-friend reward moments via new `DatabaseManager`
   events. One manual Unity Editor step still needed (wiring
   `ToastNotification.uxml` into `UIManager`'s new "Toast Asset" Inspector
   field — see the doc). Not yet compiled/playtested.
4. **Refer-a-friend UI** (`ReferAFriend_Scope.md` "UI" section) —
   **implemented**: invite-promotion section on `ProfileScreen` (code +
   copy button, confirms via a toast), reward-confirmation toast hookup, and
   `RegisterScreen` field polish to match the existing section/label
   pattern. Not yet compiled/playtested.
5. **Placement-points visual polish** (`PlacementPoints_Scope.md` "UI"
   section) — distinct styling for the post-race "+N placeringspoint" line
   and the `ProfileScreen` label, currently both visually generic.
6. **Error popup styling** (`ErrorMessaging_Scope.md` "UI" section) — a
   color/accent variant on `UniversalOverlay` for errors vs. success/info;
   lowest priority, purely cosmetic since the functional fix already shipped.
