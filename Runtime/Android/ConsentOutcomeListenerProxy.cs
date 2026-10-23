#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace Ezoic.Ads.Android
{
    /// <summary>
    /// <c>AndroidJavaProxy</c> for <c>com.ezoic.ads.sdk.privacy.cmp.ConsentOutcomeListener</c>.
    /// Retains itself until the single outcome arrives (on the Android UI thread), decodes the
    /// sealed <c>ConsentOutcome</c> into a <see cref="ConsentResult"/>, and marshals it to the
    /// Unity main thread.
    /// </summary>
    internal sealed class ConsentOutcomeListenerProxy : AndroidJavaProxy
    {
        private readonly Action<ConsentResult> _onComplete;

        internal ConsentOutcomeListenerProxy(Action<ConsentResult> onComplete)
            : base(JniNames.ConsentOutcomeListener.Class)
        {
            _onComplete = onComplete;
            AndroidBridge.Retain(this);
        }

        public void onOutcome(AndroidJavaObject outcome)
        {
            Deliver(Decode(outcome));
        }

        /// <summary>Reports a bridge-side failure (the native call threw) as <see cref="ConsentOutcome.Failed"/>.</summary>
        internal void Fail(string error)
        {
            Deliver(new ConsentResult(ConsentOutcome.Failed, ConsentDecision.None, error));
        }

        private void Deliver(ConsentResult result)
        {
            AndroidBridge.Release(this);
            EzoicMainThreadDispatcher.Enqueue(() => _onComplete?.Invoke(result));
        }

        private static ConsentResult Decode(AndroidJavaObject outcome)
        {
            if (outcome == null)
            {
                return new ConsentResult(ConsentOutcome.Failed, ConsentDecision.None, "unknown consent outcome");
            }

            try
            {
                string className;
                using (var cls = outcome.Call<AndroidJavaObject>(JniNames.Framework.getClass))
                {
                    className = cls.Call<string>(JniNames.Framework.getName);
                }

                switch (className)
                {
                    case JniNames.ConsentOutcomeNotRequired.Class:
                        return new ConsentResult(ConsentOutcome.NotRequired, ConsentDecision.None, null);
                    case JniNames.ConsentOutcomeAlreadyDecided.Class:
                        return new ConsentResult(ConsentOutcome.AlreadyDecided, ConsentDecision.None, null);
                    case JniNames.ConsentOutcomeDecided.Class:
                        using (var decision = outcome.Call<AndroidJavaObject>(JniNames.ConsentOutcomeDecided.getDecision))
                        {
                            return new ConsentResult(ConsentOutcome.Decided, DecisionFrom(decision), null);
                        }
                    case JniNames.ConsentOutcomeDismissed.Class:
                        return new ConsentResult(ConsentOutcome.Dismissed, ConsentDecision.None, null);
                    case JniNames.ConsentOutcomeAlreadyPresenting.Class:
                        return new ConsentResult(ConsentOutcome.AlreadyPresenting, ConsentDecision.None, null);
                    case JniNames.ConsentOutcomeFailed.Class:
                        using (var error = outcome.Call<AndroidJavaObject>(JniNames.ConsentOutcomeFailed.getError))
                        {
                            return new ConsentResult(ConsentOutcome.Failed, ConsentDecision.None, AndroidBridge.ErrorMessage(error));
                        }
                    default:
                        // An outcome added by a newer native SDK: closed without a known choice.
                        return new ConsentResult(ConsentOutcome.Dismissed, ConsentDecision.None, null);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new ConsentResult(ConsentOutcome.Failed, ConsentDecision.None, e.Message);
            }
        }

        private static ConsentDecision DecisionFrom(AndroidJavaObject decision)
        {
            if (decision == null)
            {
                return ConsentDecision.None;
            }

            switch (decision.Call<string>(JniNames.Framework.name))
            {
                case JniNames.ConsentDecisionType.ACCEPT_ALL: return ConsentDecision.AcceptAll;
                case JniNames.ConsentDecisionType.REJECT_ALL: return ConsentDecision.RejectAll;
                case JniNames.ConsentDecisionType.CUSTOM: return ConsentDecision.Custom;
                default: return ConsentDecision.None;
            }
        }
    }
}
#endif
