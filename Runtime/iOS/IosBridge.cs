#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace Ezoic.Ads.iOS
{
    /// <summary>
    /// Shared P/Invoke plumbing for the iOS bridge: the <c>[DllImport("__Internal")]</c> externs
    /// that call into <c>Runtime/Plugins/iOS/EzoicAdsUnityBridge.swift</c>, the reverse-callback
    /// trampolines the Swift side invokes, and strong-reference registries that keep the managed
    /// ad wrappers alive while native code holds their instance id.
    /// </summary>
    /// <remarks>
    /// THREADING: Every public entry point here is called from the Unity main thread (the SDK's
    /// documented contract — make the first call from the main thread). The Swift side always
    /// invokes the reverse callbacks below on the iOS main thread, but those trampolines
    /// immediately re-enqueue onto <see cref="EzoicMainThreadDispatcher"/> before touching any
    /// managed wrapper or user delegate — exactly like the Android proxies. So user-facing
    /// callbacks always run on the Unity main thread.
    ///
    /// C-STRING LIFETIME: A <c>const char*</c> handed to a trampoline by Swift is valid ONLY for
    /// the synchronous duration of that call (Swift passes a temporary via <c>withCString</c>).
    /// Each trampoline therefore copies the string to a managed <see cref="string"/> BEFORE
    /// enqueuing the deferred work — never inside the enqueued closure, where the pointer would
    /// already be dangling.
    ///
    /// NEVER THROW ACROSS THE ABI: forward extern calls are wrapped in try/catch by the callers
    /// (the ad classes); the trampolines here never let an exception escape into native code
    /// (the dispatcher swallows and logs exceptions from enqueued actions).
    /// </remarks>
    internal static class IosBridge
    {
        // ---- Instance-id allocator (shared across banner/interstitial/rewarded) ----

        private static int _lastId;

        /// <summary>Allocates a process-unique, monotonically increasing instance id.</summary>
        internal static int NextId()
        {
            return Interlocked.Increment(ref _lastId);
        }

        // ---- Registries. Strong references keyed by instance id. Populated when an ad is
        // created/loaded and removed on Destroy (and on terminal load failure). Concurrent for
        // safety, but by contract only mutated/read on the Unity main thread. ----

        private static readonly ConcurrentDictionary<int, EzoicBannerAd> Banners =
            new ConcurrentDictionary<int, EzoicBannerAd>();
        private static readonly ConcurrentDictionary<int, EzoicInterstitialAd> Interstitials =
            new ConcurrentDictionary<int, EzoicInterstitialAd>();
        private static readonly ConcurrentDictionary<int, EzoicRewardedAd> Rewardeds =
            new ConcurrentDictionary<int, EzoicRewardedAd>();

        internal static void RegisterBanner(int id, EzoicBannerAd ad) => Banners[id] = ad;
        internal static void UnregisterBanner(int id) => Banners.TryRemove(id, out _);
        internal static void RegisterInterstitial(int id, EzoicInterstitialAd ad) => Interstitials[id] = ad;
        internal static void UnregisterInterstitial(int id) => Interstitials.TryRemove(id, out _);
        internal static void RegisterRewarded(int id, EzoicRewardedAd ad) => Rewardeds[id] = ad;
        internal static void UnregisterRewarded(int id) => Rewardeds.TryRemove(id, out _);

        // ---- Reverse-callback delegate types (must match the Swift @convention(c) typealiases).
        // C strings arrive as IntPtr and are copied with Marshal.PtrToStringUTF8. bool is Int32.
        // [UnmanagedFunctionPointer(Cdecl)] pins the calling convention of the native thunk IL2CPP
        // emits for each so it matches the Swift @convention(c) (cdecl) callee. ----

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void IdCallback(int id);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void IdMessageCallback(int id, IntPtr message);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void IdSizeCallback(int id, int width, int height);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void IdRewardCallback(int id, IntPtr rewardType, int amount);

        // ---- Kept-alive delegate instances. Static readonly roots them for the app lifetime so
        // the function pointers handed to the Swift set_callbacks exports never dangle (IL2CPP
        // does not keep a marshaled delegate alive on its own). ----

        private static readonly IdCallback _initSuccess = OnInitSuccess;
        private static readonly IdMessageCallback _initFailure = OnInitFailure;

        private static readonly IdCallback _bannerLoaded = OnBannerLoaded;
        private static readonly IdMessageCallback _bannerLoadFailed = OnBannerLoadFailed;
        private static readonly IdCallback _bannerClicked = OnBannerClicked;
        private static readonly IdCallback _bannerImpression = OnBannerImpression;
        private static readonly IdSizeCallback _bannerSizeChanged = OnBannerSizeChanged;

        private static readonly IdCallback _interstitialLoaded = OnInterstitialLoaded;
        private static readonly IdMessageCallback _interstitialLoadFailed = OnInterstitialLoadFailed;
        private static readonly IdCallback _interstitialShown = OnInterstitialShown;
        private static readonly IdMessageCallback _interstitialFailedToShow = OnInterstitialFailedToShow;
        private static readonly IdCallback _interstitialDismissed = OnInterstitialDismissed;
        private static readonly IdCallback _interstitialImpression = OnInterstitialImpression;
        private static readonly IdCallback _interstitialClicked = OnInterstitialClicked;

        private static readonly IdCallback _rewardedLoaded = OnRewardedLoaded;
        private static readonly IdMessageCallback _rewardedLoadFailed = OnRewardedLoadFailed;
        private static readonly IdCallback _rewardedShown = OnRewardedShown;
        private static readonly IdMessageCallback _rewardedFailedToShow = OnRewardedFailedToShow;
        private static readonly IdCallback _rewardedDismissed = OnRewardedDismissed;
        private static readonly IdCallback _rewardedImpression = OnRewardedImpression;
        private static readonly IdCallback _rewardedClicked = OnRewardedClicked;
        private static readonly IdRewardCallback _rewardedUserEarnedReward = OnRewardedUserEarnedReward;

        // ---- Pending SDK-init completions, keyed by a per-call id (from NextId()). Mirrors how ad
        // instances are keyed so concurrent Initialize calls each settle their own completion — a
        // single slot would let a second Initialize overwrite the first, cross-wiring the results.
        // The SDK invokes success/failure exactly once per call; the trampoline TryRemoves the
        // entry. Touched on the main thread only, but kept concurrent for parity with the ad
        // registries. ----

        private static readonly ConcurrentDictionary<int, (Action onSuccess, Action<string> onFailure)> _pendingInit =
            new ConcurrentDictionary<int, (Action onSuccess, Action<string> onFailure)>();

        // ---- One-time callback registration. Called from the Unity main thread before the first
        // native call, so the plain bool guard needs no locking (same contract as
        // EzoicMainThreadDispatcher.Init). ----

        private static bool _callbacksRegistered;

        private static void EnsureCallbacksRegistered()
        {
            if (_callbacksRegistered)
            {
                return;
            }

            _callbacksRegistered = true;
            ezoic_unity_init_set_callbacks(_initSuccess, _initFailure);
            ezoic_unity_banner_set_callbacks(_bannerLoaded, _bannerLoadFailed, _bannerClicked, _bannerImpression, _bannerSizeChanged);
            ezoic_unity_interstitial_set_callbacks(
                _interstitialLoaded, _interstitialLoadFailed, _interstitialShown, _interstitialFailedToShow,
                _interstitialDismissed, _interstitialImpression, _interstitialClicked);
            ezoic_unity_rewarded_set_callbacks(
                _rewardedLoaded, _rewardedLoadFailed, _rewardedShown, _rewardedFailedToShow,
                _rewardedDismissed, _rewardedImpression, _rewardedClicked, _rewardedUserEarnedReward);
        }

        // ---- High-level entry points used by the managed ad classes. Each ensures callbacks are
        // registered, then forwards to the native export. bool crosses the ABI as Int32. ----

        internal static void Initialize(string domain, Action onSuccess, Action<string> onFailure)
        {
            EnsureCallbacksRegistered();
            var callId = NextId();
            _pendingInit[callId] = (onSuccess, onFailure);
            ezoic_unity_init(callId, domain);
        }

        internal static void TrackPageview()
        {
            EnsureCallbacksRegistered();
            ezoic_unity_track_pageview();
        }

        internal static void SetGDPRConsent(bool applies, string consentString)
        {
            EnsureCallbacksRegistered();
            ezoic_unity_set_gdpr_consent(applies ? 1 : 0, consentString);
        }

        internal static void SetGPPConsent(string gppString, string sectionIds)
        {
            EnsureCallbacksRegistered();
            ezoic_unity_set_gpp_consent(gppString, sectionIds);
        }

        internal static void SetSubjectToCOPPA(bool subject)
        {
            EnsureCallbacksRegistered();
            ezoic_unity_set_subject_to_coppa(subject ? 1 : 0);
        }

        /// <summary>
        /// Copies the native SDK version into a managed string and frees the native buffer.
        /// Uses <c>Marshal.PtrToStringUTF8</c> (present in the netstandard2.1 profile Unity
        /// 2021.3+ compiles against) because the Swift side hands back a strdup'd UTF-8 buffer.
        /// </summary>
        internal static string GetVersion()
        {
            var ptr = ezoic_unity_version();
            if (ptr == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
            }
            finally
            {
                ezoic_unity_free_string(ptr);
            }
        }

        internal static void BannerCreate(int id, int adUnitId, int position, string size)
        {
            EnsureCallbacksRegistered();
            ezoic_unity_banner_create(id, adUnitId, position, size);
        }

        internal static void BannerLoad(int id) => ezoic_unity_banner_load(id);
        internal static void BannerShow(int id) => ezoic_unity_banner_show(id);
        internal static void BannerHide(int id) => ezoic_unity_banner_hide(id);
        internal static void BannerDestroy(int id) => ezoic_unity_banner_destroy(id);
        internal static void BannerSetCollapseOnNoFill(int id, bool collapse) =>
            ezoic_unity_banner_set_collapse_on_no_fill(id, collapse ? 1 : 0);

        internal static void InterstitialLoad(int id, int adUnitId)
        {
            EnsureCallbacksRegistered();
            ezoic_unity_interstitial_load(id, adUnitId);
        }

        internal static void InterstitialShow(int id) => ezoic_unity_interstitial_show(id);
        internal static void InterstitialDestroy(int id) => ezoic_unity_interstitial_destroy(id);

        internal static void RewardedLoad(int id, int adUnitId)
        {
            EnsureCallbacksRegistered();
            ezoic_unity_rewarded_load(id, adUnitId);
        }

        internal static void RewardedShow(int id) => ezoic_unity_rewarded_show(id);
        internal static void RewardedDestroy(int id) => ezoic_unity_rewarded_destroy(id);

        // ---- String copy helper. The pointer is valid only during the synchronous callback, so
        // this MUST run before any Enqueue. ----

        private static string Copy(IntPtr cString)
        {
            return cString == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(cString);
        }

        // ================= Reverse-callback trampolines =================
        // Each copies any C string immediately, then enqueues the managed delivery onto the Unity
        // main thread where it looks up the wrapper and raises the event. [AOT.MonoPInvokeCallback]
        // is required so IL2CPP emits a callable native thunk for the static method.

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnInitSuccess(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (_pendingInit.TryRemove(id, out var cbs)) { cbs.onSuccess?.Invoke(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdMessageCallback))]
        private static void OnInitFailure(int id, IntPtr message)
        {
            var msg = Copy(message);
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (_pendingInit.TryRemove(id, out var cbs)) { cbs.onFailure?.Invoke(msg); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnBannerLoaded(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Banners.TryGetValue(id, out var ad)) { ad.HandleLoaded(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdMessageCallback))]
        private static void OnBannerLoadFailed(int id, IntPtr message)
        {
            var msg = Copy(message);
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Banners.TryGetValue(id, out var ad)) { ad.HandleLoadFailed(msg); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnBannerClicked(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Banners.TryGetValue(id, out var ad)) { ad.HandleClicked(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnBannerImpression(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Banners.TryGetValue(id, out var ad)) { ad.HandleImpression(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdSizeCallback))]
        private static void OnBannerSizeChanged(int id, int width, int height)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Banners.TryGetValue(id, out var ad)) { ad.HandleSizeChanged(width, height); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnInterstitialLoaded(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Interstitials.TryGetValue(id, out var ad)) { ad.HandleLoaded(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdMessageCallback))]
        private static void OnInterstitialLoadFailed(int id, IntPtr message)
        {
            var msg = Copy(message);
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Interstitials.TryGetValue(id, out var ad)) { ad.HandleLoadFailed(msg); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnInterstitialShown(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Interstitials.TryGetValue(id, out var ad)) { ad.HandleShown(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdMessageCallback))]
        private static void OnInterstitialFailedToShow(int id, IntPtr message)
        {
            var msg = Copy(message);
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Interstitials.TryGetValue(id, out var ad)) { ad.HandleFailedToShow(msg); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnInterstitialDismissed(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Interstitials.TryGetValue(id, out var ad)) { ad.HandleDismissed(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnInterstitialImpression(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Interstitials.TryGetValue(id, out var ad)) { ad.HandleImpression(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnInterstitialClicked(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Interstitials.TryGetValue(id, out var ad)) { ad.HandleClicked(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnRewardedLoaded(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Rewardeds.TryGetValue(id, out var ad)) { ad.HandleLoaded(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdMessageCallback))]
        private static void OnRewardedLoadFailed(int id, IntPtr message)
        {
            var msg = Copy(message);
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Rewardeds.TryGetValue(id, out var ad)) { ad.HandleLoadFailed(msg); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnRewardedShown(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Rewardeds.TryGetValue(id, out var ad)) { ad.HandleShown(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdMessageCallback))]
        private static void OnRewardedFailedToShow(int id, IntPtr message)
        {
            var msg = Copy(message);
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Rewardeds.TryGetValue(id, out var ad)) { ad.HandleFailedToShow(msg); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnRewardedDismissed(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Rewardeds.TryGetValue(id, out var ad)) { ad.HandleDismissed(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnRewardedImpression(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Rewardeds.TryGetValue(id, out var ad)) { ad.HandleImpression(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdCallback))]
        private static void OnRewardedClicked(int id)
        {
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Rewardeds.TryGetValue(id, out var ad)) { ad.HandleClicked(); }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(IdRewardCallback))]
        private static void OnRewardedUserEarnedReward(int id, IntPtr rewardType, int amount)
        {
            var type = Copy(rewardType);
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                if (Rewardeds.TryGetValue(id, out var ad)) { ad.HandleUserEarnedReward(type, amount); }
            });
        }

        // ================= Native externs (Runtime/Plugins/iOS/EzoicAdsUnityBridge.swift) =================
        // Names + parameter counts are kept in lockstep with the Swift @_cdecl exports by
        // tools/audit-extern.py. Strings cross C#→Swift as UTF-8 (LPUTF8Str); bool crosses as Int32.

        [DllImport("__Internal", EntryPoint = "ezoic_unity_init_set_callbacks")]
        private static extern void ezoic_unity_init_set_callbacks(IdCallback onSuccess, IdMessageCallback onFailure);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_banner_set_callbacks")]
        private static extern void ezoic_unity_banner_set_callbacks(IdCallback onLoaded, IdMessageCallback onLoadFailed, IdCallback onClicked, IdCallback onImpression, IdSizeCallback onSizeChanged);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_interstitial_set_callbacks")]
        private static extern void ezoic_unity_interstitial_set_callbacks(IdCallback onLoaded, IdMessageCallback onLoadFailed, IdCallback onShown, IdMessageCallback onFailedToShow, IdCallback onDismissed, IdCallback onImpression, IdCallback onClicked);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_rewarded_set_callbacks")]
        private static extern void ezoic_unity_rewarded_set_callbacks(IdCallback onLoaded, IdMessageCallback onLoadFailed, IdCallback onShown, IdMessageCallback onFailedToShow, IdCallback onDismissed, IdCallback onImpression, IdCallback onClicked, IdRewardCallback onUserEarnedReward);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_init")]
        private static extern void ezoic_unity_init(int callId, [MarshalAs(UnmanagedType.LPUTF8Str)] string domain);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_track_pageview")]
        private static extern void ezoic_unity_track_pageview();

        [DllImport("__Internal", EntryPoint = "ezoic_unity_set_gdpr_consent")]
        private static extern void ezoic_unity_set_gdpr_consent(int applies, [MarshalAs(UnmanagedType.LPUTF8Str)] string consentString);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_set_gpp_consent")]
        private static extern void ezoic_unity_set_gpp_consent([MarshalAs(UnmanagedType.LPUTF8Str)] string gppString, [MarshalAs(UnmanagedType.LPUTF8Str)] string sectionIds);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_set_subject_to_coppa")]
        private static extern void ezoic_unity_set_subject_to_coppa(int subject);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_version")]
        private static extern IntPtr ezoic_unity_version();

        [DllImport("__Internal", EntryPoint = "ezoic_unity_free_string")]
        private static extern void ezoic_unity_free_string(IntPtr pointer);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_banner_create")]
        private static extern void ezoic_unity_banner_create(int id, int adUnitId, int position, [MarshalAs(UnmanagedType.LPUTF8Str)] string size);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_banner_load")]
        private static extern void ezoic_unity_banner_load(int id);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_banner_show")]
        private static extern void ezoic_unity_banner_show(int id);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_banner_hide")]
        private static extern void ezoic_unity_banner_hide(int id);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_banner_destroy")]
        private static extern void ezoic_unity_banner_destroy(int id);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_banner_set_collapse_on_no_fill")]
        private static extern void ezoic_unity_banner_set_collapse_on_no_fill(int id, int collapse);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_interstitial_load")]
        private static extern void ezoic_unity_interstitial_load(int id, int adUnitId);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_interstitial_show")]
        private static extern void ezoic_unity_interstitial_show(int id);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_interstitial_destroy")]
        private static extern void ezoic_unity_interstitial_destroy(int id);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_rewarded_load")]
        private static extern void ezoic_unity_rewarded_load(int id, int adUnitId);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_rewarded_show")]
        private static extern void ezoic_unity_rewarded_show(int id);

        [DllImport("__Internal", EntryPoint = "ezoic_unity_rewarded_destroy")]
        private static extern void ezoic_unity_rewarded_destroy(int id);
    }
}
#endif
