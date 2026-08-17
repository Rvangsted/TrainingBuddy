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

## Still open

- **The points-per-rank table.** Needs a concrete formula/table (e.g.
  1st/2nd/3rd/4th/5th → fixed values, or a capacity-relative formula) and
  balancing against how often someone reasonably races — not guessed at
  here. Flat table with a default value for ranks past capacity 5 is
  probably enough as a v1 shape.
- **Does last place get 0 points, or a small consolation amount?** Affects
  whether racing and losing seven times in a row nets any progress at all.
- **UI**: how a placement-points award is communicated to the player right
  after a race (e.g. on the same win/lose popup `AnnounceWinner` already
  shows), and whether the profile screen should show this total anywhere
  alongside the existing step count.
