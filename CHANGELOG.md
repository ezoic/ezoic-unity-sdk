# Changelog

All notable changes to the Ezoic Ads Unity SDK are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[0.1.0]: https://github.com/ezoic/ezoic-unity-sdk/releases/tag/0.1.0
