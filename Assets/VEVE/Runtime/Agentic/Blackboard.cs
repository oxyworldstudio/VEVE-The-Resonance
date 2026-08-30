using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Shared knowledge base for agents with typed data storage and querying.
    /// </summary>
    public class Blackboard
    {
        private readonly Dictionary<string, object> data = new();

        /// <summary>
        /// Sets a value in the blackboard by key.
        /// </summary>
        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key)) return;
            data[key] = value;
        }

        /// <summary>
        /// Retrieves a value from the blackboard.
        /// </summary>
        public T Get<T>(string key)
        {
            if (data.TryGetValue(key, out var value) && value is T t)
            {
                return t;
            }
            return default;
        }

        /// <summary>
        /// Attempts to retrieve a value from the blackboard.
        /// </summary>
        public bool TryGet<T>(string key, out T value)
        {
            if (data.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Checks whether the blackboard contains the given key.
        /// </summary>
        public bool Has(string key)
        {
            return data.ContainsKey(key);
        }

        /// <summary>
        /// Removes a value from the blackboard.
        /// </summary>
        public void Remove(string key)
        {
            data.Remove(key);
        }

        /// <summary>
        /// Clears all data from the blackboard.
        /// </summary>
        public void Clear()
        {
            data.Clear();
        }
    }
}
