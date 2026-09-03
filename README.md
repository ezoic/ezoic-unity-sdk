# Ezoic Ads SDK for Unity

Monetize your Unity game with Ezoic banner, interstitial, and rewarded ads through a
simple C# API. This package drives the native Ezoic Ads SDK, so your game code stays
platform-agnostic.

- **Package name:** `com.ezoic.ads`
- **Namespace:** `Ezoic.Ads`
- **Minimum Unity:** 2021.3 LTS

## Platform support

| Platform | Status |
| --- | --- |
| Android | Supported |
| iOS | Supported (iOS 14+) |
| Editor / other platforms | API is callable and safe (see [Editor behavior](#editor-and-unsupported-platforms)) |

On Android the API calls the native `com.ezoic.sdk:ezoic-ads-sdk` library; on iOS it calls the
native `EzoicAdsSDK` CocoaPod. On every other platform the same calls compile and run as safe
no-ops, so you can develop and play in the editor without any platform guards in your game code.

## Installation

### 1. Add the package (UPM git URL)

In Unity, open **Window → Package Manager → + → Add package from git URL…** and enter:

```
https://github.com/ezoic/ezoic-unity-sdk.git#v1.0.1
```

Or add it to your project's `Packages/manifest.json` dependencies:

```json
{
  "dependencies": {
    "com.ezoic.ads": "https://github.com/ezoic/ezoic-unity-sdk.git#v1.0.1"
  }
}
```

The `#v1.0.1` suffix pins a released version; omit it to track the default branch.

### 2. Provide the native Android library

The Android ads run on the native `com.ezoic.sdk:ezoic-ads-sdk:1.6.1` library, which is
published on Maven Central. You need to make that dependency available to your Android build
in **one** of two ways.

**Option A — External Dependency Manager for Unity (EDM4U), recommended.**
Install [EDM4U](https://github.com/googlesamples/unity-jar-resolver). This package ships an
`Editor/EzoicDependencies.xml` manifest, so EDM4U's Android Resolver automatically adds the
native library (from Maven Central) to your generated Gradle build. No further action needed.

**Option B — Manual Gradle dependency.**
If you do not use EDM4U, enable **Custom Main Gradle Template** under
**Project Settings → Player → Android → Publishing Settings**, then add the dependency and the
Maven Central repository to the generated `Assets/Plugins/Android/mainTemplate.gradle`:

```gradle
dependencies {
    implementation 'com.ezoic.sdk:ezoic-ads-sdk:1.6.1'
}
```

Make sure `mavenCentral()` is listed in your project's repositories (in
`settingsTemplate.gradle` or the app `build.gradle`, depending on your Gradle setup).

### 3. Configure the Android manifest (Google Ad Manager app ID)

The native SDK serves through Google Ad Manager, which requires your app ID in the Android
manifest. Enable **Custom Main Manifest** under
**Project Settings → Player → Android → Publishing Settings** and add the `meta-data` entry
inside the `<application>` tag of `Assets/Plugins/Android/AndroidManifest.xml`:

```xml
<manifest>
  <application>
    <meta-data
        android:name="com.google.android.gms.ads.APPLICATION_ID"
        android:value="ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY" />
  </application>
</manifest>
```

Replace the value with the Google Ad Manager application ID provided for your account. A
missing or incorrect app ID causes the app to crash on start.

### 4. Provide the native iOS library

iOS ads run on the native `EzoicAdsSDK` CocoaPod (requires **iOS 14+**). Unity automatically
adds this package's Swift bridge (`Runtime/Plugins/iOS/EzoicAdsUnityBridge.swift`) to the
generated Xcode project as a native plugin — you do not copy or configure any source yourself.
You only need to make the pod available to the generated Xcode project in **one** of two ways.

**Option A — External Dependency Manager for Unity (EDM4U), recommended.**
Install [EDM4U](https://github.com/googlesamples/unity-jar-resolver). This package ships an
`Editor/EzoicDependencies.xml` manifest, so EDM4U's iOS Resolver automatically adds
`pod 'EzoicAdsSDK', '~> 1.6.1'` to the generated Xcode project's `Podfile` and runs
`pod install`. No further action needed.

**Option B — Manual Podfile line.**
If you do not use EDM4U, build the Xcode project from Unity, then add the pod to the generated
`Podfile` in the Xcode output directory and run `pod install`:

```ruby
target 'UnityFramework' do
  pod 'EzoicAdsSDK', '~> 1.6.1'
end
```

Set the iOS Deployment Target to **14.0 or higher** under
**Project Settings → Player → iOS → Other Settings → Target minimum iOS Version**.

## Quick start

All API members are safe to call from the Unity main thread. Make your **first** call
(usually `EzoicAds.Initialize`) from the main thread — for example from a `MonoBehaviour`'s
`Start` or `Awake`. Ad event callbacks are always delivered on the Unity main thread, so it is
safe to touch `GameObject`s, UI, and scene state directly from them.

### Initialize

Initialize once, early in your app's lifecycle, with your Ezoic-registered domain:

```csharp
using Ezoic.Ads;
using UnityEngine;

public class AdsBootstrap : MonoBehaviour
{
    void Start()
    {
        EzoicAds.Initialize("example.com", (success, error) =>
        {
            if (success)
            {
                Debug.Log("Ezoic Ads initialized. SDK version: " + EzoicAds.Version);
            }
            else
            {
                Debug.LogWarning("Ezoic Ads init failed: " + error);
            }
        });
    }
}
```

Privacy and consent helpers can be called any time after initialization:

```csharp
EzoicAds.SetGDPRConsent(true, tcfConsentString);
EzoicAds.SetGPPConsent(gppString, "7");   // applicable GPP section IDs
EzoicAds.SetSubjectToCOPPA(false);
EzoicAds.TrackPageview();                  // signal a new screen / pageview
```

### Banner ad

```csharp
using Ezoic.Ads;

// adaptive banner anchored to the bottom of the screen (pass a size like "320x50" for fixed)
var banner = new EzoicBannerAd(adUnitId: 12345, position: BannerPosition.Bottom);

banner.OnLoaded     += () => banner.Show();
banner.OnLoadFailed += error => Debug.LogWarning("Banner failed: " + error);
banner.OnClicked    += () => Debug.Log("Banner clicked");
banner.OnImpression += () => Debug.Log("Banner impression");

banner.Load();

// later: banner.Hide();  banner.Show();
// when done: banner.Destroy();
```

`BannerPosition` supports `Top`, `Bottom`, `TopLeft`, `TopRight`, `BottomLeft`,
`BottomRight`, and `Center`.

Always call `banner.Destroy()` when you are done with a banner; skipping it leaks the native
ad object, since the managed and native peers hold a cross-heap reference cycle that neither
garbage collector can collect on its own.

### Interstitial ad

Interstitials use a static load pattern that hands you a ready instance:

```csharp
using Ezoic.Ads;

EzoicInterstitialAd.Load(
    adUnitId: 23456,
    onLoaded: ad =>
    {
        ad.OnShown        += () => Debug.Log("Interstitial shown");
        ad.OnDismissed    += () => Debug.Log("Interstitial dismissed");
        ad.OnFailedToShow += error => Debug.LogWarning("Show failed: " + error);
        ad.OnImpression   += () => Debug.Log("Interstitial impression");
        ad.OnClicked      += () => Debug.Log("Interstitial clicked");

        if (ad.IsLoaded)
        {
            ad.Show();
        }
        // when finished with it: ad.Destroy();
    },
    onFailed: error => Debug.LogWarning("Interstitial load failed: " + error));
```

Always call `ad.Destroy()` when you are done with an interstitial; skipping it leaks the native
ad object, since the managed and native peers hold a cross-heap reference cycle that neither
garbage collector can collect on its own.

### Rewarded ad

Rewarded ads have the same shape as interstitials, plus an earned-reward event:

```csharp
using Ezoic.Ads;

EzoicRewardedAd.Load(
    adUnitId: 34567,
    onLoaded: ad =>
    {
        ad.OnUserEarnedReward += (type, amount) =>
            Debug.Log($"Reward earned: {amount} {type}");
        ad.OnShown        += () => Debug.Log("Rewarded shown");
        ad.OnDismissed    += () => Debug.Log("Rewarded dismissed");
        ad.OnFailedToShow += error => Debug.LogWarning("Show failed: " + error);
        ad.OnImpression   += () => Debug.Log("Rewarded impression");
        ad.OnClicked      += () => Debug.Log("Rewarded clicked");

        if (ad.IsLoaded)
        {
            ad.Show();
        }
        // when finished with it: ad.Destroy();
    },
    onFailed: error => Debug.LogWarning("Rewarded load failed: " + error));
```

Always call `ad.Destroy()` when you are done with a rewarded ad; skipping it leaks the native
ad object, since the managed and native peers hold a cross-heap reference cycle that neither
garbage collector can collect on its own.

## Samples

The package ships a **Basic Integration** sample: a single `MonoBehaviour` with an
IMGUI control panel that initializes the SDK and loads, shows, hides, and destroys
banner, interstitial, and rewarded ads while logging every ad event on screen.

To import it, open **Window → Package Manager**, select **Ezoic Ads** in the
package list, open the **Samples** tab, and click **Import** next to
*Basic Integration*. Then attach the imported `EzoicAdsDemo` script to a
`GameObject` in an empty scene, set your domain and ad unit ids in the Inspector,
and build to a device. See the sample's own README for details.

## Editor and unsupported platforms

You do not need to guard Ezoic calls with `#if UNITY_ANDROID`. On the Unity editor and any
platform that is not yet supported:

- Every method is callable and never throws.
- The SDK logs a single one-line warning that the platform is unsupported.
- Load calls (`EzoicBannerAd.Load`, `EzoicInterstitialAd.Load`, `EzoicRewardedAd.Load`) and
  `EzoicAds.Initialize` deliver their failure callback asynchronously, so your event wiring and
  game flow behave the same as on device — you just get a failure instead of a fill.
- `EzoicAds.IsInitialized` is `false` and `EzoicAds.Version` is an empty string.

## API reference

### `EzoicAds` (static)

| Member | Description |
| --- | --- |
| `Initialize(string domain, Action<bool, string> onComplete = null)` | Initialize the SDK for your domain. The callback reports `(success, error)`. |
| `IsInitialized` | `true` once initialization succeeds. |
| `TrackPageview()` | Signal a new pageview / screen. |
| `SetGDPRConsent(bool applies, string consentString)` | Set GDPR/TCF consent. |
| `SetGPPConsent(string gppString, string sectionIds)` | Set GPP consent. |
| `SetSubjectToCOPPA(bool subject)` | Flag child-directed treatment. |
| `Version` | Native SDK version string (empty on unsupported platforms). |

### `EzoicBannerAd`

Constructor `EzoicBannerAd(int adUnitId, BannerPosition position, string size = null)`
(`size` such as `"320x50"`; `null` requests an adaptive banner). Methods: `Load()`, `Show()`,
`Hide()`, `Destroy()`. Events: `OnLoaded`, `OnLoadFailed(string)`, `OnClicked`, `OnImpression`.

### `EzoicInterstitialAd`

Static `Load(int adUnitId, Action<EzoicInterstitialAd> onLoaded, Action<string> onFailed)`.
Instance members: `Show()`, `Destroy()`, `IsLoaded`. Events: `OnShown`, `OnFailedToShow(string)`,
`OnDismissed`, `OnImpression`, `OnClicked`.

### `EzoicRewardedAd`

Same shape as `EzoicInterstitialAd`, plus `OnUserEarnedReward(string type, int amount)`.

## License

See [LICENSE](LICENSE). Copyright (c) 2026 Ezoic Inc.
