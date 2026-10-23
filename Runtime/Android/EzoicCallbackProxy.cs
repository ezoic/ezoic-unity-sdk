#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace Ezoic.Ads.Android
{
    /// <summary>
    /// <c>AndroidJavaProxy</c> for <c>com.ezoic.ads.sdk.core.EzoicCallback</c> (SDK init result).
    /// Callbacks arrive on the Android UI thread; the supplied delegates are responsible for
    /// marshaling to the Unity main thread. Errors are delivered as <c>(code, message)</c>.
    /// </summary>
    internal sealed class EzoicCallbackProxy : AndroidJavaProxy
    {
        private readonly Action _onSuccess;
        private readonly Action<int, string> _onError;

        internal EzoicCallbackProxy(Action onSuccess, Action<int, string> onError)
            : base(JniNames.EzoicCallback.Class)
        {
            _onSuccess = onSuccess;
            _onError = onError;
        }

        public void onSuccess()
        {
            _onSuccess?.Invoke();
        }

        public void onError(AndroidJavaObject error)
        {
            var code = AndroidBridge.ErrorCode(error);
            var message = AndroidBridge.ErrorMessage(error);
            _onError?.Invoke(code, message);
        }
    }
}
#endif
