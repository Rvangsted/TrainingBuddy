# Paid Runs UI — Scoping

Follow-up to `PaidRuns_Scope.md`, which implemented the full backend
(`RaceEntryTier` costs/bonuses, refund ledger) but flagged **zero UI exists**
to actually reach it — every real race today is created and joined with
`tier: None`, hardcoded. This doc scopes closing that gap.

**Implemented** (hosting + joining tier selection; badge and
join-request/approval flow still not built — see "Still open"):
- `DatabaseManager.GetRaceEntryTierChoiceLabels()` — new static accessor,
  fixed enum-order labels for the picker (`DatabaseManager.cs`).
- `CreateRaceScreen` (new: `CreateRaceScreen.cs`/`.uxml`/`.uss`) — race-name
  input + `RadioButtonGroup` tier picker → `CreateLobby(race, tier)`.
  `MainMenu.OnCreateLobby` now navigates here instead of hosting directly.
- `FindLobbyScreen`'s join flow now shows a small confirm modal (embedded in
  its own UXML, reusing `UniversalOverlay.uss`'s card styling) with the same
  tier picker before calling `JoinRaceDirectlyAsync(raceId, tier)`.
- `LayoutData`/`GlobalScope`/`UIManager` wired up for the new screen.

**One manual step still required** (can't be done from a text-editing
session): open `LayoutData`'s asset in the Unity Inspector and drag
`CreateRaceScreen.uxml` into the new "Create Race Screen Visual Tree" field
— every other screen's `VisualTreeAsset` reference is a serialized
Inspector assignment, and Unity doesn't allocate the asset's GUID until the
Editor imports it, so this can't be pre-wired from outside the Editor.

## Current state (as found)

- **`MainMenu.OnCreateLobby`** (`MainMenu.cs:85-97`): a single button tap, no
  intermediate screen at all. Builds a `RaceData` with an auto-generated name
  (`"{DisplayName}'s Race"`), `Longitude`/`Latitude = 0`, calls `CreateLobby`
  → `HostRaceAsync(race, 5)` — tier always `None`. **No `RaceName`/description
  input exists anywhere in the UI today** — it's fully auto-generated.
- **`FindLobbyScreen.JoinLobby`** (`FindLobbyScreen.cs:198-211`): a single tap
  on a lobby card calls `JoinRaceDirectlyAsync(raceId)` directly — **no
  confirmation step of any kind today**, paid or free.
- **`UniversalOverlay`** (`UniversalOverlay.cs`, 407 lines): supports a
  title/message, one optional text field, up to two buttons, and one of two
  background illustrations (`PopupImage.Friends`/`Worry`/`None`). Not built
  for a multi-choice picker — it's a single fixed UXML card instantiated once
  and reused via show/hide.
- **Only existing multi-option-selector precedent**: `RegisterScreen`'s
  2-option Gender field uses a `RadioButtonGroup` (`RegisterScreen.uxml:13`,
  `RegisterScreen.cs:12,37`) — notably the *base* `RadioButtonGroup`, not the
  `LocalizedRadioButtonGroup` subclass that already exists at
  `Controls/LocalizedRadioButtonGroup.cs:8` (a pre-existing inconsistency;
  not this doc's problem to fix, but worth using the Localized version
  properly this time since it already exists).
- **Lobby/host cards are built entirely in C#**, not fixed UXML —
  `HostScreen.CreateHostCard` and `LobbyScreen.CreateLobbyCard` have no spare
  slot for a paid-tier badge today.
- **`DatabaseManager.FetchCurrentRaceParticipantsAsync`** (feeding
  `HostScreen`) returns `(displayName, isHost, joinedAt, sex, userId)` tuples
  — `paidTier` is not currently queried, even though
  `participants/{uid}/paidTier` already exists in Firebase.
- **Insufficient-balance failures already work for free**: paying for a tier
  you can't afford throws `InvalidOperationException(UserMessages.
  InsufficientStepCurrency(cost, balance))`, which already routes correctly
  through the `ShowError` helper built in the error-messaging pass — nothing
  new needed there once the create/join call sites wrap their calls in
  try/catch + `ShowError`.
- Tier costs/bonuses (`Basic` 1000/+5, `Plus` 3000/+12, `Elite` 7000/+25) are
  still "placeholder, not playtested" per `PaidRuns_Scope.md` — unaffected by
  this UI work.

## Decisions made

- **A new dedicated Create Race screen**, not an extended `UniversalOverlay`.
  This solves two gaps at once: tier selection, and the fact that race names
  are currently auto-generated with zero user input.
- **Joining gets a lightweight confirm step too** — not a full screen, a
  small purpose-built modal shown before `JoinRaceDirectlyAsync` fires.
  Deliberately kept separate from `UniversalOverlay` itself rather than
  stretching that component's fixed shape.
- **The join-request/host-approval flow stays out of scope.**
  `SubmitJoinRequestAsync`/`HandleJoinRequestAsync`/`FinalizeApprovedJoinAsync`
  have zero UI callers today — a pre-existing, separate gap. Flagged here,
  not built. This pass only touches the direct-join path.
- **The paid-participant badge is in scope** — small and directly related.
  Requires adding `paidTier` to `FetchCurrentRaceParticipantsAsync`'s
  returned tuple, and a small badge element in `CreateHostCard`/
  `CreateLobbyCard` (built in C#, same as the rest of those rows).

## Design

- **`TierPickerControl`** (new, shared control): a 4-option picker — Gratis
  (free) / Basic / Plus / Elite — each option showing the tier's cost in
  mønter and its stat bonus. Reuses the `RadioButtonGroup` precedent from
  `RegisterScreen`, via `LocalizedRadioButtonGroup` this time. Sourced from a
  new public read path on `DatabaseManager` (`RaceEntryTiers` is currently
  private) rather than hardcoding costs a second time client-side.
- **Create Race screen**: race-name text input (`LocalizedTextInput`,
  matching `RegisterScreen`'s pattern) + `TierPickerControl` + Create button
  → `HostRaceAsync(race, 5, description: null, tier)`.
- **Join confirm modal**: shows the race title (already known from the
  tapped lobby card) + `TierPickerControl` + Confirm/Cancel →
  `JoinRaceDirectlyAsync(raceId, tier)`.
- Both flows wrap their call in try/catch → `_databaseManager.ShowError(...)`
  — covers the insufficient-balance case and any other guard exception for
  free, no new error-handling work needed.
- **Paid badge**: a small `VisualElement` added to a participant row when
  `paidTier != None` (e.g. a colored dot/label naming the tier) — exact
  visual not decided (see Still open).

## Still open

- Exact visual design of `TierPickerControl` (vertical list vs. segmented
  control) and of the paid-participant badge (per-tier color? icon? text
  only?) — needs an actual mockup pass, not decided here.
- Whether to add the optional race `description` field to the new Create
  Race screen at the same time — `HostRaceAsync` already accepts it, nothing
  in the UI sets it today. Cheap to include since the plumbing exists.
- Whether the join-confirm modal should show other current participants'
  paid tiers (social proof) — not decided; simplest v1 just shows this
  player's own tier options and cost.