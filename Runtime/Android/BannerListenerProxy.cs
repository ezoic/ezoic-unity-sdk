#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;

namespace Ezoic.Ads.Android
{
    /// <summary>
    /// <c>AndroidJavaProxy</c> for <c>com.ezoic.ads.sdk.adunits.EzoicBannerViewListener</c>.
    /// Every interface method is implemented (the interface declares them all abstract), and
    /// each event is marshaled to the Unity main thread before touching the owner's C# events.
    /// </summary>
    internal sealed class BannerListenerProxy : AndroidJavaProxy
    {
        private readonly EzoicBannerAd _owner;

        internal BannerListenerProxy(EzoicBannerAd owner)
            : base(JniNames.EzoicBannerViewListener.Class)
        {
            _owner = owner;
        }

        public void onBannerLoaded(AndroidJavaObject bannerView)
        {
            EzoicMainThreadDispatcher.Enqueue(_owner.RaiseLoaded);
        }

        public void onBannerLoadFailed(AndroidJavaObject bannerView, AndroidJavaObject error)
        {
            var message = AndroidBridge.ErrorMessage(error);
            EzoicMainThreadDispatcher.Enqueue(() => _owner.RaiseLoadFailed(message));
        }

        public void onBannerImpression(AndroidJavaObject bannerView)
        {
            EzoicMainThreadDispatcher.Enqueue(_owner.RaiseImpression);
        }

        public void onBannerClicked(AndroidJavaObject bannerView)
        {
            EzoicMainThreadDispatcher.Enqueue(_owner.RaiseClicked);
        }

        public void onBannerOpened(AndroidJavaObject bannerView)
        {
            // No public event for open; implemented so the proxy handles every interface method.
        }

        public void onBannerClosed(AndroidJavaObject bannerView)
        {
            // No public event for close; implemented so the proxy handles every interface method.
        }
    }
}
#endif
