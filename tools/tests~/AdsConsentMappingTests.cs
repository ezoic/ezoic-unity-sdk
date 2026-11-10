using System;
using NUnit.Framework;
using Mapping = Ezoic.Ads.Android.ConsentOutcomeMapping;
using Names = Ezoic.Ads.Android.JniNames;
using Abi = Ezoic.Ads.iOS.IosAbi;
using Outcome = Ezoic.Ads.ConsentOutcome;
using Decision = Ezoic.Ads.ConsentDecision;

namespace Ezoic.Unity.Tests
{
    // CmpConsentMappingTests.cs runs the same cases against the com.ezoic.cmp-sdk copies;
    // keep the two files identical apart from the using aliases and the class name.
    public class AdsConsentMappingTests
    {
        private static Func<string> Never(string what) =>
            () => throw new AssertionException(what + " must not be read for this outcome");

        [TestCase(Names.ConsentOutcomeNotRequired.Class, Outcome.NotRequired)]
        [TestCase(Names.ConsentOutcomeAlreadyDecided.Class, Outcome.AlreadyDecided)]
        [TestCase(Names.ConsentOutcomeDismissed.Class, Outcome.Dismissed)]
        [TestCase(Names.ConsentOutcomeAlreadyPresenting.Class, Outcome.AlreadyPresenting)]
        public void FromJava_MapsPayloadFreeOutcomes_WithoutReadingPayload(string className, Outcome expected)
        {
            var result = Mapping.FromJava(className, Never("decision"), Never("error"));

            Assert.That(result.Outcome, Is.EqualTo(expected));
            Assert.That(result.Decision, Is.EqualTo(Decision.None));
            Assert.That(result.Error, Is.Null);
        }

        [TestCase("ACCEPT_ALL", Decision.AcceptAll)]
        [TestCase("REJECT_ALL", Decision.RejectAll)]
        [TestCase("CUSTOM", Decision.Custom)]
        [TestCase("SOMETHING_NEW", Decision.None)]
        [TestCase(null, Decision.None)]
        public void FromJava_Decided_MapsTheDecision(string decisionName, Decision expected)
        {
            var result = Mapping.FromJava(Names.ConsentOutcomeDecided.Class, () => decisionName, Never("error"));

            Assert.That(result.Outcome, Is.EqualTo(Outcome.Decided));
            Assert.That(result.Decision, Is.EqualTo(expected));
            Assert.That(result.Error, Is.Null);
        }

        [Test]
        public void FromJava_Failed_CarriesTheErrorMessage()
        {
            // 1.22.0: the payload is an Exception (EzoicError via EzoicAds, CmpError via EzoicCmp);
            // the bridge reads its message either way.
            var result = Mapping.FromJava(Names.ConsentOutcomeFailed.Class, Never("decision"),
                () => "The Ezoic CMP has not been initialized.");

            Assert.That(result.Outcome, Is.EqualTo(Outcome.Failed));
            Assert.That(result.Decision, Is.EqualTo(Decision.None));
            Assert.That(result.Error, Is.EqualTo("The Ezoic CMP has not been initialized."));
        }

        [Test]
        public void FromJava_NullClass_IsFailed()
        {
            var result = Mapping.FromJava(null, Never("decision"), Never("error"));

            Assert.That(result.Outcome, Is.EqualTo(Outcome.Failed));
            Assert.That(result.Error, Is.EqualTo("unknown consent outcome"));
        }

        [Test]
        public void FromJava_UnknownClass_IsDismissed()
        {
            var result = Mapping.FromJava("com.ezoic.ads.sdk.privacy.cmp.ConsentOutcome$SomethingNew",
                Never("decision"), Never("error"));

            Assert.That(result.Outcome, Is.EqualTo(Outcome.Dismissed));
            Assert.That(result.Error, Is.Null);
        }

        [TestCase(0, Outcome.NotRequired)]
        [TestCase(1, Outcome.AlreadyDecided)]
        [TestCase(3, Outcome.Dismissed)]
        [TestCase(4, Outcome.AlreadyPresenting)]
        public void ConsentResultFrom_MapsOutcomeInts(int outcome, Outcome expected)
        {
            var result = Abi.ConsentResultFrom(outcome, 0, "ignored");

            Assert.That(result.Outcome, Is.EqualTo(expected));
            Assert.That(result.Decision, Is.EqualTo(Decision.None));
            Assert.That(result.Error, Is.Null);
        }

        [TestCase(1, Decision.AcceptAll)]
        [TestCase(2, Decision.RejectAll)]
        [TestCase(3, Decision.Custom)]
        [TestCase(7, Decision.None)]
        public void ConsentResultFrom_Decided_MapsDecisionInts(int decision, Decision expected)
        {
            var result = Abi.ConsentResultFrom(2, decision, null);

            Assert.That(result.Outcome, Is.EqualTo(Outcome.Decided));
            Assert.That(result.Decision, Is.EqualTo(expected));
        }

        [Test]
        public void ConsentResultFrom_Failed_KeepsTheError()
        {
            var result = Abi.ConsentResultFrom(5, 0, "No root view controller");

            Assert.That(result.Outcome, Is.EqualTo(Outcome.Failed));
            Assert.That(result.Error, Is.EqualTo("No root view controller"));
        }

        [Test]
        public void ConsentResultFrom_Failed_WithoutMessage_HasAGenericError()
        {
            var result = Abi.ConsentResultFrom(5, 0, null);

            Assert.That(result.Outcome, Is.EqualTo(Outcome.Failed));
            Assert.That(result.Error, Is.EqualTo("consent failed"));
        }

        [TestCase(-3)]
        [TestCase(9)]
        public void ConsentResultFrom_UnknownOutcome_IsDismissed(int outcome)
        {
            var result = Abi.ConsentResultFrom(outcome, 1, "x");

            Assert.That(result.Outcome, Is.EqualTo(Outcome.Dismissed));
            Assert.That(result.Decision, Is.EqualTo(Decision.None));
            Assert.That(result.Error, Is.Null);
        }

        [Test]
        public void OptionalBool_RoundTripsThroughTheAbi()
        {
            Assert.That(Abi.EncodeOptionalBool(null), Is.EqualTo(-1));
            Assert.That(Abi.EncodeOptionalBool(false), Is.EqualTo(0));
            Assert.That(Abi.EncodeOptionalBool(true), Is.EqualTo(1));

            Assert.That(Abi.DecodeOptionalBool(-1), Is.Null);
            Assert.That(Abi.DecodeOptionalBool(0), Is.False);
            Assert.That(Abi.DecodeOptionalBool(1), Is.True);
            Assert.That(Abi.DecodeOptionalBool(2), Is.True);
        }
    }
}
