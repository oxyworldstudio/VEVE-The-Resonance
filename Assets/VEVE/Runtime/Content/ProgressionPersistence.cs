using System;
using UnityEngine;
using VEVE.Catalog;

namespace VEVE.Content
{
    /// <summary>Minimal persistence seam (H3): testable, no Unity PlayerPrefs hard dependency.</summary>
    public interface IKeyValueStore
    {
        string Get(string key);
        void Set(string key, string value);
    }

    /// <summary>Production store over PlayerPrefs.</summary>
    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public string Get(string key) => PlayerPrefs.GetString(key ?? string.Empty, string.Empty);
        public void Set(string key, string value) => PlayerPrefs.SetString(key ?? string.Empty, value ?? string.Empty);
    }

    /// <summary>
    /// H3: persists the family XP ledger (and any future progression blobs) through
    /// the key-value seam on mission end, restoring it at boot. Null-safe on both.
    /// </summary>
    public static class ProgressionPersistence
    {
        public const string LedgerKey = "veve.familyxp.v1";

        public static void Save(FamilyXpLedger ledger, IKeyValueStore store)
        {
            if (ledger == null || store == null) return;
            store.Set(LedgerKey, ledger.Export());
        }

        public static void Load(FamilyXpLedger ledger, IKeyValueStore store)
        {
            if (ledger == null || store == null) return;
            string payload = store.Get(LedgerKey);
            if (string.IsNullOrEmpty(payload)) return;
            ledger.Import(payload);
        }
    }
}
