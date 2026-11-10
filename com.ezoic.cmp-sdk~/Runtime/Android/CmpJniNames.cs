namespace Ezoic.Cmp.Android
{
    /// <summary>
    /// Every JNI class name and method/field name the CMP's Android bridge uses, in one place.
    /// <c>tools/audit-jni.py</c> (in the repository) parses this file and verifies with
    /// <c>javap</c> against the published <c>com.ezoic.sdk:ezoic-cmp-sdk</c> AAR that each class
    /// and member listed here exists. There must be no inline JNI-name string literals anywhere
    /// else in the bridge.
    ///
    /// Audit convention: each nested class whose <c>Class</c> constant starts with
    /// <c>com.ezoic</c> is audited; every other string constant in that nested class is treated
    /// as a member name that must exist on that class. The <see cref="Framework"/> group has no
    /// <c>Class</c> constant and is not audited.
    /// </summary>
    internal static class CmpJniNames
    {
        internal static class EzoicCmp
        {
            internal const string Class = "com.ezoic.ads.sdk.privacy.cmp.EzoicCmp";
            internal const string initialize = "initialize";
            internal const string presentConsentIfRequiredWithListener = "presentConsentIfRequiredWithListener";
            internal const string presentConsentSettingsWithListener = "presentConsentSettingsWithListener";
            internal const string resetConsent = "resetConsent";
            internal const string isConsentRequired = "isConsentRequired";
            internal const string isPrivacyOptionsRequired = "isPrivacyOptionsRequired";
        }

        internal static class EzoicCmpConfigurationBuilder
        {
            internal const string Class = "com.ezoic.ads.sdk.privacy.cmp.EzoicCmpConfiguration$Builder";
            internal const string cmpEnabled = "cmpEnabled";
            internal const string autoReadConsent = "autoReadConsent";
            internal const string debugEnabled = "debugEnabled";
            internal const string build = "build";
        }

        internal static class ConsentOutcomeListener
        {
            internal const string Class = "com.ezoic.ads.sdk.privacy.cmp.ConsentOutcomeListener";
            internal const string onOutcome = "onOutcome";
        }

        // ConsentOutcome subclasses, matched against the delivered outcome's Class.getName().
        internal static class ConsentOutcomeNotRequired
        {
            internal const string Class = "com.ezoic.ads.sdk.privacy.cmp.ConsentOutcome$NotRequired";
        }

        internal static class ConsentOutcomeAlreadyDecided
        {
            internal const string Class = "com.ezoic.ads.sdk.privacy.cmp.ConsentOutcome$AlreadyDecided";
        }

        internal static class ConsentOutcomeDecided
        {
            internal const string Class = "com.ezoic.ads.sdk.privacy.cmp.ConsentOutcome$Decided";
            internal const string getDecision = "getDecision";
        }

        internal static class ConsentOutcomeDismissed
        {
            internal const string Class = "com.ezoic.ads.sdk.privacy.cmp.ConsentOutcome$Dismissed";
        }

        internal static class ConsentOutcomeAlreadyPresenting
        {
            internal const string Class = "com.ezoic.ads.sdk.privacy.cmp.ConsentOutcome$AlreadyPresenting";
        }

        internal static class ConsentOutcomeFailed
        {
            internal const string Class = "com.ezoic.ads.sdk.privacy.cmp.ConsentOutcome$Failed";
            internal const string getError = "getError";
        }

        internal static class ConsentDecisionType
        {
            internal const string Class = "com.ezoic.ads.sdk.privacy.cmp.ConsentDecisionType";
            internal const string ACCEPT_ALL = "ACCEPT_ALL";
            internal const string REJECT_ALL = "REJECT_ALL";
            internal const string CUSTOM = "CUSTOM";
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
            internal const string getClass = "getClass";
            internal const string getName = "getName";
            internal const string getSimpleName = "getSimpleName";
            internal const string getMessage = "getMessage";
            internal const string name = "name";
            internal const string booleanValue = "booleanValue";
        }
    }
}
