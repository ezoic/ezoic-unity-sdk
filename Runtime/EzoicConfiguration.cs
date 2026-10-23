namespace Ezoic.Ads
{
    /// <summary>
    /// Options for <see cref="EzoicAds.Initialize(EzoicConfiguration, System.Action{bool, string})"/>.
    /// </summary>
    public sealed class EzoicConfiguration
    {
        /// <summary>Creates a configuration for your Ezoic-registered domain (e.g. "example.com").</summary>
        public EzoicConfiguration(string domain)
        {
            Domain = domain;
        }

        /// <summary>Your Ezoic-registered domain.</summary>
        public string Domain { get; }

        /// <summary>
        /// Enables the SDK's built-in consent dialog (IAB TCF CMP) for users in GDPR regions.
        /// Default <c>true</c>: call <see cref="EzoicAds.PresentConsentIfRequired"/> after
        /// initializing, because ad loads for GDPR-region users fail until the user has decided.
        /// Set to <c>false</c> if your app runs another CMP (UMP, OneTrust, …); the SDK then reads
        /// that CMP's <c>IABTCF_*</c> values.
        /// </summary>
        public bool CmpEnabled { get; set; } = true;
    }
}
