//
//  EzoicCmpUnityBridge.swift
//  Ezoic CMP for Unity — iOS native bridge
//
//  C-ABI shim between Runtime/iOS/CmpIosBridge.cs and the EzoicCmpSDK library.
//  Every export is prefixed `ezoic_cmp_unity_` and declared `@_cdecl` so IL2CPP
//  can bind it via [DllImport("__Internal")]; tools/audit-extern.py keeps the C#
//  extern set and this export set in lockstep.
//
//  This file compiles into the same module (UnityFramework) as com.ezoic.ads'
//  EzoicAdsUnityBridge.swift when both packages are installed, so every
//  top-level declaration is either `private` or carries an EzoicCmp-specific
//  name.
//
//  Threading: the callback is registered from the Unity (iOS main) thread, and
//  every other export hops to the main queue before it touches it or UIKit, so
//  the callback global is main-thread only. Strings handed to C# are
//  temporaries valid only for the synchronous call.
//

import Foundation
import UIKit
// The prebuilt XCFramework's module is EzoicCmpSDKBinary; the Swift package's
// product is EzoicCmpSDK.
#if canImport(EzoicCmpSDKBinary)
import EzoicCmpSDKBinary
#else
import EzoicCmpSDK
#endif

// Public because the public @_cdecl set_callbacks function takes it.
public typealias EzoicCmpConsentCallback = @convention(c) (Int32, Int32, Int32, UnsafePointer<CChar>?) -> Void

private var cmpConsentResultCb: EzoicCmpConsentCallback?

/// Forwards a consent outcome to C#. The ints match Ezoic.Cmp.ConsentOutcome
/// (NotRequired=0, AlreadyDecided=1, Decided=2, Dismissed=3, AlreadyPresenting=4,
/// Failed=5) and Ezoic.Cmp.ConsentDecision (None=0, AcceptAll=1, RejectAll=2, Custom=3).
private func fireCmpConsent(_ callId: Int32, _ outcome: ConsentOutcome) {
    switch outcome {
    case .notRequired:
        fireCmpConsent(callId, 0, 0, nil)
    case .alreadyDecided:
        fireCmpConsent(callId, 1, 0, nil)
    case .decided(let type):
        let decision: Int32
        switch type {
        case .acceptAll: decision = 1
        case .rejectAll: decision = 2
        case .custom: decision = 3
        @unknown default: decision = 0
        }
        fireCmpConsent(callId, 2, decision, nil)
    case .dismissed:
        fireCmpConsent(callId, 3, 0, nil)
    case .alreadyPresenting:
        fireCmpConsent(callId, 4, 0, nil)
    case .failed(let error):
        // A CmpError from EzoicCmp (an EzoicError if EzoicAds took the CMP over).
        fireCmpConsent(callId, 5, 0, error.localizedDescription)
    @unknown default:
        // An outcome added by a newer native SDK: closed without a known choice.
        fireCmpConsent(callId, 3, 0, nil)
    }
}

private func fireCmpConsent(_ callId: Int32, _ outcome: Int32, _ decision: Int32, _ error: String?) {
    guard let cb = cmpConsentResultCb else { return }
    if let error = error {
        error.withCString { cb(callId, outcome, decision, $0) }
    } else {
        cb(callId, outcome, decision, nil)
    }
}

/// The key window's root view controller, resolved at call time.
private func cmpRootViewController() -> UIViewController? {
    for scene in UIApplication.shared.connectedScenes {
        guard let windowScene = scene as? UIWindowScene else { continue }
        let window = windowScene.windows.first(where: { $0.isKeyWindow }) ?? windowScene.windows.first
        if let root = window?.rootViewController {
            return root
        }
    }
    // Hosts without an active UIWindowScene yet: UnityAppController's window.
    return UIApplication.shared.delegate?.window??.rootViewController
}

@_cdecl("ezoic_cmp_unity_consent_set_callbacks")
public func ezoic_cmp_unity_consent_set_callbacks(_ onResult: EzoicCmpConsentCallback?) {
    // Called once from the C# static setup on the Unity (iOS main) thread.
    cmpConsentResultCb = onResult
}

@_cdecl("ezoic_cmp_unity_init")
public func ezoic_cmp_unity_init(_ domain: UnsafePointer<CChar>?,
                                 _ cmpEnabled: Int32,
                                 _ autoReadConsent: Int32,
                                 _ debugEnabled: Int32) {
    let domainString = domain.map { String(cString: $0) } ?? ""
    DispatchQueue.main.async {
        let configuration = EzoicCmpConfiguration(cmpEnabled: cmpEnabled != 0,
                                                  autoReadConsent: autoReadConsent != 0,
                                                  debugEnabled: debugEnabled != 0)
        EzoicCmp.initialize(domain: domainString, configuration: configuration)
    }
}

private func presentCmpConsent(_ callId: Int32, settings: Bool) {
    DispatchQueue.main.async {
        guard let host = cmpRootViewController() else {
            fireCmpConsent(callId, 5, 0, "No root view controller to present the consent dialog from.")
            return
        }
        let completion: (ConsentOutcome) -> Void = { outcome in
            // Re-hop so the callback fires under the main-thread invariant.
            DispatchQueue.main.async { fireCmpConsent(callId, outcome) }
        }
        if settings {
            EzoicCmp.presentConsentSettings(from: host, completion: completion)
        } else {
            EzoicCmp.presentConsentIfRequired(from: host, completion: completion)
        }
    }
}

@_cdecl("ezoic_cmp_unity_present_consent_if_required")
public func ezoic_cmp_unity_present_consent_if_required(_ callId: Int32) {
    presentCmpConsent(callId, settings: false)
}

@_cdecl("ezoic_cmp_unity_present_consent_settings")
public func ezoic_cmp_unity_present_consent_settings(_ callId: Int32) {
    presentCmpConsent(callId, settings: true)
}

@_cdecl("ezoic_cmp_unity_reset_consent")
public func ezoic_cmp_unity_reset_consent() {
    DispatchQueue.main.async {
        EzoicCmp.resetConsent()
    }
}

/// Synchronous tri-state read (-1 unknown, 0 false, 1 true); touches no stored
/// callback or UIKit object.
@_cdecl("ezoic_cmp_unity_is_consent_required")
public func ezoic_cmp_unity_is_consent_required() -> Int32 {
    guard let required = EzoicCmp.isConsentRequired else { return -1 }
    return required ? 1 : 0
}

/// Synchronous tri-state read (-1 unknown, 0 false, 1 true).
@_cdecl("ezoic_cmp_unity_is_privacy_options_required")
public func ezoic_cmp_unity_is_privacy_options_required() -> Int32 {
    guard let required = EzoicCmp.isPrivacyOptionsRequired else { return -1 }
    return required ? 1 : 0
}
