#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace Ezoic.Ads.Android
{
    /// <summary>
    /// <c>AndroidJavaProxy</c> for <c>EzoicInterstitialAdLoadListener</c>. Retains the delivered
    /// ad object with <c>CloneReference()</c> (the raw argument is disposed when the proxy call
    /// returns) and marshals the result to the Unity main thread.
    /// </summary>
    internal sealed class InterstitialLoadListenerProxy : AndroidJavaProxy
    {
        private readonly Action<EzoicInterstitialAd> _onLoaded;
        private readonly Action<string> _onFailed;

        internal InterstitialLoadListenerProxy(Action<EzoicInterstitialAd> onLoaded, Action<string> onFailed)
            : base(JniNames.EzoicInterstitialAdLoadListener.Class)
        {
            _onLoaded = onLoaded;
            _onFailed = onFailed;
            AndroidBridge.Retain(this);
        }

        public void onInterstitialAdLoaded(AndroidJavaObject interstitialAd)
        {
            var retained = interstitialAd.CloneReference();
            AndroidBridge.Release(this);
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                var ad = new EzoicInterstitialAd(retained);
                _onLoaded?.Invoke(ad);
            });
        }

        public void onInterstitialAdFailedToLoad(AndroidJavaObject error)
        {
            var message = AndroidBridge.ErrorMessage(error);
            AndroidBridge.Release(this);
            EzoicMainThreadDispatcher.Enqueue(() => _onFailed?.Invoke(message));
        }
    }
}
#endif
