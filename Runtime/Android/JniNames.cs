namespace Ezoic.Ads.Android
{
    /// <summary>
    /// Every JNI class name and method/field name used by the Android bridge, in one place.
    /// The <c>tools/audit-jni.py</c> script parses this file and verifies (via <c>javap</c>
    /// against the published AAR) that each Ezoic SDK class and member listed here actually
    /// exists. There must be no inline JNI-name string literals anywhere else in the bridge.
    ///
    /// Audit convention: each nested class whose <c>Class</c> constant starts with
    /// <c>com.ezoic</c> is audited; every other string constant in that nested class is treated
    /// as a member name that must exist on that class. The <see cref="Framework"/> group (Unity
    /// and Android platform names) has no <c>Class</c> constant and is intentionally not audited
    /// against the Ezoic AAR.
    /// </summary>
    internal static class JniNames
    {
        internal static class EzoicAds
        {
            internal const string Class = "com.ezoic.ads.sdk.core.EzoicAds";
            internal const string getInstance = "getInstance";
            internal const string initializeWithCallback = "initializeWithCallback";
            internal const string trackPageview = "trackPageview";
            internal const string setGDPRConsent = "setGDPRConsent";
            internal const string setGPPConsent = "setGPPConsent";
            internal const string setSubjectToCOPPA = "setSubjectToCOPPA";
            internal const string VERSION = "VERSION";
        }

        internal static class EzoicConfiguration
        {
            internal const string Class = "com.ezoic.ads.sdk.core.EzoicConfiguration";
        }

        internal static class EzoicCallback
        {
            internal const string Class = "com.ezoic.ads.sdk.core.EzoicCallback";
            internal const string onSuccess = "onSuccess";
            internal const string onError = "onError";
        }

        internal static class EzoicError
        {
            internal const string Class = "com.ezoic.ads.sdk.core.EzoicError";
            internal const string getMessage = "getMessage";
        }

        internal static class EzoicBannerView
        {
            internal const string Class = "com.ezoic.ads.sdk.adunits.EzoicBannerView";
            internal const string setListener = "setListener";
            internal const string setCollapseOnNoFill = "setCollapseOnNoFill";
            internal const string loadAd = "loadAd";
            internal const string stopLoading = "stopLoading";
            internal const string destroy = "destroy";
        }

        internal static class EzoicBannerViewListener
        {
            internal const string Class = "com.ezoic.ads.sdk.adunits.EzoicBannerViewListener";
            internal const string onBannerLoaded = "onBannerLoaded";
            internal const string onBannerLoadFailed = "onBannerLoadFailed";
            internal const string onBannerImpression = "onBannerImpression";
            internal const string onBannerClicked = "onBannerClicked";
            internal const string onBannerOpened = "onBannerOpened";
            internal const string onBannerClosed = "onBannerClosed";
            internal const string onBannerSizeChanged = "onBannerSizeChanged";
        }

        internal static class EzoicInterstitialAd
        {
            internal const string Class = "com.ezoic.ads.sdk.adunits.EzoicInterstitialAd";
            internal const string load = "load";
            internal const string show = "show";
            internal const string destroy = "destroy";
            internal const string isLoaded = "isLoaded";
            internal const string setListener = "setListener";
        }

        internal static class EzoicInterstitialAdLoadListener
        {
            internal const string Class = "com.ezoic.ads.sdk.adunits.EzoicInterstitialAdLoadListener";
            internal const string onInterstitialAdLoaded = "onInterstitialAdLoaded";
            internal const string onInterstitialAdFailedToLoad = "onInterstitialAdFailedToLoad";
        }

        internal static class EzoicInterstitialAdListener
        {
            internal const string Class = "com.ezoic.ads.sdk.adunits.EzoicInterstitialAdListener";
            internal const string onInterstitialAdShown = "onInterstitialAdShown";
            internal const string onInterstitialAdFailedToShow = "onInterstitialAdFailedToShow";
            internal const string onInterstitialAdImpression = "onInterstitialAdImpression";
            internal const string onInterstitialAdClicked = "onInterstitialAdClicked";
            internal const string onInterstitialAdDismissed = "onInterstitialAdDismissed";
        }

        internal static class EzoicRewardedAd
        {
            internal const string Class = "com.ezoic.ads.sdk.adunits.EzoicRewardedAd";
            internal const string load = "load";
            internal const string show = "show";
            internal const string destroy = "destroy";
            internal const string isLoaded = "isLoaded";
            internal const string setListener = "setListener";
        }

        internal static class EzoicRewardedAdLoadListener
        {
            internal const string Class = "com.ezoic.ads.sdk.adunits.EzoicRewardedAdLoadListener";
            internal const string onRewardedAdLoaded = "onRewardedAdLoaded";
            internal const string onRewardedAdFailedToLoad = "onRewardedAdFailedToLoad";
        }

        internal static class EzoicRewardedAdListener
        {
            internal const string Class = "com.ezoic.ads.sdk.adunits.EzoicRewardedAdListener";
            internal const string onRewardedAdShown = "onRewardedAdShown";
            internal const string onRewardedAdFailedToShow = "onRewardedAdFailedToShow";
            internal const string onRewardedAdImpression = "onRewardedAdImpression";
            internal const string onRewardedAdClicked = "onRewardedAdClicked";
            internal const string onUserEarnedReward = "onUserEarnedReward";
            internal const string onRewardedAdDismissed = "onRewardedAdDismissed";
        }

        internal static class EzoicReward
        {
            internal const string Class = "com.ezoic.ads.sdk.adunits.EzoicReward";
            internal const string getType = "getType";
            internal const string getAmount = "getAmount";
        }

        /// <summary>
        /// Unity and Android platform JNI names. Not audited against the Ezoic AAR (these are
        /// guaranteed by the Unity player and the Android platform, not the Ezoic SDK).
        /// </summary>
        internal static class Framework
        {
            internal const string UnityPlayer = "com.unity3d.player.UnityPlayer";
            internal const string currentActivity = "currentActivity";
            internal const string getApplication = "getApplication";
            internal const string runOnUiThread = "runOnUiThread";
            internal const string addContentView = "addContentView";
            internal const string setVisibility = "setVisibility";
            internal const string getParent = "getParent";
            internal const string removeView = "removeView";
            internal const string getClass = "getClass";
            internal const string getSimpleName = "getSimpleName";
            internal const string FrameLayoutLayoutParams = "android.widget.FrameLayout$LayoutParams";
        }
    }
}
