using System;
using UnityEngine;
using UnityEngine.UI;
using VEVE.UI;

namespace VEVE.Net
{
    /// <summary>
    /// Lobby/session panel built from UiFactory primitives, driving SessionFlowRules
    /// against an ISessionBackend (NGO adapter injected at Start). The whole state
    /// machine is callable with explicit time so EditMode tests can drive reconnect.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SessionLobbyPanel : MonoBehaviour
    {
        [SerializeField] private string lastHostAddress = "127.0.0.1";
        [SerializeField] private ushort lastHostPort = 7777;
        [SerializeField] private bool startVisible = true;

        private ISessionBackend backend;
        public LobbyState State { get; private set; } = LobbyState.Offline;
        public int ReconnectAttempts { get; private set; }
        public DateTime NextReconnectUtc { get; private set; }

        private Text statusText;
        private Button hostButton;
        private Button joinButton;
        private Button leaveButton;
        private Func<DateTime> clock = () => DateTime.UtcNow;

        public void BindBackend(ISessionBackend sessionBackend) { backend = sessionBackend; }
        public void UseTestClock(Func<DateTime> now) { clock = now ?? (() => DateTime.UtcNow); }

        private void Awake()
        {
            BuildLobby();
        }

        private void Start()
        {
            if (backend == null)
            {
                NetworkGameFlow flow = UnityEngine.Object.FindFirstObjectByType<NetworkGameFlow>();
                if (flow != null) backend = new NetworkGameFlowBackend(flow);
            }
        }

        private void BuildLobby()
        {
            Canvas canvas = UiFactory.CreateCanvas("SessionLobby", 250);
            var rootTr = canvas.transform as RectTransform;
            Image root = UiFactory.CreatePanel(rootTr, "Root", new Color(0.04f, 0.05f, 0.05f, 0.92f));
            UiFactory.StretchFull(root.rectTransform);

            RectTransform body = UiFactory.CreatePanel(root.rectTransform, "Body", new Color(0.07f, 0.09f, 0.09f, 0.97f)).rectTransform;
            body.anchorMin = new Vector2(0.5f, 0.5f);
            body.anchorMax = new Vector2(0.5f, 0.5f);
            body.pivot = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(540f, 240f);
            body.anchoredPosition = Vector2.zero;

            statusText = UiFactory.CreateText(body, "Status", "OFFLINE", 18,
                HudThemeLibrary.SquadBlue, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(520f, 48f), new Vector2(0f, -30f));

            hostButton = UiFactory.CreateTableButton(body, "HostBtn", "HOST OPERATION",
                HudThemeLibrary.OliveBright, HudThemeLibrary.TextOnDark, HudThemeLibrary.FontSubhead, new Vector2(220f, 36f));
            hostButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -84f);
            hostButton.onClick.AddListener(() => TryHost());

            joinButton = UiFactory.CreateTableButton(body, "JoinBtn", "JOIN OPERATION",
                HudThemeLibrary.SquadBlue, HudThemeLibrary.TextOnDark, HudThemeLibrary.FontSubhead, new Vector2(220f, 36f));
            joinButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -130f);
            joinButton.onClick.AddListener(() => TryJoin(lastHostAddress, lastHostPort));

            leaveButton = UiFactory.CreateTableButton(body, "LeaveBtn", "LEAVE SESSION",
                HudThemeLibrary.AlertRed, HudThemeLibrary.TextOnDark, HudThemeLibrary.FontSubhead, new Vector2(220f, 36f));
            leaveButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -176f);
            leaveButton.onClick.AddListener(() => TryLeave());

            canvas.gameObject.SetActive(startVisible);
            Refresh();
        }

        // ------------------------------------------------------------ commands

        /// <summary>Never silent: every failed action paints the reason on the lobby (W-BUG-001).</summary>
        public bool LastActionFailed { get; private set; }

        public bool TryHost()
        {
            if (backend == null || !SessionFlowRules.CanHost(State)) return Fail("cannot host now");
            if (!backend.Host(lastHostAddress, lastHostPort)) return Fail("host start failed (transport)");
            LastActionFailed = false;
            State = LobbyState.InSession;
            Refresh();
            return true;
        }

        public bool TryJoin(string address, ushort port)
        {
            if (backend == null || !SessionFlowRules.CanJoin(State)) return Fail("cannot join now");
            lastHostAddress = address;
            lastHostPort = port;
            if (!backend.Join(address, port)) return Fail("join refused by transport");
            LastActionFailed = false;
            State = LobbyState.InSession;
            Refresh();
            return true;
        }

        bool Fail(string reason)
        {
            LastActionFailed = true;
            if (statusText != null)
            {
                statusText.color = HudThemeLibrary.AlertRed;
                statusText.text = "ERROR: " + reason;
            }
            Refresh();
            return false;
        }

        public bool TryLeave()
        {
            if (backend == null || !SessionFlowRules.CanLeave(State)) return false;
            backend.Leave();
            State = LobbyState.Offline;
            Refresh();
            return true;
        }

        /// <summary>Called by NGO adapter or tests when the connection drops.</summary>
        public void OnConnectionLost()
        {
            State = SessionFlowRules.OnDisconnect(State);
            ReconnectAttempts = 0;
            Refresh();
        }

        /// <summary>Explicit-now so reconnect timing is testable without editing time.</summary>
        public void TickReconnect(DateTime nowUtc)
        {
            if (State != LobbyState.Reconnecting) return;
            if (backend != null && backend.IsListening)
            {
                State = LobbyState.InSession;
                ReconnectAttempts = 0;
                Refresh();
                return;
            }
            if (!SessionFlowRules.ShouldAttemptReconnect(ReconnectAttempts))
            {
                State = SessionFlowRules.OnConnectResult(State, false, ReconnectAttempts);
                Refresh();
                return;
            }
            if (nowUtc < NextReconnectUtc) return;

            bool ok = backend != null && backend.Join(lastHostAddress, lastHostPort);
            ReconnectAttempts = ok ? 0 : ReconnectAttempts + 1;
            NextReconnectUtc = nowUtc.AddSeconds(SessionFlowRules.ReconnectDelaySeconds(ReconnectAttempts));
            State = SessionFlowRules.OnConnectResult(State, ok, ReconnectAttempts);
            Refresh();
        }

        private void Update()
        {
            if (backend != null) TickReconnect(DateTime.UtcNow);
        }

        // ---------------------------------------------------------- presentation

        private void Refresh()
        {
            if (statusText != null)
                statusText.text = LabelFor(State);
            if (hostButton != null) hostButton.interactable = SessionFlowRules.CanHost(State);
            if (joinButton != null) joinButton.interactable = SessionFlowRules.CanJoin(State);
            if (leaveButton != null) leaveButton.interactable = SessionFlowRules.CanLeave(State);
        }

        public static string LabelFor(LobbyState s)
        {
            switch (s)
            {
                case LobbyState.InSession: return "IN SESSION - pawns live";
                case LobbyState.Reconnecting: return "RECONNECTING...";
                case LobbyState.Ended: return "OP ENDED";
                default: return "OFFLINE";
            }
        }
    }

    /// <summary>NGO adapter: drives NetworkGameFlow; disconnect event fed back into the rules.</summary>
    public sealed class NetworkGameFlowBackend : ISessionBackend
    {
        private readonly NetworkGameFlow flow;
        private SessionLobbyPanel panel;

        public NetworkGameFlowBackend(NetworkGameFlow gameFlow, SessionLobbyPanel lobbyPanel = null)
        {
            flow = gameFlow;
            panel = lobbyPanel;
            if (flow != null) flow.ConnectionLost = OnLost;
        }

        private void OnLost()
        {
            if (panel != null) panel.OnConnectionLost();
        }

        public bool Host(string listenAddress, ushort port) => flow.StartHostSession(listenAddress);
        public bool Join(string serverAddress, ushort port) => flow.JoinSession(serverAddress, port);
        public void Leave() => flow.Shutdown();
        public bool IsListening => Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening;
        public bool IsHost => Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsHost;
    }
}
