using NUnit.Framework;
using Ezoic.Cmp;

namespace Ezoic.Unity.Tests
{
    public class CmpConfigurationTests
    {
        [Test]
        public void Defaults_MatchTheNativeAndWrapperDefaults()
        {
            var configuration = new EzoicCmpConfiguration();

            Assert.That(configuration.CmpEnabled, Is.True);
            Assert.That(configuration.AutoReadConsent, Is.True);
            Assert.That(configuration.DebugEnabled, Is.False);
            Assert.That(configuration.AutoPresentConsent, Is.True);
        }

        [Test]
        public void AutoPresentGate_OpensOncePerProcess_AndOnlyWhenEnabled()
        {
            var gate = new AutoPresentGate();

            Assert.That(gate.TryOpen(false), Is.False, "disabled never presents");
            Assert.That(gate.TryOpen(true), Is.True, "first enabled Initialize presents");
            Assert.That(gate.TryOpen(true), Is.False, "a repeat Initialize (scene reload) does not");
        }
    }
}
