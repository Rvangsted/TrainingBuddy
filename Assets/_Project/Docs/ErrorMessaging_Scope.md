# Better Error Messaging — Scoping

Roadmap item #6. Unlike the other roadmap items, this isn't a new feature —
it's fixing gaps in an existing, partially-built mechanism. Scoping only,
not implemented yet.

## Current state (as found)

- **A shared popup mechanism already exists**, just not error-specific:
  `UniversalOverlay` (`Assets/_Project/Scripts/UI/Controls/UniversalOverlay.cs`)
  via `UIManager.ShowOverlay(...)` and the convenience wrapper
  `DatabaseManager.ShowMessage(title, message, buttonText)`
  (`DatabaseManager.cs:1957-1960`). It's reused for successes, confirmations,
  *and* errors alike — no dedicated error styling, and no queueing: a second
  `Show()` call while one is already open silently overwrites the first.
- **Where it's used, `ex.Message` is often shown verbatim** — e.g.
  `HostScreen.StartRace()` (`HostScreen.cs:55-68`) catches, logs, then calls
  `_uiManager.ShowOverlay("Kan ikke starte", ex.Message, "OK", ...)` with
  whatever text the exception happens to carry. This works today only
  because `DatabaseManager`'s own validation exceptions (e.g. `"Løbet
  kræver mindst {MinRaceParticipants} spillere for at starte."`) happen to
  already be pre-written, user-safe Danish — but the same call site would
  just as happily show a raw `NullReferenceException` message or an English
  Firebase SDK exception if one bubbled up instead. Nothing distinguishes
  "safe to show" from "internal/technical."
- **Many failures show nothing at all.** Confirmed silent (`catch` →
  `LogError` only, no UI touch): the join-race flow
  (`FindLobbyScreen.cs:200-209`), the leave-race flow
  (`LobbyScreen.cs:152-161`), and nearly every read method in
  `DatabaseManager.cs` (`GetUserByFriendCodeAsync`, `FetchUserData`,
  `GetAllRaces`, `FetchIncomingRequestsAsync`, `FetchFriendsAsync`,
  `FetchLeaderboardAsync`, `FetchDailyStepsAsync`, `ArchiveDailyStepsAsync`,
  `WriteStepsToFirebaseAsync`, among others). Worst concrete case:
  `FirebaseController.FirebaseLogin` (`FirebaseController.cs:60-95`) builds
  a friendly, translated message from the Firebase `AuthError` and then
  just logs it — the caller only gets a bare `false` back, so a login
  failure currently shows the user **nothing**.
- **Language is a genuine mix**, including within single features. Danish:
  most `DatabaseManager` validation exceptions, `FirebaseController.cs`'s
  invalid-email/no-such-account messages, `ProfileScreen.cs`'s "Ikke
  fundet"/"Fejl". English, shown to the user in an otherwise-Danish app:
  `DatabaseManager.DeleteAccountAsync`'s catch blocks
  (`DatabaseManager.cs:1993,2059,2079` — "Incorrect password...",
  "Something went wrong deleting your data...").
- **Two ad hoc fallback strings exist, not shared**: `"Der opstod en fejl.
  Prøv venligst igen."` (`FirebaseController.cs:116`) and `"Registrering
  fejlede. Tjek venligst at oplysningerne er korrekte."`
  (`FirebaseController.cs:196`) — each call site that bothered to have a
  fallback wrote its own, no shared constant.
- `WelcomeScreen.cs`'s Health Connect permission-denied UI bypasses
  `UniversalOverlay`/`UIManager` entirely — its own ad hoc panel/Label
  (`WelcomeScreen.cs:158-165`).

## Decisions made

- **Scope: user-initiated actions first, not a full sweep.** Fix every
  place a user directly triggers something (join/leave race, cancel, kick,
  register, login, delete account) and currently gets no feedback on
  failure. Passive background fetches (leaderboard, friends list, daily
  steps polling) stay logging-only for now — popping a modal every time a
  background read fails would be noisy, and "I tapped a button and nothing
  happened" is the actual problem being solved here, not "a background
  refresh silently failed." Revisit background-fetch feedback (e.g. inline
  "couldn't load" states) as a separate, later pass if it comes up.
- **Never show raw/technical/English exception text to the user, ever.**
  Only two kinds of message reach the UI: (a) a pre-written, Danish,
  user-safe string for a known failure case, or (b) one shared generic
  Danish fallback. Raw `ex.Message` is never passed to `ShowOverlay`
  directly anymore, even for `DatabaseManager`'s own exceptions — see
  design below for why that distinction still matters even though those
  particular messages are currently fine.
- **One shared fallback string, not one per call site.** Replaces the two
  existing ad hoc ones. Something like `"Der opstod en fejl. Prøv venligst
  igen."` (already exists, just needs to become the one canonical constant
  instead of duplicated text).

## Design

- **A single helper, e.g. `UIManager.ShowError(string title, Exception ex)`**
  (or on `DatabaseManager`, next to the existing `ShowMessage`), used at
  every retrofitted catch site instead of ad hoc `ShowOverlay(...,
  ex.Message, ...)` calls. Internally:
  - Full exception is always logged (`Debug.LogError`/Crashlytics — this
    part already happens almost everywhere and doesn't change).
  - If the exception is one of the app's own deliberately-thrown,
    pre-written business-rule exceptions (the `InvalidOperationException`s
    in `DatabaseManager.cs`/`FirebaseController.cs` written specifically to
    be user-facing), show that message as-is.
  - Otherwise — network errors, Firebase SDK exceptions, anything
    unexpected — show the one shared generic fallback, never the raw
    exception text. This is the actual fix for the `HostScreen.cs`-style
    "works by accident" call sites: today it happens to be safe because
    only the expected exception type ever reaches that catch block in
    practice; the helper makes that guarantee explicit instead of implicit.
- **Retrofit list (user-initiated, currently silent or English)**:
  `FindLobbyScreen`'s join flow, `LobbyScreen`'s leave flow,
  `FirebaseController.FirebaseLogin` (the translated message it already
  computes just needs to actually reach the UI — no new translation work,
  just wiring), `DeleteAccountAsync`'s three English catch blocks
  (translate to Danish + route through the shared helper).
- **Overlay queueing bug, worth fixing alongside this**: since this work
  means error popups will fire more often and from more places, the
  existing "second `ShowOverlay` call overwrites the first" behavior in
  `UniversalOverlay` becomes more likely to actually bite (e.g. a
  background success popup and a newly-added error popup racing each
  other). Not strictly required for v1, but flagged because this change is
  what makes the bug more likely to surface in practice.

## Resolved

- **Bigger than first scoped**: nearly every deliberately-thrown
  `InvalidOperationException` in `DatabaseManager.cs` (~40 of them — race
  join/leave/kick/cancel/start guards, friend-request guards) was written in
  **English**, not Danish, contradicting the "show business-rule exceptions
  as-is" design's assumption that they were already safe. All of them are
  now translated to Danish and centralized in a new
  `Assets/_Project/Scripts/Firebase/UserMessages.cs` — one dedicated file,
  separate from business logic, so copy can be reviewed/tweaked without
  hunting through `DatabaseManager`/`FirebaseController`. Near-duplicate
  guards (every "not authenticated"/"account deactivated"/"race not
  found"/"race full" message) were consolidated to one shared constant each
  rather than kept as ~10 near-identical translations.
- **`DatabaseManager.ShowError(title, ex)`** is the new shared helper (next
  to the existing `ShowMessage`): shows `ex.Message` as-is only for
  `InvalidOperationException` (now guaranteed Danish/safe via
  `UserMessages`), otherwise shows `UserMessages.GenericFallback`. Does not
  log — call sites keep their own existing `LogError` calls.
- **Retrofit list ended up bigger than the doc's original bullets**: also
  fixed `HostScreen`'s kick and cancel flows (silent, not just
  join/leave/start as originally named) and `ProfileScreen`'s friend
  request send/accept/deny (had no error handling at all — an unhandled
  exception in an `async void` handler). `FirebaseController.FirebaseLogin`
  now actually shows its already-computed message instead of just logging
  it, and gained a null-guard on the `FirebaseException` cast (was an
  unguarded `as` cast that would NPE on a non-Firebase exception).
- **Overlay-stacking**: retrofitted catch blocks that fire while a confirm
  overlay is still open (`LobbyScreen` leave, `HostScreen` kick/cancel) call
  `HideOverlay()` before `ShowError` — a cheap fix for the exact stacking
  case the doc flagged, short of building real queueing.
- `DeleteAccountAsync`'s three English catches now route through
  `UserMessages` (Danish) instead of inline English strings.

## Still open

- Whether error popups should look visually distinct from success/info
  ones (icon, color) — a `UniversalOverlay` styling question, not required
  for the coverage fix itself.
- Whether/when to do the deferred full sweep of passive background-fetch
  feedback.
- A full `UniversalOverlay` queueing mechanism (only the two-line
  `HideOverlay()`-before-`ShowError` fix above was applied, not a real queue).
