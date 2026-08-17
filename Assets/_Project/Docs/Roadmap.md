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

- Pay to **create** a run.
- Pay to **join** a run.
- Three separate payable amounts, each unlocking a different bonus tier.
- Presumably paid for using the steps-currency from #2 — needs confirming.

## 4. Placement points (new leaderboard currency)

- New point type awarded based on **placement/ranking within a run**, not raw step count.
- This point type — not steps — is what the leaderboard should be ranked by.
- Implies leaderboard logic needs to shift from summing steps to summing placement points.

## 5. Refer-a-friend system

- Referring a friend rewards the referrer with points (presumably the placement points
  from #4, or possibly a separate reward — needs confirming).

## 6. Better error messaging

- General pass on error messages shown to the user throughout the app.
