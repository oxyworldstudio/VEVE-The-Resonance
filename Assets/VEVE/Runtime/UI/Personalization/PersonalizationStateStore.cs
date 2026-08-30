using System;
using System.Collections.Generic;
using UnityEngine;
using IOFile = System.IO.File;

namespace VEVE.UI.Personalization
{
    /// <summary>KV row used because JsonUtility cannot serialize dictionaries directly.</summary>
    [Serializable]
    public sealed class StringPairEntry
    {
        public string key;
        public string value;

        public StringPairEntry() { }

        public StringPairEntry(string key, string value)
        {
            this.key = key;
            this.value = value;
        }
    }

    /// <summary>
    /// The player's persisted personalization picks. Pure data + JsonUtility round trip,
    /// unit-testable without any file or scene access. Attachment/gear maps are kept as
    /// key/value entry lists for JsonUtility and exposed through dictionary-style helpers.
    /// </summary>
    [Serializable]
    public sealed class UserLoadoutSelection
    {
        /// <summary>Current schema version written by ToJson and enforced by Migrate().</summary>
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string operatorId = string.Empty;
        public string weaponId = string.Empty;
        public string finishId = string.Empty;

        /// <summary>Attachment-slot key -> attachment id (both plain strings, slot keys uppercase).</summary>
        public List<StringPairEntry> attachedSlots = new List<StringPairEntry>();

        /// <summary>Gear-slot key -> gear item id.</summary>
        public List<StringPairEntry> gearSlots = new List<StringPairEntry>();

        // ------------------------------------------------------------- dictionary helpers

        public void SetAttachment(string slotKey, string attachmentId)
        {
            SetPair(attachedSlots, slotKey, attachmentId);
        }

        public bool TryGetAttachment(string slotKey, out string attachmentId)
        {
            return TryGetPair(attachedSlots, slotKey, out attachmentId);
        }

        public void SetGear(string slotKey, string gearId)
        {
            SetPair(gearSlots, slotKey, gearId);
        }

        public bool TryGetGear(string slotKey, out string gearId)
        {
            return TryGetPair(gearSlots, slotKey, out gearId);
        }

        private static void SetPair(List<StringPairEntry> list, string key, string value)
        {
            if (list == null || string.IsNullOrEmpty(key))
                return;
            string normalized = key.Trim().ToUpperInvariant();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null)
                {
                    list[i] = new StringPairEntry(normalized, value ?? string.Empty);
                    return;
                }
                if (string.Equals(list[i].key, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(value))
                        list.RemoveAt(i);
                    else
                        list[i].value = value;
                    return;
                }
            }
            if (!string.IsNullOrEmpty(value))
                list.Add(new StringPairEntry(normalized, value));
        }

        private static bool TryGetPair(List<StringPairEntry> list, string key, out string value)
        {
            value = null;
            if (list == null || string.IsNullOrEmpty(key))
                return false;
            foreach (StringPairEntry entry in list)
            {
                if (entry != null && string.Equals(entry.key, key, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(entry.value))
                {
                    value = entry.value;
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------- serialization

        public string ToJson(bool pretty = true)
        {
            return JsonUtility.ToJson(this, pretty);
        }

        public static UserLoadoutSelection FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new UserLoadoutSelection();
            try
            {
                UserLoadoutSelection parsed =
                    JsonUtility.FromJson<UserLoadoutSelection>(json);
                return parsed ?? new UserLoadoutSelection();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PersonalizationStateStore] Malformed selection JSON discarded: " + ex.Message);
                return new UserLoadoutSelection();
            }
        }

        /// <summary>
        /// Versioned migration. Currently a guarded no-op beyond filling collection defaults
        /// and normalizing the version stamp; future schema bumps add per-step fixups here.
        /// Returns true when anything changed.
        /// </summary>
        public bool Migrate()
        {
            bool changed = false;
            try
            {
                if (attachedSlots == null)
                {
                    attachedSlots = new List<StringPairEntry>();
                    changed = true;
                }
                if (gearSlots == null)
                {
                    gearSlots = new List<StringPairEntry>();
                    changed = true;
                }
                if (version != CurrentVersion)
                {
                    // v0 -> v1: schema additions only (finishId / pair lists), nothing to rewrite.
                    version = CurrentVersion;
                    changed = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[PersonalizationStateStore] Migration aborted: " + ex.Message);
            }
            return changed;
        }
    }

    /// <summary>Injectable file access so tests never touch the real disk and no IO happens at construction.</summary>
    public interface ILoadoutFileProvider
    {
        bool Exists(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string text);
    }

    public sealed class DiskLoadoutFileProvider : ILoadoutFileProvider
    {
        public bool Exists(string path) => path != null && IOFile.Exists(path);
        public string ReadAllText(string path) => IOFile.ReadAllText(path);
        public void WriteAllText(string path, string text) => IOFile.WriteAllText(path, text);
    }

    /// <summary>
    /// Save/load wrapper around <see cref="UserLoadoutSelection"/>. Paths default to
    /// <c>Application.persistentDataPath</c> but are resolved lazily at Save/Load time; the
    /// constructor performs no IO. Any <see cref="ILoadoutFileProvider"/> can be injected.
    /// </summary>
    public sealed class PersonalizationStateStore
    {
        public const string DefaultFileName = "veve_personalization_v1.json";

        private readonly ILoadoutFileProvider _provider;
        private readonly string _explicitPath;

        public UserLoadoutSelection Selection { get; private set; }
        public string LastError { get; private set; }

        /// <summary>Replaces the in-memory selection (e.g. the workspace seeding an empty store).</summary>
        public void Adopt(UserLoadoutSelection selection)
        {
            Selection = selection ?? new UserLoadoutSelection();
            Selection.Migrate();
        }

        public PersonalizationStateStore(
            ILoadoutFileProvider provider = null,
            string path = null,
            UserLoadoutSelection selection = null)
        {
            _provider = provider ?? new DiskLoadoutFileProvider();
            _explicitPath = path;
            Selection = selection ?? new UserLoadoutSelection();
            Selection.Migrate();
        }

        /// <summary>Resolved lazily so tests without persistentDataPath still construct fine.</summary>
        public string ResolvePath()
        {
            if (!string.IsNullOrEmpty(_explicitPath))
                return _explicitPath;
            return System.IO.Path.Combine(Application.persistentDataPath, DefaultFileName);
        }

        public bool Save()
        {
            try
            {
                Selection.Migrate();
                _provider.WriteAllText(ResolvePath(), Selection.ToJson(true));
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogWarning("[PersonalizationStateStore] Save failed: " + ex.Message);
                return false;
            }
        }

        public bool Load()
        {
            try
            {
                string path = ResolvePath();
                if (!_provider.Exists(path))
                {
                    LastError = null;
                    return false;
                }
                Selection = UserLoadoutSelection.FromJson(_provider.ReadAllText(path));
                Selection.Migrate();
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogWarning("[PersonalizationStateStore] Load failed: " + ex.Message);
                return false;
            }
        }
    }
}
