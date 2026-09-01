using UnityEngine;
using VEVE.AI;

namespace VEVE.Combat
{
    /// <summary>
    /// W-H6: slow-cadence director that makes AI throwers actually fire grenades. Every
    /// <see cref="ScanIntervalSeconds"/> it re-scans the scene with
    /// <see cref="UnityEngine.Object.FindObjectsByType{T}(FindObjectsSortMode)"/> (the result is cached
    /// for that window), and for each registered <see cref="GrenadeThrowerAI"/> whose target sits inside
    /// the throw band with the cooldown elapsed it drives <see cref="GrenadeThrowerAI.TryThrowAt"/>.
    ///
    /// Edit-mode safe by construction: no OnEnable/OnDisable/Awake lifecycle, no DontDestroyOnLoad, and
    /// the deterministic entry point <see cref="ScanOnce"/> takes an explicit unscaled clock so tests and
    /// squad code drive it without frames.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AiGrenadeDirector : MonoBehaviour
    {
        /// <summary>Scan/cache window in seconds; <see cref="FindObjectsByType{T}"/> runs at most this often.</summary>
        public const float ScanIntervalSeconds = 2f;

        /// <summary>Inspector-facing master switch backing <see cref="Enabled"/>.</summary>
        [SerializeField] private bool directorEnabled = true;

        private float lastScanTime = float.NegativeInfinity;
        private float lastThrowTime = -1f;

        private GrenadeThrowerAI[] registeredThrowers;

        /// <summary>Master switch; false makes every scan a no-op (no scan, no throw).</summary>
        public bool Enabled
        {
            get { return directorEnabled; }
            set { directorEnabled = value; }
        }

        /// <summary>Throwers registered by the most recent scan (0 until the first scan runs).</summary>
        public int RegisteredCount
        {
            get { return registeredThrowers == null ? 0 : registeredThrowers.Length; }
        }

        /// <summary>Unscaled clock of the last successful throw; -1 until the first throw.</summary>
        public float LastThrowTime
        {
            get { return lastThrowTime; }
        }

        private void Update()
        {
            if (!directorEnabled) return;
            if (Time.time - lastScanTime < ScanIntervalSeconds) return;
            ScanOnce(Time.time);
        }

        /// <summary>
        /// Deterministic scan step: refreshes the thrower cache when the <see cref="ScanIntervalSeconds"/>
        /// window elapsed (or the cache is empty), then walks it, skipping destroyed entries and
        /// out-of-band/cooldown-locked throwers, and issues <see cref="GrenadeThrowerAI.TryThrowAt"/>
        /// (which re-validates authoritatively). Null-safe end to end.
        /// </summary>
        /// <param name="nowTime">Unscaled game clock in seconds.</param>
        /// <returns>Number of grenades thrown by this scan.</returns>
        public int ScanOnce(float nowTime)
        {
            if (!directorEnabled) return 0;
            if (registeredThrowers == null || registeredThrowers.Length == 0 || nowTime - lastScanTime >= ScanIntervalSeconds)
            {
                registeredThrowers = Object.FindObjectsByType<GrenadeThrowerAI>(FindObjectsSortMode.None);
                lastScanTime = nowTime;
            }

            int thrown = 0;
            GrenadeThrowerAI[] cache = registeredThrowers;
            if (cache == null) return 0;
            for (int i = 0; i < cache.Length; i++)
            {
                GrenadeThrowerAI thrower = cache[i];
                if (thrower == null) continue; // destroyed since the scan (Unity null)
                Transform target = thrower.Target;
                if (target == null) continue;
                float distance = Vector3.Distance(thrower.transform.position, target.position);
                if (distance < AiThrowRules.MinThrowRangeM || distance > thrower.EngageRangeM) continue;
                if (nowTime - thrower.LastThrowTime < thrower.CooldownSeconds) continue;
                if (thrower.TryThrowAt(target, nowTime))
                {
                    lastThrowTime = nowTime;
                    thrown++;
                }
            }
            return thrown;
        }
    }
}
