# Step Counter Fix — Device Inconsistency

## Problem

The step counter worked on some phones but silently never counted steps on
others (reported on a Samsung S20 FE among others), with no error visible to
the user or in the logs.

## Root cause

`DatabaseManager.EnsureStepCounterDevice()` used to do this when the Input
System's `StepCounter.current` was `null`:

```csharp
if (StepCounter.current == null)
{
    InputSystem.AddDevice<StepCounter>();
}
```

On Android, the real, hardware-backed device is
`UnityEngine.InputSystem.Android.AndroidStepCounter`. It's only auto-registered
by Unity's native plugin when the phone actually exposes a `TYPE_STEP_COUNTER`
sensor, and only once native sensor enumeration has completed.

`InputSystem.AddDevice<StepCounter>()` does **not** create that native-backed
device — it creates a generic, disconnected `StepCounter` (the abstract base
class) with no native sensor binding. It never receives real events, so
`ReadValue()` on it just sits at 0 forever.

So on any phone where:
- the real sensor hadn't finished registering yet when the app first checked
  (some OEM sensor hubs, e.g. Samsung's, register it a little after app
  start), or
- the phone genuinely has no `TYPE_STEP_COUNTER` sensor,

...the code silently manufactured a fake device instead of surfacing the
problem, and steps never counted again for that install — with no crash and
no log signal.

## Fix

**`DatabaseManager.cs`**
- `EnsureStepCounterDevice()` no longer fabricates a device. It only checks
  for/enables the real native-backed one.
- Added `StepCounterAvailability` enum: `Available`, `PermissionDenied`,
  `SensorUnsupported`.
- Added `CheckStepCounterAvailabilityAsync()` — checks the
  `ACTIVITY_RECOGNITION` permission, then retries sensor detection for ~2.5s
  (5 × 500ms) before concluding the sensor is genuinely unsupported. The retry
  window exists to tolerate OEM sensor hubs that register the sensor slightly
  after app start.
- `StartStepCounter()` now runs this check first (before any Firebase reads),
  returns the resulting `StepCounterAvailability` to its caller, and on
  failure calls `HandleStepCounterUnavailable()`, which signs the user out and
  shows a blocking overlay explaining why (different message for
  permission-denied vs. no-sensor).

**`UIManager.cs`**
- Added `ReturnToWelcomeScreen()`, used by the overlay's dismiss button.

**Call sites updated** for the new `Task<StepCounterAvailability>` return
type:
- `FirebaseController.FirebaseLogin` / `FirebaseRegister` — now refuse login
  / registration (`return false`) if the check fails.
- `MainMenu.cs` — awaits the result on its resume-check.
- `GameManager.cs` — fire-and-forget on app resume for an already-logged-in
  session; if permission is revoked mid-session, this also signs the user out
  and shows the same overlay.

## Policy change

The app now requires a working step counter to be used at all. A phone with
no step sensor, or one that hasn't granted `ACTIVITY_RECOGNITION`, can no
longer log in — or stay logged in — without seeing an explicit prompt.

## Open question

The 2.5s retry window for delayed sensor registration (the Samsung-style
case) is a first guess, not a measured value. If devices like the S20 FE
still intermittently get flagged as `SensorUnsupported`, this window may need
to be lengthened.