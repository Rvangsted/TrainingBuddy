# Step Counter Migration — Health Connect Implementation (Android)

This documents what has actually been **built and fixed** so far in the
Health Connect migration, as a companion to
[`StepCounter_HealthPlatform_Migration_Scope.md`](./StepCounter_HealthPlatform_Migration_Scope.md)
(which is the original *plan*). Read that doc first for the "why" — the
6-month step-loss bug, the Health Connect/HealthKit comparison, and the
phasing decisions. This doc explains the "what" and "how" of the Android side
that's now in place.

**Status at a glance:**
- ✅ Android — Health Connect provider built, permission flow built, two
  real-world bugs found in testing and fixed, anti-cheat gap closed.
- ✅ Daily-breakdown graph (`ProfileScreen`) now queries providers directly
  (read path only) — see §9.
- 🟡 iOS — provider code written (see
  [`StepCounter_HealthKit_Implementation.md`](./StepCounter_HealthKit_Implementation.md)),
  but not yet compiled, build-profile'd, or QA'd — no Mac/Xcode was
  available when it was written. Still effectively on the old raw-sensor
  path (see `StepCounter_Fix.md`) until that happens.
- ❌ Device QA matrix, store review prep — not started.

---

## 1. The abstraction: `IStepDataProvider`

`Assets/_Project/Scripts/Managers/IStepDataProvider.cs` defines the
platform-agnostic contract `DatabaseManager` codes against, instead of
knowing anything about Health Connect directly:

```csharp
public interface IStepDataProvider
{
    Task<StepCounterAvailability> CheckAvailabilityAsync();
    Task<StepCounterAvailability> RequestPermissionAsync();
    Task<long> GetStepsSinceAsync(DateTimeOffset since);
    bool OpenPlatformSettings();
}
```

- `CheckAvailabilityAsync` — is the provider usable right now (installed +
  permission already granted)?
- `RequestPermissionAsync` — shows the platform's own consent UI.
- `GetStepsSinceAsync` — "how many steps between this timestamp and now" —
  the core query that replaces the old raw-sensor diffing.
- `OpenPlatformSettings` — deep-links into the platform's own permission
  settings as a manual-fix fallback (added today, see §4).

`DatabaseManager.cs` picks an implementation once, per platform:

```csharp
private static IStepDataProvider CreateStepDataProvider()
{
#if UNITY_ANDROID && !UNITY_EDITOR
    return new HealthConnectStepProvider();
#else
    return null;
#endif
}
```

`null` means "no OS-level provider on this platform" — `DatabaseManager`
falls back to the legacy raw `StepCounter.current` sensor path (still the
*only* path on iOS/Editor today) via `HasStepDataProvider`. Once a
`HealthKitStepProvider` exists for iOS, this is the only line that needs to
change.

---

## 2. The Android native plugin — `HealthConnectBridge.androidlib`

Unity has no first-party Health Connect support, so this is a small native
Kotlin Android library module living at
`Assets/Plugins/Android/HealthConnectBridge.androidlib/`, built as part of
the app the same way `FirebaseApp.androidlib` is.

### `build.gradle`
```gradle
android {
    namespace 'dk.trainingbuddy.game.healthconnect'
    compileSdk 36        // connect-client:1.1.0 requires this
    defaultConfig {
        minSdkVersion 30
        targetSdkVersion 34
    }
}
dependencies {
    implementation 'androidx.health.connect:connect-client:1.1.0'
    implementation 'androidx.activity:activity-ktx:1.9.0'
    implementation 'org.jetbrains.kotlinx:kotlinx-coroutines-android:1.8.1'
}
```
No Kotlin Gradle plugin is applied — this project's AGP 9 has built-in Kotlin
support, and applying the old plugin on top breaks (see the comment in the
file).

### `src/main/AndroidManifest.xml` (library-local)
```xml
<queries>
    <package android:name="com.google.android.apps.healthdata" />
</queries>
<uses-permission android:name="android.permission.health.READ_STEPS" />
<application>
    <activity android:name="...HealthConnectPermissionActivity" ... />
</application>
```
- The `<queries>` block is required by Android 11+'s package-visibility
  rules — without it, `PackageManager` queries for the Health Connect app
  always report "not installed", even when it is.
- `READ_STEPS` is the actual data permission being requested.

### `HealthConnectBridge.kt` — the entry points

A Kotlin `object` (singleton) with `@JvmStatic` methods, called from C# via
`AndroidJavaClass("dk.trainingbuddy.game.healthconnect.HealthConnectBridge")`.
`@JvmStatic` is required — a plain Kotlin `object` only exposes instance
methods on its `INSTANCE` field to JNI, so `CallStatic()` wouldn't resolve
them otherwise.

| Method | Purpose |
|---|---|
| `isProviderInstalled(context)` | `HealthConnectClient.getSdkStatus(...) == SDK_AVAILABLE` |
| `checkAvailability(context, receiver)` | Not installed → `"notInstalled"`. Installed → reads `permissionController.getGrantedPermissions()` and reports `"available"`/`"permissionDenied"`. |
| `requestPermission(context, receiver)` | Launches `HealthConnectPermissionActivity` (see below) to run the actual consent flow. |
| `openHealthConnectSettings(context)` | Deep-links into Health Connect's own permission screen for this app (added today, see §4). |
| `getStepsSince(context, sinceEpochMillis, receiver)` | `client.aggregate(AggregateRequest(StepsRecord.COUNT_TOTAL, timeRange))` — the actual step query. |
| `getDailyStepsSince(context, days, receiver)` | `client.aggregateGroupByPeriod(AggregateGroupByPeriodRequest(..., timeRangeSlicer = Period.ofDays(1)))` — calendar-based (local-day) bucketing, distinct from `getStepsSince`'s fixed-duration total. Backs the daily-breakdown graph, see §9. |

Results are delivered back to C# through small callback interfaces —
`AvailabilityReceiver`/`StepsReceiver`/`DailyStepsReceiver` — implemented on
the C# side as `AndroidJavaProxy` (see §3).

### `HealthConnectPermissionActivity.kt` — why a whole extra Activity exists

Health Connect's permission request must go through the AndroidX **Activity
Result API** (`registerForActivityResult`), which only works inside
`onCreate()` of a `ComponentActivity`. Unity's `UnityPlayerActivity` doesn't
do this. Rather than modifying Unity's main activity, `requestPermission()`
launches this transparent, single-purpose, throwaway activity
(`Theme.Translucent.NoTitleBar`) that:
1. Registers the permission-result contract in `onCreate`.
2. Immediately launches the request for `STEPS_PERMISSION`.
3. On result, hands the granted/denied outcome back through
   `HealthConnectBridge.pendingPermissionReceiver` (a single-slot static —
   safe because Unity never issues a second request before the first one's
   transparent activity has finished) and calls `finish()`.

---

## 3. The C# wrapper — `HealthConnectStepProvider.cs`

Implements `IStepDataProvider`, wrapping every native call in
`AndroidJavaClass`/`AndroidJavaObject`. The interesting part is how async
results come back: C# passes an `AndroidJavaProxy` subclass as the
"receiver", and Kotlin calls `.onResult(...)` on it like any Java object.

```csharp
private class AvailabilityCallback : AndroidJavaProxy
{
    private readonly TaskCompletionSource<StepCounterAvailability> _tcs;
    public AvailabilityCallback(...) : base("dk.trainingbuddy.game.healthconnect.AvailabilityReceiver") { ... }

    // Must be named exactly onResult (lowercase, case-sensitive dispatch)
    public void onResult(string status) => _tcs.TrySetResult(status switch { ... });
}
```

This turns Kotlin's callback style into a normal `Task`-returning C# API
(`CheckAvailabilityAsync`, `RequestPermissionAsync`, `GetStepsSinceAsync`),
which is what lets `DatabaseManager` `await` it like anything else.

`CurrentActivity` is fetched fresh each call via
`AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity")`
— the same pattern used elsewhere in the project for native Android calls.

---

## 4. `DatabaseManager.cs` — how the provider is actually used

`StartStepCounter()` branches early on `_stepDataProvider`:

```csharp
StepCounterAvailability availability = _stepDataProvider != null
    ? await _stepDataProvider.CheckAvailabilityAsync()
    : await CheckStepCounterAvailabilityAsync(); // legacy sensor path
```

and picks which background loop to run:

```csharp
_ = _stepDataProvider != null
    ? ProviderSyncLoop(_stepDataProvider, _stepCts.Token)
    : StepCounterLoop(_stepCts.Token);
```

`ProviderSyncLoop` calls `SyncFromProviderAsync` every 60s
(`FirebaseSyncMs`) — much coarser than the legacy path's 2s sensor poll,
because Health Connect is a queryable aggregator, not a live stream; there's
nothing to poll quickly.

`SyncFromProviderAsync` is the actual sync logic:

```csharp
if (_lastSyncTimestampMillis <= 0)
{
    _lastSyncTimestampMillis = GetUnixTimestampMilliseconds(); // first-ever anchor, no backfill
}
else
{
    long earliestAllowed = GetUnixTimestampMilliseconds() - DeviceSyncMaxBacklogMillis; // 30 days
    long since = Math.Max(_lastSyncTimestampMillis, earliestAllowed);

    long delta = await provider.GetStepsSinceAsync(DateTimeOffset.FromUnixTimeMilliseconds(since));
    if (delta > MaxPlausibleStepsPerSync) delta = MaxPlausibleStepsPerSync; // 20,000 sanity clamp

    if (delta > 0) { _currentTotal += delta; StepCountChanged?.Invoke(_currentTotal); }
    _lastSyncTimestampMillis = GetUnixTimestampMilliseconds();
}

await WriteStepsToFirebaseAsync(new Dictionary<string, object>
{
    { $"deviceSync/{DeviceId}/lastSyncTimestamp", _lastSyncTimestampMillis }
});
```

This is where the anti-cheat fix (§6) lives — the 30-day cap and the
plausibility clamp were added there, and the Firebase field changed from a
single account-wide value to a per-device one.

---

## 5. The permission UI — `WelcomeScreen.cs`

Health Connect's consent status can't be read synchronously (unlike classic
OS runtime permissions), so the whole flow is async, gated by a
`PermissionOverlay` UI element:

- **`EnsurePermissionsAsync()`** — runs once on screen load. Checks OS
  runtime permission (`CheckPermission()`) *and* provider readiness
  together; if both are fine, hides the overlay. Otherwise shows it and
  immediately tries `RequestPermissionAsync()`.
- **`RequestPermissionAsync()`** — requests classic Android runtime
  permissions (`ACCESS_FINE_LOCATION`, `ACTIVITY_RECOGNITION`) *and*, if a
  step provider exists, the Health Connect permission
  (`RequestStepProviderPermissionAsync`). Only hides the overlay if both
  succeed.
- **`_permissionRequestFailedOnce`** — a specific gotcha this guards
  against: Health Connect's consent screen shows a **Steps toggle that
  defaults off**. Completing that screen without switching it on looks
  identical, from the app's side, to an outright denial. Rather than
  silently re-showing the same "Grant" button forever, the *second* failure
  reveals an "Open Settings" fallback button with a message telling the user
  explicitly to check the Steps toggle.
- **`OpenAppSettings()`** — what that fallback button does (see §6 for
  today's fix to it).

---

## 6. Bugs found in real-device testing today, and their fixes

Two things looked broken when actually testing on a device, even though the
provider/permission code above was already in place and "correct" in
isolation.

### 6a. Health Connect never showed the app as a participant at all

**Symptom:** Steps toggle was already on in Health Connect, but Health
Connect still didn't "recognize" the app — it never showed up in Health
Connect's own app list.

**Root cause:** declaring `<uses-permission android:name="android.permission.health.READ_STEPS">`
is *not* sufficient for Health Connect to register an app as a participant.
Health Connect (especially on Android 14+, where it's built into the OS
permission framework) only lists apps that also declare a
`ViewPermissionUsageActivity` — an `activity-alias` with specific
intent-filters that let Health Connect link into an app's permission-usage
screen.

**Fix** — added to `Assets/Plugins/Android/AndroidManifest.xml` (the main
app manifest, since it needs a real `targetActivity`):

```xml
<activity-alias
    android:name="ViewPermissionUsageActivity"
    android:exported="true"
    android:targetActivity="com.unity3d.player.UnityPlayerActivity"
    android:permission="android.permission.START_VIEW_PERMISSION_USAGE">
    <intent-filter>
        <action android:name="android.intent.action.VIEW_PERMISSION_USAGE" />
        <category android:name="android.intent.category.HEALTH_PERMISSIONS" />
    </intent-filter>
    <intent-filter>
        <action android:name="androidx.health.ACTION_SHOW_PERMISSIONS_RATIONALE" />
    </intent-filter>
</activity-alias>
```

The two intent-filters cover both eras: `VIEW_PERMISSION_USAGE` +
`HEALTH_PERMISSIONS` for Android 14+ (where Health Connect is a system
component), and `ACTION_SHOW_PERMISSIONS_RATIONALE` for older Health Connect
versions where it's still a separate Play Store app. It targets the app's
own launcher activity since there's no dedicated privacy-policy screen —
Health Connect just needs somewhere to resolve to, not a specific
destination.

**Important operational note:** this requires a **full uninstall before
reinstalling** to test — Android/Health Connect scan manifest permission
declarations at install time, and an in-place "build and run" over an
already-installed APK doesn't reliably force a rescan.

### 6b. "Open Settings" fallback opened the wrong settings entirely

**Symptom:** the fallback button (from §5) opened the OS's plain **App
info** screen for this app — which has no Health Connect section
whatsoever, so it could never actually fix a Health Connect permission
problem.

**Fix** — added `openHealthConnectSettings(context)` to
`HealthConnectBridge.kt`, deep-linking into Health Connect itself instead:

```kotlin
val intent = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
    Intent(HealthConnectManager.ACTION_MANAGE_HEALTH_PERMISSIONS)   // Android 14+: per-app screen
        .putExtra(Intent.EXTRA_PACKAGE_NAME, context.packageName)
} else {
    Intent(HealthConnectClient.ACTION_HEALTH_CONNECT_SETTINGS)      // 13 and below: general HC app
}
```
Falls back to `false` (caught `ActivityNotFoundException`) if Health Connect
itself isn't installed, in which case the C# side still falls back further
to the old "App info" intent.

Wired through the same layers as everything else:
`IStepDataProvider.OpenPlatformSettings()` →
`HealthConnectStepProvider.OpenPlatformSettings()` (calls the new bridge
method) → `DatabaseManager.OpenStepProviderSettings()` →
`WelcomeScreen.OpenAppSettings()` tries this first, only falling back to the
OS App info screen if there's no step provider or Health Connect can't be
opened.

---

## 7. Anti-cheat: per-device sync anchoring

**The gap:** `GetStepsSinceAsync` reads a device's *real, pre-existing*
Health Connect history — unlike the old live-sensor path, which had nothing
to read before the app started polling it. That makes a new exploit
possible: log an account into a device that already has months of Health
Connect step history, and the very first sync would credit that entire
backlog to the account (e.g. borrowing/using a friend's phone).

**The fix**, entirely in `DatabaseManager.cs`:

1. **Per-device Firebase field.** The single account-wide
   `users/{uid}/LastSyncTimestamp` became
   `users/{uid}/deviceSync/{deviceId}/lastSyncTimestamp`, where `deviceId`
   is `SystemInfo.deviceUniqueIdentifier` (sanitized for Firebase's
   forbidden key characters). Every device a given account logs into now
   gets its own independent anchor — a brand-new device always starts at
   "now" with zero backfill, exactly like a brand-new account always has.
2. **30-day cap** (`DeviceSyncMaxBacklogMillis`). Even for an already-seen
   device, a single sync can never reach further back than 30 days. This
   bounds the remaining "delayed harvest" exploit (log in once to anchor a
   friend's device, walk away, come back a month later to collect their
   month of steps) to at most 30 days' worth, rather than unlimited.
3. **Plausibility clamp** (`MaxPlausibleStepsPerSync = 20000`). Any single
   sync's delta is clamped at 20,000 steps as a cheap sanity backstop,
   independent of the above two.

This required no changes to `WriteStepsToFirebaseAsync`, `IStepDataProvider`,
or the native Kotlin bridge — it already supported arbitrary
slash-separated nested Firebase keys (the same trick `ArchiveDailyStepsAsync`
uses for `dailySteps/{date}`), so this was a pure `DatabaseManager.cs`
change. It also means it applies automatically to iOS once a
`HealthKitStepProvider` exists, with no extra work needed there.

**Deliberately not done:** server-side (Cloud Functions) validation. This
only defends against the legitimate app being misused via device-swapping —
a hostile client writing directly to Firebase, bypassing the app's own sync
logic, is a separate, larger trust-boundary problem that (per the scope doc)
can't be fully solved client-side anyway.

---

## 9. Daily-breakdown graph: querying providers directly

`ProfileScreen`'s weekly activity graph used to source its history purely
from Firebase's hand-maintained `dailySteps/{date}` buckets (written
universally by `WriteStepsToFirebaseAsync`/`ArchiveDailyStepsAsync`,
regardless of platform). `DatabaseManager.FetchDailyStepsAsync` now prefers
the provider when one exists:

```csharp
if (_stepDataProvider != null)
{
    var providerDays = await _stepDataProvider.GetDailyStepsAsync(days);
    string todayLocal = DateTime.Now.ToString("yyyy-MM-dd");
    // ...filter out todayLocal, return the rest
}
// else: unchanged Firebase-backed fallback (Editor / no provider)
```

Real semantics fix in the process: the Firebase buckets were **UTC-day**
keyed (`DateTime.UtcNow`); Health Connect's `aggregateGroupByPeriod` and
HealthKit's `HKStatisticsCollectionQuery` both bucket by the device's
**local calendar day**. `getDailyStepsSince`'s Kotlin implementation ends
its query range at local midnight *today* (not "now"), so it never returns
a same-day partial bucket at all — `GetDailyStepsAsync`'s contract is
"never includes today" on both platforms, which let `ProfileScreen`'s
`LoadActivityGraph` drop its old UTC-string comparison entirely (it was
there specifically to strip today's entry, which the provider path now
guarantees never appears).

**Deliberately conservative — read path only.** The Firebase
`dailySteps/{date}` writes are untouched and still happen on every
platform. They're redundant on Android/iOS now (nothing reads them there
anymore) but stay as a harmless fallback and historical record; removing
that write path is a separate, lower-risk future cleanup, not bundled here.

---

## 10. What's left

Straight from the scope doc's phasing, in order:

1. **Device QA matrix** for the Android path above — real devices across
   Android versions (Health Connect behaves differently pre/post API 34),
   including the manual test scenarios in this session's per-device
   anchoring plan (new-device isolation, 30-day cap, plausibility clamp),
   plus the new daily-breakdown query (§9).
2. **iOS: HealthKit provider** — code written (native Obj-C bridge, the
   `HealthKitStepProvider : IStepDataProvider`, and the Xcode
   capability/entitlement build-postprocessing), see
   [`StepCounter_HealthKit_Implementation.md`](./StepCounter_HealthKit_Implementation.md).
   Still needs: the iOS Build Profile + bundle identifier (doesn't exist in
   this project yet), an actual compile/build on a Mac, and on-device QA —
   none of that was possible in the environment this was written in.
3. **Store review prep** — privacy policy text, usage strings.

**Also flagged, unrelated to this migration but noticed along the way:**
`WelcomeScreen.cs`'s `OnTest` method has a hardcoded admin email/password
committed in plaintext. Worth removing/rotating independent of this work.