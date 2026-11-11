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
            try
            {
                string className = null;
                if (outcome != null)
                {
                    using (var cls = outcome.Call<AndroidJavaObject>(JniNames.Framework.getClass))
                    {
                        className = cls.Call<string>(JniNames.Framework.getName);
                    }
                }

                return ConsentOutcomeMapping.FromJava(
                    className,
                    () =>
                    {
                        using (var decision = outcome.Call<AndroidJavaObject>(JniNames.ConsentOutcomeDecided.getDecision))
                        {
                            return decision?.Call<string>(JniNames.Framework.name);
                        }
                    },
                    () =>
                    {
                        using (var error = outcome.Call<AndroidJavaObject>(JniNames.ConsentOutcomeFailed.getError))
                        {
                            return AndroidBridge.ErrorMessage(error);
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
