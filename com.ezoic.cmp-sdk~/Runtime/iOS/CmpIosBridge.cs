#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ezoic.Cmp.iOS
{
    /// <summary>
    /// P/Invoke plumbing for the CMP's iOS bridge: the <c>[DllImport("__Internal")]</c> externs
    /// that call into <c>Runtime/Plugins/iOS/EzoicCmpUnityBridge.swift</c> and the consent-result
    /// trampoline the Swift side invokes.
    /// </summary>
    /// <remarks>
    /// Same contract as com.ezoic.ads' IosBridge: called from the Unity main thread; the Swift side
    /// invokes the trampoline on the iOS main thread, and it re-enqueues onto
    /// <see cref="EzoicCmpMainThreadDispatcher"/> after copying the C string (valid only for the
    /// synchronous call). Export names are prefixed <c>ezoic_cmp_unity_</c> so they never clash
    /// with com.ezoic.ads' <c>ezoic_unity_</c> exports when both packages are installed.
    /// </remarks>
    internal static class CmpIosBridge
    {
        private static int _lastId;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ConsentCallback(int id, int outcome, int decision, IntPtr error);

        // Rooted for the app lifetime so the function pointer handed to Swift never dangles.
        private static readonly ConsentCallback _consentResult = OnConsentResult;

        // Pending consent completions keyed by a per-call id, so overlapping requests each settle
        // their own callback.
        private static readonly ConcurrentDictionary<int, Action<ConsentResult>> _pendingConsent =
            new ConcurrentDictionary<int, Action<ConsentResult>>();

        private static bool _callbacksRegistered;

        private static void EnsureCallbacksRegistered()
        {
            if (_callbacksRegistered)
            {
                return;
            }

            _callbacksRegistered = true;
            ezoic_cmp_unity_consent_set_callbacks(_consentResult);
        }

        internal static void Initialize(string domain, bool cmpEnabled, bool autoReadConsent, bool debugEnabled)
        {
            EnsureCallbacksRegistered();
            ezoic_cmp_unity_init(domain, cmpEnabled ? 1 : 0, autoReadConsent ? 1 : 0, debugEnabled ? 1 : 0);
        }

        internal static void PresentConsent(bool settings, Action<ConsentResult> onComplete)
        {
            EnsureCallbacksRegistered();
            var callId = Interlocked.Increment(ref _lastId);
            _pendingConsent[callId] = onComplete;
            if (settings)
            {
                ezoic_cmp_unity_present_consent_settings(callId);
            }
            else
            {
                ezoic_cmp_unity_present_consent_if_required(callId);
            }
        }

        internal static void ResetConsent()
        {
            EnsureCallbacksRegistered();
            ezoic_cmp_unity_reset_consent();
        }

        internal static bool? IsConsentRequired()
        {
            return IosAbi.DecodeOptionalBool(ezoic_cmp_unity_is_consent_required());
        }

        internal static bool? IsPrivacyOptionsRequired()
        {
            return IosAbi.DecodeOptionalBool(ezoic_cmp_unity_is_privacy_options_required());
        }

        [AOT.MonoPInvokeCallback(typeof(ConsentCallback))]
        private static void OnConsentResult(int id, int outcome, int decision, IntPtr error)
        {
            var msg = error == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(error);
            EzoicCmpMainThreadDispatcher.Enqueue(() =>
            {
                if (_pendingConsent.TryRemove(id, out var cb))
                {
                    cb?.Invoke(IosAbi.ConsentResultFrom(outcome, decision, msg));
                }
            });
        }

        // ================= Native externs (Runtime/Plugins/iOS/EzoicCmpUnityBridge.swift) =================
        // Names + parameter counts are kept in lockstep with the Swift @_cdecl exports by
        // tools/audit-extern.py. Strings cross as UTF-8 (LPUTF8Str); bool crosses as Int32.

        [DllImport("__Internal", EntryPoint = "ezoic_cmp_unity_consent_set_callbacks")]
        private static extern void ezoic_cmp_unity_consent_set_callbacks(ConsentCallback onResult);

        [DllImport("__Internal", EntryPoint = "ezoic_cmp_unity_init")]
        private static extern void ezoic_cmp_unity_init([MarshalAs(UnmanagedType.LPUTF8Str)] string domain, int cmpEnabled, int autoReadConsent, int debugEnabled);

        [DllImport("__Internal", EntryPoint = "ezoic_cmp_unity_present_consent_if_required")]
        private static extern void ezoic_cmp_unity_present_consent_if_required(int callId);

        [DllImport("__Internal", EntryPoint = "ezoic_cmp_unity_present_consent_settings")]
        private static extern void ezoic_cmp_unity_present_consent_settings(int callId);

        [DllImport("__Internal", EntryPoint = "ezoic_cmp_unity_reset_consent")]
        private static extern void ezoic_cmp_unity_reset_consent();

        [DllImport("__Internal", EntryPoint = "ezoic_cmp_unity_is_consent_required")]
        private static extern int ezoic_cmp_unity_is_consent_required();

        [DllImport("__Internal", EntryPoint = "ezoic_cmp_unity_is_privacy_options_required")]
        private static extern int ezoic_cmp_unity_is_privacy_options_required();
    }
}
#endif
