#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;

namespace Ezoic.Ads.Android
{
    /// <summary>
    /// <c>AndroidJavaProxy</c> for <c>EzoicInterstitialAdListener</c> (presentation lifecycle).
    /// Every event is marshaled to the Unity main thread before touching the owner's C# events.
    /// </summary>
    internal sealed class InterstitialAdListenerProxy : AndroidJavaProxy
    {
        private readonly EzoicInterstitialAd _owner;

        internal InterstitialAdListenerProxy(EzoicInterstitialAd owner)
            : base(JniNames.EzoicInterstitialAdListener.Class)
        {
            _owner = owner;
        }

        public void onInterstitialAdShown(AndroidJavaObject interstitialAd)
        {
            EzoicMainThreadDispatcher.Enqueue(_owner.RaiseShown);
        }

        public void onInterstitialAdFailedToShow(AndroidJavaObject interstitialAd, AndroidJavaObject error)
        {
            var message = AndroidBridge.ErrorMessage(error);
            EzoicMainThreadDispatcher.Enqueue(() => _owner.RaiseFailedToShow(message));
        }

        public void onInterstitialAdImpression(AndroidJavaObject interstitialAd)
        {
            EzoicMainThreadDispatcher.Enqueue(_owner.RaiseImpression);
        }

        public void onInterstitialAdClicked(AndroidJavaObject interstitialAd)
        {
            EzoicMainThreadDispatcher.Enqueue(_owner.RaiseClicked);
        }

        public void onInterstitialAdDismissed(AndroidJavaObject interstitialAd)
        {
            EzoicMainThreadDispatcher.Enqueue(_owner.RaiseDismissed);
        }
    }
}
#endif
