namespace Ezoic.Ads
{
    /// <summary>
    /// What a consent request did. Reported by <see cref="EzoicAds.PresentConsentIfRequired"/>
    /// and <see cref="EzoicAds.PresentConsentSettings"/>.
    /// </summary>
    /// <remarks>The numeric values are part of the iOS bridge ABI; do not reorder.</remarks>
    public enum ConsentOutcome
    {
        /// <summary>
        /// GDPR does not apply, the built-in consent dialog is disabled
        /// (<see cref="EzoicConfiguration.CmpEnabled"/> is false, or by Ezoic), another CMP owns
        /// consent, or the app manages consent itself (<see cref="EzoicAds.SetGDPRConsent"/>).
        /// </summary>
        NotRequired = 0,

        /// <summary>A still-valid decision is stored; no dialog was shown.</summary>
        AlreadyDecided = 1,

        /// <summary>The user made a choice (see <see cref="ConsentResult.Decision"/>), which has been saved.</summary>
        Decided = 2,

        /// <summary>
        /// The dialog closed without a choice; ads stay gated for this session. Ask again later
        /// with <see cref="EzoicAds.PresentConsentIfRequired"/>.
        /// </summary>
        Dismissed = 3,

        /// <summary>A consent dialog is already on screen or being prepared.</summary>
        AlreadyPresenting = 4,

        /// <summary>
        /// The dialog could not be shown (see <see cref="ConsentResult.Error"/>). Ads proceed as
        /// limited ads. Also reported when called before <see cref="EzoicAds.Initialize(string, System.Action{bool, string})"/>
        /// or when initialization failed.
        /// </summary>
        Failed = 5
    }

    /// <summary>The button the user closed the consent dialog with.</summary>
    /// <remarks>The numeric values are part of the iOS bridge ABI; do not reorder.</remarks>
    public enum ConsentDecision
    {
        /// <summary>No decision was made in this request (any outcome other than <see cref="ConsentOutcome.Decided"/>).</summary>
        None = 0,

        /// <summary>The user accepted all purposes and vendors.</summary>
        AcceptAll = 1,

        /// <summary>The user rejected all purposes and vendors.</summary>
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
