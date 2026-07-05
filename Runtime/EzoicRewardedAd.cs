using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Ezoic.Ads.Android;
#endif

namespace Ezoic.Ads
{
    /// <summary>
    /// A rewarded ad that grants a reward when the user watches it to completion. Load with the
    /// static <see cref="Load"/>, subscribe to <see cref="OnUserEarnedReward"/> plus the
    /// presentation events, then <see cref="Show"/>. All methods are no-ops after
    /// <see cref="Destroy"/>.
    /// </summary>
    public sealed class EzoicRewardedAd
    {
        /// <summary>Raised when the ad begins presenting.</summary>
        public event Action OnShown;

        /// <summary>Raised when presentation fails; the argument is an error message.</summary>
        public event Action<string> OnFailedToShow;

        /// <summary>Raised when the ad is dismissed and control returns to the game.</summary>
        public event Action OnDismissed;

        /// <summary>Raised when the ad records an impression.</summary>
        public event Action OnImpression;

        /// <summary>Raised when the ad is clicked.</summary>
        public event Action OnClicked;

        /// <summary>Raised when the user earns a reward: <c>(type, amount)</c>.</summary>
        public event Action<string, int> OnUserEarnedReward;

        internal void RaiseShown() => OnShown?.Invoke();

        internal void RaiseFailedToShow(string error) => OnFailedToShow?.Invoke(error);

        internal void RaiseDismissed() => OnDismissed?.Invoke();

        internal void RaiseImpression() => OnImpression?.Invoke();

        internal void RaiseClicked() => OnClicked?.Invoke();

        internal void RaiseUserEarnedReward(string type, int amount) => OnUserEarnedReward?.Invoke(type, amount);

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _ad;
        private RewardedAdListenerProxy _proxy;
        private volatile bool _destroyed;

        internal EzoicRewardedAd(AndroidJavaObject ad)
        {
            _ad = ad;
            _proxy = new RewardedAdListenerProxy(this);
            AndroidBridge.RunOnUiThread(() =>
            {
                if (_destroyed || _ad == null)
                {
                    return;
                }

                try
                {
                    _ad.Call(JniNames.EzoicRewardedAd.setListener, _proxy);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }

        /// <summary>Loads a rewarded ad for <paramref name="adUnitId"/>.</summary>
        /// <param name="adUnitId">Ezoic ad unit identifier.</param>
        /// <param name="onLoaded">Invoked with a ready ad instance on success.</param>
        /// <param name="onFailed">Invoked with an error message on failure.</param>
        public static void Load(int adUnitId, Action<EzoicRewardedAd> onLoaded, Action<string> onFailed)
        {
            EzoicMainThreadDispatcher.Init();
            var listener = new RewardedLoadListenerProxy(onLoaded, onFailed);
            AndroidBridge.RunOnUiThread(() =>
            {
                try
                {
                    using (var adClass = new AndroidJavaClass(JniNames.EzoicRewardedAd.Class))
                    {
                        adClass.CallStatic(JniNames.EzoicRewardedAd.load, AndroidBridge.Activity, adUnitId, listener);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    EzoicMainThreadDispatcher.Enqueue(() => onFailed?.Invoke(e.Message));
                }
            });
        }

        /// <summary>True while the ad is loaded and ready to show.</summary>
        public bool IsLoaded
        {
            get
            {
                // Calling a plain boolean JNI getter is thread-safe from any attached thread (no
                // View or lifecycle work), so it does not need the UI thread.
                if (_destroyed || _ad == null)
                {
                    return false;
                }

                try
                {
                    return _ad.Call<bool>(JniNames.EzoicRewardedAd.isLoaded);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return false;
                }
            }
        }

        /// <summary>Presents the ad. No-op after <see cref="Destroy"/>.</summary>
        public void Show()
        {
            if (_destroyed)
            {
                return;
            }

            AndroidBridge.RunOnUiThread(() =>
            {
                if (_destroyed || _ad == null)
                {
                    return;
                }

                try
                {
                    _ad.Call(JniNames.EzoicRewardedAd.show, AndroidBridge.Activity);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    EzoicMainThreadDispatcher.Enqueue(() => RaiseFailedToShow(e.Message));
                }
            });
        }

        /// <summary>Destroys the ad and releases native resources.</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;

            AndroidBridge.RunOnUiThread(() =>
            {
                if (_ad == null)
                {
                    return;
                }

                try { _ad.Call(JniNames.EzoicRewardedAd.destroy); } catch (Exception e) { Debug.LogException(e); }

                _ad.Dispose();
                _ad = null;
            });
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
            Debug.LogWarning(EzoicAds.UnsupportedMessage);
        }

        internal EzoicRewardedAd()
        {
        }

        /// <inheritdoc cref="Load(int, Action{EzoicRewardedAd}, Action{string})"/>
        public static void Load(int adUnitId, Action<EzoicRewardedAd> onLoaded, Action<string> onFailed)
        {
            EzoicMainThreadDispatcher.Init();
            WarnOnce();
            EzoicMainThreadDispatcher.Enqueue(() => onFailed?.Invoke(EzoicAds.UnsupportedMessage));
        }

        /// <summary>Always false on unsupported platforms.</summary>
        public bool IsLoaded => false;

        /// <summary>Fires <see cref="OnFailedToShow"/> asynchronously on unsupported platforms.</summary>
        public void Show()
        {
            WarnOnce();
            EzoicMainThreadDispatcher.Enqueue(() => RaiseFailedToShow(EzoicAds.UnsupportedMessage));
        }

        /// <summary>No-op on unsupported platforms.</summary>
        public void Destroy()
        {
        }
#endif
    }
}
