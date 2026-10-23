using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Ezoic.Ads.Android;
#elif UNITY_IOS && !UNITY_EDITOR
using Ezoic.Ads.iOS;
#endif

namespace Ezoic.Ads
{
    /// <summary>
    /// Entry point for the Ezoic Ads SDK. Initialize once with your Ezoic domain, then create
    /// banner, interstitial, or rewarded ads. All members are safe to call from the Unity main
    /// thread; the first call lazily creates the main-thread dispatcher, so make it from the
    /// main thread.
    /// </summary>
    public static class EzoicAds
    {
        internal const string UnsupportedMessage =
            "Ezoic Ads: the current platform is not supported (Android and iOS only). Calls are no-ops.";

        private const string EmptyDomainMessage = "Ezoic Ads: domain must not be empty.";

        /// <summary>
        /// Initializes the SDK for the given Ezoic domain (e.g. "example.com") with the default
        /// <see cref="EzoicConfiguration"/> (built-in consent dialog enabled).
        /// </summary>
        /// <param name="domain">Your Ezoic-registered domain.</param>
        /// <param name="onComplete">Optional result callback: <c>(success, error)</c>. On success
        /// <c>error</c> is null; on failure <c>success</c> is false and <c>error</c> describes it.</param>
        public static void Initialize(string domain, Action<bool, string> onComplete = null)
        {
            Initialize(new EzoicConfiguration(domain), onComplete);
        }

        /// <summary>
        /// Shows the built-in consent dialog if this user must decide (GDPR applies and no valid
        /// decision is stored). In GDPR regions ad loads wait for, then fail without, a decision,
        /// so call this once per app launch right after <see cref="Initialize(EzoicConfiguration, Action{bool, string})"/>
        /// (it waits for initialization to finish). Repeat calls are harmless; call it again when
        /// a previous request ended with <see cref="ConsentOutcome.Dismissed"/> or
        /// <see cref="ConsentOutcome.Failed"/>. Does nothing outside GDPR regions, when
        /// <see cref="EzoicConfiguration.CmpEnabled"/> is false, or after <see cref="SetGDPRConsent"/>.
        /// </summary>
        /// <param name="onComplete">Optional result callback, delivered on the Unity main thread
        /// after the dialog (if any) has closed.</param>
        public static void PresentConsentIfRequired(Action<ConsentResult> onComplete = null)
        {
            PresentConsent(false, onComplete);
        }

        /// <summary>
        /// Reopens the consent dialog with the user's stored choices so they can change or
        /// withdraw consent. TCF policy requires an always-reachable "Privacy settings" entry
        /// point that calls this. Reports <see cref="ConsentOutcome.NotRequired"/> outside GDPR
        /// regions, when <see cref="EzoicConfiguration.CmpEnabled"/> is false, when another CMP
        /// is present, or after <see cref="SetGDPRConsent"/>.
        /// </summary>
        /// <param name="onComplete">Optional result callback, delivered on the Unity main thread.</param>
        public static void PresentConsentSettings(Action<ConsentResult> onComplete = null)
        {
            PresentConsent(true, onComplete);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // EzoicError.AlreadyInitialized.code
        private const int AlreadyInitializedErrorCode = 1002;

        private static EzoicCallbackProxy _initProxy;
        private static volatile bool _initialized;

        /// <summary>Initializes the SDK with the given configuration.</summary>
        /// <param name="configuration">Your Ezoic domain and SDK options.</param>
        /// <param name="onComplete">Optional result callback: <c>(success, error)</c>. On success
        /// <c>error</c> is null; on failure <c>success</c> is false and <c>error</c> describes it.</param>
        public static void Initialize(EzoicConfiguration configuration, Action<bool, string> onComplete = null)
        {
            EzoicMainThreadDispatcher.Init();

            var domain = configuration?.Domain;
            if (string.IsNullOrEmpty(domain))
            {
                EzoicMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(false, EmptyDomainMessage));
                return;
            }

            var cmpEnabled = configuration.CmpEnabled;
            Action onSuccess = () =>
            {
                _initialized = true;
                EzoicMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(true, null));
                _initProxy = null;
            };
            var proxy = _initProxy = new EzoicCallbackProxy(
                onSuccess,
                (code, error) =>
                {
                    // A repeat Initialize (e.g. after a scene reload) succeeds; the native SDK
                    // keeps the configuration from the first call.
                    if (code == AlreadyInitializedErrorCode)
                    {
                        onSuccess();
                        return;
                    }

                    EzoicMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(false, error));
                    _initProxy = null;
                });

            AndroidBridge.RunOnUiThread(() =>
            {
                try
                {
                    using (var application = AndroidBridge.Activity.Call<AndroidJavaObject>(JniNames.Framework.getApplication))
                    using (var sdkClass = new AndroidJavaClass(JniNames.EzoicAds.Class))
                    using (var instance = sdkClass.CallStatic<AndroidJavaObject>(JniNames.EzoicAds.getInstance))
                    using (var nativeConfiguration = BuildConfiguration(domain, cmpEnabled))
                    {
                        instance.Call(JniNames.EzoicAds.initializeWithCallback, application, nativeConfiguration, proxy);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    EzoicMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(false, e.Message));
                }
            });
        }

        private static AndroidJavaObject BuildConfiguration(string domain, bool cmpEnabled)
        {
            using (var builder = new AndroidJavaObject(JniNames.EzoicConfigurationBuilder.Class))
            {
                // Each fluent call returns the builder again as a new JNI reference.
                builder.Call<AndroidJavaObject>(JniNames.EzoicConfigurationBuilder.domain, domain).Dispose();
                builder.Call<AndroidJavaObject>(JniNames.EzoicConfigurationBuilder.cmpEnabled, cmpEnabled).Dispose();
                return builder.Call<AndroidJavaObject>(JniNames.EzoicConfigurationBuilder.build);
            }
        }

        /// <summary>True once initialization has completed successfully.</summary>
        public static bool IsInitialized => _initialized;

        private static void PresentConsent(bool settings, Action<ConsentResult> onComplete)
        {
            EzoicMainThreadDispatcher.Init();
            var listener = new ConsentOutcomeListenerProxy(onComplete);
            AndroidBridge.RunOnUiThread(() =>
            {
                try
                {
                    using (var activity = AndroidBridge.Activity)
                    using (var sdkClass = new AndroidJavaClass(JniNames.EzoicAds.Class))
                    using (var instance = sdkClass.CallStatic<AndroidJavaObject>(JniNames.EzoicAds.getInstance))
                    {
                        var method = settings
                            ? JniNames.EzoicAds.presentConsentSettingsWithListener
                            : JniNames.EzoicAds.presentConsentIfRequiredWithListener;
                        instance.Call(method, activity, listener);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    listener.Fail(e.Message);
                }
            });
        }

        /// <summary>
        /// Deletes the decision stored by the built-in consent dialog so it is shown again (ads
        /// re-gate until the user decides). Values written by another CMP are left alone.
        /// </summary>
        public static void ResetConsent()
        {
            EzoicMainThreadDispatcher.Init();
            AndroidBridge.RunOnUiThread(() =>
            {
                try
                {
                    using (var sdkClass = new AndroidJavaClass(JniNames.EzoicAds.Class))
                    using (var instance = sdkClass.CallStatic<AndroidJavaObject>(JniNames.EzoicAds.getInstance))
                    {
                        instance.Call(JniNames.EzoicAds.resetConsent);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }

        /// <summary>
        /// Whether GDPR applies and the built-in consent dialog is in charge (true even after the
        /// user has decided). <c>null</c> until initialization completes, or when Ezoic sent no
        /// consent information. Use it to decide whether to show a "Privacy settings" button.
        /// </summary>
        public static bool? IsConsentRequired
        {
            get
            {
                // The native getter reads volatile state, so it does not need the UI thread.
                try
                {
                    using (var sdkClass = new AndroidJavaClass(JniNames.EzoicAds.Class))
                    using (var instance = sdkClass.CallStatic<AndroidJavaObject>(JniNames.EzoicAds.getInstance))
                    using (var required = instance.Call<AndroidJavaObject>(JniNames.EzoicAds.isConsentRequired))
                    {
                        return required == null ? (bool?)null : required.Call<bool>(JniNames.Framework.booleanValue);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return null;
                }
            }
        }

        /// <summary>Tracks a pageview (Ezoic session/analytics signal).</summary>
        public static void TrackPageview()
        {
            EzoicMainThreadDispatcher.Init();
            AndroidBridge.RunOnUiThread(() =>
            {
                try
                {
                    using (var sdkClass = new AndroidJavaClass(JniNames.EzoicAds.Class))
                    using (var instance = sdkClass.CallStatic<AndroidJavaObject>(JniNames.EzoicAds.getInstance))
                    {
                        instance.Call(JniNames.EzoicAds.trackPageview);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }

        /// <summary>
        /// Sets GDPR consent manually. While in effect the built-in consent dialog stands down
        /// (no gating, dialog, or <c>IABTCF_*</c> writes). It lasts for the current app launch
        /// only: call it before <see cref="Initialize(EzoicConfiguration, Action{bool, string})"/>
        /// on every launch, or set <see cref="EzoicConfiguration.CmpEnabled"/> to false.
        /// </summary>
        /// <param name="applies">Whether GDPR applies to this user.</param>
        /// <param name="consentString">IAB TCF consent string.</param>
        public static void SetGDPRConsent(bool applies, string consentString)
        {
            EzoicMainThreadDispatcher.Init();
            AndroidBridge.RunOnUiThread(() =>
            {
                try
                {
                    using (var sdkClass = new AndroidJavaClass(JniNames.EzoicAds.Class))
                    using (var instance = sdkClass.CallStatic<AndroidJavaObject>(JniNames.EzoicAds.getInstance))
                    {
                        instance.Call(JniNames.EzoicAds.setGDPRConsent, applies, consentString);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }

        /// <summary>Sets GPP (Global Privacy Platform) consent.</summary>
        /// <param name="gppString">GPP string.</param>
        /// <param name="sectionIds">Applicable GPP section IDs.</param>
        public static void SetGPPConsent(string gppString, string sectionIds)
        {
            EzoicMainThreadDispatcher.Init();
            AndroidBridge.RunOnUiThread(() =>
            {
                try
                {
                    using (var sdkClass = new AndroidJavaClass(JniNames.EzoicAds.Class))
                    using (var instance = sdkClass.CallStatic<AndroidJavaObject>(JniNames.EzoicAds.getInstance))
                    {
                        instance.Call(JniNames.EzoicAds.setGPPConsent, gppString, sectionIds);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }

        /// <summary>Flags whether the user is subject to COPPA (child-directed treatment).</summary>
        public static void SetSubjectToCOPPA(bool subject)
        {
            EzoicMainThreadDispatcher.Init();
            AndroidBridge.RunOnUiThread(() =>
            {
                try
                {
                    using (var sdkClass = new AndroidJavaClass(JniNames.EzoicAds.Class))
                    using (var instance = sdkClass.CallStatic<AndroidJavaObject>(JniNames.EzoicAds.getInstance))
                    {
                        instance.Call(JniNames.EzoicAds.setSubjectToCOPPA, subject);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }

        /// <summary>Native Ezoic Ads SDK version. Empty string on unsupported platforms.</summary>
        public static string Version
        {
            get
            {
                // Reading a static final String constant via JNI is thread-safe from any attached
                // thread (no View or lifecycle work), so it does not need the UI thread.
                try
                {
                    using (var sdkClass = new AndroidJavaClass(JniNames.EzoicAds.Class))
                    {
                        return sdkClass.GetStatic<string>(JniNames.EzoicAds.VERSION);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return string.Empty;
                }
            }
        }
#elif UNITY_IOS && !UNITY_EDITOR
        private static volatile bool _initialized;

        /// <summary>Initializes the SDK with the given configuration.</summary>
        /// <param name="configuration">Your Ezoic domain and SDK options.</param>
        /// <param name="onComplete">Optional result callback: <c>(success, error)</c>. On success
        /// <c>error</c> is null; on failure <c>success</c> is false and <c>error</c> describes it.</param>
        public static void Initialize(EzoicConfiguration configuration, Action<bool, string> onComplete = null)
        {
            EzoicMainThreadDispatcher.Init();

            var domain = configuration?.Domain;
            if (string.IsNullOrEmpty(domain))
            {
                EzoicMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(false, EmptyDomainMessage));
                return;
            }

            try
            {
                // IosBridge delivers both completions on the Unity main thread (its trampolines
                // enqueue to EzoicMainThreadDispatcher), so we update state and invoke onComplete
                // directly from these lambdas.
                IosBridge.Initialize(
                    domain,
                    configuration.CmpEnabled,
                    () =>
                    {
                        _initialized = true;
                        onComplete?.Invoke(true, null);
                    },
                    error => onComplete?.Invoke(false, error));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EzoicMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(false, e.Message));
            }
        }

        /// <summary>True once initialization has completed successfully.</summary>
        public static bool IsInitialized => _initialized;

        private static void PresentConsent(bool settings, Action<ConsentResult> onComplete)
        {
            EzoicMainThreadDispatcher.Init();
            try
            {
                IosBridge.PresentConsent(settings, onComplete);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EzoicMainThreadDispatcher.Enqueue(() =>
                    onComplete?.Invoke(new ConsentResult(ConsentOutcome.Failed, ConsentDecision.None, e.Message)));
            }
        }

        /// <summary>
        /// Deletes the decision stored by the built-in consent dialog so it is shown again (ads
        /// re-gate until the user decides). Values written by another CMP are left alone.
        /// </summary>
        public static void ResetConsent()
        {
            EzoicMainThreadDispatcher.Init();
            try
            {
                IosBridge.ResetConsent();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Whether GDPR applies and the built-in consent dialog is in charge (true even after the
        /// user has decided). <c>null</c> until initialization completes, or when Ezoic sent no
        /// consent information. Use it to decide whether to show a "Privacy settings" button.
        /// </summary>
        public static bool? IsConsentRequired
        {
            get
            {
                try
                {
                    return IosBridge.IsConsentRequired();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return null;
                }
            }
        }

        /// <summary>Tracks a pageview (Ezoic session/analytics signal).</summary>
        public static void TrackPageview()
        {
            EzoicMainThreadDispatcher.Init();
            try
            {
                IosBridge.TrackPageview();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Sets GDPR consent manually. While in effect the built-in consent dialog stands down
        /// (no gating, dialog, or <c>IABTCF_*</c> writes). It lasts for the current app launch
        /// only: call it before <see cref="Initialize(EzoicConfiguration, Action{bool, string})"/>
        /// on every launch, or set <see cref="EzoicConfiguration.CmpEnabled"/> to false.
        /// </summary>
        /// <param name="applies">Whether GDPR applies to this user.</param>
        /// <param name="consentString">IAB TCF consent string.</param>
        public static void SetGDPRConsent(bool applies, string consentString)
        {
            EzoicMainThreadDispatcher.Init();
            try
            {
                IosBridge.SetGDPRConsent(applies, consentString);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Sets GPP (Global Privacy Platform) consent.</summary>
        /// <param name="gppString">GPP string.</param>
        /// <param name="sectionIds">Applicable GPP section IDs.</param>
        public static void SetGPPConsent(string gppString, string sectionIds)
        {
            EzoicMainThreadDispatcher.Init();
            try
            {
                IosBridge.SetGPPConsent(gppString, sectionIds);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Flags whether the user is subject to COPPA (child-directed treatment).</summary>
        public static void SetSubjectToCOPPA(bool subject)
        {
            EzoicMainThreadDispatcher.Init();
            try
            {
                IosBridge.SetSubjectToCOPPA(subject);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Native Ezoic Ads SDK version. Empty string on unsupported platforms.</summary>
        public static string Version
        {
            get
            {
                try
                {
                    return IosBridge.GetVersion();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return string.Empty;
                }
            }
        }
#else
        private static bool _warned;

        private static void WarnOnce()
        {
            if (_warned)
            {
                return;
            }

            _warned = true;
            Debug.LogWarning(UnsupportedMessage);
        }

        /// <summary>Fails <paramref name="onComplete"/> asynchronously on unsupported platforms.</summary>
        public static void Initialize(EzoicConfiguration configuration, Action<bool, string> onComplete = null)
        {
            EzoicMainThreadDispatcher.Init();
            WarnOnce();
            EzoicMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(false, UnsupportedMessage));
        }

        /// <summary>Always false on unsupported platforms.</summary>
        public static bool IsInitialized => false;

        private static void PresentConsent(bool settings, Action<ConsentResult> onComplete)
        {
            EzoicMainThreadDispatcher.Init();
            WarnOnce();
            EzoicMainThreadDispatcher.Enqueue(() =>
                onComplete?.Invoke(new ConsentResult(ConsentOutcome.Failed, ConsentDecision.None, UnsupportedMessage)));
        }

        /// <summary>No-op on unsupported platforms.</summary>
        public static void ResetConsent()
        {
            EzoicMainThreadDispatcher.Init();
            WarnOnce();
        }

        /// <summary>Always null on unsupported platforms.</summary>
        public static bool? IsConsentRequired => null;

        /// <summary>No-op on unsupported platforms.</summary>
        public static void TrackPageview()
        {
            EzoicMainThreadDispatcher.Init();
            WarnOnce();
        }

        /// <summary>No-op on unsupported platforms.</summary>
        public static void SetGDPRConsent(bool applies, string consentString)
        {
            EzoicMainThreadDispatcher.Init();
            WarnOnce();
        }

        /// <summary>No-op on unsupported platforms.</summary>
        public static void SetGPPConsent(string gppString, string sectionIds)
        {
            EzoicMainThreadDispatcher.Init();
            WarnOnce();
        }

        /// <summary>No-op on unsupported platforms.</summary>
        public static void SetSubjectToCOPPA(bool subject)
        {
            EzoicMainThreadDispatcher.Init();
            WarnOnce();
        }

        /// <summary>Empty string on unsupported platforms.</summary>
        public static string Version => string.Empty;
#endif
    }
}
