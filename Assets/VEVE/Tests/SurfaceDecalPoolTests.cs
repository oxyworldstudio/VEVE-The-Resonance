using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VEVE.Graphics;
using QualityLevel = VEVE.Realism.QualityLevel;
using VEVE.VFX;

public sealed class SurfaceDecalPoolTests
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static SurfaceDecalPool CreateInactivePool(QualityLevel quality)
    {
        GameObject go = new GameObject("SurfaceDecalPoolTest");
        go.SetActive(false);
        SurfaceDecalPool pool = go.AddComponent<SurfaceDecalPool>();
        pool.Quality = quality;
        return pool;
    }

    [Test]
    public void Capacity_PerQuality_IsFixedAndMonotonic()
    {
        Assert.AreEqual(32, DecalPoolRules.CapacityFor(QualityLevel.Low));
        Assert.AreEqual(64, DecalPoolRules.CapacityFor(QualityLevel.Medium));
        Assert.AreEqual(128, DecalPoolRules.CapacityFor(QualityLevel.High));
        Assert.AreEqual(256, DecalPoolRules.CapacityFor(QualityLevel.Ultra));
        Assert.Greater(DecalPoolRules.CapacityFor(QualityLevel.Medium),
            DecalPoolRules.CapacityFor(QualityLevel.Low));
        Assert.Greater(DecalPoolRules.CapacityFor(QualityLevel.High),
            DecalPoolRules.CapacityFor(QualityLevel.Medium));
        Assert.Greater(DecalPoolRules.CapacityFor(QualityLevel.Ultra),
            DecalPoolRules.CapacityFor(QualityLevel.High));
        Assert.AreEqual(32, DecalPoolRules.CapacityFor((QualityLevel)(-1)));
        Assert.AreEqual(256, DecalPoolRules.CapacityFor((QualityLevel)99));
    }

    [Test]
    public void FadeSeconds_MatchContractAndOrder()
    {
        Assert.AreEqual(20f, DecalPoolRules.FadeSecondsFor(DecalKind.BulletHole), 1e-4f);
        Assert.AreEqual(12f, DecalPoolRules.FadeSecondsFor(DecalKind.BloodSplat), 1e-4f);
        Assert.AreEqual(45f, DecalPoolRules.FadeSecondsFor(DecalKind.Scorch), 1e-4f);
        Assert.AreEqual(8f, DecalPoolRules.FadeSecondsFor(DecalKind.Chip), 1e-4f);
        Assert.Greater(DecalPoolRules.FadeSecondsFor(DecalKind.Scorch),
            DecalPoolRules.FadeSecondsFor(DecalKind.BulletHole));
        Assert.Greater(DecalPoolRules.FadeSecondsFor(DecalKind.BulletHole),
            DecalPoolRules.FadeSecondsFor(DecalKind.BloodSplat));
        Assert.Greater(DecalPoolRules.FadeSecondsFor(DecalKind.BloodSplat),
            DecalPoolRules.FadeSecondsFor(DecalKind.Chip));
    }

    [Test]
    public void ScaleJitter_StaysInRangeAndIsDeterministic()
    {
        var kinds = new[] { DecalKind.BulletHole, DecalKind.BloodSplat, DecalKind.Scorch, DecalKind.Chip };
        foreach (DecalKind kind in kinds)
        {
            float min = DecalPoolRules.MinScale(kind);
            float max = DecalPoolRules.MaxScale(kind);
            Assert.LessOrEqual(min, max, kind.ToString());
            HashSet<int> distinct = new HashSet<int>();
            for (int seed = 0; seed < 64; seed++)
            {
                float s = DecalPoolRules.ScaleFor(kind, seed);
                Assert.GreaterOrEqual(s, min, kind + " seed " + seed);
                Assert.LessOrEqual(s, max, kind + " seed " + seed);
                Assert.AreEqual(s, DecalPoolRules.ScaleFor(kind, seed), 0f, "deterministic");
                distinct.Add(Mathf.RoundToInt(s * 10000f));
            }
            Assert.Greater(distinct.Count, 1, kind + " jitter must spread");
            Assert.AreEqual((min + max) * 0.5f, DecalPoolRules.ScaleFor(kind), 1e-4f);
        }
    }

    [Test]
    public void RotationJitter_MatchesKindContract()
    {
        Assert.AreEqual(360f, DecalPoolRules.RotationJitterDeg(DecalKind.BulletHole), 1e-4f);
        Assert.AreEqual(25f, DecalPoolRules.RotationJitterDeg(DecalKind.BloodSplat), 1e-4f);
        Assert.AreEqual(15f, DecalPoolRules.RotationJitterDeg(DecalKind.Scorch), 1e-4f);
        Assert.AreEqual(90f, DecalPoolRules.RotationJitterDeg(DecalKind.Chip), 1e-4f);

        var kinds = new[] { DecalKind.BulletHole, DecalKind.BloodSplat, DecalKind.Scorch, DecalKind.Chip };
        foreach (DecalKind kind in kinds)
        {
            float span = DecalPoolRules.RotationJitterDeg(kind);
            float maxSeen = 0f;
            for (int seed = 0; seed < 128; seed++)
            {
                float r = DecalPoolRules.RotationFor(kind, seed);
                Assert.GreaterOrEqual(r, 0f);
                Assert.LessOrEqual(r, span);
                Assert.AreEqual(r, DecalPoolRules.RotationFor(kind, seed), 0f, "deterministic");
                if (r > maxSeen) maxSeen = r;
            }
            Assert.Greater(maxSeen, span * 0.25f, kind + " jitter must actually spread");
        }
    }

    [Test]
    public void ColorFor_BlendsInDocumentedDirection()
    {
        Color baseSurface = new Color(0.5f, 0.5f, 0.55f);
        float baseGray = baseSurface.grayscale;

        Color blood = DecalPoolRules.ColorFor(DecalKind.BloodSplat, baseSurface);
        Color scorch = DecalPoolRules.ColorFor(DecalKind.Scorch, baseSurface);
        Color bullet = DecalPoolRules.ColorFor(DecalKind.BulletHole, baseSurface);
        Color chip = DecalPoolRules.ColorFor(DecalKind.Chip, baseSurface);

        Assert.Less(blood.grayscale, baseGray, "blood darkens");
        Assert.Less(scorch.grayscale, baseGray, "scorch blackens");
        Assert.Less(bullet.grayscale, baseGray, "bullet hole darkens");
        Assert.Greater(chip.grayscale, baseGray, "chip lightens");
        Assert.Less(scorch.grayscale, blood.grayscale, "scorch is the darkest wear");
        foreach (Color c in new[] { blood, scorch, bullet, chip })
        {
            Assert.GreaterOrEqual(c.r, 0f); Assert.LessOrEqual(c.r, 1f);
            Assert.GreaterOrEqual(c.g, 0f); Assert.LessOrEqual(c.g, 1f);
            Assert.GreaterOrEqual(c.b, 0f); Assert.LessOrEqual(c.b, 1f);
            Assert.AreEqual(1f, c.a);
        }
        Assert.Greater(blood.r, blood.g, "blood stays red-shifted");
    }

    [Test]
    public void Jitter01_IsDeterministicBoundedAndKindSeedsDiffer()
    {
        for (int seed = -32; seed < 96; seed++)
        {
            float j = DecalPoolRules.Jitter01(seed);
            Assert.GreaterOrEqual(j, 0f);
            Assert.LessOrEqual(j, 1f);
            Assert.AreEqual(j, DecalPoolRules.Jitter01(seed), 0f, "deterministic");
        }
        int baseSeed = DecalPoolRules.InstanceSeed(DecalKind.BulletHole, 7);
        Assert.AreEqual(baseSeed, DecalPoolRules.InstanceSeed(DecalKind.BulletHole, 7), "stable seed");
        Assert.AreNotEqual(baseSeed, DecalPoolRules.InstanceSeed(DecalKind.BloodSplat, 7));
        Assert.AreNotEqual(baseSeed, DecalPoolRules.InstanceSeed(DecalKind.Scorch, 7));
        Assert.AreNotEqual(baseSeed, DecalPoolRules.InstanceSeed(DecalKind.Chip, 7));
    }

    [Test]
    public void Textures_AreAlphaShapesAndByteIdenticalAcrossRebuilds()
    {
        var kinds = new[] { DecalKind.BulletHole, DecalKind.BloodSplat, DecalKind.Scorch, DecalKind.Chip };
        foreach (DecalKind kind in kinds)
        {
            Texture2D first = DecalTextureFactory.GetTextureFor(kind);
            Assert.AreEqual(DecalTextureFactory.TextureSize, first.width);
            Assert.AreEqual(DecalTextureFactory.TextureSize, first.height);
            Assert.AreSame(first, DecalTextureFactory.GetTextureFor(kind), "cached");

            List<Color> sample = new List<Color>();
            Dictionary<Vector2Int, Color> byCoord = new Dictionary<Vector2Int, Color>();
            bool hasOpaque = false, hasTransparent = false;
            for (int y = 0; y < first.height; y += 2)
            {
                for (int x = 0; x < first.width; x += 2)
                {
                    Color p = first.GetPixel(x, y);
                    sample.Add(p);
                    byCoord[new Vector2Int(x, y)] = p;
                    if (p.a > 0.8f) hasOpaque = true;
                    if (p.a < 0.2f) hasTransparent = true;
                }
            }
            Assert.IsTrue(hasOpaque, kind + " must have shape coverage");
            Assert.IsTrue(hasTransparent, kind + " must leave the surface visible");
            Assert.AreEqual(1f, sample[896].r, "RGB carries white, alpha carries shape");

            DecalTextureFactory.Clear();
            Texture2D rebuilt = DecalTextureFactory.GetTextureFor(kind);
            Assert.AreNotSame(first, rebuilt, "Clear resets the cache");
            foreach (KeyValuePair<Vector2Int, Color> kv in byCoord)
            {
                Assert.AreEqual(kv.Value, rebuilt.GetPixel(kv.Key.x, kv.Key.y),
                    kind + " rebuild must be byte-identical at " + kv.Key);
            }
            DecalTextureFactory.Clear();
        }
    }

    [Test]
    public void Pool_PlacesUntilCapacityThenRecyclesOldest()
    {
        SurfaceDecalPool pool = CreateInactivePool(QualityLevel.Low);
        try
        {
            int capacity = DecalPoolRules.CapacityFor(QualityLevel.Low);
            bool[] seen = new bool[capacity];
            for (int i = 0; i < capacity; i++)
            {
                int index = pool.Place(DecalKind.BulletHole, new Vector3(i, 0f, 0f), Vector3.up, "Concrete");
                Assert.GreaterOrEqual(index, 0);
                Assert.Less(index, capacity);
                Assert.IsFalse(seen[index], "fresh slots before the ring wraps");
                seen[index] = true;
            }
            Assert.AreEqual(capacity, pool.ActiveCount);

            int oldest = pool.Place(DecalKind.BloodSplat, Vector3.zero, Vector3.up, "Concrete");
            Assert.AreEqual(0, oldest, "full ring recycles the OLDEST slot");
            Assert.AreEqual(capacity, pool.ActiveCount);
            Assert.AreEqual(1, pool.Place(DecalKind.Chip, Vector3.zero, Vector3.up, "Concrete"));
            Assert.AreEqual(2, pool.Place(DecalKind.Scorch, Vector3.zero, Vector3.up, "Concrete"));
            for (int i = 3; i < capacity; i++)
            {
                Assert.AreEqual(i, pool.Place(DecalKind.BulletHole, Vector3.zero, Vector3.up, "Concrete"),
                    "recycling walks the ring in placement order");
            }
            Assert.AreEqual(capacity, pool.ActiveCount);
            Assert.AreEqual(0, pool.Place(DecalKind.BulletHole, Vector3.zero, Vector3.up, "Concrete"),
                "after a full lap the head wraps back to slot 0");
            Assert.AreEqual(capacity, pool.ActiveCount);
        }
        finally
        {
            Object.DestroyImmediate(pool.gameObject);
        }
    }

    [Test]
    public void Pool_DisabledComponent_ReturnsMinusOne()
    {
        SurfaceDecalPool pool = CreateInactivePool(QualityLevel.Medium);
        try
        {
            pool.enabled = false;
            Assert.AreEqual(-1, pool.Place(DecalKind.BulletHole, Vector3.zero, Vector3.up, "Concrete"));
            Assert.AreEqual(0, pool.ActiveCount);
            pool.enabled = true;
            Assert.GreaterOrEqual(pool.Place(DecalKind.BulletHole, Vector3.zero, Vector3.up, "Concrete"), 0);
        }
        finally
        {
            Object.DestroyImmediate(pool.gameObject);
        }
    }

    [Test]
    public void Pool_FadeReleasesExpiredOldestFirst()
    {
        SurfaceDecalPool pool = CreateInactivePool(QualityLevel.Medium);
        try
        {
            int chip = pool.Place(DecalKind.Chip, new Vector3(0f, 0f, 0f), Vector3.up, "Concrete");
            int blood = pool.Place(DecalKind.BloodSplat, new Vector3(1f, 0f, 0f), Vector3.up, "Concrete");
            int scorch = pool.Place(DecalKind.Scorch, new Vector3(2f, 0f, 0f), Vector3.up, "Concrete");
            Assert.AreEqual(0, chip);
            Assert.AreEqual(1, blood);
            Assert.AreEqual(2, scorch);
            Assert.AreEqual(3, pool.ActiveCount);

            pool.FadeAndRelease(0f);
            Assert.AreEqual(3, pool.ActiveCount, "zero dt is a no-op");

            pool.FadeAndRelease(8.5f);
            Assert.AreEqual(2, pool.ActiveCount, "chip (8s) fades first");

            pool.FadeAndRelease(4.5f);
            Assert.AreEqual(1, pool.ActiveCount, "blood (12s) next");

            pool.FadeAndRelease(46f);
            Assert.AreEqual(0, pool.ActiveCount, "scorch (45s) last");

            Assert.GreaterOrEqual(pool.Place(DecalKind.BulletHole, Vector3.zero, Vector3.up, "Concrete"), 0,
                "released slots are reusable");
        }
        finally
        {
            Object.DestroyImmediate(pool.gameObject);
        }
    }

    [Test]
    public void Pool_PlacementFollowsNormalWithDeterministicJitter()
    {
        SurfaceDecalPool a = CreateInactivePool(QualityLevel.Medium);
        SurfaceDecalPool b = CreateInactivePool(QualityLevel.Medium);
        try
        {
            Vector3 hit = new Vector3(1f, 2f, 3f);
            Vector3 normal = Vector3.up;
            int ia = a.Place(DecalKind.BulletHole, hit, normal, "Concrete");
            int ib = b.Place(DecalKind.BulletHole, hit, normal, "Concrete");
            Assert.AreEqual(ia, ib);

            Assert.IsTrue(a.TryGetDecalTransform(ia, out Transform ta));
            Assert.IsTrue(b.TryGetDecalTransform(ib, out Transform tb));
            Assert.That(ta.position, Is.EqualTo(hit + normal * SurfaceDecalPool.SurfaceOffsetMeters).Within(1e-4f));
            Assert.Less(Quaternion.Angle(ta.rotation, tb.rotation), 0.01f, "same placement stream = same jitter");
            Assert.That(ta.localScale.x, Is.EqualTo(tb.localScale.x).Within(1e-5f));
            Assert.GreaterOrEqual(ta.localScale.x, DecalPoolRules.MinScale(DecalKind.BulletHole));
            Assert.LessOrEqual(ta.localScale.x, DecalPoolRules.MaxScale(DecalKind.BulletHole));
            Assert.Greater(Vector3.Dot(ta.forward, normal), 0.99f, "quad faces outward along the normal");

            int wall = a.Place(DecalKind.Chip, new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, -1f), "Concrete");
            Assert.IsTrue(a.TryGetDecalTransform(wall, out Transform tw));
            Assert.That(tw.position, Is.EqualTo(new Vector3(0f, 1f, -SurfaceDecalPool.SurfaceOffsetMeters)).Within(1e-4f));
            Assert.Greater(Vector3.Dot(tw.forward, new Vector3(0f, 0f, -1f)), 0.99f);
        }
        finally
        {
            Object.DestroyImmediate(a.gameObject);
            Object.DestroyImmediate(b.gameObject);
        }
    }

    [Test]
    public void Pool_TintFollowsSurfacePaletteAndKindBlend()
    {
        SurfaceDecalPool pool = CreateInactivePool(QualityLevel.Medium);
        try
        {
            int bulletIdx = pool.Place(DecalKind.BulletHole, Vector3.zero, Vector3.up, "Metal");
            Assert.IsTrue(pool.TryGetDecalTransform(bulletIdx, out Transform bulletDecal));
            MeshRenderer bulletRenderer = bulletDecal.GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(bulletRenderer);
            var probe = new MaterialPropertyBlock();
            bulletRenderer.GetPropertyBlock(probe);
            Color bulletTint = probe.GetColor(ColorId);
            Assert.IsTrue(SurfaceArtRules.TryPalette("Metal", out SurfaceArtRules.Palette metal));
            Assert.Less(bulletTint.grayscale,
                DecalPoolRules.ColorFor(DecalKind.BulletHole, metal.baseColor).grayscale + 1e-4f);
            Assert.Less(bulletTint.grayscale, metal.baseColor.grayscale, "bullet hole darkens metal");

            int chipIdx = pool.Place(DecalKind.Chip, Vector3.zero, Vector3.up, "Concrete");
            Assert.IsTrue(pool.TryGetDecalTransform(chipIdx, out Transform chipDecal));
            var chipProbe = new MaterialPropertyBlock();
            chipDecal.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(chipProbe);
            Assert.Greater(chipProbe.GetColor(ColorId).grayscale, 0.5f, "chip lightens concrete");
        }
        finally
        {
            Object.DestroyImmediate(pool.gameObject);
        }
    }

    [Test]
    public void Pool_ToleratesUnknownSurfaceKinds()
    {
        SurfaceDecalPool pool = CreateInactivePool(QualityLevel.Medium);
        try
        {
            Assert.GreaterOrEqual(pool.Place(DecalKind.BloodSplat, Vector3.zero, Vector3.up, null), 0);
            Assert.GreaterOrEqual(pool.Place(DecalKind.BloodSplat, Vector3.zero, Vector3.up, ""), 0);
            Assert.GreaterOrEqual(pool.Place(DecalKind.Scorch, Vector3.zero, Vector3.up, "Unobtanium"), 0);
            Assert.AreEqual(3, pool.ActiveCount);

            int idx = pool.Place(DecalKind.BulletHole, Vector3.zero, Vector3.zero, "Concrete");
            Assert.GreaterOrEqual(idx, 0, "degenerate normal falls back to up");
            Assert.IsTrue(pool.TryGetDecalTransform(idx, out Transform t));
            Assert.Greater(Vector3.Dot(t.forward, Vector3.up), 0.99f);
        }
        finally
        {
            Object.DestroyImmediate(pool.gameObject);
        }
    }
}
