# Paid Runs — Scoping

Roadmap item #3. Depends on `StepsAsCurrency_Scope.md` (the `StepCurrency`
balance and `walletTransactions/{uid}` ledger) landing first — this is what
spends it. **Implemented** (backend/schema; tier-selection UI still not
built — see "Still open"). This doc has been updated to match what actually
shipped, including two corrections made mid-implementation (see "Payment
timing" and "Refund triggers" below) that the original plan didn't
anticipate.

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
  (`SubmitJoinRequestAsync` → `HandleJoinRequestAsync` →
  `FinalizeApprovedJoinAsync`, the last one added during implementation —
  see "Payment timing") are the entry points that take a tier argument.
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

- `races/{raceId}/participants/{uid}/paidTier` — `0` (none, e.g. AI, or a
  free/untiered race) `| 1 | 2 | 3` (`RaceEntryTier` enum: `Basic`/`Plus`/
  `Elite`). Set on the host's own participant entry at creation time, and
  on each joiner's entry when their payment clears.
- `races/{raceId}/participants/{uid}/spendTxId` — the specific
  `walletTransactions/{uid}/{txId}` this participant's entry fee was
  charged as. Added during implementation (not anticipated in the original
  plan): a refund needs to flip *that exact* transaction's status to avoid
  double-refunding, and there's no other index from "this participant in
  this race" back to "which ledger entry paid for it" without one.
- `RaceEntryTiers`: a static `Dictionary<RaceEntryTier, (int Cost, int
  StatBonus)>` in `DatabaseManager.cs` — same shape as
  `MinRaceParticipants`/`StepsPerPoint`. Current placeholder values: Basic
  1000/+5, Plus 3000/+12, Elite 7000/+25 (cost in `StepCurrency`, bonus
  added to both `SpeedPoints` and `AccelerationPoints` for that race's
  simulation only) — picked relative to `MaxStatPoints = 50`, not
  playtested, trivially tunable.
- Reuses `walletTransactions/{uid}/{txId}` from the currency doc: every
  entry/join payment writes a `spend` entry with `relatedRaceId` set.
- New top-level `pendingRefunds/{uid}/{raceId}` node (`amount`,
  `spendTxId`, `createdAt`) — see "Refund triggers" below for why this
  exists; not in the original plan.

## Payment timing (avoiding refund cases where possible)

- **Creating a run:** charge at `HostRaceAsync` time, in the same
  multi-location `UpdateChildrenAsync` call that writes the race itself —
  race-creation and payment succeed or fail together, so there's no
  "charged but no race exists" state to begin with.
- **Joining an open race** (`JoinRaceDirectlyAsync`): same pattern — charge
  and add-participant happen in one atomic multi-location update.
- **Joining an approval-gated race — corrected during implementation.**
  The original plan ("charge on `HandleJoinRequestAsync(approve: true)`")
  turned out to be impossible as written: the host calling that method is a
  *different* user than the requester being charged, and Firebase rules
  only let a user write their own `users/{uid}/StepCurrency` (the same
  no-Cloud-Functions constraint `StepsAsCurrency_Scope.md` and
  `ReferAFriend_Scope.md` both hit). Actual design: `HandleJoinRequestAsync`
  now only flips the join request's status to `"approved"`/`"rejected"` —
  it no longer creates the participant entry either. The requester's own
  client calls a new `FinalizeApprovedJoinAsync(raceId)` once it observes
  approval; that method creates the participant entry *and* charges the
  tier in one self-authored atomic update (allowed under the existing
  "create a fresh participant entry while the race is open" rule branch —
  the same one `JoinRaceDirectlyAsync` already relies on). A pending or
  rejected request still never touches currency. Net effect: approval is a
  two-step handshake (host approves → requester finalizes) for *every*
  join-request-based join now, paid or free — simpler than having two
  different shapes depending on whether a tier was requested, and this path
  has no UI callers yet so there's no existing behavior to preserve.

## Refund triggers (using the `walletTransactions` ledger from the currency doc)

- **Leaving before the race starts (`LeaveRaceAsync`):** self-authored —
  the leaving participant *is* the acting user, so this credits
  `StepCurrency` directly, writes a `refund` entry, and flips the original
  `spend` entry's `status` to `"refunded"` (so it can't be refunded twice)
  all in one update, exactly as originally planned.
- **Kicked, or the host cancels via `CancelRaceAsync` — corrected during
  implementation.** Same cross-user-write problem as the approval case
  above: the host kicking/cancelling isn't the participant being refunded,
  so a direct credit to that participant's `StepCurrency` is not possible.
  Fix: reuses the same pull pattern `ReferAFriend_Scope.md` established —
  the host writes a claim to a new `pendingRefunds/{uid}/{raceId}` node
  (write allowed there for that race's host, mirroring the existing
  `joinRequests/{raceId}/{requesterId}` rule shape), and the affected
  user's own client redeems it via a new `ClaimPendingRefundsAsync()` —
  called on login/app-resume — which credits `StepCurrency`, writes the
  `refund` entry, flips the original `spend` entry, and clears the claim.
  One exception: if the acting user *is* one of the affected participants
  (the common case — a host cancelling their own paid race), that one
  refund is credited directly and instantly instead of going through the
  claim/pull step, since no cross-user write is needed there.
- **Race never reaches `MinRaceParticipants` (3) and is abandoned.** No
  expiry/timeout mechanism exists today for a race that just sits `open`
  indefinitely — this is new behavior needed, not something to hook into.
  Simplest option: require the host to explicitly cancel (the trigger above
  covers it) rather than building an auto-expiry job; flagging auto-expiry
  as a possible follow-up, not required for v1.
- **Payment write fails mid-flow** (network drop, app crash) — mitigated
  structurally by the atomic multi-location updates above rather than by a
  refund: either both sides of the update land or neither does, so this
  failure mode shouldn't produce a charged-but-nothing-happened state in
  the first place.

No refund case for "race completes and I placed badly" — paying buys entry
into the race with a stat bonus, not a placement guarantee.

## Still open

- **Tier numbers are placeholders, not playtested** — see `RaceEntryTiers`
  above. Easy to retune, no schema impact.
- **UI**: where tier selection happens in the create/join flow, how a
  paid-tier participant is visually indicated (if at all) to others in the
  race, and a UI hook to actually call the new `ClaimPendingRefundsAsync()`/
  `FinalizeApprovedJoinAsync()` methods (currently wired into login and
  app-resume for the refund claim; the join-request/approval flow itself
  still has no UI callers at all, paid or free).
- **Auto-expiry for stale open races** — noted above as a possible follow-up
  once the "abandoned race" refund trigger needs to be automatic rather
  than host-initiated.
