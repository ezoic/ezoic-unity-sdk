#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace Ezoic.Ads.Android
{
    /// <summary>
    /// <c>AndroidJavaProxy</c> for <c>EzoicRewardedAdLoadListener</c>. Retains the delivered ad
    /// object with <c>CloneReference()</c> and marshals the result to the Unity main thread.
    /// </summary>
    internal sealed class RewardedLoadListenerProxy : AndroidJavaProxy
    {
        private readonly Action<EzoicRewardedAd> _onLoaded;
        private readonly Action<string> _onFailed;

        internal RewardedLoadListenerProxy(Action<EzoicRewardedAd> onLoaded, Action<string> onFailed)
            : base(JniNames.EzoicRewardedAdLoadListener.Class)
        {
            _onLoaded = onLoaded;
            _onFailed = onFailed;
            AndroidBridge.Retain(this);
        }

        public void onRewardedAdLoaded(AndroidJavaObject rewardedAd)
        {
            var retained = rewardedAd.CloneReference();
            AndroidBridge.Release(this);
            EzoicMainThreadDispatcher.Enqueue(() =>
            {
                var ad = new EzoicRewardedAd(retained);
                _onLoaded?.Invoke(ad);
            });
        }

        public void onRewardedAdFailedToLoad(AndroidJavaObject error)
        {
            var message = AndroidBridge.ErrorMessage(error);
            AndroidBridge.Release(this);
            EzoicMainThreadDispatcher.Enqueue(() => _onFailed?.Invoke(message));
        }
    }
}
#endif
