using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Ezoic.Cmp.Android;
#elif UNITY_IOS && !UNITY_EDITOR
using Ezoic.Cmp.iOS;
#endif

namespace Ezoic.Cmp
{
    /// <summary>
    /// Ezoic's consent management (CMP) for apps of Ezoic publishers that don't use the Ezoic
    /// Ads package (<c>com.ezoic.ads</c>, whose <c>EzoicAds</c> has the same consent methods).
    /// It shows the IAB TCF consent dialog in GDPR regions and the privacy notice and "Do Not
    /// Sell" choice in covered US states, driven by your Ezoic settings for the domain.
    /// </summary>
    /// <remarks>
    /// Use either this or <c>EzoicAds</c>, not both. If both are initialized, in either order,
    /// <c>EzoicAds</c> takes over the same CMP and its stored decision, and these calls reach it.
    /// For Ezoic publishers only: a domain Ezoic doesn't know leaves the CMP off, and every
    /// present reports <see cref="ConsentOutcome.Failed"/>. All members are safe to call from the
    /// Unity main thread; make the first call from the main thread.
    /// </remarks>
    public static class EzoicCmp
    {
        internal const string UnsupportedMessage =
            "Ezoic CMP: the current platform is not supported (Android and iOS only). Calls are no-ops.";

        private const string EmptyDomainMessage = "Ezoic CMP: domain must not be empty.";

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        private static readonly AutoPresentGate AutoPresent = new AutoPresentGate();
#endif

        /// <summary>
        /// Starts the CMP for your Ezoic domain (e.g. "example.com") with the default
        /// <see cref="EzoicCmpConfiguration"/>. Returns at once: the settings load in the
        /// background, and <see cref="PresentConsentIfRequired"/> waits for them. With the default
        /// <see cref="EzoicCmpConfiguration.AutoPresentConsent"/> it then shows the consent dialog
        /// once if this user must decide. Call it once per launch; repeat calls are ignored by the
        /// native CMP and do not present again.
        /// </summary>
        /// <param name="domain">Your Ezoic-registered domain.</param>
        public static void Initialize(string domain)
        {
            Initialize(domain, null);
        }

        /// <summary>
        /// Shows the consent dialog if this user must decide (GDPR applies and no valid decision
        /// is stored). <see cref="Initialize(string)"/> already calls it once unless
        /// <see cref="EzoicCmpConfiguration.AutoPresentConsent"/> is false; call it yourself to
        /// read the result, and again after a <see cref="ConsentOutcome.Dismissed"/> or
        /// <see cref="ConsentOutcome.Failed"/> result. In covered US states it reports <see cref="ConsentOutcome.NotRequired"/>: the
        /// CMP shows its US privacy notice by itself.
        /// </summary>
        /// <param name="onComplete">Optional result callback, delivered on the Unity main thread
        /// after the dialog (if any) has closed.</param>
        public static void PresentConsentIfRequired(Action<ConsentResult> onComplete = null)
        {
            PresentConsent(false, onComplete);
        }

        /// <summary>
        /// The "Privacy settings" entry point; show it unless <see cref="IsPrivacyOptionsRequired"/>
        /// is false (null means not known yet). In a GDPR region it reopens the consent dialog with the stored choices (TCF
        /// policy requires an always-reachable entry point). In a covered US state it opens the US
        /// privacy settings: <see cref="ConsentOutcome.Decided"/> with
        /// <see cref="ConsentDecision.AcceptAll"/> when saved not opted out,
        /// <see cref="ConsentDecision.RejectAll"/> when saved opted out, or
        /// <see cref="ConsentOutcome.Dismissed"/> when closed without saving.
        /// <see cref="ConsentOutcome.NotRequired"/> elsewhere or when the CMP is off.
        /// </summary>
        /// <param name="onComplete">Optional result callback, delivered on the Unity main thread.</param>
        public static void PresentConsentSettings(Action<ConsentResult> onComplete = null)
        {
            PresentConsent(true, onComplete);
        }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        /// <summary>
        /// <see cref="EzoicCmpConfiguration.AutoPresentConsent"/>: the native present waits for the
        /// settings, and both bridges queue it behind the initialize call, so it can go out at once.
        /// </summary>
        private static void PresentAfterInitialize(EzoicCmpConfiguration options)
        {
            if (!AutoPresent.TryOpen(options.AutoPresentConsent))
            {
                return;
            }

            var debug = options.DebugEnabled;
            PresentConsent(false, result =>
            {
                if (debug)
                {
                    Debug.Log("Ezoic CMP: automatic consent request: " + result);
                }
            });
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Starts the CMP for your Ezoic domain with the given options.</summary>
        /// <param name="domain">Your Ezoic-registered domain.</param>
        /// <param name="configuration">Options; null uses the defaults.</param>
        public static void Initialize(string domain, EzoicCmpConfiguration configuration)
        {
            EzoicCmpMainThreadDispatcher.Init();
            if (string.IsNullOrEmpty(domain))
            {
                Debug.LogError(EmptyDomainMessage);
                return;
            }

            var options = configuration ?? new EzoicCmpConfiguration();
            var cmpEnabled = options.CmpEnabled;
            var autoReadConsent = options.AutoReadConsent;
            var debugEnabled = options.DebugEnabled;
            CmpAndroidBridge.RunOnUiThread(() =>
            {
                try
                {
                    using (var application = CmpAndroidBridge.Activity.Call<AndroidJavaObject>(CmpJniNames.Framework.getApplication))
                    using (var cmpClass = new AndroidJavaClass(CmpJniNames.EzoicCmp.Class))
                    using (var nativeConfiguration = BuildConfiguration(cmpEnabled, autoReadConsent, debugEnabled))
                    {
                        cmpClass.CallStatic(CmpJniNames.EzoicCmp.initialize, application, domain, nativeConfiguration);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
            PresentAfterInitialize(options);
        }

        private static AndroidJavaObject BuildConfiguration(bool cmpEnabled, bool autoReadConsent, bool debugEnabled)
        {
            using (var builder = new AndroidJavaObject(CmpJniNames.EzoicCmpConfigurationBuilder.Class))
            {
                // Each fluent call returns the builder again as a new JNI reference.
                builder.Call<AndroidJavaObject>(CmpJniNames.EzoicCmpConfigurationBuilder.cmpEnabled, cmpEnabled).Dispose();
                builder.Call<AndroidJavaObject>(CmpJniNames.EzoicCmpConfigurationBuilder.autoReadConsent, autoReadConsent).Dispose();
                builder.Call<AndroidJavaObject>(CmpJniNames.EzoicCmpConfigurationBuilder.debugEnabled, debugEnabled).Dispose();
                return builder.Call<AndroidJavaObject>(CmpJniNames.EzoicCmpConfigurationBuilder.build);
            }
        }

        private static void PresentConsent(bool settings, Action<ConsentResult> onComplete)
        {
            EzoicCmpMainThreadDispatcher.Init();
            var listener = new ConsentOutcomeListenerProxy(onComplete);
            CmpAndroidBridge.RunOnUiThread(() =>
            {
                try
                {
                    using (var activity = CmpAndroidBridge.Activity)
                    using (var cmpClass = new AndroidJavaClass(CmpJniNames.EzoicCmp.Class))
                    {
                        var method = settings
                            ? CmpJniNames.EzoicCmp.presentConsentSettingsWithListener
                            : CmpJniNames.EzoicCmp.presentConsentIfRequiredWithListener;
                        cmpClass.CallStatic(method, activity, listener);
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
        /// Deletes the stored decision so the consent dialog is shown again, and forgets the US
        /// choice (the US notice shows again at the next launch). Values another CMP or your app
        /// wrote are kept. Does nothing before <see cref="Initialize(string)"/>.
        /// </summary>
        public static void ResetConsent()
        {
            EzoicCmpMainThreadDispatcher.Init();
            CmpAndroidBridge.RunOnUiThread(() =>
            {
                try
                {
                    using (var cmpClass = new AndroidJavaClass(CmpJniNames.EzoicCmp.Class))
                    {
                        cmpClass.CallStatic(CmpJniNames.EzoicCmp.resetConsent);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }

        /// <summary>
        /// Whether GDPR applies and the CMP handles consent (true even after the user decided).
        /// <c>null</c> while unknown: before <see cref="Initialize(string)"/>, while the settings
        /// load, or when none could be loaded (unknown domain, offline with nothing cached).
        /// </summary>
        public static bool? IsConsentRequired => ReadOptionalBool(CmpJniNames.EzoicCmp.isConsentRequired);

        /// <summary>
        /// Whether to show a "Privacy choices" entry point that calls
        /// <see cref="PresentConsentSettings"/>: true in a GDPR region or a covered US state while
        /// the CMP handles consent, false otherwise, <c>null</c> while unknown.
        /// </summary>
        public static bool? IsPrivacyOptionsRequired => ReadOptionalBool(CmpJniNames.EzoicCmp.isPrivacyOptionsRequired);

        private static bool? ReadOptionalBool(string getter)
        {
            // The native getters are synchronized reads, so they do not need the UI thread.
            try
            {
                using (var cmpClass = new AndroidJavaClass(CmpJniNames.EzoicCmp.Class))
                using (var value = cmpClass.CallStatic<AndroidJavaObject>(getter))
                {
                    return value == null ? (bool?)null : value.Call<bool>(CmpJniNames.Framework.booleanValue);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }
#elif UNITY_IOS && !UNITY_EDITOR
        /// <summary>Starts the CMP for your Ezoic domain with the given options.</summary>
        /// <param name="domain">Your Ezoic-registered domain.</param>
        /// <param name="configuration">Options; null uses the defaults.</param>
        public static void Initialize(string domain, EzoicCmpConfiguration configuration)
        {
            EzoicCmpMainThreadDispatcher.Init();
            if (string.IsNullOrEmpty(domain))
            {
                Debug.LogError(EmptyDomainMessage);
                return;
            }

            var options = configuration ?? new EzoicCmpConfiguration();
            try
            {
                CmpIosBridge.Initialize(domain, options.CmpEnabled, options.AutoReadConsent, options.DebugEnabled);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return;
            }
            PresentAfterInitialize(options);
        }

        private static void PresentConsent(bool settings, Action<ConsentResult> onComplete)
        {
            EzoicCmpMainThreadDispatcher.Init();
            try
            {
                CmpIosBridge.PresentConsent(settings, onComplete);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EzoicCmpMainThreadDispatcher.Enqueue(() =>
                    onComplete?.Invoke(new ConsentResult(ConsentOutcome.Failed, ConsentDecision.None, e.Message)));
            }
        }

        /// <summary>
        /// Deletes the stored decision so the consent dialog is shown again, and forgets the US
        /// choice (the US notice shows again at the next launch). Values another CMP or your app
        /// wrote are kept. Does nothing before <see cref="Initialize(string)"/>.
        /// </summary>
        public static void ResetConsent()
        {
            EzoicCmpMainThreadDispatcher.Init();
            try
            {
                CmpIosBridge.ResetConsent();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Whether GDPR applies and the CMP handles consent (true even after the user decided).
        /// <c>null</c> while unknown: before <see cref="Initialize(string)"/>, while the settings
        /// load, or when none could be loaded (unknown domain, offline with nothing cached).
        /// </summary>
        public static bool? IsConsentRequired
        {
            get
            {
                try
                {
                    return CmpIosBridge.IsConsentRequired();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return null;
                }
            }
        }

        /// <summary>
        /// Whether to show a "Privacy choices" entry point that calls
        /// <see cref="PresentConsentSettings"/>: true in a GDPR region or a covered US state while
        /// the CMP handles consent, false otherwise, <c>null</c> while unknown.
        /// </summary>
        public static bool? IsPrivacyOptionsRequired
        {
            get
            {
                try
                {
                    return CmpIosBridge.IsPrivacyOptionsRequired();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return null;
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

        /// <summary>No-op on unsupported platforms (logs one warning).</summary>
        public static void Initialize(string domain, EzoicCmpConfiguration configuration)
        {
            EzoicCmpMainThreadDispatcher.Init();
            if (string.IsNullOrEmpty(domain))
            {
                Debug.LogError(EmptyDomainMessage);
                return;
            }

            WarnOnce();
        }

        private static void PresentConsent(bool settings, Action<ConsentResult> onComplete)
        {
            EzoicCmpMainThreadDispatcher.Init();
            WarnOnce();
            EzoicCmpMainThreadDispatcher.Enqueue(() =>
                onComplete?.Invoke(new ConsentResult(ConsentOutcome.Failed, ConsentDecision.None, UnsupportedMessage)));
        }

        /// <summary>No-op on unsupported platforms.</summary>
        public static void ResetConsent()
        {
            EzoicCmpMainThreadDispatcher.Init();
            WarnOnce();
        }

        /// <summary>Always null on unsupported platforms.</summary>
        public static bool? IsConsentRequired => null;

        /// <summary>Always null on unsupported platforms.</summary>
        public static bool? IsPrivacyOptionsRequired => null;
#endif
    }
}
