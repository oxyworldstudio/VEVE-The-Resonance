using UnityEngine;

namespace VEVE.Net
{
    /// <summary>Session lifecycle states of the lobby/online layer.</summary>
    public enum LobbyState { Offline = 0, InSession = 1, Reconnecting = 2, Ended = 3 }

    /// <summary>
    /// Pure session-flow rules: transitions, reconnect backoff, and the
    /// authority matrix the lobby UI binds to. NGO lives behind
    /// <see cref="ISessionBackend"/> so the logic runs in EditMode with a fake.
    /// </summary>
    public static class SessionFlowRules
    {
        public const float BaseBackoffSeconds = 0.5f;
        public const float MaxBackoffSeconds = 8f;
        public const int MaxReconnectAttempts = 5;

        public static bool CanHost(LobbyState s) => s == LobbyState.Offline;
        public static bool CanJoin(LobbyState s) => s == LobbyState.Offline;
        public static bool CanLeave(LobbyState s) => s != LobbyState.Offline && s != LobbyState.Ended;

        /// <summary>Exponential backoff, capped at <see cref="MaxBackoffSeconds"/>.</summary>
        public static float ReconnectDelaySeconds(int attempt)
        {
            if (attempt <= 0) return BaseBackoffSeconds;
            float d = BaseBackoffSeconds * Mathf.Pow(2f, Mathf.Min(attempt, 10));
            return d > MaxBackoffSeconds || float.IsNaN(d) ? MaxBackoffSeconds : d;
        }

        public static bool ShouldAttemptReconnect(int attempt) => attempt < MaxReconnectAttempts;

        /// <summary>Transition on connection result; failure at cap falls to Offline (terminal).</summary>
        public static LobbyState OnConnectResult(LobbyState current, bool success, int attempt)
        {
            if (current == LobbyState.Ended) return LobbyState.Ended;
            if (success) return LobbyState.InSession;
            if (current != LobbyState.Reconnecting) return LobbyState.Reconnecting;
            return ShouldAttemptReconnect(attempt) ? LobbyState.Reconnecting : LobbyState.Offline;
        }

        public static LobbyState OnDisconnect(LobbyState current)
        {
            if (current == LobbyState.InSession || current == LobbyState.Reconnecting) return LobbyState.Reconnecting;
            return current;
        }
    }

    /// <summary>Transport seam: implemented by the NGO backend and by test doubles.</summary>
    public interface ISessionBackend
    {
        bool Host(string listenAddress, ushort port);
        bool Join(string serverAddress, ushort port);
        void Leave();
        bool IsListening { get; }
        bool IsHost { get; }
    }
}
