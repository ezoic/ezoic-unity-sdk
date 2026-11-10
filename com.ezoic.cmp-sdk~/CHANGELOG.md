# Changelog

All notable changes to the Ezoic CMP Unity package (`com.ezoic.cmp-sdk`) are documented in
this file. It is released from the same `vX.Y.Z` tag as `com.ezoic.ads`, at the same version.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.22.0] - Unreleased

### Added
- First release. `EzoicCmp` (`Initialize`, `PresentConsentIfRequired`, `PresentConsentSettings`, `IsConsentRequired`, `IsPrivacyOptionsRequired`, `ResetConsent`), `EzoicCmpConfiguration` (`CmpEnabled`, `AutoReadConsent`, `DebugEnabled`) and the `ConsentResult` / `ConsentOutcome` / `ConsentDecision` types, for Ezoic publishers' apps that don't use `com.ezoic.ads`. Native `com.ezoic.sdk:ezoic-cmp-sdk:1.22.0` on Android and the `EzoicCmpSDK` product of the `ezoic-swift-sdk-dist` Swift package 1.22.0 (up to next major) on iOS, through `Editor/EzoicCmpDependencies.xml`.
- Covered US states: the CMP shows a privacy notice by itself and writes the `IABGPP_*` values; `PresentConsentSettings` opens the US privacy settings there.
- A **Basic Consent** sample.
