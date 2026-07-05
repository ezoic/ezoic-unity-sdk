# Changelog

All notable changes to the Ezoic Ads Unity SDK are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- iOS support backed by the native `EzoicAdsSDK` CocoaPod (`~> 1.5.0`, iOS 14+),
  resolved through the External Dependency Manager for Unity (EDM4U) iOS Resolver
  or a manual Podfile line. The full `EzoicAds`, `EzoicBannerAd`,
  `EzoicInterstitialAd`, and `EzoicRewardedAd` API behaves identically to Android:
  ad event callbacks are delivered on the Unity main thread, calls after
  `Destroy()` are safe no-ops, and no call throws.

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

[Unreleased]: https://github.com/ezoic/ezoic-unity-sdk/compare/0.1.0...HEAD
[0.1.0]: https://github.com/ezoic/ezoic-unity-sdk/releases/tag/0.1.0
