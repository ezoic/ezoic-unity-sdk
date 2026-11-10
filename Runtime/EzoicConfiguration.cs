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
        /// Enables the SDK's built-in consent management: the IAB TCF consent dialog for users in
        /// GDPR regions, and the privacy notice and "Do Not Sell" choice in covered US states.
        /// Default <c>true</c>: call <see cref="EzoicAds.PresentConsentIfRequired"/> after
        /// initializing, because ad loads for GDPR-region users fail until the user has decided.
        /// Set to <c>false</c> if your app runs another CMP (UMP, OneTrust, …); the SDK then reads
        /// that CMP's <c>IABTCF_*</c> and <c>IABGPP_*</c> values.
        /// </summary>
        public bool CmpEnabled { get; set; } = true;

        /// <summary>
        /// Whether the app is a paid app, sent to bidders as OpenRTB <c>app.paid</c> (1 paid,
        /// 0 free). Default <c>null</c>: not sent.
        /// </summary>
        public bool? AppPaid { get; set; }
    }
}
