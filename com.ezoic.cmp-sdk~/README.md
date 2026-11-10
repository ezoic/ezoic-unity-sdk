# Ezoic CMP for Unity

Ezoic's consent management (CMP) for Unity apps of **Ezoic publishers** that don't use the
Ezoic Ads package. It shows the IAB TCF consent dialog to users in GDPR regions and the
privacy notice and "Do Not Sell" choice to users in covered US states, driven by your Ezoic
settings for your domain, and writes the standard `IABTCF_*` / `IABGPP_*` values that ad and
analytics SDKs read.

- **Package name:** `com.ezoic.cmp-sdk`
- **Namespace:** `Ezoic.Cmp`
- **Minimum Unity:** 2021.3 LTS

> **Use one package.** The Ezoic Ads package (`com.ezoic.ads`) already includes this CMP
> through `EzoicAds`, with the same consent methods. If your app shows Ezoic ads, use
> `EzoicAds` and don't add this package. Installing both works (each native library is linked
> once), but `EzoicAds` then takes the CMP over.

The CMP is for Ezoic publishers only: Ezoic refuses domains it doesn't know, and the CMP then
stays off (every request reports `Failed`, nothing is written).

## Platform support

| Platform | Status |
| --- | --- |
| Android | Supported (Minimum API Level 24) — native `com.ezoic.sdk:ezoic-cmp-sdk:1.22.0` |
| iOS | Supported (iOS 15+, Xcode 26.2+) — `EzoicCmpSDK` product of the `ezoic-swift-sdk-dist` Swift package 1.22.0 (up to next major) |
| Editor / other platforms | Calls are safe no-ops: requests report `Failed`, getters return `null` |

## Installation

### 1. Add the package (UPM git URL)

The package lives in the `com.ezoic.cmp-sdk~` folder of the
[ezoic-unity-sdk](https://github.com/ezoic/ezoic-unity-sdk) repository. In Unity, open
**Window → Package Manager → + → Add package from git URL…** and enter:

```
https://github.com/ezoic/ezoic-unity-sdk.git?path=/com.ezoic.cmp-sdk~#v1.22.0
```

Or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.ezoic.cmp-sdk": "https://github.com/ezoic/ezoic-unity-sdk.git?path=/com.ezoic.cmp-sdk~#v1.22.0"
  }
}
```

### 2. Native libraries

**With the External Dependency Manager (recommended).** Install Unity's
[External Dependency Manager](https://docs.unity3d.com/Packages/com.unity.external-dependency-manager@2.1/manual/index.html)
2.1+ or Google's [EDM4U](https://github.com/googlesamples/unity-jar-resolver) 1.2.189+.
This package's `Editor/EzoicCmpDependencies.xml` makes the Android Resolver add
`com.ezoic.sdk:ezoic-cmp-sdk` (Maven Central) to your Gradle build, and the iOS Resolver add the
`https://github.com/ezoic/ezoic-swift-sdk-dist.git` Swift package (up to next major from
1.22.0) with its `EzoicCmpSDK` product to `UnityFramework`. Keep **Swift Package Manager
Integration** on in the iOS Resolver settings; there is no CocoaPod.

**Without EDM.** Android: enable **Custom Main Gradle Template** and add
`implementation 'com.ezoic.sdk:ezoic-cmp-sdk:1.22.0'` (with `mavenCentral()` in your
repositories). iOS: in the generated Xcode project choose **File → Add Package
Dependencies…**, enter `https://github.com/ezoic/ezoic-swift-sdk-dist.git`, **Up to Next
Major Version** from `1.22.0`, and add the `EzoicCmpSDK` product to the `UnityFramework`
target. A **Replace** build from Unity drops the package, so use **Append** or add it again.

Set the iOS Deployment Target to **15.0 or higher** and the Android **Minimum API Level** to 24
or higher.

## Quick start

Make your first call from the Unity main thread (e.g. a `MonoBehaviour`'s `Start`). Results are
delivered on the Unity main thread.

```csharp
using Ezoic.Cmp;
using UnityEngine;

public class ConsentBootstrap : MonoBehaviour
{
    void Start()
    {
        EzoicCmp.Initialize("example.com");

        // Waits for the CMP's settings, then shows the dialog only where it is required.
        EzoicCmp.PresentConsentIfRequired(result =>
        {
            // result.Outcome: NotRequired, AlreadyDecided, Decided (see result.Decision),
            // Dismissed (ask again later), AlreadyPresenting, or Failed (see result.Error).
            Debug.Log("Consent: " + result);
        });
    }

    // "Privacy choices" button handler. Show the button while
    // EzoicCmp.IsPrivacyOptionsRequired == true.
    public void OnPrivacyChoices()
    {
        EzoicCmp.PresentConsentSettings(result => Debug.Log("Privacy settings: " + result));
    }
}
```

- **GDPR regions:** `PresentConsentIfRequired` shows the TCF dialog until the user decides.
  `PresentConsentSettings` reopens it; TCF requires that entry point to be always reachable.
- **Covered US states:** the CMP shows a privacy notice card by itself, once per launch until
  the user chooses; nothing to call. `PresentConsentIfRequired` reports `NotRequired`, and
  `PresentConsentSettings` opens the US privacy settings: `Decided` with `AcceptAll` = not
  opted out, `Decided` with `RejectAll` = opted out ("Do Not Sell"), `Dismissed` = closed
  without saving.
- **Elsewhere,** or when the CMP is off, both report `NotRequired`.

Options:

```csharp
EzoicCmp.Initialize("example.com", new EzoicCmpConfiguration
{
    CmpEnabled = true,       // run the CMP (default true)
    AutoReadConsent = true,  // false: your app manages consent; the CMP writes nothing
    DebugEnabled = false,    // also log the native CMP's debug/info lines
});
```

## API reference

### `EzoicCmp` (static)

| Member | Description |
| --- | --- |
| `Initialize(string domain)` / `Initialize(string domain, EzoicCmpConfiguration configuration)` | Start the CMP for your Ezoic domain. Returns at once; settings load in the background. Repeat calls are ignored. |
| `PresentConsentIfRequired(Action<ConsentResult> onComplete = null)` | Show the consent dialog if this user must decide (GDPR). Call once per launch after `Initialize`. |
| `PresentConsentSettings(Action<ConsentResult> onComplete = null)` | "Privacy settings": TCF dialog in GDPR regions, US privacy settings in covered US states. |
| `IsConsentRequired` | `bool?`: GDPR applies and the CMP handles consent. `null` while unknown. |
| `IsPrivacyOptionsRequired` | `bool?`: show a "Privacy choices" entry point (GDPR region or covered US state). `null` while unknown. |
| `ResetConsent()` | Delete the stored decision and the US choice; values another CMP wrote are kept. |

`ConsentResult` has `Outcome` (`ConsentOutcome`: `NotRequired`, `AlreadyDecided`, `Decided`,
`Dismissed`, `AlreadyPresenting`, `Failed`), `Decision` (`ConsentDecision`: `None`,
`AcceptAll`, `RejectAll`, `Custom`) and `Error` (set when `Outcome` is `Failed`). These match
`Ezoic.Ads.ConsentResult` in `com.ezoic.ads`.

## Samples

Import **Basic Consent** from the package's **Samples** tab in the Package Manager: one
`MonoBehaviour` with buttons for every call and an on-screen log.

## License

See [LICENSE](LICENSE). Copyright (c) 2026 Ezoic Inc.
