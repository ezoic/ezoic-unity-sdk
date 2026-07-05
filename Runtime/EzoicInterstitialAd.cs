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
    /// A full-screen interstitial ad. Call the static <see cref="Load"/> to fetch one; the
    /// <c>onLoaded</c> callback delivers a ready instance you can <see cref="Show"/>. Subscribe to
    /// the presentation events before showing. All methods are no-ops after <see cref="Destroy"/>.
    /// </summary>
    public sealed class EzoicInterstitialAd
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

        internal void RaiseShown() => OnShown?.Invoke();

        internal void RaiseFailedToShow(string error) => OnFailedToShow?.Invoke(error);

        internal void RaiseDismissed() => OnDismissed?.Invoke();

        internal void RaiseImpression() => OnImpression?.Invoke();

        internal void RaiseClicked() => OnClicked?.Invoke();

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _ad;
        private InterstitialAdListenerProxy _proxy;
        private volatile bool _destroyed;

        internal EzoicInterstitialAd(AndroidJavaObject ad)
        {
            _ad = ad;
            _proxy = new InterstitialAdListenerProxy(this);
            AndroidBridge.RunOnUiThread(() =>
            {
                if (_destroyed || _ad == null)
                {
                    return;
                }

                try
                {
                    _ad.Call(JniNames.EzoicInterstitialAd.setListener, _proxy);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }

        /// <summary>Loads an interstitial ad for <paramref name="adUnitId"/>.</summary>
        /// <param name="adUnitId">Ezoic ad unit identifier.</param>
        /// <param name="onLoaded">Invoked with a ready ad instance on success.</param>
        /// <param name="onFailed">Invoked with an error message on failure.</param>
        public static void Load(int adUnitId, Action<EzoicInterstitialAd> onLoaded, Action<string> onFailed)
        {
            EzoicMainThreadDispatcher.Init();
            var listener = new InterstitialLoadListenerProxy(onLoaded, onFailed);
            AndroidBridge.RunOnUiThread(() =>
            {
                try
                {
                    using (var adClass = new AndroidJavaClass(JniNames.EzoicInterstitialAd.Class))
                    {
                        adClass.CallStatic(JniNames.EzoicInterstitialAd.load, AndroidBridge.Activity, adUnitId, listener);
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
                    return _ad.Call<bool>(JniNames.EzoicInterstitialAd.isLoaded);
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
                    _ad.Call(JniNames.EzoicInterstitialAd.show, AndroidBridge.Activity);
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

                try { _ad.Call(JniNames.EzoicInterstitialAd.destroy); } catch (Exception e) { Debug.LogException(e); }

                _ad.Dispose();
                _ad = null;
            });
        }
#elif UNITY_IOS && !UNITY_EDITOR
        private readonly int _id;
        private Action<EzoicInterstitialAd> _onLoaded;
        private Action<string> _onFailed;
        private volatile bool _loaded;
        private volatile bool _destroyed;

        private EzoicInterstitialAd(int id, Action<EzoicInterstitialAd> onLoaded, Action<string> onFailed)
        {
            _id = id;
            _onLoaded = onLoaded;
            _onFailed = onFailed;
        }

        /// <summary>Loads an interstitial ad for <paramref name="adUnitId"/>.</summary>
        /// <param name="adUnitId">Ezoic ad unit identifier.</param>
        /// <param name="onLoaded">Invoked with a ready ad instance on success.</param>
        /// <param name="onFailed">Invoked with an error message on failure.</param>
        public static void Load(int adUnitId, Action<EzoicInterstitialAd> onLoaded, Action<string> onFailed)
        {
            EzoicMainThreadDispatcher.Init();
            var id = IosBridge.NextId();
            var ad = new EzoicInterstitialAd(id, onLoaded, onFailed);
            // Register before the native load so the load callbacks resolve the instance.
            // Removed again on terminal load failure (HandleLoadFailed) or Destroy.
            IosBridge.RegisterInterstitial(id, ad);
            try
            {
                IosBridge.InterstitialLoad(id, adUnitId);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                IosBridge.UnregisterInterstitial(id);
                EzoicMainThreadDispatcher.Enqueue(() => onFailed?.Invoke(e.Message));
            }
        }

        // Native -> managed delivery, invoked on the Unity main thread by IosBridge.
        internal void HandleLoaded()
        {
            _loaded = true;
            var cb = _onLoaded;
            _onLoaded = null;
            _onFailed = null;
            cb?.Invoke(this);
        }

        internal void HandleLoadFailed(string error)
        {
            _loaded = false;
            _destroyed = true; // terminal: native stored no ad for this id
            var cb = _onFailed;
            _onLoaded = null;
            _onFailed = null;
            IosBridge.UnregisterInterstitial(_id);
            cb?.Invoke(error);
        }

        internal void HandleShown()
        {
            _loaded = false; // presented -> consumed
            RaiseShown();
        }

        internal void HandleFailedToShow(string error)
        {
            _loaded = false;
            RaiseFailedToShow(error);
        }

        internal void HandleDismissed()
        {
            _loaded = false;
            RaiseDismissed();
        }

        internal void HandleImpression() => RaiseImpression();
        internal void HandleClicked() => RaiseClicked();

        /// <summary>True while the ad is loaded and ready to show.</summary>
        public bool IsLoaded => _loaded && !_destroyed;

        /// <summary>Presents the ad. No-op after <see cref="Destroy"/>; fires OnFailedToShow if never loaded.</summary>
        public void Show()
        {
            if (_destroyed)
            {
                return;
            }

            try
            {
                IosBridge.InterstitialShow(_id);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EzoicMainThreadDispatcher.Enqueue(() => RaiseFailedToShow(e.Message));
            }
        }

        /// <summary>Destroys the ad and releases native resources.</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;
            _loaded = false;
            IosBridge.UnregisterInterstitial(_id);
            try
            {
                IosBridge.InterstitialDestroy(_id);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
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
            Debug.LogWarning(EzoicAds.UnsupportedMessage);
        }

        internal EzoicInterstitialAd()
        {
        }

        /// <inheritdoc cref="Load(int, Action{EzoicInterstitialAd}, Action{string})"/>
        public static void Load(int adUnitId, Action<EzoicInterstitialAd> onLoaded, Action<string> onFailed)
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
