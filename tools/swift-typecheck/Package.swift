// swift-tools-version:5.9
import PackageDescription

// Scratch SwiftPM package used ONLY by the `swift-bridge` CI job to type-check
// Runtime/Plugins/iOS/*.swift against the real published EzoicAdsSDK API. The CI
// step copies the bridge .swift file(s) into Sources/EzoicUnityBridgeCheck/ before
// building, so a fresh checkout has only a .gitkeep there (SwiftPM needs the copied
// sources to build — this package is not meant to `swift build` on its own).
//
// Nothing here ships in the Unity package or the Xcode player build: the committed
// Package.swift carries a PluginImporter .meta with every platform disabled so Unity
// never treats it as a native plugin, and the copied sources are gitignored.
let package = Package(
    name: "EzoicUnityBridgeCheck",
    platforms: [
        .iOS(.v14)
    ],
    dependencies: [
        .package(
            // Matches Editor/EzoicDependencies.xml's `~> 1.8.0` (CocoaPods optimistic
            // operator) so CI type-checks the same 1.8.x range the pod can resolve to.
            url: "https://github.com/ezoic/ezoic-swift-sdk-dist",
            .upToNextMinor(from: "1.8.0")
        )
    ],
    targets: [
        .target(
            name: "EzoicUnityBridgeCheck",
            dependencies: [
                .product(name: "EzoicAdsSDK", package: "ezoic-swift-sdk-dist")
            ],
            path: "Sources/EzoicUnityBridgeCheck"
        )
    ]
)
