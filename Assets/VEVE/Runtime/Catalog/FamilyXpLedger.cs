using System;
using System.Collections.Generic;
using VEVE.Content;

namespace VEVE.Catalog
{
    /// <summary>
    /// Per-connection proficiency ledger: family XP is attributed to the owning
    /// client so co-op sessions never pool one man's gun skill into another's
    /// account. Offline host (owner == 0) is intentionally never credited by
    /// the grant path - the single-player systems keep their existing pipeline.
    /// </summary>
    public sealed class FamilyXpLedger
    {
        /// <summary>Optional global sink wired by session bootstrap; null-safe everywhere.</summary>
        public static FamilyXpLedger Default;

        private readonly Dictionary<string, double> xpByKey = new Dictionary<string, double>(StringComparer.Ordinal);

        public static string Key(ulong clientId, string family)
        {
            return MapClient(clientId) + "|" + (family ?? string.Empty);
        }

        /// <summary>Reserved transport sentinel (ulong.MaxValue) is remapped into a high
        /// band so it can never read another connection's row or collide with a live id.</summary>
        static ulong MapClient(ulong id)
        {
            if (id != ulong.MaxValue) return id;
            return ulong.MinValue == 0u ? 0xF000000000000000ul : id; // fixed reserved bucket
        }

        public const ulong OfflineOwner = 0;

        public int Count => xpByKey.Count;

        public bool ClaimedGrant(ulong clientId, string family) => clientId != OfflineOwner && !string.IsNullOrEmpty(family);

        public void Grant(ulong clientId, string family, double amount)
        {
            if (!ClaimedGrant(clientId, family)) return;
            if (double.IsNaN(amount) || amount <= 0) return;
            string key = Key(clientId, family);
            double capped = amount > MaxGrant ? MaxGrant : amount;
            xpByKey.TryGetValue(key, out double cur);
            double next = cur + capped;
            xpByKey[key] = next > CeilingTotal ? CeilingTotal : next;
        }

        public double Xp(ulong clientId, string family)
        {
            return xpByKey.TryGetValue(Key(clientId, family), out double v) ? v : 0d;
        }

        /// <summary>Authority reversal: remove up to <paramref name="amount"/> credit (clamped at 0).
        /// Offline owner id 0 and negative amounts cannot revoke anything.</summary>
        public void Revoke(ulong clientId, string family, double amount)
        {
            if (!ClaimedGrant(clientId, family)) return;
            if (double.IsNaN(amount) || amount <= 0) return;
            string key = Key(clientId, family);
            if (!xpByKey.TryGetValue(key, out double cur)) return;
            double clamped = amount > MaxRevoke ? MaxRevoke : amount;
            if (clamped > cur) clamped = cur;
            xpByKey[key] = cur - clamped;
        }

        public int Skill(ulong clientId, string family) => VEVE.Catalog.WeaponProficiencyMath.SkillFromXp((int)Math.Round(Xp(clientId, family), MidpointRounding.AwayFromZero));

        // single-entry and session caps: one event never smuggles unlimited xp, totals bounded
        public const double XpPerHitOnTarget = 6d;
        public const double MaxGrant = 240d;
        public const double MaxRevoke = 480d;
        public const double CeilingTotal = 2000000d;

        public string Export()
        {
            var pairs = new List<KeyValuePair<string, string>>(xpByKey.Count);
            foreach (var kv in xpByKey) pairs.Add(new KeyValuePair<string, string>("xp." + kv.Key, kv.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            return PayloadCodec.Encode(pairs);
        }

        public void Import(string payload)
        {
            var map = PayloadCodec.Decode(payload);
            xpByKey.Clear();
            foreach (var kv in map)
            {
                if (!kv.Key.StartsWith("xp.", StringComparison.Ordinal)) continue;
                if (double.TryParse(kv.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) && v > 0)
                    xpByKey[kv.Key.Substring(3)] = v > CeilingTotal ? CeilingTotal : v;
            }
        }
    }
}
