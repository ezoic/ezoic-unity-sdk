namespace Ezoic.Cmp
{
    /// <summary>Options for <see cref="EzoicCmp.Initialize(string, EzoicCmpConfiguration)"/>.</summary>
    public sealed class EzoicCmpConfiguration
    {
        /// <summary>
        /// Runs the built-in consent management: the IAB TCF consent dialog in GDPR regions and
        /// the privacy notice and "Do Not Sell" choice in covered US states. Default <c>true</c>.
        /// </summary>
        public bool CmpEnabled { get; set; } = true;

        /// <summary>
        /// Default <c>true</c>. <c>false</c> means your app manages consent itself: the CMP then
        /// never presents anything and writes no <c>IABTCF_*</c> or <c>IABGPP_*</c> values.
        /// </summary>
        public bool AutoReadConsent { get; set; } = true;

        /// <summary>
        /// Also write the native CMP's debug and info lines to the device log (logcat tag
        /// <c>EzoicCmp</c> / unified log subsystem <c>com.ezoic.cmp.sdk</c>); warnings and errors
        /// are always logged. Default <c>false</c>.
        /// </summary>
        public bool DebugEnabled { get; set; }
    }
}
