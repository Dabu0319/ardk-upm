using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Niantic.Lightship.AR.Utilities
{
    /// <summary>
    /// Methods subscribed to the static events on this class will be invoked when the corresponding
    /// Unity event is invoked on this MonoBehaviour. Methods can only be subscribed and unsubscribed
    /// from the main Unity thread.
    /// <note>
    ///   Class is not entirely thread safe because this ref is only set in Awake (see comment in
    ///   PrioritizingEvent.AddListener for more). But this class is internal and Lightship will never do anything
    ///   multi-threaded during the initialization stage, so it's an acceptable solution.
    /// </note>
    /// </summary>
    [DefaultExecutionOrder(int.MinValue)]
    internal sealed class MonoBehaviourEventDispatcher : MonoBehaviour
    {
        private static MonoBehaviourEventDispatcher s_instance;

        public static readonly PrioritizingEvent Updating = new ();
        public static readonly PrioritizingEvent LateUpdating = new ();

        private static Thread s_mainThread;
        private static readonly bool s_staticConstructorWasInvoked;

        private bool _wasCreatedByInternalConstructor;

        // Instantiation of the MonoBehaviourEventDispatcher component must be delayed until after scenes load.
        // Therefore, if the static constructor is invoked before scenes are loaded, it'll mark itself as having
        // been invoked, and the CreateAfterSceneLoad method will check if the static constructor was invoked and
        // call Instantiate() if needed. If the static constructor is invoked after scenes are loaded, the
        // CreateAfterSceneLoad will have no-oped, and Instantiate() will be called directly from the constructor.
        static MonoBehaviourEventDispatcher()
        {
            s_staticConstructorWasInvoked = true;

            if (SceneManager.sceneCount > 0)
            {
                Instantiate();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateAfterSceneLoad()
        {
            if (s_staticConstructorWasInvoked && s_instance == null)
            {
                Instantiate();
            }
        }

        private static void Instantiate()
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            if (s_instance != null)
            {
                return;
            }

            var go =
                new GameObject("__lightship_ar_monobehaviour_event_dispatcher__", typeof(MonoBehaviourEventDispatcher));

            go.hideFlags = HideFlags.HideInHierarchy;

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }
#endif
        }

        private void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
                s_mainThread = Thread.CurrentThread;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            Updating.InvokeListeners();
        }

        private void OnDestroy()
        {
            Updating.Clear();
            LateUpdating.Clear();
        }

        /// <summary>
        /// Class to subscribe to Unity Monobehaviour events with a priority.
        /// Lowest priority will be called first.
        /// </summary>
        private class Listener
        {
            public Action CallbackFunction { get; }

            public int Priority { get; }

            public Listener(Action callback, int priority)
            {
                CallbackFunction = callback;
                Priority = priority;
            }
        }

        public class PrioritizingEvent
        {
            private readonly List<Listener> _listeners = new ();

            private bool _isInvoking;
            private readonly List<Listener> _queuedAddedListeners = new();
            private readonly List<Listener> _queuedRemovedListeners = new();

            public void AddListener(Action callback, int priority = 999)
            {
                // If main thread reference has not yet been initialized, then the method was invoked
                // before the Awake frame, and no MonobehaviourEventDispatcher events happen before Awake,
                // so it's allowed
                if (s_mainThread != null && Thread.CurrentThread != s_mainThread)
                {
                    Debug.LogError("AddListener can only be called from the main thread.");
                    return;
                }

                if (_isInvoking)
                {
                    _queuedAddedListeners.Add(new Listener(callback, priority));
                }
                else
                {
                    AddListener(new Listener(callback, priority));
                    SortListenersByPriority();
                }
            }

            private void AddListener(Listener listener)
            {
                _listeners.Add(listener);
            }

            // If multiple instances of this callback are subscribed to this event, only one will be removed.
            // If those instances have different priorities, the only with the lowest priority will be removed.
            // Note:
            //   Removing a listener has a time complexity of O(n) where n is the number of subscribers.
            //   This is fine for now because it's called only a few times and n is very small (< 10).
            public void RemoveListener(Action callback)
            {
                if (s_mainThread != null && Thread.CurrentThread != s_mainThread)
                {
                    Debug.LogError("RemoveListener can only be called from the main thread.");
                    return;
                }

                // Will silently no-op if a callback was removed that was not present
                // in the listeners collection, same as C# events.
                var listener = _listeners.Find(e => e.CallbackFunction == callback);
                if (listener != null)
                {
                    if (_isInvoking)
                    {
                        _queuedRemovedListeners.Add(listener);
                    }
                    else
                    {
                        RemoveListener(listener);
                        SortListenersByPriority();
                    }
                }
            }

            private void RemoveListener(Listener listener)
            {
                _listeners.Remove(listener);
            }

            public void InvokeListeners()
            {
                if (s_mainThread != null && Thread.CurrentThread != s_mainThread)
                {
                    Debug.LogError("InvokeListeners can only be called from the main thread.");
                    return;
                }

                _isInvoking = true;
                foreach (var listener in _listeners)
                    listener.CallbackFunction.Invoke();

                _isInvoking = false;
                foreach (var listener in _queuedAddedListeners)
                    AddListener(listener);

                foreach (var listener in _queuedRemovedListeners)
                    RemoveListener(listener);

                _queuedAddedListeners.Clear();
                _queuedRemovedListeners.Clear();

                SortListenersByPriority();
            }

            public void Clear()
            {
                _listeners.Clear();
            }

            private void SortListenersByPriority()
            {
                _listeners.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }
        }

        private void LateUpdate()
        {
            LateUpdating?.InvokeListeners();
        }
    }
}
