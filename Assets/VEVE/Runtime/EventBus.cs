using UnityEngine;
using System;
using System.Collections.Generic;
using VEVE.Realism;

namespace VEVE
{
    public interface IEvent { }

    public sealed class QualityPresetChangedEvent : IEvent
    {
        public VEVE.Realism.QualityLevel NewLevel { get; }
        public QualityPresetChangedEvent(VEVE.Realism.QualityLevel newLevel) => NewLevel = newLevel;
    }

    public sealed class SimulationStateChangedEvent : IEvent
    {
        public SimulatorState NewState { get; }
        public SimulationStateChangedEvent(SimulatorState newState) => NewState = newState;
    }

    public sealed class PlayerDeathEvent : IEvent
    {
        public GameObject Player { get; }
        public PlayerDeathEvent(GameObject player) => Player = player;
    }

    public sealed class MissionCompleteEvent : IEvent
    {
        public bool Success { get; }
        public MissionCompleteEvent(bool success) => Success = success;
    }

    public sealed class DamageEvent : IEvent
    {
        public GameObject Victim { get; }
        public float Amount { get; }
        public string Source { get; }
        public DamageEvent(GameObject victim, float amount, string source = "")
        {
            Victim = victim;
            Amount = amount;
            Source = source;
        }
    }

    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _listeners = new Dictionary<Type, List<Delegate>>();
        private static readonly Dictionary<Type, List<Delegate>> _globalListeners = new Dictionary<Type, List<Delegate>>();
        private static readonly List<IEvent> _eventQueue = new List<IEvent>();
        private static bool _processing;

        public static void Subscribe<T>(Action<T> handler) where T : IEvent
        {
            var type = typeof(T);
            if (!_listeners.ContainsKey(type))
                _listeners[type] = new List<Delegate>();
            if (!_listeners[type].Contains(handler))
                _listeners[type].Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : IEvent
        {
            var type = typeof(T);
            if (_listeners.ContainsKey(type))
                _listeners[type].Remove(handler);
        }

        public static void SubscribeGlobal<T>(Action<T> handler) where T : IEvent
        {
            var type = typeof(T);
            if (!_globalListeners.ContainsKey(type))
                _globalListeners[type] = new List<Delegate>();
            if (!_globalListeners[type].Contains(handler))
                _globalListeners[type].Add(handler);
        }

        public static void UnsubscribeGlobal<T>(Action<T> handler) where T : IEvent
        {
            var type = typeof(T);
            if (_globalListeners.ContainsKey(type))
                _globalListeners[type].Remove(handler);
        }

        public static void Publish<T>(T evt) where T : IEvent
        {
            lock (_eventQueue)
            {
                _eventQueue.Add(evt);
            }
        }

        public static void PublishGlobal<T>(T evt) where T : IEvent
        {
            lock (_eventQueue)
            {
                _eventQueue.Add(evt);
            }
        }

        public static void ProcessQueue()
        {
            if (_processing) return;
            _processing = true;

            List<IEvent> events;
            lock (_eventQueue)
            {
                if (_eventQueue.Count == 0)
                {
                    _processing = false;
                    return;
                }
                events = new List<IEvent>(_eventQueue);
                _eventQueue.Clear();
            }

            foreach (var evt in events)
            {
                var type = evt.GetType();
                if (_listeners.ContainsKey(type))
                {
                    foreach (var d in _listeners[type])
                    {
                        try { d.DynamicInvoke(evt); }
                        catch (Exception ex) { UnityEngine.Debug.LogError($"VEVE EventBus error on {type.Name}: {ex}"); }
                    }
                }
                if (_globalListeners.ContainsKey(type))
                {
                    foreach (var d in _globalListeners[type])
                    {
                        try { d.DynamicInvoke(evt); }
                        catch (Exception ex) { UnityEngine.Debug.LogError($"VEVE EventBus global error on {type.Name}: {ex}"); }
                    }
                }
            }

            _processing = false;
        }

        public static void ClearAll()
        {
            lock (_eventQueue)
            {
                _listeners.Clear();
                _globalListeners.Clear();
                _eventQueue.Clear();
            }
            _processing = false;
        }
    }
}
