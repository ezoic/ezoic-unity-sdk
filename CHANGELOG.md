# Changelog

All notable changes to the Ezoic Ads Unity SDK are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.1] - 2026-07-06

### Fixed

- `EzoicMainThreadDispatcher.Init()` called `DontDestroyOnLoad` unconditionally,
  which throws `InvalidOperationException` outside play mode. Any first SDK call
  from an editor script or EditMode test crashed instead of being a safe no-op as
  documented. `DontDestroyOnLoad` is now only applied while playing.

## [1.0.0] - 2026-07-05

First public release.

### Added

- Android support backed by the native `com.ezoic.sdk:ezoic-ads-sdk:1.5.0`
  library and iOS support backed by the native `EzoicAdsSDK` CocoaPod
  (`~> 1.5.0`, iOS 14+), both resolved through the External Dependency Manager
  for Unity (EDM4U) or a manual Gradle dependency / Podfile line. The full
  `EzoicAds`, `EzoicBannerAd`, `EzoicInterstitialAd`, and `EzoicRewardedAd` API
  behaves identically on both platforms: ad event callbacks are delivered on the
  Unity main thread, calls after `Destroy()` are safe no-ops, and no call throws.
- Banner, interstitial, and rewarded ad types with their full event surfaces,
  including the rewarded ad's `OnUserEarnedReward(string type, int amount)`.
- On unsupported platforms (including the Unity editor) the full API remains
  callable: it logs a one-line warning and delivers load callbacks as failures so
  game code runs unchanged.
- "Basic Integration" sample (importable from the Package Manager Samples tab): a
  single MonoBehaviour with an IMGUI control panel that initializes the SDK and
  loads, shows, hides, and destroys every ad type while logging each ad event.

## [0.1.0] - 2026-07-05

Initial release.

### Added

- `EzoicAds` entry point: `Initialize`, `IsInitialized`, `TrackPageview`,
  `SetGDPRConsent`, `SetGPPConsent`, `SetSubjectToCOPPA`, and `Version`.
- `EzoicBannerAd` with positional placement (`BannerPosition`), `Load`, `Show`,
  `Hide`, `Destroy`, and the `OnLoaded`, `OnLoadFailed`, `OnClicked`, and
  `OnImpression` events.
- `EzoicInterstitialAd` with a static `Load` pattern, `Show`, `Destroy`,
  `IsLoaded`, and the `OnShown`, `OnFailedToShow`, `OnDismissed`, `OnImpression`,
  and `OnClicked` events.
- `EzoicRewardedAd` with the same shape as the interstitial plus
  `OnUserEarnedReward`.
- Android support backed by the native `com.ezoic.sdk:ezoic-ads-sdk:1.5.0`
  library, resolved through the External Dependency Manager for Unity (EDM4U) or
  a manual Gradle dependency.
- On unsupported platforms (including the Unity editor) the full API is callable:
  it logs a one-line warning and delivers load callbacks as failures so game code
  runs unchanged.

[Unreleased]: https://github.com/ezoic/ezoic-unity-sdk/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/ezoic/ezoic-unity-sdk/releases/tag/v1.0.1
[1.0.0]: https://github.com/ezoic/ezoic-unity-sdk/releases/tag/v1.0.0
[0.1.0]: https://github.com/ezoic/ezoic-unity-sdk/releases/tag/0.1.0
