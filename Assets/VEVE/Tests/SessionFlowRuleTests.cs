using System;
using NUnit.Framework;
using UnityEngine;
using VEVE.Net;

public sealed class SessionFlowRuleTests
{
    private sealed class FakeBackend : ISessionBackend
    {
        public int HostCalls, JoinCalls, LeaveCalls;
        public bool FailNextJoin;
        public bool listening;
        public bool isHost;
        public bool Host(string listenAddress, ushort port) { HostCalls++; if (listening && isHost) return false; listening = true; isHost = true; return true; }
        public bool Join(string serverAddress, ushort port) { JoinCalls++; if (FailNextJoin) return false; listening = true; isHost = false; return true; }
        public void Leave() { LeaveCalls++; listening = false; isHost = false; }
        public bool IsListening => listening;
        public bool IsHost => isHost;
    }

    private static DateTime t0 => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void BackoffRisesAndCaps()
    {
        Assert.Greater(SessionFlowRules.ReconnectDelaySeconds(3), SessionFlowRules.ReconnectDelaySeconds(1));
        Assert.GreaterOrEqual(SessionFlowRules.ReconnectDelaySeconds(9), SessionFlowRules.MaxBackoffSeconds);
        Assert.AreEqual(SessionFlowRules.MaxBackoffSeconds, SessionFlowRules.ReconnectDelaySeconds(40), 0.01f);
    }

    [Test]
    public void TransitionsAreGuarded()
    {
        Assert.IsTrue(SessionFlowRules.CanHost(LobbyState.Offline));
        Assert.IsFalse(SessionFlowRules.CanHost(LobbyState.InSession));
        Assert.AreEqual(LobbyState.Ended, SessionFlowRules.OnConnectResult(LobbyState.Ended, true, 0), "ended is terminal");
        Assert.AreEqual(LobbyState.Reconnecting, SessionFlowRules.OnDisconnect(LobbyState.InSession));
        Assert.AreEqual(LobbyState.InSession, SessionFlowRules.OnConnectResult(LobbyState.Reconnecting, true, 2));
        Assert.AreEqual(LobbyState.Offline, SessionFlowRules.OnConnectResult(LobbyState.Reconnecting, false, SessionFlowRules.MaxReconnectAttempts));
    }

    [Test]
    public void LobbyPanelFollowsFakeBackend()
    {
        var go = new GameObject("lobby-test");
        FakeBackend fake = new FakeBackend();
        try
        {
            var panel = go.AddComponent<SessionLobbyPanel>();
            panel.BindBackend(fake);
            panel.UseTestClock(() => t0);

            Assert.IsTrue(panel.TryHost());
            Assert.AreEqual(LobbyState.InSession, panel.State);
            Assert.IsFalse(panel.TryHost(), "cannot host while in session");
            Assert.IsTrue(panel.TryLeave());
            Assert.AreEqual(1, fake.LeaveCalls);
            Assert.AreEqual(LobbyState.Offline, panel.State);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ReconnectExhaustsToOffline()
    {
        var go = new GameObject("lobby-rc");
        FakeBackend fake = new FakeBackend { FailNextJoin = true };
        try
        {
            var panel = go.AddComponent<SessionLobbyPanel>();
            panel.BindBackend(fake);
            panel.TryJoin("h", 7777); // failJoin -> state stays Offline (false result, no exception)
            Assert.AreEqual(1, fake.JoinCalls);

            // force reconnect path: enter session then drop
            fake.FailNextJoin = false;
            Assert.IsTrue(panel.TryJoin("h", 7777));
            Assert.AreEqual(LobbyState.InSession, panel.State);

            panel.OnConnectionLost();
            fake.listening = false; // transport actually down: reconnect path engages
            Assert.AreEqual(LobbyState.Reconnecting, panel.State);

            fake.FailNextJoin = true;
            DateTime now = t0;
            for (int i = 0; i <= SessionFlowRules.MaxReconnectAttempts; i++)
            {
                now = now.AddSeconds(SessionFlowRules.MaxBackoffSeconds + 0.1);
                panel.UseTestClock(() => now);
                panel.TickReconnect(now);
            }
            Assert.AreEqual(LobbyState.Offline, panel.State, "backoff exhausts to offline");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SuccessfulReconnectRestoresInSession()
    {
        var go = new GameObject("lobby-ok");
        FakeBackend fake = new FakeBackend();
        try
        {
            var panel = go.AddComponent<SessionLobbyPanel>();
            panel.BindBackend(fake);
            Assert.IsTrue(panel.TryJoin("h", 7777));
            fake.listening = false;
            panel.OnConnectionLost();
            fake.listening = true; // transport reports the listener is alive again
            panel.TickReconnect(DateTime.UtcNow.AddSeconds(60));
            Assert.AreEqual(LobbyState.InSession, panel.State);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
