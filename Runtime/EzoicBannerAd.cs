using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Ezoic.Ads.Android;
#endif

namespace Ezoic.Ads
{
    /// <summary>
    /// A banner ad anchored to a screen position. Construct it, subscribe to events, then call
    /// <see cref="Load"/>. Use <see cref="Show"/>/<see cref="Hide"/> to toggle visibility and
    /// <see cref="Destroy"/> to release it. All methods are no-ops after <see cref="Destroy"/>
    /// and never throw.
    /// </summary>
    public sealed class EzoicBannerAd
    {
        /// <summary>Raised when the banner successfully loads an ad.</summary>
        public event Action OnLoaded;

        /// <summary>Raised when the banner fails to load; the argument is an error message.</summary>
        public event Action<string> OnLoadFailed;

        /// <summary>Raised when the banner is clicked.</summary>
        public event Action OnClicked;

        /// <summary>Raised when the banner records an impression.</summary>
        public event Action OnImpression;

        internal void RaiseLoaded() => OnLoaded?.Invoke();

        internal void RaiseLoadFailed(string error) => OnLoadFailed?.Invoke(error);

        internal void RaiseClicked() => OnClicked?.Invoke();

        internal void RaiseImpression() => OnImpression?.Invoke();

#if UNITY_ANDROID && !UNITY_EDITOR
        // android.view.View visibility constants.
        private const int VisibilityVisible = 0; // View.VISIBLE
        private const int VisibilityGone = 8;    // View.GONE

        // android.view.ViewGroup.LayoutParams.WRAP_CONTENT.
        private const int WrapContent = -2;

        private readonly int _adUnitId;
        private readonly BannerPosition _position;
        private readonly string _size;

        private AndroidJavaObject _view;
        private BannerListenerProxy _proxy;
        private volatile bool _destroyed;
        private volatile bool _createFailed;

        /// <summary>Creates a banner and adds it to the Activity content view at <paramref name="position"/>.</summary>
        /// <param name="adUnitId">Ezoic ad unit identifier.</param>
        /// <param name="position">Screen anchor position.</param>
        /// <param name="size">Optional size string such as "320x50"; null requests an adaptive banner.</param>
        public EzoicBannerAd(int adUnitId, BannerPosition position, string size = null)
        {
            _adUnitId = adUnitId;
            _position = position;
            _size = size;

            EzoicMainThreadDispatcher.Init();
            _proxy = new BannerListenerProxy(this);

            AndroidBridge.RunOnUiThread(() =>
            {
                if (_destroyed)
                {
                    return;
                }

                try
                {
                    var activity = AndroidBridge.Activity;
                    _view = new AndroidJavaObject(JniNames.EzoicBannerView.Class, activity, _adUnitId);
                    _view.Call(JniNames.EzoicBannerView.setListener, _proxy);

                    var gravity = GravityFor(_position);
                    using (var layoutParams = new AndroidJavaObject(JniNames.Framework.FrameLayoutLayoutParams, WrapContent, WrapContent, gravity))
                    {
                        activity.Call(JniNames.Framework.addContentView, _view, layoutParams);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _createFailed = true;
                }
            });
        }

        /// <summary>Requests an ad. No-op after <see cref="Destroy"/>.</summary>
        public void Load()
        {
            if (_destroyed)
            {
                return;
            }

            if (_createFailed)
            {
                EzoicMainThreadDispatcher.Enqueue(() => RaiseLoadFailed("Ezoic Ads: banner view creation failed; see earlier exception."));
                return;
            }

            AndroidBridge.RunOnUiThread(() =>
            {
                if (_destroyed)
                {
                    return;
                }

                if (_view == null)
                {
                    EzoicMainThreadDispatcher.Enqueue(() => RaiseLoadFailed("Ezoic Ads: banner view creation failed; see earlier exception."));
                    return;
                }

                try
                {
                    if (string.IsNullOrEmpty(_size))
                    {
                        _view.Call(JniNames.EzoicBannerView.loadAd);
                    }
                    else
                    {
                        _view.Call(JniNames.EzoicBannerView.loadAd, _size);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }

        /// <summary>Makes the banner visible. No-op after <see cref="Destroy"/>.</summary>
        public void Show() => SetVisibility(VisibilityVisible);

        /// <summary>Hides the banner without destroying it. No-op after <see cref="Destroy"/>.</summary>
        public void Hide() => SetVisibility(VisibilityGone);

        /// <summary>Stops loading, destroys the native view, and removes it from the view tree.</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;

            AndroidBridge.RunOnUiThread(() =>
            {
                if (_view == null)
                {
                    return;
                }

                try { _view.Call(JniNames.EzoicBannerView.stopLoading); } catch (Exception e) { Debug.LogException(e); }
                try { _view.Call(JniNames.EzoicBannerView.destroy); } catch (Exception e) { Debug.LogException(e); }

                try
                {
                    using (var parent = _view.Call<AndroidJavaObject>(JniNames.Framework.getParent))
                    {
                        if (parent != null)
                        {
                            parent.Call(JniNames.Framework.removeView, _view);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                _view.Dispose();
                _view = null;
            });
        }

        private void SetVisibility(int visibility)
        {
            if (_destroyed)
            {
                return;
            }

            AndroidBridge.RunOnUiThread(() =>
            {
                if (_destroyed || _view == null)
                {
                    return;
                }

                try
                {
                    _view.Call(JniNames.Framework.setVisibility, visibility);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }

        // Composed android.view.Gravity constants (stable platform values).
        private static int GravityFor(BannerPosition position)
        {
            const int top = 48;      // Gravity.TOP
            const int bottom = 80;   // Gravity.BOTTOM
            const int left = 3;      // Gravity.LEFT
            const int right = 5;     // Gravity.RIGHT
            const int centerH = 1;   // Gravity.CENTER_HORIZONTAL
            const int center = 17;   // Gravity.CENTER

            switch (position)
            {
                case BannerPosition.Top: return top | centerH;
                case BannerPosition.Bottom: return bottom | centerH;
                case BannerPosition.TopLeft: return top | left;
                case BannerPosition.TopRight: return top | right;
                case BannerPosition.BottomLeft: return bottom | left;
                case BannerPosition.BottomRight: return bottom | right;
                case BannerPosition.Center: return center;
                default: return bottom | centerH;
            }
        }
#else
        private bool _warned;

        /// <inheritdoc cref="EzoicBannerAd(int, BannerPosition, string)"/>
        public EzoicBannerAd(int adUnitId, BannerPosition position, string size = null)
        {
            EzoicMainThreadDispatcher.Init();
            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning(EzoicAds.UnsupportedMessage);
            }
        }

        /// <summary>Fires <see cref="OnLoadFailed"/> asynchronously on unsupported platforms.</summary>
        public void Load()
        {
            EzoicMainThreadDispatcher.Enqueue(() => RaiseLoadFailed(EzoicAds.UnsupportedMessage));
        }

        /// <summary>No-op on unsupported platforms.</summary>
        public void Show()
        {
        }

        /// <summary>No-op on unsupported platforms.</summary>
        public void Hide()
        {
        }

        /// <summary>No-op on unsupported platforms.</summary>
        public void Destroy()
        {
        }
#endif
    }
}
