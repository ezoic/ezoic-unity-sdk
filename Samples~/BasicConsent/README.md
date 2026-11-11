# Basic Consent Sample

A single `MonoBehaviour` (`EzoicCmpDemo.cs`) that drives the Ezoic CMP package from one
on-screen control panel: initialize, ask for consent, open the privacy settings, and
reset consent. Every result is written to a scrolling status log.

The UI is drawn with Unity IMGUI (`OnGUI` / `GUILayout`), so the sample needs no scene
assets, prefabs, or `UnityEngine.UI`.

## Import

In Unity open **Window → Package Manager**, select **Ezoic CMP**, open the **Samples**
tab, and click **Import** next to *Basic Consent*.

## Run

1. Create (or open) an empty scene.
2. Add an empty `GameObject` and attach `EzoicCmpDemo` to it.
3. In the Inspector set **Domain** to your Ezoic-registered domain (e.g. `example.com`).
   The CMP is for Ezoic publishers only: an unknown domain leaves it off, and every
   request reports `Failed`.
4. Build to a device. With **Initialize On Start** checked, the sample initializes and
   asks for consent at launch. **Privacy settings** opens the consent dialog in GDPR
   regions and the US privacy settings in covered US states; **Reset consent** clears
   the stored decision.

## Platform notes

The CMP runs only on Android and iOS devices. In the Unity editor (and any other
platform) the calls are safe no-ops: requests report `Failed` and the getters are
`null`.

Building to a device needs the native setup in the package README: the External
Dependency Manager to resolve the native Android library and iOS Swift package.
