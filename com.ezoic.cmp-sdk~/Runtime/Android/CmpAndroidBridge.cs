#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ezoic.Cmp.Android
{
    /// <summary>
    /// Shared JNI plumbing for the CMP's Android bridge: the current Activity, a helper to run
    /// work on the Android UI thread, error-message extraction, and strong-reference retention
    /// for listener proxies while native code holds them.
    /// </summary>
    internal static class CmpAndroidBridge
    {
        private static readonly HashSet<object> Retained = new HashSet<object>();

        /// <summary>
        /// The current Unity player Activity (also the Android <c>Context</c>), fetched fresh on
        /// every access: <c>UnityPlayer.currentActivity</c> changes if the Activity is recreated.
        /// </summary>
        internal static AndroidJavaObject Activity
        {
            get
            {
                using (var player = new AndroidJavaClass(CmpJniNames.Framework.UnityPlayer))
                {
                    return player.GetStatic<AndroidJavaObject>(CmpJniNames.Framework.currentActivity);
                }
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> on the Android UI thread. Every native CMP call goes
        /// through here; the Unity main thread is not the Android UI thread.
        /// </summary>
        internal static void RunOnUiThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            Activity.Call(CmpJniNames.Framework.runOnUiThread, new AndroidJavaRunnable(action));
        }

        /// <summary>
        /// A human-readable message from a <c>Throwable</c> (the <c>CmpError</c> in a
        /// <c>Failed</c> outcome), falling back to its simple class name, then a generic string.
        /// Must be called while the reference is still valid (inside the proxy callback).
        /// </summary>
        internal static string ErrorMessage(AndroidJavaObject error)
        {
            if (error == null)
            {
                return "unknown error";
            }

            try
            {
                var message = error.Call<string>(CmpJniNames.Framework.getMessage);
                if (!string.IsNullOrEmpty(message))
                {
                    return message;
                }
            }
            catch (Exception)
            {
                // Fall through to the class name.
            }

            try
            {
                using (var cls = error.Call<AndroidJavaObject>(CmpJniNames.Framework.getClass))
                {
                    return cls.Call<string>(CmpJniNames.Framework.getSimpleName);
                }
            }
            catch (Exception)
            {
                return "unknown error";
            }
        }

        /// <summary>Roots an object so it is not garbage collected while native code holds it.</summary>
        internal static void Retain(object handle)
        {
            if (handle == null)
            {
                return;
            }

            lock (Retained)
            {
                Retained.Add(handle);
            }
        }

        /// <summary>Releases a previously <see cref="Retain"/>ed object.</summary>
        internal static void Release(object handle)
        {
            if (handle == null)
            {
                return;
            }

            lock (Retained)
            {
                Retained.Remove(handle);
            }
        }
    }
}
#endif
