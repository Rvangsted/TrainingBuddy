 # Migrating Step Data to Health Connect (Android) / HealthKit (iOS)

## Why

The current implementation (see `StepCounter_Fix.md`) reads the raw
`TYPE_STEP_COUNTER` hardware sensor directly and reconstructs history by
diffing against the last value it saved. That value is "steps since last
reboot" — the app has no way to see the sensor while it isn't running, so any
steps taken between the last sync and a subsequent device reboot are
permanently lost. Over months of infrequent app opens, this compounds into
exactly the kind of large shortfall reported (~25,000 steps over 6 months).

The fix is architectural, not a patch: stop reading a live sensor and instead
query an OS-level service that has already been continuously aggregating step
data in the background, independent of whether this app was running. Both
platforms have one:

| Platform | Service | Backed by |
|---|---|---|
| Android | **Health Connect** (`androidx.health.connect`) | System-level aggregator (system app on Android 14+, installable from Play Store on 13 and below) |
| iOS | **HealthKit** (`HKHealthStore`) | System-level store, fed continuously by the device's motion coprocessor / Apple Watch |

Both let you ask "how many steps between date X and date Y" directly — no
anchor, no reboot detection, no raw-counter diffing. That whole class of bug
stops being possible.

## Current project constraints (checked)

- Unity: `6000.3.21f1`
- `AndroidMinSdkVersion: 30` — already well above Health Connect's floor of
  API 26, no min-SDK bump needed.
- Only an Android Build Profile currently exists
  (`Assets/Settings/Build Profiles/New Android™ Profile.asset`), but
  `ProjectSettings.asset` already has iPhone build-target sections scaffolded
  — iOS was set up before but never actively built out.
- Unity's Input System already ships an iOS pedometer bridge
  (`iOSStepCounter.mm`, CoreMotion-based) — not HealthKit, but proof the repo
  already has the Xcode/native-plugin plumbing pattern (see options below).

## Platform-specific notes

### Android — Health Connect
- Requires the Health Connect app to be present. System app on Android
  14+ (API 34+); on 13 and below it's a separate Play Store install — the app
  needs an explicit "not installed" state (distinct from "permission denied")
  with a prompt to install it.
- Permission flow is its own system UI (`PermissionController` /
  `rationale intent`), not the classic runtime permission dialog we used for
  `ACTIVITY_RECOGNITION`.
- No background service or boot receiver needed — Health Connect aggregates
  independently of any app, so unlike the current design there's nothing to
  keep alive or re-register after a reboot.
- Query: `aggregate(AggregateRequest(StepsRecord.COUNT_TOTAL, timeRange))` for
  a total between two timestamps, or a per-day bucketed query for the
  activity graph.
- No first-party Unity support → needs a small native Android plugin (Kotlin,
  packaged as an `.aar`) exposing enable/permission-check/aggregate-query
  calls to C# via `AndroidJavaObject`, the same pattern already used for
  `Assets/Plugins/AndroidRuntimePermissions/Android/RuntimePermissions.aar`.
  Worth a short search for an existing "Health Connect for Unity" package
  before building one from scratch — unverified whether a maintained one
  exists at the moment.

### iOS — HealthKit
- Requires the "HealthKit" capability + entitlement enabled in the generated
  Xcode project (normally automated via a Unity `IPostprocessBuildWithReport`
  script that edits the Xcode project/entitlements, similar to how the
  Firebase plugin already patches the Android manifest here), plus an
  `NSHealthShareUsageDescription` string in `Info.plist` explaining why the
  app reads step data.
- Permission is a single system sheet (`HKHealthStore.requestAuthorization`)
  — simpler than Health Connect's flow, but Apple's App Store review is
  notably strict about HealthKit usage justification; expect the usage
  string and possibly review notes to be scrutinized.
- Query: `HKStatisticsQuery` (sum of `HKQuantityTypeIdentifier.stepCount`
  samples) for a total over a range, or `HKStatisticsCollectionQuery` for
  daily buckets.
- Retains data indefinitely (as long as some source — Health app, Apple
  Watch, the phone's own motion coprocessor — keeps writing it), so it fully
  matches Health Connect's guarantee: no reboot- or gap-related loss.
- **Lighter alternative worth knowing about:** the CoreMotion `CMPedometer`
  API the Input System already bridges (`iOSStepCounter.mm`) can answer
  "steps between two dates" *without* full HealthKit, but Apple only retains
  roughly 7 days of pedometer history on-device. That would fix short gaps
  but not the exact 6-month scenario reported — flagging it because it's a
  much smaller integration if a faster, partial fix is ever wanted, but it
  does **not** solve the same problem HealthKit does for long gaps. Recommend
  going straight to HealthKit for parity with Android's complete fix, unless
  timeline pressure argues otherwise.
- No native step sensor to fall back to needing "not supported" handling —
  virtually all iPhones support HealthKit; the real failure mode is
  permission denial, not missing hardware.
- **HealthKit read-permission opacity (gotcha):** for privacy reasons, Apple
  deliberately does not let an app reliably distinguish "read access denied"
  from "read access granted but zero samples" —
  `HKHealthStore.authorizationStatus(for:)` is not meaningful for read-only
  types the way Health Connect's permission check is. The accepted pattern
  industry-wide is: request authorization, then just attempt the query
  regardless of what the status API reports, and treat a persistently empty
  result the same as "unavailable" in the UI. This affects how precisely we
  can report `StepCounterAvailability.PermissionDenied` vs. `Available` on
  iOS specifically — Android's Health Connect does not have this limitation.

### Building the iOS provider natively (no third-party plugin)

Decided against BEHealthKit to avoid an extra third-party dependency —
scoping the from-scratch version instead, at the same depth as the Android
Health Connect plugin below. This is genuinely native-plugin work, same
category of effort as the Android side, not a small task.

**Native side (Objective-C, in `Assets/Plugins/iOS/`):**
Unity compiles `.mm`/`.m`/`.h` files dropped in `Assets/Plugins/iOS/`
directly into the generated Xcode project — the same mechanism the Input
System package already uses for `iOSStepCounter.mm`, and a reasonable
reference for the calling convention. Needed C-linkage entry points, callable
from C# via `[DllImport("__Internal")]`:

- `_HealthKit_IsAvailable()` → `HKHealthStore.isHealthDataAvailable()`.
- `_HealthKit_RequestAuthorization(callback)` → requests read access to
  `HKQuantityType.quantityType(forIdentifier: .stepCount)` via
  `requestAuthorization(toShare:read:completion:)`.
- `_HealthKit_QueryStepsSince(sinceUnixMillis, callback)` → runs an
  `HKStatisticsQuery` with the `.cumulativeSum` option over
  `HKQuery.predicateForSamples(withStart:end:options:)` from the given date
  to now, returns the summed step count.
- Optional (daily breakdown for `ActivityGraph`):
  `_HealthKit_QueryDailySteps(startUnixMillis, endUnixMillis, callback)` using
  `HKStatisticsCollectionQuery` with day-long interval components — the
  result is a set of per-day buckets, easiest to marshal back as a small JSON
  string (`const char*`) and parse on the C# side with the
  `Newtonsoft.Json` already used throughout this project, rather than
  fighting with array marshaling across the ObjC/C# boundary.
- Async callbacks: HealthKit queries complete on a background queue, so the
  native code needs to hold a reference to a C# static callback (the
  `[MonoPInvokeCallback]` + delegate-pointer pattern `iOSStepCounter.cs`
  already demonstrates in this repo) and the C# side resolves a
  `TaskCompletionSource<T>` from it, giving `async`/`await` call sites the
  same shape as the Android provider.

**Xcode project configuration (build-time, automated):**
- Enabling the HealthKit capability and entitlement is first-party Unity, not
  third-party — `UnityEditor.iOS.Xcode.ProjectCapabilityManager.AddHealthKit`
  exists specifically for this, called from an `IPostprocessBuildWithReport`
  script the same way the project likely already patches other Xcode
  settings.
- Add `NSHealthShareUsageDescription` to `Info.plist` via the same
  post-process script (`PlistDocument` API).
- Link `HealthKit.framework` — either drop a framework reference under
  `Assets/Plugins/iOS/` with the right import settings, or add it in the
  same post-process step via `PBXProject.AddFrameworkToProject`.

**What this avoids vs. BEHealthKit:** no paid asset, no dependency on a
third party keeping the plugin updated for future Unity/iOS versions, full
visibility into exactly what the native code does (relevant given it touches
health data and App Store review). **What it costs:** genuine Objective-C
development, and testing requires a real device or a simulator with manually
seeded Health app sample data — either way, an actual Mac with Xcode
somewhere in the pipeline (Unity Cloud Build, Xcode Cloud, or a physical Mac)
is a hard requirement for this piece regardless of which option is chosen,
since Unity can generate the Xcode project on Windows but can't compile or
sign it.

## What changes in the C# layer

Replace the anchor/diff machinery in `DatabaseManager.cs`
(`_baseStepCount`, `_deviceAnchor`, the reboot-detection branch, the raw
`StepCounter.current.stepCounter.ReadValue()` polling loop) with a small
platform-agnostic interface:

```csharp
public interface IStepDataProvider
{
    Task<StepProviderAvailability> CheckAvailabilityAsync();
    Task<long> GetStepsSinceAsync(DateTimeOffset since);
}
```

with `HealthConnectStepProvider` (Android) and `HealthKitStepProvider` (iOS)
implementations, each wrapping its native plugin. `StartStepCounter()`
becomes: check availability → read `GetStepsSinceAsync(lastSyncedTimestamp)`
→ add the delta to the Firebase-stored total → save the new timestamp as the
sync point. `StepCountSnapshot` (a raw sensor value) is replaced by a
timestamp; nothing about the existing `StepCount` totals in Firebase needs to
change, so this is a non-breaking swap of the *source* of new deltas —
existing users keep their history.

The existing `StepCounterAvailability` enum (`Available` /
`PermissionDenied` / `SensorUnsupported`) still fits conceptually but gains a
value for "provider not installed" (Health Connect missing on Android 13 and
below).

## Suggested phasing

1. **Android: Health Connect provider.** Swap the existing, working Android
   path first — build the `.aar`, the `HealthConnectStepProvider`, and the
   provider abstraction in `DatabaseManager`. This alone fixes the reported
   bug on the platform you can ship today.
2. **iOS: bring up the build target + native HealthKit provider.** Can
   happen in parallel once the `IStepDataProvider` interface exists — same
   shape of work as Android, against a different native API and a different
   toolchain (needs a Mac/Xcode in the loop, see above).
3. **Device QA matrix.** Real-device testing across Android versions
   (Health Connect behaves differently pre/post API 34) and iOS versions;
   simulators don't produce real step data on either platform.
4. **App Store / Play Store review prep.** Privacy policy updates, usage
   strings, and (for iOS) anticipating HealthKit review scrutiny.
5. **Stretch — done (read path only):** `ProfileScreen`'s daily-breakdown
   graph now pulls from `IStepDataProvider.GetDailyStepsAsync` (Health
   Connect's `aggregateGroupByPeriod`, HealthKit's
   `HKStatisticsCollectionQuery`) instead of the `dailySteps/{date}` Firebase
   buckets, on any platform with a provider. Deliberately conservative:
   the buckets are still *written* exactly as before on every platform (kept
   as a harmless fallback/historical record and as the Editor's only data
   source) — full removal of that write path is a separate future cleanup.
   Also fixed a latent bug in the process: the buckets were UTC-day keyed;
   the new provider-backed path is local-calendar-day keyed, matching what
   Health Connect/HealthKit (and users) actually mean by "today"/"yesterday".
   See `StepCounter_HealthConnect_Implementation.md`/`StepCounter_HealthKit_Implementation.md`.

## Maintained Unity package check (done)

- **iOS — [BEHealthKit](https://assetstore.unity.com/packages/tools/integration/behealthkit-39962)
  exists and is a good fit** (Unity Asset Store, ~€41, actively maintained,
  last update Aug 2025, 17 reviews, 136 favorites, wraps exactly the
  `ReadSteps`/`ReadStatistics`/`ReadStatisticsCollection` calls we'd need)
  **but decided against it** to avoid taking on a third-party dependency for
  a health-data code path — see "Building the iOS provider natively" above
  for the from-scratch plan instead. Worth reconsidering only if the native
  build timeline becomes a real blocker.
- **Android — no equivalent exists for Health Connect specifically.** The
  one health-data asset that shows up, "G-Fit Connect" (€46, last updated
  Apr 2024), targets the older **Google Fit** API, not Health Connect — and
  Google has an active 2026 migration pushing developers *off* Google Fit
  and onto Health Connect, so adopting it now would mean building on a path
  Google is already deprecating. Unity's own forums confirm there's no
  ready-made Health Connect plugin; the standing advice there is to use the
  Input System's native `StepCounter` device (i.e. what we already have and
  are moving away from) or roll your own AAR. **The native Kotlin/AAR plugin
  work for Android in the scope above still stands as-is.**
- Minor FYI unrelated to the plan itself: starting with a June 2026 platform
  update, Health Connect began attributing natively-tracked steps to an
  internal synthetic package name rather than the app that recorded them.
  Doesn't change our integration, just means some of Google's own
  documentation/screenshots may look different from what we see in testing.

## Preventing steps from being attributed to the wrong account

Worth solving in the same migration, since the app has races and a
leaderboard — real incentive to game step counts, e.g. by logging into an
account on a phone that already has months of accumulated Health
Connect/HealthKit history and instantly harvesting it, or by borrowing a more
active friend's phone during a race.

**What's realistically preventable vs. not:** nothing can cryptographically
prove which human's legs produced a given step — if someone hands their
already-logged-in phone to a friend and lets them walk around with it, no
software fix stops that (the same limitation applies to Fitbit, Strava,
Google Fit, etc. today). What *is* preventable is the much easier, more
damaging exploit: logging into an account on an unfamiliar device and
instantly inheriting that device's entire pre-existing step history.

**Design: per-(account, device) sync anchoring, not per-account.**

Today's plan already tracks "steps since last synced timestamp" per
*account*. Change that to per **(account, device)** pair, keyed by
`SystemInfo.deviceUniqueIdentifier`:

- Firebase gains `users/{uid}/deviceSync/{deviceId}: { lastSyncTimestamp }`
  instead of one global `lastSyncTimestamp` per account.
- The first time an account is used on a device that has no
  `deviceSync` record yet, **do not backfill any history** — anchor the sync
  timestamp to "now" and only count steps from that moment forward. This is
  the exact same principle already used today for brand-new accounts
  ("prevents counting steps taken before account creation" in the current
  code) — generalized to "before this device was linked to this account."
- Legitimate case (own phone + own tablet, or a phone upgrade): works
  correctly with no special handling — each device just starts counting
  fresh the first time it's seen, and both sync independently from then on.
  No steps are lost for future walking on either device.
- Exploit case (log into a friend's phone that already has 6 months of
  their steps): blocked — the pre-existing backlog on that device is never
  read into the account, because the anchor starts at "now," not at
  whatever Health Connect/HealthKit already had stored.

**Second exploit: delayed harvest on an already-anchored device.** The
anchor-at-"now" rule only protects against backlog from *before* the first
login on a device — it does nothing to stop this:

1. Log into your account on a friend's phone once. New device → anchored at
   "now" → 0 steps. Looks safe.
2. Walk away. He carries his own phone for a month, racking up his own
   steps in Health Connect/HealthKit, unrelated to your account.
3. Log in again a month later. The stored anchor is still that first
   login's timestamp, so "steps since then" is his entire month.

The system has no way to distinguish "my own phone I didn't open the app on
for a month" from "his phone he carried for a month while my account stayed
linked to it" — both look like a device with a stale sync timestamp
reporting a large delta. This is the same mechanism that fixes the original
bug (crediting long gaps between app opens), viewed from the attacker's
side, so it can't be closed outright without undoing the fix.

**Mitigation: cap how far back any single sync can reach, per device — 30
days.**

```
steps = GetStepsSinceAsync(max(deviceSync[deviceId].lastSyncTimestamp, now − 30 days))
```

- Normal use (weekly-ish app opens): unaffected, always well inside the cap.
- The original 6-month bug: still fixed for realistic usage — the bug was
  the app silently never working for months, not a person deliberately
  avoiding it; once it actually works, monthly-or-more-frequent opens are
  the expected pattern.
- The delayed-harvest exploit: bounded rather than closed. Instead of
  walking away and collecting an unlimited amount whenever convenient, at
  most 30 days of someone else's steps are reachable, and only if you
  physically return to their device within that window. Small, low-value
  target for a hobby leaderboard rather than an open-ended one.
- Cheap additional guard, independent of the above: cap the plausibility of
  any single delta (e.g. flag or clip syncs implying >20,000 steps since the
  last sync) as a sanity backstop against both bugs and casual
  manipulation — not a hard security boundary, just a cheap defense-in-depth
  check.
- Considered and **decided against**: locking step crediting to the device a
  race was started on for its duration. The 30-day sync cap plus per-device
  anchoring above is the accepted level of protection; not adding the extra
  complexity of a per-race device lock on top of it.

This fits into the `IStepDataProvider` work above as a schema change
(`deviceSync/{deviceId}` instead of one timestamp) plus a small addition to
`StartStepCounter()`'s reconciliation logic — not a separate feature.

## Decisions

- **Priority:** hold release until both platforms are ready together —
  Android Health Connect and iOS HealthKit ship as one combined release, not
  Android first.
- **iOS approach:** go straight to the full native HealthKit provider (see
  "Building the iOS provider natively" above). CMPedometer stopgap rejected —
  it only fixes short (~7-day) gaps, not the 6-month scenario the migration
  exists to solve, so it would be throwaway work once HealthKit lands anyway.