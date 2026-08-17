# Paid Runs — Scoping

Roadmap item #3. Depends on `StepsAsCurrency_Scope.md` (the `StepCurrency`
balance and `walletTransactions/{uid}` ledger) landing first — this is what
spends it. Scoping only, not implemented yet.

## Decisions made

- **One shared set of 3 tiers, usable for both creating and joining a run.**
  Not separate ladders for host vs. participant.
- **Bonus is a fixed, guaranteed reward for paying that tier — not a shared
  pot.** No pooling of entry fees, no pot-splitting logic, no risk of a pot
  running dry. Paid amounts are simply spent (a currency sink), same as a
  store purchase.
- **Pay-to-win is acceptable.** A tier's bonus is a real advantage in that
  specific race's outcome, not just a bigger payout afterward. Worth flagging
  once: this does mean placement in a paid race is no longer driven purely by
  real steps taken, which is a shift in what the leaderboard-adjacent
  placement-points system (#4) is actually rewarding once paid runs are
  mixed in with free ones. Not blocking — just worth being aware of if it
  comes up in reviews or player feedback later.

## How this attaches to the existing race system

Grounded in `DatabaseManager.cs` (`Race Management` region) and
`RaceSimulator.cs`:

- `HostRaceAsync(RaceData, capacity, description)` and
  `JoinRaceDirectlyAsync(raceId)` / the join-request flow
  (`SubmitJoinRequestAsync` → `HandleJoinRequestAsync`) are the two entry
  points that need a tier argument added.
- `StartRaceAsync` is where the bonus actually takes effect: it already
  builds a `participantInputs` list of `(userId, displayName, sex,
  speedPoints, accelPoints)` per real participant, fetched fresh from
  `SpeedPoints`/`AccelerationPoints` on `users/{uid}`, before calling
  `RaceSimulator.Generate`. **The tier bonus is added to `speedPoints`/
  `accelPoints` in that local list only** — it is never written back to the
  user's actual `SpeedPoints`/`AccelerationPoints` fields. This keeps the
  boost scoped to the one race it was paid for; it doesn't permanently
  inflate a stat used elsewhere (other races, or any future "real" stat
  display). AI filler participants (added when the race is under capacity)
  get no tier/bonus, same as today.
- `RaceSimulator`'s normalization cap is `MaxStatPoints = 50` — tier bonus
  sizes need to be picked relative to that, not in isolation (see "Still
  open" below).

## Schema additions

- `races/{raceId}/participants/{uid}/paidTier` — `0` (none, e.g. AI or a
  free/untiered race if those still exist) `| 1 | 2 | 3`. Set on the host's
  own participant entry at creation time, and on each joiner's entry when
  their payment clears.
- Tier definitions (cost + bonus) live in a static config, not per-race data
  — same shape as `MinRaceParticipants`/`StepsPerPoint` today (constants in
  code, or a small `RaceEntryTier` table), not something stored per-race.
- Reuses `walletTransactions/{uid}/{txId}` from the currency doc: every
  entry/join payment writes a `spend` entry with `relatedRaceId` set. No new
  ledger shape needed.

## Payment timing (avoiding refund cases where possible)

- **Creating a run:** charge at `HostRaceAsync` time, in the same
  multi-location `UpdateChildrenAsync` call that writes the race itself —
  race-creation and payment succeed or fail together, so there's no
  "charged but no race exists" state to begin with.
- **Joining an open race** (`JoinRaceDirectlyAsync`): same pattern — charge
  and add-participant happen in one atomic multi-location update.
- **Joining an approval-gated race:** charge **only on host approval**
  (`HandleJoinRequestAsync(approve: true)`), not at request submission. A
  pending or rejected join request never touches currency at all — this
  sidesteps an entire class of refund case by construction instead of
  handling it after the fact.

## Refund triggers (using the `walletTransactions` ledger from the currency doc)

Every trigger below writes a `refund` entry against the original `spend`
`txId` and flips that entry's `status` to `"refunded"`, per the ledger
design — so a given payment can't be refunded twice.

1. **Host cancels via the existing `CancelRaceAsync`.** Refund every
   participant (including the host) who has a settled `spend` transaction
   for that `raceId`.
2. **Race never reaches `MinRaceParticipants` (3) and is abandoned.** No
   expiry/timeout mechanism exists today for a race that just sits `open`
   indefinitely — this is new behavior needed, not something to hook into.
   Simplest option: require the host to explicitly cancel (trigger #1
   covers it) rather than building an auto-expiry job; flagging auto-expiry
   as a possible follow-up, not required for v1.
3. **A paid participant is kicked**, or leaves before the race starts
   (`StartRaceAsync` hasn't run yet) — refund that participant's entry fee.
4. **Payment write fails mid-flow** (network drop, app crash) — mitigated
   structurally by the atomic multi-location updates above rather than by a
   refund: either both sides of the update land or neither does, so this
   failure mode shouldn't produce a charged-but-nothing-happened state in
   the first place.

No refund case for "race completes and I placed badly" — paying buys entry
into the race with a stat bonus, not a placement guarantee.

## Still open

- **Concrete tier numbers** (cost in `StepCurrency`, and speed/accel bonus
  per tier) — needs picking relative to `MaxStatPoints = 50` and realistic
  earned `SpeedPoints`/`AccelerationPoints` values (`StepsPerPoint = 2000`),
  then playtesting for feel. Not guessed at here.
- **UI**: where tier selection happens in the create/join flow, and how a
  paid-tier participant is visually indicated (if at all) to others in the
  race.
- **Auto-expiry for stale open races** — noted above as a possible follow-up
  once refund trigger #2 needs to be automatic rather than host-initiated.
