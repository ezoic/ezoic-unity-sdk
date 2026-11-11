// swift-tools-version:5.9
import PackageDescription

// Scratch SwiftPM package used ONLY by the `swift-bridge` CI job to type-check the
// Unity iOS bridges against the real published Ezoic Swift package. The CI step
// copies the bridge .swift files into the Sources/ target directories before
// building, so a fresh checkout has only a .gitkeep in each (this package is not
// meant to `swift build` on its own):
//
//   EzoicUnityBridgeCheck      com.ezoic.ads' bridge, against the EzoicAdsSDK product
//   EzoicCmpUnityBridgeCheck   com.ezoic.cmp-sdk's bridge, against EzoicCmpSDK only
//   EzoicBothUnityBridgeCheck  both bridges in one module, as UnityFramework compiles
//                              them when an app installs both packages
//
// Nothing here ships in the Unity package or the Xcode player build: the committed
// Package.swift carries a PluginImporter .meta with every platform disabled so Unity
// never treats it as a native plugin, and the copied sources are gitignored.
let package = Package(
    name: "EzoicUnityBridgeCheck",
    platforms: [
        .iOS(.v15)
    ],
    products: [
        .library(name: "EzoicUnityBridgeCheck", targets: ["EzoicUnityBridgeCheck"]),
        .library(name: "EzoicCmpUnityBridgeCheck", targets: ["EzoicCmpUnityBridgeCheck"]),
        .library(name: "EzoicBothUnityBridgeCheck", targets: ["EzoicBothUnityBridgeCheck"])
    ],
    dependencies: [
        .package(
            // Matches the remoteSwiftPackage in Editor/EzoicDependencies.xml and
            // com.ezoic.cmp-sdk~/Editor/EzoicCmpDependencies.xml (same URL, version and
            // upToNextMajor rule, enforced by the CI `validate` job) so CI type-checks
            // the same range EDM adds to a Unity Xcode project.
            url: "https://github.com/ezoic/ezoic-swift-sdk-dist.git",
            .upToNextMajor(from: "1.22.0")
        )
    ],
    targets: [
        .target(
            name: "EzoicUnityBridgeCheck",
            dependencies: [
                .product(name: "EzoicAdsSDK", package: "ezoic-swift-sdk-dist")
            ],
            path: "Sources/EzoicUnityBridgeCheck"
        ),
        .target(
            name: "EzoicCmpUnityBridgeCheck",
            dependencies: [
                .product(name: "EzoicCmpSDK", package: "ezoic-swift-sdk-dist")
            ],
            path: "Sources/EzoicCmpUnityBridgeCheck"
        ),
        .target(
            name: "EzoicBothUnityBridgeCheck",
            dependencies: [
                .product(name: "EzoicAdsSDK", package: "ezoic-swift-sdk-dist"),
                .product(name: "EzoicCmpSDK", package: "ezoic-swift-sdk-dist")
            ],
            path: "Sources/EzoicBothUnityBridgeCheck"
        )
    ]
)
