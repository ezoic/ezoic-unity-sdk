#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace Ezoic.Ads.Android
{
    /// <summary>
    /// <c>AndroidJavaProxy</c> for <c>EzoicRewardedAdListener</c> (presentation lifecycle plus the
    /// earned-reward callback). Reward fields are read while the reward reference is still valid,
    /// then every event is marshaled to the Unity main thread.
    /// </summary>
    internal sealed class RewardedAdListenerProxy : AndroidJavaProxy
    {
        private readonly EzoicRewardedAd _owner;

        internal RewardedAdListenerProxy(EzoicRewardedAd owner)
            : base(JniNames.EzoicRewardedAdListener.Class)
        {
            _owner = owner;
        }

        public void onRewardedAdShown(AndroidJavaObject rewardedAd)
        {
            EzoicMainThreadDispatcher.Enqueue(_owner.RaiseShown);
        }

        public void onRewardedAdFailedToShow(AndroidJavaObject rewardedAd, AndroidJavaObject error)
        {
            var message = AndroidBridge.ErrorMessage(error);
            EzoicMainThreadDispatcher.Enqueue(() => _owner.RaiseFailedToShow(message));
        }

        public void onRewardedAdImpression(AndroidJavaObject rewardedAd)
        {
            EzoicMainThreadDispatcher.Enqueue(_owner.RaiseImpression);
        }

        public void onRewardedAdClicked(AndroidJavaObject rewardedAd)
        {
            EzoicMainThreadDispatcher.Enqueue(_owner.RaiseClicked);
        }

        public void onUserEarnedReward(AndroidJavaObject rewardedAd, AndroidJavaObject reward)
        {
            string type = null;
            var amount = 0;
            try
            {
                type = reward.Call<string>(JniNames.EzoicReward.getType);
                amount = reward.Call<int>(JniNames.EzoicReward.getAmount);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            var rewardType = type;
            var rewardAmount = amount;
            EzoicMainThreadDispatcher.Enqueue(() => _owner.RaiseUserEarnedReward(rewardType, rewardAmount));
        }

        public void onRewardedAdDismissed(AndroidJavaObject rewardedAd)
        {
            EzoicMainThreadDispatcher.Enqueue(_owner.RaiseDismissed);
        }
    }
}
#endif
