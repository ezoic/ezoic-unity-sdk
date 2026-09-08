//
//  EzoicAdsUnityBridge.swift
//  Ezoic Ads SDK for Unity — iOS native bridge
//
//  C-ABI shim between the Unity C# runtime (Runtime/iOS/IosBridge.cs) and the
//  published EzoicAdsSDK 1.7.0 xcframework. Every export is prefixed
//  `ezoic_unity_` and declared `@_cdecl` so IL2CPP can bind it via
//  [DllImport("__Internal")]. The C# extern set and this export set are kept in
//  lockstep by tools/audit-extern.py (name + parameter-count equality, both
//  directions).
//
//  ==========================================================================
//  THREADING INVARIANT — READ BEFORE EDITING
//  --------------------------------------------------------------------------
//  Every @_cdecl entry point hops to DispatchQueue.main before it touches a
//  registry, a callback pointer, or any UIKit object. The SDK delivers its
//  delegate callbacks on the main thread, and the init / load completions are
//  re-dispatched to main here. Therefore ALL registry access and ALL C
//  function-pointer invocations happen on the main thread only — no locking is
//  used or needed. Do not call these exports off-main and do not add
//  background work that reads or mutates the registries or callback globals.
//
//  C-STRING ABI
//  --------------------------------------------------------------------------
//  Strings passed C#→Swift arrive as UnsafePointer<CChar>? and are copied into
//  a Swift String immediately (String(cString:)). Strings passed Swift→C# are
//  handed to the callback as a temporary C string that is valid ONLY for the
//  synchronous duration of the call (`message.withCString { cb($0) }`); the C#
//  trampoline copies it immediately (Marshal.PtrToStringUTF8). The only
//  heap-allocated string that crosses the boundary is ezoic_unity_version()
//  (strdup); C# frees it with ezoic_unity_free_string().
//
//  NO-THROW ABI
//  --------------------------------------------------------------------------
//  Nothing here is `throws`; the SDK calls used below are non-throwing. Do not
//  introduce a throwing call inside an @_cdecl body without wrapping it.
//  ==========================================================================

import Foundation
import UIKit
import EzoicAdsSDK

// MARK: - Callback function-pointer types (kept comma-free so the extern audit
// can count parameters by top-level commas alone).

// Public because the public @_cdecl set_callbacks functions take them as
// parameters (a public function cannot use a less-accessible type).
public typealias IdCallback = @convention(c) (Int32) -> Void
public typealias IdMessageCallback = @convention(c) (Int32, UnsafePointer<CChar>?) -> Void
public typealias IdRewardCallback = @convention(c) (Int32, UnsafePointer<CChar>?, Int32) -> Void

// MARK: - Stored callbacks (main-thread only; see threading invariant).

// Init completions carry the C#-allocated call id so concurrent Initialize calls
// settle their own completion (see IosBridge._pendingInit). success -> IdCallback,
// failure -> IdMessageCallback.
private var initSuccessCb: IdCallback?
private var initFailureCb: IdMessageCallback?

private var bannerLoadedCb: IdCallback?
private var bannerLoadFailedCb: IdMessageCallback?
private var bannerClickedCb: IdCallback?
private var bannerImpressionCb: IdCallback?

private var interstitialLoadedCb: IdCallback?
private var interstitialLoadFailedCb: IdMessageCallback?
private var interstitialShownCb: IdCallback?
private var interstitialFailedToShowCb: IdMessageCallback?
private var interstitialDismissedCb: IdCallback?
private var interstitialImpressionCb: IdCallback?
private var interstitialClickedCb: IdCallback?

private var rewardedLoadedCb: IdCallback?
private var rewardedLoadFailedCb: IdMessageCallback?
private var rewardedShownCb: IdCallback?
private var rewardedFailedToShowCb: IdMessageCallback?
private var rewardedDismissedCb: IdCallback?
private var rewardedImpressionCb: IdCallback?
private var rewardedClickedCb: IdCallback?
private var rewardedUserEarnedRewardCb: IdRewardCallback?

// MARK: - Registries (main-thread only). Each entry strongly holds both the ad
// and its delegate adapter; the SDK holds the adapter only weakly, so the entry
// is what keeps the adapter alive. Destroy removes the entry, releasing both.

private final class BannerEntry {
    let view: EzoicBannerView
    let adapter: BannerDelegateAdapter
    let size: String?
    let position: Int32
    init(view: EzoicBannerView, adapter: BannerDelegateAdapter, size: String?, position: Int32) {
        self.view = view
        self.adapter = adapter
        self.size = size
        self.position = position
    }
}

private final class InterstitialEntry {
    let ad: EzoicInterstitialAd
    let adapter: InterstitialDelegateAdapter
    init(ad: EzoicInterstitialAd, adapter: InterstitialDelegateAdapter) {
        self.ad = ad
        self.adapter = adapter
    }
}

private final class RewardedEntry {
    let ad: EzoicRewardedAd
    let adapter: RewardedDelegateAdapter
    init(ad: EzoicRewardedAd, adapter: RewardedDelegateAdapter) {
        self.ad = ad
        self.adapter = adapter
    }
}

private var bannerRegistry: [Int32: BannerEntry] = [:]
private var interstitialRegistry: [Int32: InterstitialEntry] = [:]
private var rewardedRegistry: [Int32: RewardedEntry] = [:]

// MARK: - Destroy-mid-load tombstones (main-thread only, same invariant as the
// registries). If C# Destroy() runs while a load is still in flight there is no
// registry entry to remove, so the id is recorded here; the load completion then
// destroys the freshly-loaded ad instead of registering it (which would leak the
// native ad and fire loadedCb for an id C# already dropped).

private var destroyedPendingInterstitialIds: Set<Int32> = []
private var destroyedPendingRewardedIds: Set<Int32> = []

// MARK: - Small helpers.

/// Invokes an id+message callback with a temporary C string valid only for the
/// synchronous call. See the C-string ABI note above.
private func fire(_ cb: IdMessageCallback?, _ id: Int32, _ message: String) {
    guard let cb = cb else { return }
    message.withCString { cb(id, $0) }
}

/// Resolves the current key window's root view controller at call time (never
/// cached — the root VC can change across scene/window transitions).
private func currentRootViewController() -> UIViewController? {
    for scene in UIApplication.shared.connectedScenes {
        guard let windowScene = scene as? UIWindowScene else { continue }
        let window = windowScene.windows.first(where: { $0.isKeyWindow }) ?? windowScene.windows.first
        if let root = window?.rootViewController {
            return root
        }
    }
    // Legacy fallback for hosts without an active UIWindowScene yet.
    return UIApplication.shared.windows.first(where: { $0.isKeyWindow })?.rootViewController
        ?? UIApplication.shared.windows.first?.rootViewController
}

/// Resolves the point dimensions to pin the banner container to. The SDK centers
/// its inner GMA banner inside the outer EzoicBannerView, which is init'd at frame
/// .zero and has no intrinsicContentSize, so without an explicit width/height the
/// container collapses to 0×0 under position-only constraints. Parses the stored
/// "320x50"-style size when present; otherwise mirrors EzoicBannerView's private
/// adaptiveSize() (EzoicBannerView.swift:798-803).
private func bannerSize(from size: String?) -> CGSize {
    if let size = size, !size.isEmpty {
        let parts = size.split(separator: "x")
        if parts.count == 2, let w = Int(parts[0]), let h = Int(parts[1]) {
            return CGSize(width: w, height: h)
        }
    }
    let width = UIScreen.main.bounds.width
    if width >= 728 { return CGSize(width: 728, height: 90) }
    if width >= 468 { return CGSize(width: 468, height: 60) }
    return CGSize(width: 320, height: 50)
}

/// Adds the banner to the current root view with safe-area position constraints and
/// explicit width/height (see bannerSize) if it is not already in the view tree.
/// Idempotent; resolves the host view at call time. Position ints match
/// Ezoic.Ads.BannerPosition:
/// Top=0, Bottom=1, TopLeft=2, TopRight=3, BottomLeft=4, BottomRight=5, Center=6.
private func attachBannerIfNeeded(_ view: EzoicBannerView, position: Int32, size: String?) {
    guard view.superview == nil else { return }
    guard let host = currentRootViewController()?.view else { return }

    view.translatesAutoresizingMaskIntoConstraints = false
    host.addSubview(view)

    let guide = host.safeAreaLayoutGuide
    let positionConstraints: [NSLayoutConstraint]
    switch position {
    case 0: // Top
        positionConstraints = [view.topAnchor.constraint(equalTo: guide.topAnchor),
                               view.centerXAnchor.constraint(equalTo: guide.centerXAnchor)]
    case 2: // TopLeft
        positionConstraints = [view.topAnchor.constraint(equalTo: guide.topAnchor),
                               view.leadingAnchor.constraint(equalTo: guide.leadingAnchor)]
    case 3: // TopRight
        positionConstraints = [view.topAnchor.constraint(equalTo: guide.topAnchor),
                               view.trailingAnchor.constraint(equalTo: guide.trailingAnchor)]
    case 4: // BottomLeft
        positionConstraints = [view.bottomAnchor.constraint(equalTo: guide.bottomAnchor),
                               view.leadingAnchor.constraint(equalTo: guide.leadingAnchor)]
    case 5: // BottomRight
        positionConstraints = [view.bottomAnchor.constraint(equalTo: guide.bottomAnchor),
                               view.trailingAnchor.constraint(equalTo: guide.trailingAnchor)]
    case 6: // Center
        positionConstraints = [view.centerXAnchor.constraint(equalTo: guide.centerXAnchor),
                               view.centerYAnchor.constraint(equalTo: guide.centerYAnchor)]
    default: // Bottom (case 1) and any unknown value — matches the C# default.
        positionConstraints = [view.bottomAnchor.constraint(equalTo: guide.bottomAnchor),
                               view.centerXAnchor.constraint(equalTo: guide.centerXAnchor)]
    }

    let dimensions = bannerSize(from: size)
    NSLayoutConstraint.activate(positionConstraints + [
        view.widthAnchor.constraint(equalToConstant: dimensions.width),
        view.heightAnchor.constraint(equalToConstant: dimensions.height),
    ])
}

// MARK: - Delegate adapters. Each holds only the Int32 instance id; the entry in
// the registry keeps the adapter alive. Every method just forwards to the stored
// C callback with the id (and message/reward). All fire on the main thread.

private final class BannerDelegateAdapter: EzoicBannerViewDelegate {
    let id: Int32
    init(id: Int32) { self.id = id }

    func bannerViewDidLoad(_ bannerView: EzoicBannerView) {
        bannerLoadedCb?(id)
    }

    func bannerView(_ bannerView: EzoicBannerView, didFailToLoadWithError error: EzoicError) {
        fire(bannerLoadFailedCb, id, error.localizedDescription)
    }

    func bannerViewDidRecordImpression(_ bannerView: EzoicBannerView) {
        bannerImpressionCb?(id)
    }

    func bannerViewDidRecordClick(_ bannerView: EzoicBannerView) {
        bannerClickedCb?(id)
    }
}

private final class InterstitialDelegateAdapter: EzoicInterstitialAdDelegate {
    let id: Int32
    init(id: Int32) { self.id = id }

    func interstitialAdDidPresent(_ interstitialAd: EzoicInterstitialAd) {
        interstitialShownCb?(id)
    }

    func interstitialAd(_ interstitialAd: EzoicInterstitialAd, didFailToPresentWithError error: EzoicError) {
        fire(interstitialFailedToShowCb, id, error.localizedDescription)
    }

    func interstitialAdDidRecordImpression(_ interstitialAd: EzoicInterstitialAd) {
        interstitialImpressionCb?(id)
    }

    func interstitialAdDidRecordClick(_ interstitialAd: EzoicInterstitialAd) {
        interstitialClickedCb?(id)
    }

    func interstitialAdDidDismiss(_ interstitialAd: EzoicInterstitialAd) {
        interstitialDismissedCb?(id)
    }
}

private final class RewardedDelegateAdapter: EzoicRewardedAdDelegate {
    let id: Int32
    init(id: Int32) { self.id = id }

    func rewardedAdDidPresent(_ rewardedAd: EzoicRewardedAd) {
        rewardedShownCb?(id)
    }

    func rewardedAd(_ rewardedAd: EzoicRewardedAd, didFailToPresentWithError error: EzoicError) {
        fire(rewardedFailedToShowCb, id, error.localizedDescription)
    }

    func rewardedAdDidRecordImpression(_ rewardedAd: EzoicRewardedAd) {
        rewardedImpressionCb?(id)
    }

    func rewardedAdDidRecordClick(_ rewardedAd: EzoicRewardedAd) {
        rewardedClickedCb?(id)
    }

    func rewardedAd(_ rewardedAd: EzoicRewardedAd, userDidEarn reward: EzoicReward) {
        guard let cb = rewardedUserEarnedRewardCb else { return }
        reward.type.withCString { cb(id, $0, Int32(reward.amount)) }
    }

    func rewardedAdDidDismiss(_ rewardedAd: EzoicRewardedAd) {
        rewardedDismissedCb?(id)
    }
}

// MARK: - Callback registration (called once from the C# static initializer).

@_cdecl("ezoic_unity_init_set_callbacks")
public func ezoic_unity_init_set_callbacks(_ onSuccess: IdCallback?, _ onFailure: IdMessageCallback?) {
    initSuccessCb = onSuccess
    initFailureCb = onFailure
}

@_cdecl("ezoic_unity_banner_set_callbacks")
public func ezoic_unity_banner_set_callbacks(_ onLoaded: IdCallback?,
                                             _ onLoadFailed: IdMessageCallback?,
                                             _ onClicked: IdCallback?,
                                             _ onImpression: IdCallback?) {
    bannerLoadedCb = onLoaded
    bannerLoadFailedCb = onLoadFailed
    bannerClickedCb = onClicked
    bannerImpressionCb = onImpression
}

@_cdecl("ezoic_unity_interstitial_set_callbacks")
public func ezoic_unity_interstitial_set_callbacks(_ onLoaded: IdCallback?,
                                                   _ onLoadFailed: IdMessageCallback?,
                                                   _ onShown: IdCallback?,
                                                   _ onFailedToShow: IdMessageCallback?,
                                                   _ onDismissed: IdCallback?,
                                                   _ onImpression: IdCallback?,
                                                   _ onClicked: IdCallback?) {
    interstitialLoadedCb = onLoaded
    interstitialLoadFailedCb = onLoadFailed
    interstitialShownCb = onShown
    interstitialFailedToShowCb = onFailedToShow
    interstitialDismissedCb = onDismissed
    interstitialImpressionCb = onImpression
    interstitialClickedCb = onClicked
}

@_cdecl("ezoic_unity_rewarded_set_callbacks")
public func ezoic_unity_rewarded_set_callbacks(_ onLoaded: IdCallback?,
                                               _ onLoadFailed: IdMessageCallback?,
                                               _ onShown: IdCallback?,
                                               _ onFailedToShow: IdMessageCallback?,
                                               _ onDismissed: IdCallback?,
                                               _ onImpression: IdCallback?,
                                               _ onClicked: IdCallback?,
                                               _ onUserEarnedReward: IdRewardCallback?) {
    rewardedLoadedCb = onLoaded
    rewardedLoadFailedCb = onLoadFailed
    rewardedShownCb = onShown
    rewardedFailedToShowCb = onFailedToShow
    rewardedDismissedCb = onDismissed
    rewardedImpressionCb = onImpression
    rewardedClickedCb = onClicked
    rewardedUserEarnedRewardCb = onUserEarnedReward
}

// MARK: - SDK lifecycle / global.

@_cdecl("ezoic_unity_init")
public func ezoic_unity_init(_ callId: Int32, _ domain: UnsafePointer<CChar>?) {
    let domainString = domain.map { String(cString: $0) } ?? ""
    DispatchQueue.main.async {
        let configuration = EzoicConfiguration(domain: domainString)
        EzoicAds.shared.initialize(with: configuration) { result in
            // The SDK may complete off the main thread; re-hop so the callback
            // fires under the threading invariant.
            DispatchQueue.main.async {
                switch result {
                case .success:
                    initSuccessCb?(callId)
                case .failure(let error):
                    fire(initFailureCb, callId, error.localizedDescription)
                }
            }
        }
    }
}

@_cdecl("ezoic_unity_track_pageview")
public func ezoic_unity_track_pageview() {
    DispatchQueue.main.async {
        EzoicAds.shared.trackPageview(completion: nil)
    }
}

@_cdecl("ezoic_unity_set_gdpr_consent")
public func ezoic_unity_set_gdpr_consent(_ applies: Int32, _ consentString: UnsafePointer<CChar>?) {
    let consent = consentString.map { String(cString: $0) }
    DispatchQueue.main.async {
        EzoicAds.shared.setGDPRConsent(applies: applies != 0, consentString: consent)
    }
}

@_cdecl("ezoic_unity_set_gpp_consent")
public func ezoic_unity_set_gpp_consent(_ gppString: UnsafePointer<CChar>?, _ sectionIds: UnsafePointer<CChar>?) {
    let gpp = gppString.map { String(cString: $0) }
    let sections = sectionIds.map { String(cString: $0) }
    DispatchQueue.main.async {
        EzoicAds.shared.setGPPConsent(gppString: gpp, sectionIds: sections)
    }
}

@_cdecl("ezoic_unity_set_subject_to_coppa")
public func ezoic_unity_set_subject_to_coppa(_ subject: Int32) {
    DispatchQueue.main.async {
        EzoicAds.shared.setSubjectToCOPPA(subject != 0)
    }
}

/// Returns a heap-allocated (strdup) UTF-8 copy of the SDK version. The caller
/// (C#) must free it with ezoic_unity_free_string.
@_cdecl("ezoic_unity_version")
public func ezoic_unity_version() -> UnsafeMutablePointer<CChar>? {
    return strdup(EzoicAds.version)
}

@_cdecl("ezoic_unity_free_string")
public func ezoic_unity_free_string(_ pointer: UnsafeMutablePointer<CChar>?) {
    free(pointer)
}

// MARK: - Banner.

@_cdecl("ezoic_unity_banner_create")
public func ezoic_unity_banner_create(_ id: Int32,
                                      _ adUnitId: Int32,
                                      _ position: Int32,
                                      _ size: UnsafePointer<CChar>?) {
    let sizeString = size.map { String(cString: $0) }
    DispatchQueue.main.async {
        guard bannerRegistry[id] == nil else { return }
        let view = EzoicBannerView(adUnitIdentifier: Int(adUnitId))
        let adapter = BannerDelegateAdapter(id: id)
        view.delegate = adapter
        bannerRegistry[id] = BannerEntry(view: view, adapter: adapter, size: sizeString, position: position)
        attachBannerIfNeeded(view, position: position, size: sizeString)
    }
}

@_cdecl("ezoic_unity_banner_load")
public func ezoic_unity_banner_load(_ id: Int32) {
    DispatchQueue.main.async {
        guard let entry = bannerRegistry[id] else {
            fire(bannerLoadFailedCb, id, "Ezoic Ads: banner instance is not available (never created or already destroyed).")
            return
        }
        // The key window's root VC may not have existed at create time; attach
        // now if still detached. attachBannerIfNeeded is idempotent and a no-op
        // once the view is in the hierarchy.
        attachBannerIfNeeded(entry.view, position: entry.position, size: entry.size)
        // If no root VC was available the view is still detached; loading would
        // succeed with nothing on screen. Report a non-terminal load failure and
        // keep the instance registered so a later Load() can retry once a root VC
        // exists (matches the Android OnLoadFailed-when-unattached path).
        guard entry.view.superview != nil else {
            fire(bannerLoadFailedCb, id, "Ezoic Ads: no root view controller available to attach the banner.")
            return
        }
        if let size = entry.size, !size.isEmpty {
            entry.view.loadAd(size: size)
        } else {
            entry.view.loadAd()
        }
    }
}

@_cdecl("ezoic_unity_banner_show")
public func ezoic_unity_banner_show(_ id: Int32) {
    DispatchQueue.main.async {
        guard let entry = bannerRegistry[id] else { return }
        entry.view.isHidden = false
    }
}

@_cdecl("ezoic_unity_banner_hide")
public func ezoic_unity_banner_hide(_ id: Int32) {
    DispatchQueue.main.async {
        guard let entry = bannerRegistry[id] else { return }
        entry.view.isHidden = true
    }
}

@_cdecl("ezoic_unity_banner_destroy")
public func ezoic_unity_banner_destroy(_ id: Int32) {
    DispatchQueue.main.async {
        guard let entry = bannerRegistry.removeValue(forKey: id) else { return }
        entry.view.stopLoading()
        entry.view.destroy()
        entry.view.removeFromSuperview()
    }
}

// MARK: - Interstitial.

@_cdecl("ezoic_unity_interstitial_load")
public func ezoic_unity_interstitial_load(_ id: Int32, _ adUnitId: Int32) {
    DispatchQueue.main.async {
        guard interstitialRegistry[id] == nil else {
            fire(interstitialLoadFailedCb, id, "Ezoic Ads: interstitial instance id is already in use.")
            return
        }
        EzoicInterstitialAd.load(adUnitIdentifier: Int(adUnitId)) { result in
            DispatchQueue.main.async {
                switch result {
                case .success(let ad):
                    // Destroy() ran mid-load: discard the tombstone and the ad
                    // without registering it or firing loadedCb for a dead id.
                    if destroyedPendingInterstitialIds.remove(id) != nil {
                        ad.destroy()
                        return
                    }
                    let adapter = InterstitialDelegateAdapter(id: id)
                    ad.delegate = adapter
                    interstitialRegistry[id] = InterstitialEntry(ad: ad, adapter: adapter)
                    interstitialLoadedCb?(id)
                case .failure(let error):
                    // Discard any tombstone so the set can't grow; the failure
                    // event is harmless (C# already dropped the instance).
                    destroyedPendingInterstitialIds.remove(id)
                    fire(interstitialLoadFailedCb, id, error.localizedDescription)
                }
            }
        }
    }
}

@_cdecl("ezoic_unity_interstitial_show")
public func ezoic_unity_interstitial_show(_ id: Int32) {
    DispatchQueue.main.async {
        guard let entry = interstitialRegistry[id] else {
            fire(interstitialFailedToShowCb, id, "Ezoic Ads: interstitial ad is not available to present (never loaded or already destroyed).")
            return
        }
        entry.ad.show(from: nil)
    }
}

@_cdecl("ezoic_unity_interstitial_destroy")
public func ezoic_unity_interstitial_destroy(_ id: Int32) {
    DispatchQueue.main.async {
        guard let entry = interstitialRegistry.removeValue(forKey: id) else {
            // No entry yet: a load may be in flight. Tombstone the id so the
            // completion destroys the ad instead of registering it.
            destroyedPendingInterstitialIds.insert(id)
            return
        }
        entry.ad.destroy()
    }
}

// MARK: - Rewarded.

@_cdecl("ezoic_unity_rewarded_load")
public func ezoic_unity_rewarded_load(_ id: Int32, _ adUnitId: Int32) {
    DispatchQueue.main.async {
        guard rewardedRegistry[id] == nil else {
            fire(rewardedLoadFailedCb, id, "Ezoic Ads: rewarded instance id is already in use.")
            return
        }
        EzoicRewardedAd.load(adUnitIdentifier: Int(adUnitId)) { result in
            DispatchQueue.main.async {
                switch result {
                case .success(let ad):
                    // Destroy() ran mid-load: discard the tombstone and the ad
                    // without registering it or firing loadedCb for a dead id.
                    if destroyedPendingRewardedIds.remove(id) != nil {
                        ad.destroy()
                        return
                    }
                    let adapter = RewardedDelegateAdapter(id: id)
                    ad.delegate = adapter
                    rewardedRegistry[id] = RewardedEntry(ad: ad, adapter: adapter)
                    rewardedLoadedCb?(id)
                case .failure(let error):
                    // Discard any tombstone so the set can't grow; the failure
                    // event is harmless (C# already dropped the instance).
                    destroyedPendingRewardedIds.remove(id)
                    fire(rewardedLoadFailedCb, id, error.localizedDescription)
                }
            }
        }
    }
}

@_cdecl("ezoic_unity_rewarded_show")
public func ezoic_unity_rewarded_show(_ id: Int32) {
    DispatchQueue.main.async {
        guard let entry = rewardedRegistry[id] else {
            fire(rewardedFailedToShowCb, id, "Ezoic Ads: rewarded ad is not available to present (never loaded or already destroyed).")
            return
        }
        entry.ad.show(from: nil)
    }
}

@_cdecl("ezoic_unity_rewarded_destroy")
public func ezoic_unity_rewarded_destroy(_ id: Int32) {
    DispatchQueue.main.async {
        guard let entry = rewardedRegistry.removeValue(forKey: id) else {
            // No entry yet: a load may be in flight. Tombstone the id so the
            // completion destroys the ad instead of registering it.
            destroyedPendingRewardedIds.insert(id)
            return
        }
        entry.ad.destroy()
    }
}
