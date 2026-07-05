#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ezoic.Ads.Android
{
    /// <summary>
    /// Shared JNI plumbing for the Android bridge: cached current Activity, a helper to run work
    /// on the Android UI thread, error-message extraction, and strong-reference retention for
    /// proxies that have no long-lived C# owner (load listeners).
    /// </summary>
    internal static class AndroidBridge
    {
        private static readonly HashSet<object> Retained = new HashSet<object>();

        /// <summary>
        /// The current Unity player Activity (also the Android <c>Context</c>). Fetched fresh on
        /// every access rather than cached: <c>UnityPlayer.currentActivity</c> can change out from
        /// under a cached reference if the player Activity is recreated (e.g. a configuration
        /// change), and operating on a stale Activity afterward would fail or misbehave silently.
        /// All access happens on the Unity main thread or the Android UI thread, both of which
        /// are attached to the JVM.
        /// </summary>
        internal static AndroidJavaObject Activity
        {
            get
            {
                using (var player = new AndroidJavaClass(JniNames.Framework.UnityPlayer))
                {
                    return player.GetStatic<AndroidJavaObject>(JniNames.Framework.currentActivity);
                }
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> on the Android UI thread. Every native SDK and View
        /// call must go through here — the Unity main thread is not the Android UI thread.
        /// </summary>
        internal static void RunOnUiThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            Activity.Call(JniNames.Framework.runOnUiThread, new AndroidJavaRunnable(action));
        }

        /// <summary>
        /// Extracts a human-readable message from an <c>EzoicError</c> (a sealed Exception
        /// subclass). Falls back to the error's simple class name, then a generic string.
        /// Must be called while the passed reference is still valid (i.e. inside the proxy).
        /// </summary>
        internal static string ErrorMessage(AndroidJavaObject error)
        {
            if (error == null)
            {
                return "unknown error";
            }

            try
            {
                var message = error.Call<string>(JniNames.EzoicError.getMessage);
                if (!string.IsNullOrEmpty(message))
                {
                    return message;
                }
            }
            catch (Exception)
            {
                // Fall through to class-name based message.
            }

            try
            {
                using (var cls = error.Call<AndroidJavaObject>(JniNames.Framework.getClass))
                {
                    return cls.Call<string>(JniNames.Framework.getSimpleName);
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
