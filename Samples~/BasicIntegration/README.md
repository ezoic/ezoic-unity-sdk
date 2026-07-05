# Basic Integration Sample

A single `MonoBehaviour` (`EzoicAdsDemo.cs`) that demonstrates the whole Ezoic Ads
Unity SDK from one on-screen control panel: initialize the SDK, then load, show,
hide, and destroy banner, interstitial, and rewarded ads. Every public ad event
(including a rewarded ad's earned-reward callback with its type and amount) is
written to a scrolling status log so you can watch the full lifecycle on device.

The UI is drawn entirely with Unity IMGUI (`OnGUI` / `GUILayout`) — no scene
assets, prefabs, or `UnityEngine.UI` dependency — and is DPI-scaled so the buttons
stay tappable on phones.

## Import

In Unity open **Window → Package Manager**, select **Ezoic Ads** in the package
list, open the **Samples** tab, and click **Import** next to *Basic Integration*.
Unity copies the sample into `Assets/Samples/Ezoic Ads/<version>/Basic Integration`.

## Run

1. Create (or open) an empty scene.
2. Add an empty `GameObject` and attach `EzoicAdsDemo` to it.
3. In the Inspector set:
   - **Domain** — your Ezoic-registered domain (e.g. `example.com`).
   - **Banner / Interstitial / Rewarded Ad Unit Id** — your Ezoic ad unit ids.
4. Build to a device and use the on-screen buttons: **Initialize** first, then
   load/show any ad type.

## Platform notes

Ads only serve on Android and iOS devices. In the Unity editor (and any other
platform) the SDK runs as a safe no-op stub: calls never throw and load callbacks
report failure, so the demo still runs but shows no ads.

Building to a device requires the native SDK setup described in the package
[README](https://github.com/ezoic/ezoic-unity-sdk#readme): the External Dependency Manager for Unity (EDM4U) to
resolve the native Android library / iOS CocoaPod, the Google Ad Manager
application id in your Android manifest / iOS `Info.plist`, and `pod install` on
iOS after EDM4U generates the Podfile.
