namespace Ezoic.Ads.iOS
{
    /// <summary>
    /// Value encodings shared with <c>Runtime/Plugins/iOS/EzoicAdsUnityBridge.swift</c>.
    /// Platform-independent so it compiles everywhere and is unit tested.
    /// </summary>
    internal static class IosAbi
    {
        /// <summary><c>bool?</c> as an Int32: -1 unset, 0 false, 1 true.</summary>
        internal static int EncodeOptionalBool(bool? value)
        {
            return value.HasValue ? (value.Value ? 1 : 0) : -1;
        }

        /// <summary>The native tri-state: negative is unknown (null), 0 false, anything else true.</summary>
        internal static bool? DecodeOptionalBool(int value)
        {
            return value < 0 ? (bool?)null : value != 0;
        }

        /// <summary>
        /// Builds a <see cref="ConsentResult"/> from the bridge's ints, which match
        /// <see cref="ConsentOutcome"/> and <see cref="ConsentDecision"/>. A value outside those
        /// enums (a newer bridge) reads as <see cref="ConsentOutcome.Dismissed"/> /
        /// <see cref="ConsentDecision.None"/>.
        /// </summary>
        internal static ConsentResult ConsentResultFrom(int outcome, int decision, string error)
        {
            switch ((ConsentOutcome)outcome)
            {
                case ConsentOutcome.NotRequired:
                case ConsentOutcome.AlreadyDecided:
                case ConsentOutcome.Dismissed:
                case ConsentOutcome.AlreadyPresenting:
                    return new ConsentResult((ConsentOutcome)outcome, ConsentDecision.None, null);
                case ConsentOutcome.Decided:
                    return new ConsentResult(ConsentOutcome.Decided, DecisionFrom(decision), null);
                case ConsentOutcome.Failed:
                    return new ConsentResult(ConsentOutcome.Failed, ConsentDecision.None,
                        string.IsNullOrEmpty(error) ? "consent failed" : error);
                default:
                    return new ConsentResult(ConsentOutcome.Dismissed, ConsentDecision.None, null);
            }
        }

        private static ConsentDecision DecisionFrom(int decision)
        {
            switch ((ConsentDecision)decision)
            {
                case ConsentDecision.AcceptAll:
                case ConsentDecision.RejectAll:
                case ConsentDecision.Custom:
                    return (ConsentDecision)decision;
                default:
                    return ConsentDecision.None;
            }
        }
    }
}
