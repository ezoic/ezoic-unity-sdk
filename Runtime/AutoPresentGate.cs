namespace Ezoic.Cmp
{
    /// <summary>
    /// Lets <see cref="EzoicCmpConfiguration.AutoPresentConsent"/> present at most once per
    /// process, so an <see cref="EzoicCmp.Initialize(string)"/> repeated by a reloaded scene does
    /// not ask again. Platform-independent so it is unit tested. Unity main thread only.
    /// </summary>
    internal sealed class AutoPresentGate
    {
        private bool _opened;

        /// <summary>True the first time it is called with <paramref name="enabled"/> true.</summary>
        internal bool TryOpen(bool enabled)
        {
            if (!enabled || _opened)
            {
                return false;
            }

            _opened = true;
            return true;
        }
    }
}
