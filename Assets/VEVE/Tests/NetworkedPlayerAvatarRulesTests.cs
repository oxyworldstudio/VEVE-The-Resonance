using NUnit.Framework;
using VEVE.Net;

public sealed class NetworkedPlayerAvatarRulesTests
{
    [Test]
    public void OfflineAlwaysOwnsLocalInput()
    {
        // sessionOnline false: single-player can never be locked by a missing NetworkManager
        Assert.IsTrue(NetAvatarRules.ShouldAcceptLocalInput(false, false, false));
        Assert.IsTrue(NetAvatarRules.ShouldAcceptLocalInput(false, true, false));
    }

    [Test]
    public void UnspawnedGracefulTrue()
    {
        Assert.IsTrue(NetAvatarRules.ShouldAcceptLocalInput(true, false, false), "pre-spawn frame keeps input");
    }

    [Test]
    public void LiveSessionSplitsAuthority()
    {
        Assert.IsTrue(NetAvatarRules.ShouldAcceptLocalInput(true, true, true));
        Assert.IsFalse(NetAvatarRules.ShouldAcceptLocalInput(true, true, false), "remote avatar never runs local input");
    }

    [Test]
    public void RemoteTransformDrivenMatrix()
    {
        Assert.IsFalse(NetAvatarRules.ShouldDriveTransform(false, true, true), "offline: no networked transform drive");
        Assert.IsTrue(NetAvatarRules.ShouldDriveTransform(true, true, true));
        Assert.IsFalse(NetAvatarRules.ShouldDriveTransform(true, false, true), "never drive before spawn completes");
    }
}
