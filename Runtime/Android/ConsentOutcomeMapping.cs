using System;

namespace Ezoic.Ads.Android
{
    /// <summary>
    /// Maps a native <c>ConsentOutcome</c> to a <see cref="ConsentResult"/> from its Java class
    /// name. Platform-independent (no JNI types) so it compiles everywhere and is unit tested;
    /// <see cref="ConsentOutcomeListenerProxy"/> supplies the JNI reads.
    /// </summary>
    internal static class ConsentOutcomeMapping
    {
        /// <param name="className">The outcome's <c>Class.getName()</c>; null when the outcome was null.</param>
        /// <param name="readDecisionName">Reads <c>Decided.getDecision().name()</c>; called only for <c>Decided</c>.</param>
        /// <param name="readErrorMessage">Reads the <c>Failed</c> error's message; called only for <c>Failed</c>.
        /// The error is an <c>EzoicError</c> through <c>EzoicAds</c> and a <c>CmpError</c> through
        /// <c>EzoicCmp</c> (native 1.22.0+); both are <c>Exception</c>s with a message.</param>
        internal static ConsentResult FromJava(string className, Func<string> readDecisionName, Func<string> readErrorMessage)
        {
            switch (className)
            {
                case null:
                    return new ConsentResult(ConsentOutcome.Failed, ConsentDecision.None, "unknown consent outcome");
                case JniNames.ConsentOutcomeNotRequired.Class:
                    return new ConsentResult(ConsentOutcome.NotRequired, ConsentDecision.None, null);
                case JniNames.ConsentOutcomeAlreadyDecided.Class:
                    return new ConsentResult(ConsentOutcome.AlreadyDecided, ConsentDecision.None, null);
                case JniNames.ConsentOutcomeDecided.Class:
                    return new ConsentResult(ConsentOutcome.Decided, DecisionFrom(readDecisionName()), null);
                case JniNames.ConsentOutcomeDismissed.Class:
                    return new ConsentResult(ConsentOutcome.Dismissed, ConsentDecision.None, null);
                case JniNames.ConsentOutcomeAlreadyPresenting.Class:
                    return new ConsentResult(ConsentOutcome.AlreadyPresenting, ConsentDecision.None, null);
                case JniNames.ConsentOutcomeFailed.Class:
                    return new ConsentResult(ConsentOutcome.Failed, ConsentDecision.None, readErrorMessage());
                default:
                    // An outcome added by a newer native SDK: closed without a known choice.
                    return new ConsentResult(ConsentOutcome.Dismissed, ConsentDecision.None, null);
            }
        }

        private static ConsentDecision DecisionFrom(string name)
        {
            switch (name)
            {
                case JniNames.ConsentDecisionType.ACCEPT_ALL: return ConsentDecision.AcceptAll;
                case JniNames.ConsentDecisionType.REJECT_ALL: return ConsentDecision.RejectAll;
                case JniNames.ConsentDecisionType.CUSTOM: return ConsentDecision.Custom;
                default: return ConsentDecision.None;
            }
        }
    }
}
