using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VEVE.UI
{
    /// <summary>Live transport-agnostic ping probe (session backend fills real RTT elsewhere).</summary>
    public interface IClientPingSource
    {
        int PingMs(ulong clientId);
    }

    /// <summary>One lobby roster row.</summary>
    public struct LobbyRosterRow
    {
        public ulong owner;
        public int pingMs;
    }

    /// <summary>
    /// Pure roster presenter: distinct owners ascending with their ping probe -
    /// never leaks the offline sentinel and is empty-list safe.
    /// </summary>
    public static class LobbyRosterModel
    {
        public const string EmptyLabel = "no pawns in session";

        public static List<LobbyRosterRow> Build(IReadOnlyList<ulong> ownerIds, IClientPingSource pings)
        {
            var rows = new List<LobbyRosterRow>();
            if (ownerIds == null) return rows;
            var seen = new HashSet<ulong>();
            for (int i = 0; i < ownerIds.Count; i++)
            {
                ulong id = ownerIds[i];
                if (id == 0 || id == Net.LagCompRules.OfflineOwner) continue;
                if (seen.Add(id))
                {
                    rows.Add(new LobbyRosterRow
                    {
                        owner = id,
                        pingMs = pings != null ? Math.Max(0, pings.PingMs(id)) : 0
                    });
                }
            }
            rows.Sort((a, b) => a.owner.CompareTo(b.owner));
            return rows;
        }

        public static string Format(IReadOnlyList<LobbyRosterRow> rows)
        {
            if (rows == null || rows.Count == 0) return EmptyLabel;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append("CLIENT ").Append(rows[i].owner).Append("  ·  ").Append(rows[i].pingMs).Append(" ms");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Lobby-side panel text: samples pawn owners on a half-second cadence (never per
    /// frame) from <see cref="VEVE.Net.NetworkedPlayerPawn.CollectCombatTargets"/> owners.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyRosterPanel : MonoBehaviour
    {
        [SerializeField] private float interval = 0.5f;
        [SerializeField] private IClientPingSource pingSource;

        private Canvas canvas;
        private Text label;
        private float t;

        public void BindPingSource(IClientPingSource src) { pingSource = src; }

        private void Start()
        {
            canvas = UiFactory.CreateCanvas("LobbyRoster", 245);
            var root = UiFactory.CreatePanel(canvas.transform as RectTransform, "Root",
                new Color(0f, 0f, 0f, 0.45f));
            UiFactory.StretchFull(root.rectTransform);
            label = UiFactory.CreateText(root.rectTransform, "List", LobbyRosterModel.EmptyLabel, 16,
                HudThemeLibrary.TextOnDark, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(520f, 200f), Vector2.zero);
            canvas.gameObject.SetActive(false);
        }

        private void OnEnable() { t = 0f; }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t < interval) return;
            t = 0f;
            RefreshNow();
        }

        public void RefreshNow()
        {
            var owners = new List<ulong>();
            var transforms = new List<Transform>();
            Net.NetworkedPlayerPawn.CollectCombatTargets(transforms);
            foreach (var t2 in transforms)
            {
                var pawn = t2.GetComponent<Net.NetworkedPlayerPawn>();
                if (pawn != null) owners.Add(pawn.OwnerClientId);
            }
            var rows = LobbyRosterModel.Build(owners, pingSource);
            if (label != null) label.text = LobbyRosterModel.Format(rows);
        }

        public bool Visible => canvas != null && canvas.gameObject.activeSelf;
        public void SetVisible(bool v) { if (canvas != null) canvas.gameObject.SetActive(v); }
    }
}
