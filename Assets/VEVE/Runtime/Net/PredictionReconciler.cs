using UnityEngine;
using VEVE.Catalog;

namespace VEVE.Net
{
    /// <summary>Outcome of reconciling one authoritative journal shot against a local prediction.</summary>
    public enum PredictionResult { Foreign, NotPredicted, Confirmed, Reverted, LateOutsideWindow }

    /// <summary>
    /// Reconcile v2 (W5): the journal is retroactive authority. An optimistic local hit
    /// that the authoritative record denies revokes its exact XP credit (bounded, offline
    /// safe); a confirmed prediction locks it. No network code invents new authority -
    /// everything here just rolls back what local prediction already granted.
    /// </summary>
    public sealed class PredictionReconciler
    {
        public int ConfirmedCount { get; private set; }
        public int RevertCount { get; private set; }
        public int LateCount { get; private set; }

        /// <summary>Ring lookup + window + ledger revoke on mismatch. Ledger may be null (telemetry only).</summary>
        public PredictionResult Reconcile(ShotReplayWindow ring, ulong owner, int authoritativeTick,
            bool serverHit, FamilyXpLedger ledger, string family, double predictedXp)
        {
            if (owner == 0 || owner == LagCompRules.OfflineOwner || ring == null)
                return PredictionResult.Foreign;
            if (!ring.TryGetLatest(owner, authoritativeTick, out ShotPrediction pred))
                return PredictionResult.NotPredicted;

            int window = LagCompRules.AuthorityWindowFrames(LagCompRules.DefaultPingSeconds * 1000.0, LagCompRules.DefaultTickHz);
            if (!LagCompRules.AuthoritativeWithinWindow(pred.tick, authoritativeTick, window))
            {
                LateCount++;
                return PredictionResult.LateOutsideWindow;
            }

            if (pred.localHit == serverHit)
            {
                ConfirmedCount++;
                return PredictionResult.Confirmed;
            }

            RevertCount++;
            if (pred.localHit && !serverHit && ledger != null && predictedXp > 0)
            {
                ledger.Revoke(owner, family, predictedXp);
            }
            return PredictionResult.Reverted;
        }

        public string Telemetry => $"confirmed={ConfirmedCount} reverted={RevertCount} late={LateCount}";
    }
}
