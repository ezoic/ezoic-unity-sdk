#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace Ezoic.Cmp.Android
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
            : base(CmpJniNames.ConsentOutcomeListener.Class)
        {
            _onComplete = onComplete;
            CmpAndroidBridge.Retain(this);
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
            CmpAndroidBridge.Release(this);
            EzoicCmpMainThreadDispatcher.Enqueue(() => _onComplete?.Invoke(result));
        }

        private static ConsentResult Decode(AndroidJavaObject outcome)
        {
            try
            {
                string className = null;
                if (outcome != null)
                {
                    using (var cls = outcome.Call<AndroidJavaObject>(CmpJniNames.Framework.getClass))
                    {
                        className = cls.Call<string>(CmpJniNames.Framework.getName);
                    }
                }

                return ConsentOutcomeMapping.FromJava(
                    className,
                    () =>
                    {
                        using (var decision = outcome.Call<AndroidJavaObject>(CmpJniNames.ConsentOutcomeDecided.getDecision))
                        {
                            return decision?.Call<string>(CmpJniNames.Framework.name);
                        }
                    },
                    () =>
                    {
                        using (var error = outcome.Call<AndroidJavaObject>(CmpJniNames.ConsentOutcomeFailed.getError))
                        {
                            return CmpAndroidBridge.ErrorMessage(error);
                        }
                    });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new ConsentResult(ConsentOutcome.Failed, ConsentDecision.None, e.Message);
            }
        }
    }
}
#endif
