# Changelog

All notable changes to the Ezoic CMP Unity package (`com.ezoic.cmp-sdk`) are documented in
this file. It is released with `com.ezoic.ads` at the same version: each `vX.Y.Z` tag of the
repository is split into the tag `cmp-sdk/vX.Y.Z` (and the branch `upm/cmp-sdk`), whose root is
this package.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.22.0] - Unreleased

### Added
- First release, installed with `https://github.com/ezoic/ezoic-unity-sdk.git#cmp-sdk/v1.22.0`. `EzoicCmp` (`Initialize`, `PresentConsentIfRequired`, `PresentConsentSettings`, `IsConsentRequired`, `IsPrivacyOptionsRequired`, `ResetConsent`), `EzoicCmpConfiguration` (`CmpEnabled`, `AutoReadConsent`, `DebugEnabled`, and `AutoPresentConsent`, default `true`, which asks for consent once right after `Initialize`) and the `ConsentResult` / `ConsentOutcome` / `ConsentDecision` types, for Ezoic publishers' apps that don't use `com.ezoic.ads`. Native `com.ezoic.sdk:ezoic-cmp-sdk:1.22.0` on Android and the `EzoicCmpSDK` product of the `ezoic-swift-sdk-dist` Swift package 1.22.0 (up to next major) on iOS, through `Editor/EzoicCmpDependencies.xml`.
- Covered US states: the CMP shows a privacy notice by itself and writes the `IABGPP_*` values; `PresentConsentSettings` opens the US privacy settings there.
- A **Basic Consent** sample.
