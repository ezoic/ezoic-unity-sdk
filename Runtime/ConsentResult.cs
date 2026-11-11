namespace Ezoic.Cmp
{
    // Same shape and values as Ezoic.Ads.ConsentResult / ConsentOutcome / ConsentDecision in
    // com.ezoic.ads, so one consent guide covers both packages.

    /// <summary>
    /// What a consent request did. Reported by <see cref="EzoicCmp.PresentConsentIfRequired"/>
    /// and <see cref="EzoicCmp.PresentConsentSettings"/>.
    /// </summary>
    /// <remarks>The numeric values are part of the iOS bridge ABI; do not reorder.</remarks>
    public enum ConsentOutcome
    {
        /// <summary>
        /// GDPR does not apply (for <see cref="EzoicCmp.PresentConsentIfRequired"/>, also in
        /// covered US states, where the CMP shows its notice by itself), the CMP is off
        /// (<see cref="EzoicCmpConfiguration.CmpEnabled"/> or your Ezoic settings), another CMP
        /// owns consent, or the app manages consent itself
        /// (<see cref="EzoicCmpConfiguration.AutoReadConsent"/> is false).
        /// </summary>
        NotRequired = 0,

        /// <summary>A still-valid decision is stored; no dialog was shown.</summary>
        AlreadyDecided = 1,

        /// <summary>
        /// The user made a choice (see <see cref="ConsentResult.Decision"/>), which has been saved.
        /// From the US privacy settings: <see cref="ConsentDecision.AcceptAll"/> means not opted
        /// out, <see cref="ConsentDecision.RejectAll"/> means opted out ("Do Not Sell").
        /// </summary>
        Decided = 2,

        /// <summary>
        /// The dialog or settings screen closed without a choice. Ask again later with
        /// <see cref="EzoicCmp.PresentConsentIfRequired"/>.
        /// </summary>
        Dismissed = 3,

        /// <summary>A consent dialog is already on screen or being prepared.</summary>
        AlreadyPresenting = 4,

        /// <summary>
        /// The dialog could not be shown (see <see cref="ConsentResult.Error"/>). Also reported
        /// before <see cref="EzoicCmp.Initialize(string, EzoicCmpConfiguration)"/>, and for every
        /// call when Ezoic refused the domain (the CMP is for Ezoic publishers only).
        /// </summary>
        Failed = 5
    }

    /// <summary>The button the user closed the consent dialog with.</summary>
    /// <remarks>The numeric values are part of the iOS bridge ABI; do not reorder.</remarks>
    public enum ConsentDecision
    {
        /// <summary>No decision was made in this request (any outcome other than <see cref="ConsentOutcome.Decided"/>).</summary>
        None = 0,

        /// <summary>The user accepted all purposes and vendors (US settings: not opted out).</summary>
        AcceptAll = 1,

        /// <summary>The user rejected all purposes and vendors (US settings: opted out).</summary>
        RejectAll = 2,

        /// <summary>The user saved a custom selection.</summary>
        Custom = 3
    }

    /// <summary>Result of a consent request, delivered on the Unity main thread.</summary>
    public sealed class ConsentResult
    {
        internal ConsentResult(ConsentOutcome outcome, ConsentDecision decision, string error)
        {
            Outcome = outcome;
            Decision = decision;
            Error = error;
        }

        /// <summary>What the request did.</summary>
        public ConsentOutcome Outcome { get; }

        /// <summary>The user's choice when <see cref="Outcome"/> is <see cref="ConsentOutcome.Decided"/>; otherwise <see cref="ConsentDecision.None"/>.</summary>
        public ConsentDecision Decision { get; }

        /// <summary>Error message when <see cref="Outcome"/> is <see cref="ConsentOutcome.Failed"/>; otherwise null.</summary>
        public string Error { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (Outcome == ConsentOutcome.Decided)
            {
                return Outcome + " (" + Decision + ")";
            }

            return Error == null ? Outcome.ToString() : Outcome + " (" + Error + ")";
        }
    }
}
