# Notification Toast — Scoping

New shared UI infrastructure, motivated by a gap found while punch-listing
the remaining roadmap UI work: refer-a-friend's reward moments currently
land completely silently. Written as reusable infra rather than a one-off,
since the underlying need ("something happened in the background, tell the
player without interrupting them") isn't specific to referrals. Scoping
only, not implemented yet.

## Current state

- **No toast/snackbar/transient-notification component exists anywhere** in
  the codebase (verified: zero hits searching `Scripts/` for
  toast/snackbar/notification).
- The only existing feedback mechanism is `UniversalOverlay` — a full-screen
  blocking modal requiring an explicit button tap to dismiss.
- **Concrete motivating gap**: `DatabaseManager.GrantReferralMilestoneRewardAsync`
  (the new user's 500-mønter welcome bonus, fired inside the background
  step-sync loop) and `ClaimReferralRewardsAsync` (the referrer's 1000-mønter
  payout, fired at app start/login) both already run today with zero UI
  hook — the StepCurrency balance just changes with no on-screen
  acknowledgment at all.

## Decisions made

- **Build a new lightweight, non-blocking, auto-dismissing toast**, rather
  than extending `UniversalOverlay` or forcing every passive event through a
  blocking modal. A referral reward landing is background good news, not
  something that should force a tap to dismiss before the player can keep
  doing whatever they were doing.
- **Scope for v1: wire it to exactly the two referral moments above.** Not a
  general-purpose replacement for `UniversalOverlay` — anything needing an
  explicit response (errors, confirmations) stays a blocking overlay.

## Design

- **New `ToastNotification` control** (UXML + C#, in `Controls/` alongside
  `UniversalOverlay`): message text only for v1 — no title, no buttons, no
  image. Simplest shape that fits the referral use case.
- **`UIManager.ShowToast(string message)`** — new method, analogous to
  `ShowOverlay`. Auto-dismisses after a fixed duration (~3s), sliding out.
- **Stacking**: a second toast firing while one is showing gets queued
  (shown after the first dismisses), not overwritten and not shown
  simultaneously — deliberately different from `UniversalOverlay`'s existing
  overwrite-on-second-call behavior, since toasts are expected to fire more
  casually/often than blocking modals.
- **Hook points**:
  - `GrantReferralMilestoneRewardAsync`'s success path runs deep in the
    background step-sync loop, not from a UI click — needs a new
    `DatabaseManager.ReferralRewardGranted` event that `UIManager`
    subscribes to once at startup, calling
    `ShowToast("Du fik 500 mønter som velkomstbonus!")`.
  - `ClaimReferralRewardsAsync`'s per-claim credit fires at app
    start/login/resume (`FirebaseController.FirebaseLogin`,
    `GameManager.OnApplicationPause`) — same event-based approach, a new
    `DatabaseManager.ReferralRewardClaimed` event carrying the claimed
    amount, e.g. `ShowToast("En ven du henviste gav dig 1000 mønter!")`.

## Still open

- Exact visual design (colors, on-screen position, slide/fade animation) —
  not decided here.
- Exact Danish copy for both toast messages.
- Whether toasts should eventually cover other passive events (placement
  points, friend-request auto-processing, etc.) — not required for v1; this
  doc only scopes the referral hookup.