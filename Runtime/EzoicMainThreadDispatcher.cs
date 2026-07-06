using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Ezoic.Ads
{
    /// <summary>
    /// Marshals work from background threads (native SDK callbacks arrive on the Android UI
    /// thread) onto the Unity main thread. Backed by a hidden <see cref="DontDestroyOnLoad"/>
    /// component that drains a <see cref="ConcurrentQueue{T}"/> every <c>Update</c>.
    /// </summary>
    /// <remarks>
    /// The dispatcher is created lazily on the first public API call. That first call MUST be
    /// made from the Unity main thread (creating a <see cref="GameObject"/> off the main thread
    /// throws). Subsequent <see cref="Enqueue"/> calls are safe from any thread.
    /// </remarks>
    internal sealed class EzoicMainThreadDispatcher : MonoBehaviour
    {
        private static EzoicMainThreadDispatcher _instance;
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();

        /// <summary>
        /// Ensures the dispatcher exists. Must be called from the Unity main thread. Idempotent.
        /// </summary>
        internal static void Init()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("EzoicMainThreadDispatcher")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            // DontDestroyOnLoad throws InvalidOperationException outside play mode
            // (e.g. editor scripts and EditMode tests calling into the SDK API).
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }
            _instance = go.AddComponent<EzoicMainThreadDispatcher>();
        }

        /// <summary>
        /// Queues an action to run on the next main-thread <c>Update</c>. Safe from any thread.
        /// Actions are always delivered asynchronously (never inline on the caller's stack).
        /// </summary>
        internal static void Enqueue(Action action)
        {
            if (action == null)
            {
                return;
            }

            Queue.Enqueue(action);
        }

        private void Update()
        {
            while (Queue.TryDequeue(out var action))
            {
                // One faulty action must not stop the pump or leak the exception into the SDK.
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
