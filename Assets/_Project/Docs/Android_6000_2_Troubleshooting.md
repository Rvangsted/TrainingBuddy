# Unity 6000.2 Android Build Remediation Guide

This guide consolidates the device log errors gathered from `AndroidLog.txt` and describes how to fix them in a Unity 6000.2 project. Work through the sections in order—each addresses a distinct class of errors that caused install or runtime failures on the target device.

## 1. Rebuild a clean, full APK
1. **Disable Apply Changes / incremental packaging**
    - In Unity, open **File ▸ Build Settings** and make sure *Build and Run* targets **Android**.
    - Disable "Scripts Only Build" and the **App Bundle (Google Play)** option (if you need an AAB later, rebuild it after completing these fixes).
    - Close Android Studio or stop the "Apply Changes" session if you used it earlier.
2. **Remove leftover Gradle caches**
    - From a terminal run:
      ```bash
      ./gradlew --stop
      rm -rf Library/Bee AndroidBuild/gradlebuild gradle-user-home/.gradle
      ```
      (adjust paths if you keep a custom `gradle-user-home`).
3. **Perform a clean build**
    - Back in **Build Settings**, click **Build** and save the APK into a fresh folder such as `Builds/Android/Clean/`.
    - After the build completes, install it with `adb install -r <apk>` instead of incremental deployment.
4. **Reset pre-compiled code on the device**
    - Run `adb shell cmd package compile --reset dk.trainingbuddy.game` so ART rebuilds the profile that previously failed to prepare.

## 2. Restore manifest attributes and metadata
Unity lets you override AndroidManifest entries via Gradle templates.

1. **Enable the manifest template**
    - In Unity, go to **Project Settings ▸ Player ▸ Android ▸ Publishing Settings** and check **Custom Main Manifest**.
    - Unity creates `Assets/Plugins/Android/AndroidManifest.xml` (copy from `Editor/Data/PlaybackEngines/AndroidPlayer/Apk/AndroidManifest.xml` if missing).
2. **Reintroduce the install location**
    - Add the attribute to the `<application>` node:
      ```xml
      <manifest ...>
        <application
          android:installLocation="auto"
          ...>
      ```
      Choose `internalOnly`, `auto`, or `preferExternal` per your storage strategy.
3. **Add missing accessory metadata**
    - Inside the `<application>` node insert:
      ```xml
      <meta-data
          android:name="AccessoryServicesLocation"
          android:value="@xml/accessory_services" />
      ```
    - Create `Assets/Plugins/Android/res/xml/accessory_services.xml` with the descriptors required by your wearable integration, or remove SDK code that expects this metadata.
4. **Remove unsupported runtime flags**
    - Ensure `<application android:debuggable="false" tools:remove="debuggable"/>` is set for release.
    - Delete custom `android:appComponentFactory` or `android:useEmbeddedDex` overrides unless you explicitly require them.

## 3. Ship the resources you reference
The runtime error `No package ID 6a found for ID 0x6a0b000f` means Unity referenced a resource that never made it into the merged APK.

1. **Track down the missing resource**
    - Use `aapt2 dump resources <apk>` and search for `0x6a0b000f` to learn which asset is missing.
    - In Unity, check any custom render pipeline, skin, or remote configuration code that loads resources by name.
2. **Ensure resource folders are included**
    - Assets under `Assets/Plugins/Android/res/` must follow the Android folder naming scheme (`values/`, `drawable/`, etc.).
    - If you rely on AAssetPacks or Addressables, confirm the asset is marked for the Android platform and is downloaded before use.
3. **Rebuild and verify**
    - After fixes, rebuild the APK and run `aapt2 dump resources` again to confirm the package ID resolves.

## 4. Guard vendor-specific integrations
The logs showed repeated errors referencing `com.oplus.*`, `Athena`, `ROMUpdateEngine`, and other ColorOS hooks that are unavailable on stock Android.

1. **Wrap vendor calls in availability checks**
    - In C#, look for reflection or AndroidJavaObject usages targeting OPlus/ColorOS classes. Only execute them when `AndroidJavaClass("android.os.Build").GetStatic<string>("MANUFACTURER")` matches the vendor.
2. **Disable optional services**
    - If you ship SDKs for vendor-specific watch or theme services, move their initialization behind a runtime toggle or remove the AARs from `Assets/Plugins/Android/`.
3. **NFC routing**
    - If the game does not use NFC, delete any `NfcAdapter` registration in your plugins. Otherwise, configure the secure element AID list in `res/xml/nfc_apduservice.xml` and declare it via `<meta-data>` as required by your payment provider.

## 5. Fix graphics pipeline and input stalls
Unity froze at startup because required files could not be opened and SurfaceFlinger could not synchronize frames.

1. **Verify streaming assets and permissions**
    - Check `Assets/StreamingAssets/` for any file referenced during boot. On Android 13+ you must use scoped storage APIs—do not read from absolute `/sdcard/` paths without the `READ_MEDIA_*` permissions.
2. **Shorten your startup work**
    - Profile with the Unity Profiler attached to the Android player. Ensure expensive asset loads run on background threads and avoid blocking calls on the main thread before the first frame.
3. **Check Unity Player Settings**
    - Under **Player ▸ Resolution and Presentation**, enable **Optimized Frame Pacing** and make sure **Render Outside Safe Area** and **Start in Fullscreen Mode** are configured for your device. Disable experimental render backends unless required.

## 6. Clean up analytics and package metadata
1. **Reconcile Dex/ART metadata after install**
    - After installing a new build run:
      ```bash
      adb shell cmd package reconcile-secondary-dex-files dk.trainingbuddy.game
      ```
      to refresh package info and remove stale dex records.
2. **Reset vendor performance services**
    - If bundled SDKs (e.g., OPlus GamePerf) log negative counters, configure them with valid session IDs or disable their initialization in production builds until the vendor provides updated binaries.

## 7. Validation checklist
After applying all fixes:

- [ ] Build a full release APK from Unity 6000.2 without incremental deployment.
- [ ] Install with `adb install -r` and confirm there are no installer warnings in `adb logcat`.
- [ ] Verify the manifest contains the expected metadata and no unexpected debug/runtime flags.
- [ ] Run the app and confirm logcat has no `E/` entries for missing resources or vendor class lookups.
- [ ] Ensure rendering starts within a few seconds and input is responsive.
- [ ] Re-run `aapt2 dump badging <apk>` to confirm metadata integrity.

Completing this checklist should eliminate the installation, manifest, vendor hook, and rendering errors observed in the original logs.