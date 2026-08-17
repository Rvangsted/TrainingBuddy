# Step Counter Migration — HealthKit Implementation (iOS)

Companion to
[`StepCounter_HealthConnect_Implementation.md`](./StepCounter_HealthConnect_Implementation.md)
(Android) and [`StepCounter_HealthPlatform_Migration_Scope.md`](./StepCounter_HealthPlatform_Migration_Scope.md)
(the original plan). This covers the iOS side: a native HealthKit bridge, a
`HealthKitStepProvider : IStepDataProvider`, and the Xcode build-postprocessing
HealthKit needs.

**Status: code-complete, not yet build-profile'd or QA'd.** This project has
no iOS Build Profile or bundle identifier configured yet (Android-only so
far) and everything below was written without a Mac/Xcode available to
compile or run it — see "What's left" at the bottom before assuming this
works on a device.

---

## 1. The native bridge — `Assets/Plugins/iOS/HealthKitBridge.mm`

Mirrors `HealthConnectBridge.kt`'s shape, but as `extern "C"` functions
callable via `[DllImport("__Internal")]` — the same convention Unity's own
Input System uses for its CoreMotion pedometer bridge
(`iOSStepCounter.mm`, in the Input System package cache, not this repo).

| Function | Purpose |
|---|---|
| `_HealthKit_IsAvailable()` | `[HKHealthStore isHealthDataAvailable]` |
| `_HealthKit_RequestAuthorization(requestId, callback)` | `requestAuthorizationToShareTypes:nil readTypes:{stepCount}` |
| `_HealthKit_QueryStepsSince(requestId, sinceUnixMillis, callback)` | `HKStatisticsQuery` with `HKStatisticsOptionCumulativeSum` over `[HKQuery predicateForSamplesWithStartDate:endDate:options:]` |
| `_HealthKit_OpenSettings()` | Opens `UIApplicationOpenSettingsURLString` — this app's own Settings page, which on iOS *is* where Health access lives (unlike Android's plain App Info screen) |

All async callbacks dispatch back onto the main queue before invoking the
function pointer.

## 2. The C# wrapper — `HealthKitStepProvider.cs`

Same `IStepDataProvider` shape as `HealthConnectStepProvider.cs`, but the
callback mechanism is necessarily different: HealthKit's native completion
handlers must call back into a **static** `[MonoPInvokeCallback]` method
(IL2CPP/AOT can only marshal a static method group to a raw native function
pointer, not a closure) — there's no per-call object like Android's
`AndroidJavaProxy` to carry state. Instead, each call gets a monotonically
increasing `requestId`, stashed in a static `Dictionary<int, TaskCompletionSource<T>>`,
resolved by the static callback looking the id back up. Same correlation
pattern the Input System's own `iOSStepCounter.cs` uses via `deviceId`.

**The HealthKit permission-opacity gotcha, and how it shapes this code:**
Apple deliberately does not let an app reliably distinguish "read access
denied" from "read access granted but zero samples" for read-only types.
So:
- `CheckAvailabilityAsync()` reports `Available` whenever HealthKit exists
  on the device at all — not based on a permission check Apple won't
  answer truthfully.
- `RequestPermissionAsync()` reports `Available` once the authorization
  sheet completes, regardless of what the user actually picked; only
  reports `PermissionDenied` if the request itself failed to present.
- `GetStepsSinceAsync()` is the real signal — a persistently empty result
  is what actually indicates no access, per the scope doc's documented
  industry-standard workaround.

One consequence: `WelcomeScreen.cs`'s existing Danish `PermissionDenied`
message specifically mentions "Health Connect" by name. With the semantics
above, iOS essentially never reaches that state in normal operation, so
that Android-specific copy won't incorrectly show there — no
`WelcomeScreen.cs` changes were needed for this pass.

## 3. Build-postprocessing — `Assets/_Project/Scripts/Editor/iOSHealthKitPostProcessor.cs`

A `[PostProcessBuild]` callback that patches the generated Xcode project:

1. Sets `NSHealthShareUsageDescription` in `Info.plist` (Apple reviews this
   text specifically — check the drafted Danish wording before any store
   submission).
2. Links `HealthKit.framework` — on **`GetUnityFrameworkTargetGuid()`**,
   since Unity 2019.3+'s generated iOS project compiles native plugin code
   (`Assets/Plugins/iOS/*.mm`) into the embedded `UnityFramework` target,
   not the app wrapper.
3. Adds the HealthKit capability/entitlement via `ProjectCapabilityManager`
   on **`GetUnityMainTargetGuid()`** — entitlements are an app-level
   concept, so this is the other target from #2. Getting this split wrong
   is the most common mistake in this kind of script.

This needed its own Editor-only assembly definition
(`Assets/_Project/Scripts/Editor/TrainingBuddy.Editor.asmdef`,
`includePlatforms: ["Editor"]`) — the existing `GameLogic.asmdef` at
`Assets/_Project/Scripts/` has `includePlatforms: []` (all platforms) and
would otherwise have pulled this file into player builds too, where
`UnityEditor.iOS.Xcode` doesn't exist.

## 4. `DatabaseManager.cs` wiring

`CreateStepDataProvider()` gained an iOS branch:
```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
    return new HealthConnectStepProvider();
#elif UNITY_IOS && !UNITY_EDITOR
    return new HealthKitStepProvider();
#else
    return null;
#endif
```
Nothing else needed to change — `ProviderSyncLoop`/`SyncFromProviderAsync`
(including the per-device anchoring, 30-day cap, and plausibility clamp) are
already platform-agnostic. Side benefit: once `_stepDataProvider` is
non-null on iOS, the legacy `CheckStepCounterAvailabilityAsync()` path
(which checks the Android-specific `"android.permission.ACTIVITY_RECOGNITION"`
string) stops running on real iOS devices entirely, same as it already does
on Android.

## What's left

None of this has been compiled or run — there's no Unity Editor or Mac/Xcode
in the environment this was written in. Before trusting it:

1. Open the project in Unity, confirm the Console shows no compile errors
   in `HealthKitStepProvider.cs`, `iOSHealthKitPostProcessor.cs`, or the
   `DatabaseManager.cs` edit.
2. Set up the iOS Build Profile and bundle identifier (not done — this
   project only has an Android Build Profile today), then build for iOS to
   generate the Xcode project.
3. In Xcode: confirm HealthKit appears under Signing & Capabilities on the
   main app target, `HealthKit.framework` is linked under `UnityFramework`,
   and `Info.plist` has the usage string.
4. Run on a real device (or Simulator with manually seeded Health app
   data). Confirm the authorization sheet appears, `GetStepsSinceAsync`
   returns real data matching the Health app, and "Open Settings" lands on
   this app's Settings page.
5. Device QA matrix, store review prep, and the daily-breakdown UI
   migration are still entirely open — see the scope doc's phasing.
