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

#if UNITY_ANDROID && !UNITY_EDITOR
        private static EzoicCallbackProxy _initProxy;
        private static volatile bool _initialized;

        /// <summary>Initializes the SDK for the given Ezoic domain (e.g. "example.com").</summary>
        /// <param name="domain">Your Ezoic-registered domain.</param>
        /// <param name="onComplete">Optional result callback: <c>(success, error)</c>. On success
        /// <c>error</c> is null; on failure <c>success</c> is false and <c>error</c> describes it.</param>
        public static void Initialize(string domain, Action<bool, string> onComplete = null)
        {
            EzoicMainThreadDispatcher.Init();

            if (string.IsNullOrEmpty(domain))
            {
                EzoicMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(false, "Ezoic Ads: domain must not be empty."));
                return;
            }

            _initProxy = new EzoicCallbackProxy(
                () =>
                {
                    _initialized = true;
                    EzoicMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(true, null));
                    _initProxy = null;
                },
                error =>
                {
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
                    using (var configuration = new AndroidJavaObject(JniNames.EzoicConfiguration.Class, domain))
                    {
                        instance.Call(JniNames.EzoicAds.initializeWithCallback, application, configuration, _initProxy);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    EzoicMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(false, e.Message));
                }
            });
        }

        /// <summary>True once initialization has completed successfully.</summary>
        public static bool IsInitialized => _initialized;

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

        /// <summary>Sets GDPR consent.</summary>
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

        /// <summary>Initializes the SDK for the given Ezoic domain (e.g. "example.com").</summary>
        /// <param name="domain">Your Ezoic-registered domain.</param>
        /// <param name="onComplete">Optional result callback: <c>(success, error)</c>. On success
        /// <c>error</c> is null; on failure <c>success</c> is false and <c>error</c> describes it.</param>
        public static void Initialize(string domain, Action<bool, string> onComplete = null)
        {
            EzoicMainThreadDispatcher.Init();

            if (string.IsNullOrEmpty(domain))
            {
                EzoicMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(false, "Ezoic Ads: domain must not be empty."));
                return;
            }

            try
            {
                // IosBridge delivers both completions on the Unity main thread (its trampolines
                // enqueue to EzoicMainThreadDispatcher), so we update state and invoke onComplete
                // directly from these lambdas.
                IosBridge.Initialize(
                    domain,
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

        /// <summary>Sets GDPR consent.</summary>
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

        /// <inheritdoc cref="Initialize(string, Action{bool, string})"/>
        public static void Initialize(string domain, Action<bool, string> onComplete = null)
        {
            EzoicMainThreadDispatcher.Init();
            WarnOnce();
            EzoicMainThreadDispatcher.Enqueue(() => onComplete?.Invoke(false, UnsupportedMessage));
        }

        /// <summary>Always false on unsupported platforms.</summary>
        public static bool IsInitialized => false;

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
