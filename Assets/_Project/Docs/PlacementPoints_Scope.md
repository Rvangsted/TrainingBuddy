# Placement Points — Scoping

Roadmap item #4. A new point type earned from race placement, which the
leaderboard ranks by instead of raw step count. Scoping only, not
implemented yet.

## Why

The leaderboard (`HighScoreScreen.cs`) currently ranks by lifetime
`StepCount` — literally how much you've walked, with no connection to the
race system at all. The goal is to make the leaderboard reflect competitive
race performance instead: what you earn depends on how you place in a race,
not how many steps you've banked.

## Current state (as implemented today)

- **No placement/rank is computed anywhere today.** `RaceSimulator.Generate`
  produces a `FinishTime` per participant, but the only thing done with it
  is `RaceScreen.AnnounceWinner()` scanning for the single lowest
  `FinishTime` to show a win/lose popup. There's no 1st/2nd/3rd/4th/5th
  ranking, and nothing is written back to Firebase about placement.
- **The natural hook point already exists:** `MarkRaceWatchedAsync`
  (`DatabaseManager.cs`) sets `races/{raceId}/status = "completed"` exactly
  once, the moment every participant's `watchedAt` is set. AI participants
  are pre-marked `watchedAt` at creation so they never block this. This is
  the one place in the codebase where "the race is fully, finally done" is
  unambiguous — the right place to compute and award placement points.
- **Leaderboard today:** `leaderboard/{uid}` mirrors `{UserName, Sex,
  StepCount}`; `HighScoreScreen` sorts `OrderByDescending(Steps)`. `StepCount`
  is also used by `ProfileScreen`'s activity graph and its steps-as-currency
  "points earned" display (`StepsAsCurrency_Scope.md`) — those are unrelated
  and untouched by this change; only the leaderboard's sort key and
  displayed number move to the new points.
- **No season/reset concept exists anywhere** in the project — `StepCount`,
  `SpeedPoints`, `AccelerationPoints` all accumulate forever. Placement
  points will follow the same lifetime-cumulative pattern; no reset job.
- **AI participants are a real footgun for a naive implementation.** AI
  slots live at `races/{raceId}/participants/{ai_<guid>}` with `isAI: true`,
  but that flag is **not** duplicated onto the stored simulation subtree —
  only the participants node has it. Any placement-awarding code must join
  back to `participants/{userId}/isAI` (or check the `ai_` prefix) before
  writing anything, or it will try to write `leaderboard/ai_<guid>` entries
  for users that don't exist.

## Decisions made

- **Rank across all participants, AI included.** No separate real-only
  re-ranking step. A real player's placement is their position by
  `FinishTime` across the full field (real + AI) exactly as the simulation
  already produces it — simplest to compute, and AI are filled in using the
  *lowest* real stats in the race, so they rarely skew who's actually
  competitive.
- **Lifetime cumulative, no seasonal reset.** Matches every other
  accumulating stat in this codebase. If a season/reset system is wanted
  later, it's a separate, larger feature (archiving, period keys, a reset
  trigger) — not bundled into this one.
- **Interaction with paid runs (#3):** the pay-to-win stat bonus from
  `PaidRuns_Scope.md` affects `FinishTime`, which affects placement, which
  affects placement points — this is an accepted, intentional consequence
  of that earlier decision, not a new one. Paying for a race can indirectly
  buy leaderboard position, not just a one-off in-race reward.

## Design

- **New field:** `users/{uid}/PlacementPoints` (int, lifetime total) —
  alongside `StepCount`/`SpeedPoints`/`AccelerationPoints`, same pattern.
- **Award logic, in `MarkRaceWatchedAsync`'s "all watched" branch:**
  1. Fetch the stored `RaceSimulation` for the race (`FetchRaceSimulationAsync`
     already exists for this).
  2. Sort `simulation.Participants` by `FinishTime` ascending → rank 1..N.
  3. For each ranked entry, skip if `participants/{userId}/isAI == true`
     (or `userId` starts with `ai_`).
  4. Look up the points value for that rank (see table below), add it to
     that user's `users/{uid}/PlacementPoints`, and update
     `leaderboard/{uid}/PlacementPoints` to match — same incremental-write
     pattern `WriteStepsToFirebaseAsync` already uses for `StepCount`.
  5. Do this once, guarded by the same `allWatched` check that already
     exists — a race's points are awarded exactly once, not re-awarded if
     `MarkRaceWatchedAsync` is somehow called again after completion (guard
     explicitly on `status != "completed"` before running the award step,
     since the existing check only gates the status transition itself).
- **Leaderboard changes:**
  - `DataCollections.cs`'s `LeaderboardEntry` gains `PlacementPoints`;
    `StepCount` can stay on the struct as secondary info (e.g. shown as
    smaller text under the points) rather than being removed outright.
  - `HighScoreScreen.cs` sorts `OrderByDescending(e => e.PlacementPoints)`
    instead of `Steps`.
  - `RealtimeDatabase.rules.json`'s `leaderboard/$uid` `.validate` rule
    (currently requires `UserName`/`Sex`/`StepCount`) needs `PlacementPoints`
    added to the required-children list.

## Resolved

- **Points-per-rank table** (race capacity is a fixed 5 today —
  `RaceScreen.MaxPlayers` — so this covers the real case; any future
  capacity > 5 falls back to the flat default):

  | Rank | Points |
  |------|--------|
  | 1st  | 50     |
  | 2nd  | 30     |
  | 3rd  | 20     |
  | 4th  | 10     |
  | 5th  | 5      |
  | 6th+ (default, future-proofing only) | 5 |

- **Last place gets the consolation value (5), not 0.** Matches the
  lifetime-cumulative, no-reset design above — losing repeatedly still
  nets slow progress rather than none.
- **Post-race UI: computed client-side, shown immediately.**
  `RaceScreen.AnnounceWinner()` already holds full rank data in memory
  (`_activePlayers`, each with `FinishTime`/`UserId`/`Name` —
  `RaceScreen.cs:354-367`) — enough to rank the human player and look up
  the table above with no DB round-trip. Display "+N Placement Points" on
  the existing popup at that point.
  `MarkRaceWatchedAsync` still does the authoritative award (same table,
  same rank computation, server-of-record write) when it runs afterward,
  guarded by the existing idempotency check — the popup's number is
  cosmetic display, not the source of truth, so there's no risk of the
  shown number and the written number disagreeing under normal operation.
  `MarkRaceWatchedAsync` runs fire-and-forget after the popup is dismissed
  today (`RaceScreen.cs:198-211`); that flow is unchanged.
- **Every leaderboard write must include `PlacementPoints`.** The
  `leaderboard/$uid` validate rule requires it on every write (a partial update
  merges into existing data, so a pre-existing entry without this field would
  otherwise fail validation the next time `WriteLeaderboardEntryAsync` runs a
  routine `StepCount` sync). Mirrored via a new in-memory
  `_cachedPlacementPoints` field, loaded alongside `_cachedUserName`/`_cachedSex`
  and kept in sync whenever this client credits its own points.
- **Profile screen:** add a `PlacementPoints` label next to the existing
  `StepCount`/`StepCurrency`/`SpeedPoints`/`AccelerationPoints` labels in
  `ProfileScreen.cs`, same `_dataSnapshot.Child("X").Value` read pattern
  already used for those (`ProfileScreen.cs:54,57,79-80`).
- **Award needs a pending-claim path, same as refunds.** `MarkRaceWatchedAsync`'s
  "all watched" branch runs on whichever participant's client happens to be last
  to watch — not necessarily the host or an admin. Firebase rules only let a user
  write their own `users/{uid}` and `leaderboard/{uid}` (the latter has no admin
  override at all), so that acting client can only credit *itself* directly.
  Every other real participant instead gets a `pendingPlacementPoints/{uid}/{raceId}`
  claim — same shape and pattern as `pendingRefunds`/`ClaimPendingRefundsAsync` —
  redeemed by their own client on next app start/login. `pendingPlacementPoints`
  write access is granted to any current participant of that race (not just the
  host, since the awarding client may not be one), validated to require
  `points`/`createdAt`.
- **Table must be easy to rebalance later.** The table is looked up from
  two places (client-side display in `AnnounceWinner`, authoritative award
  in `MarkRaceWatchedAsync`) — it must live as a single shared source of
  truth, not duplicated magic numbers in both places. A small static
  lookup (rank → points, with the flat default for ranks past the table)
  in one shared class is enough; no need for a ScriptableObject or remote
  config unless balancing turns out to need runtime tuning without a
  rebuild.
