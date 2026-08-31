using System;
using System.Collections.Generic;
using System.IO;

namespace VEVE.Content.SimData
{
    /// <summary>
    /// Versioned binary content pack (F0 of the roadmap): all numeric sim truth
    /// travels through a hash-sealed blob so content is auditable, diffable and
    /// replayable (same hash = same physics, journal seed binds it).
    /// </summary>
    public sealed class SimDataPack
    {
        public const int Magic = unchecked((int)0x56455645); // 'VEVE'
        public const int Version = 1;
        public const uint FnvOffset = 2166136261u;
        public const uint FnvPrime = 16777619u;

        public int SchemaVersion = Version;
        public uint PayloadHash;
        public readonly List<Entry> Entries = new List<Entry>(256);

        public struct Entry
        {
            public string key;   // ascii keys (catalog ids / table names)
            public double[] values;
        }

        public SimDataPack Add(string key, params double[] values)
        {
            if (string.IsNullOrEmpty(key)) return this;
            Entries.Add(new Entry { key = key, values = (double[])(values ?? Array.Empty<double>()).Clone() });
            RecomputeHash();
            return this;
        }

        public bool TryGet(string key, out double[] values)
        {
            values = null;
            foreach (var e in Entries)
            {
                if (e.key == key) { values = (double[])e.values.Clone(); return true; }
            }
            return false;
        }

        public void RecomputeHash()
        {
            uint h = FnvOffset;
            foreach (var e in Entries)
            {
                h = MixString(h, e.key);
                foreach (double v in e.values)
                {
                    long bits = BitConverter.DoubleToInt64Bits(v);
                    h = Mix(h, (uint)(bits & 0xFFFFFFFF));
                    h = Mix(h, (uint)(bits >> 32));
                }
            }
            PayloadHash = h;
        }

        static uint MixString(uint h, string s)
        {
            if (s == null) return h;
            for (int i = 0; i < s.Length; i++) h = Mix(h, s[i]);
            return h;
        }

        static uint Mix(uint h, uint v)
        {
            unchecked
            {
                h = (h ^ v) * FnvPrime;
                return h;
            }
        }

        // ------------------------------------------------------------- encoding

        public byte[] Encode()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Magic);
                bw.Write(SchemaVersion);
                RehashIfDirty();
                bw.Write(PayloadHash);
                bw.Write(Entries.Count);
                foreach (var e in Entries)
                {
                    bw.Write(e.key ?? string.Empty);
                    bw.Write(e.values.Length);
                    for (int i = 0; i < e.values.Length; i++) bw.Write(e.values[i]);
                }
                bw.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>Strict parse: bad magic/version breaks verification (returns 0 entries).</summary>
        public static SimDataPack Decode(byte[] data, bool validate = true)
        {
            var pack = new SimDataPack();
            if (data == null || data.Length < 16) return pack;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var br = new BinaryReader(ms))
                {
                    if (br.ReadInt32() != Magic) return new SimDataPack();
                    pack.SchemaVersion = br.ReadInt32();
                    uint headerHash = br.ReadUInt32();
                    int count = br.ReadInt32();
                    var es = new List<Entry>(Math.Max(0, count));
                    for (int i = 0; i < count; i++)
                    {
                        string k = br.ReadString();
                        int n = br.ReadInt32();
                        var vals = new double[Math.Max(0, n)];
                        for (int j = 0; j < vals.Length; j++) vals[j] = br.ReadDouble();
                        es.Add(new Entry { key = k, values = vals });
                    }
                    pack.Entries.AddRange(es);
                    pack.RecomputeHash();
                    if (validate && headerHash != pack.PayloadHash) return new SimDataPack(); // tamper = empty
                }
            }
            catch (Exception) { return new SimDataPack(); }
            return pack;
        }

        public void RehashIfDirty()
        {
            RecomputeHash();
        }
    }
}
