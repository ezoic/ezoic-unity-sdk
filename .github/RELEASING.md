# Releasing

One `vX.Y.Z` tag releases both packages in this repository at the same version:

- `com.ezoic.ads` installs straight from the tag
  (`https://github.com/ezoic/ezoic-unity-sdk.git#vX.Y.Z`).
- `com.ezoic.cmp-sdk` is published by `.github/workflows/release.yml`, which runs
  `git subtree split --prefix='com.ezoic.cmp-sdk~'` on the tag and pushes the result as the tag
  `cmp-sdk/vX.Y.Z` and the branch `upm/cmp-sdk`
  (`https://github.com/ezoic/ezoic-unity-sdk.git#cmp-sdk/vX.Y.Z`).

Published tags never move, on either name.

## Steps

1. The native SDKs are released first: `com.ezoic.sdk:ezoic-ads-sdk`,
   `com.ezoic.sdk:ezoic-consent-core` and `com.ezoic.sdk:ezoic-cmp-sdk` resolve from Maven
   Central, and `ezoic-swift-sdk-dist` has the tag of the iOS version. CI's `jni-audit`,
   `cmp-removal-android`, `cmp-removal-ios` and `swift-bridge` jobs need them. The Android and
   iOS native SDKs can have their own version numbers (Android 1.22.3 went with iOS 1.23.2;
   from 1.24.0 they share one); the packages' version `X.Y.Z` follows the Android one.
2. Bump `version` in `package.json` and `com.ezoic.cmp-sdk~/package.json`, the native pins in
   `Editor/EzoicDependencies.xml`, `com.ezoic.cmp-sdk~/Editor/EzoicCmpDependencies.xml` and
   `tools/swift-typecheck/Package.swift`, the install URLs in both READMEs, and both CHANGELOGs.
   `python3 tools/validate-packages.py` must pass.
3. Merge to `master` with CI green, then push the tag `vX.Y.Z` on the merge commit.
4. Check the **Release** workflow run: it validates the split tree and pushes `cmp-sdk/vX.Y.Z`.

## Verify in a real Unity Editor before announcing

CI type-checks the C# against Unity's managed assemblies and the Swift bridges against the dist
package, but no job opens Unity. Before announcing a release, in **Unity 2021.3 LTS** and
**Unity 6000.x**, each with the External Dependency Manager (Swift Package Manager Integration
on):

1. Install both packages from their release URLs (`#vX.Y.Z` and `#cmp-sdk/vX.Y.Z`) and confirm
   the Package Manager shows **Ezoic Ads** and **Ezoic CMP** at `X.Y.Z` with no import warnings
   (a missing `.meta` shows up here).
2. Let the project import and compile with no errors, including a script that uses both
   `Ezoic.Ads` and `Ezoic.Cmp` with qualified consent types.
3. Import and run each package's sample in the editor (calls are no-ops there; nothing throws).
4. Export one Android build and one iOS build for each of the three cases, and confirm each
   builds (Gradle for Android, Xcode for iOS) and starts on a device or simulator:
   - `com.ezoic.ads` alone
   - `com.ezoic.cmp-sdk` alone
   - both packages together: one `ezoic-cmp-sdk` in the Gradle dependencies, one
     `ezoic-swift-sdk-dist` package reference in the Xcode project with both the `EzoicAdsSDK`
     and `EzoicCmpSDK` products linked to `UnityFramework`.
   - `com.ezoic.ads` alone with **Include Ezoic CMP** off (Project Settings → Ezoic):
     `unityLibrary/build.gradle` and `launcher/build.gradle` end with the `ezoic-cmp-sdk`
     exclude block and the APK has no `com/ezoic/ads/sdk/privacy/cmp/EzoicCmp` class; the Xcode
     project links `EzoicAdsSDKWithoutCMP` (not `EzoicAdsSDK`) to `UnityFramework`; on both,
     the sample logs "Ezoic CMP included: False", and `True` with the setting back on (a
     **Replace** build). Building again into the same folder (Append) changes nothing.
   - **Include Ezoic CMP** off with `com.ezoic.cmp-sdk` installed: the build stops with the
     error that names the standalone package.
