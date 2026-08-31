using NUnit.Framework;
using UnityEngine;
using VEVE.Customization;
using VEVE.WeaponCustomPro;
using VEVE.UI;

public sealed class OpticCatalogBridgeTests
{
    [Test]
    public void EnsureOpticAttachmentsIsIdempotentAndComplete()
    {
        var manager = new WeaponCustomizationManager();
        int addedFirst = OpticCatalogBridge.EnsureOpticAttachments(manager);
        Assert.That(addedFirst, Is.EqualTo(ScopeCatalog.Count));
        Assert.AreEqual(0, OpticCatalogBridge.EnsureOpticAttachments(manager), "second pass must be a no-op");
        Assert.AreEqual(0, OpticCatalogBridge.EnsureOpticAttachments(null));
    }

    [Test]
    public void MountedOpticResolvesBackToScopeProfile()
    {
        var manager = new WeaponCustomizationManager();
        OpticCatalogBridge.EnsureOpticAttachments(manager);
        string opticId = ScopeCatalog.All[2].id;

        Assert.IsTrue(manager.Attach("m4a1", opticId));
        Assert.IsTrue(OpticCatalogBridge.TryGetMounted(manager, "m4a1", out ScopeProfile scope));
        Assert.AreEqual(opticId, scope.id);
        Assert.AreEqual(opticId, OpticCatalogBridge.MountedOpticId(manager, "m4a1"));
        Assert.IsFalse(OpticCatalogBridge.TryGetMounted(manager, "ak74m", out _), "unset weapon has no mount");
        Assert.IsNull(OpticCatalogBridge.MountedOpticId(null, "m4a1"));
    }

    [Test]
    public void HighPowerOpticsGateBehindLevel()
    {
        var manager = new WeaponCustomizationManager();
        OpticCatalogBridge.EnsureOpticAttachments(manager);
        string sniperScope = null;
        foreach (ScopeProfile p in ScopeCatalog.All)
        {
            if (p.magnificationMax >= 10f) { sniperScope = p.id; break; }
        }
        Assert.NotNull(sniperScope);

        manager.Detach("m4a1", AttachmentSlot.Optic);
        Assert.IsTrue(manager.CanAttach("m4a1", sniperScope));

        var lowLevel = manager.GetAttachmentsForSlot(AttachmentSlot.Optic, 1);
        bool visibleAt1 = false;
        foreach (AttachmentDefinition def in lowLevel) if (def.attachmentId == sniperScope) visibleAt1 = true;
        Assert.IsFalse(visibleAt1, "high power glass locked at level 1");

        var highLevel = manager.GetAttachmentsForSlot(AttachmentSlot.Optic, 6);
        bool visibleAt6 = false;
        foreach (AttachmentDefinition def in highLevel) if (def.attachmentId == sniperScope) visibleAt6 = true;
        Assert.IsTrue(visibleAt6, "high power glass unlocked at level 6");
    }

    [Test]
    public void ReticleScaleFollowsMountedOpticEvent()
    {
        var go = new GameObject("hud");
        try
        {
            var overlay = go.AddComponent<ScopeReticleOverlay>();
            overlay.SetReticleGeometry(1920f, 6f);
            Assert.That(overlay.PixelsPerMoaCurrent, Is.EqualTo(1920f / 360f).Within(0.01f));

            VEVE.EventBus.PublishGlobal(new OpticMountedEvent
            {
                weaponId = "m4a1",
                scopeId = ScopeCatalog.All[0].id,
                fovDegAtMinZoom = 12f
            });
            VEVE.EventBus.ProcessQueue();
            Assert.That(overlay.PixelsPerMoaCurrent, Is.EqualTo(1920f / 720f).Within(0.01f));

            VEVE.EventBus.PublishGlobal(new OpticMountedEvent { weaponId = "m4a1", fovDegAtMinZoom = 0f });
            VEVE.EventBus.ProcessQueue();
            Assert.That(overlay.PixelsPerMoaCurrent, Is.EqualTo(ScopeReticleOverlay.DefaultCanvasWidthPx
                / (ScopeReticleOverlay.DefaultFieldOfViewDegrees * 60f)).Within(0.01f),
                "no-mount restores default geometry");
        }
        finally
        {
            VEVE.EventBus.ClearAll();
            var canvasGo = GameObject.Find("ScopeReticle");
            if (canvasGo != null) Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(go);
        }
    }
}
