using NUnit.Framework;
using UnityEngine;
using VEVE;
using VEVE.Graphics;

/// <summary>
/// EditMode validation for <see cref="ProceduralSkyController"/> and
/// <see cref="AtmosphereTintBridge"/>: dome mesh geometry (vertex counts, inward normals
/// verified position-vs-normal without raycasts), deterministic star textures, child
/// hierarchy construction, null-safety without an <see cref="EnvironmentSimulation"/>,
/// ForceRefresh stability and the atmosphere palette evaluation. Uses a low-poly dome
/// (16x8) and small textures so the whole suite stays well under 5 seconds.
/// </summary>
public sealed class SkyControllerTests
{
    private Color prevFogColor;
    private float prevFogDensity;
    private FogMode prevFogMode;
    private Color prevAmbient;
    private UnityEngine.Rendering.AmbientMode prevAmbientMode;

    [SetUp]
    public void CleanScene()
    {
        prevFogColor = RenderSettings.fogColor;
        prevFogDensity = RenderSettings.fogDensity;
        prevFogMode = RenderSettings.fogMode;
        prevAmbient = RenderSettings.ambientLight;
        prevAmbientMode = RenderSettings.ambientMode;
        foreach (var sim in Object.FindObjectsByType<EnvironmentSimulation>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(sim.gameObject);
        }

        foreach (var sky in Object.FindObjectsByType<ProceduralSkyController>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(sky.gameObject);
        }

        foreach (var bridge in Object.FindObjectsByType<AtmosphereTintBridge>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(bridge.gameObject);
        }
    }

    [TearDown]
    public void Cleanup()
    {
        RenderSettings.fogColor = prevFogColor;
        RenderSettings.fogDensity = prevFogDensity;
        RenderSettings.fogMode = prevFogMode;
        RenderSettings.ambientLight = prevAmbient;
        RenderSettings.ambientMode = prevAmbientMode;
        CleanScene();
    }

    private static ProceduralSkyController CreateSky()
    {
        var go = new GameObject("SkyRoot");
        return go.AddComponent<ProceduralSkyController>();
    }

    [Test]
    public void DomeMeshHasExpectedVertexCount()
    {
        using (var mesh = new MeshScope(ProceduralSkyController.CreateDomeMesh(16, 8)))
        {
            Assert.AreEqual((16 + 1) * (8 + 1), mesh.Mesh.vertexCount, "UV-sphere: (segments+1)*(rings+1)");
            Assert.AreEqual(16 * 8 * 6, mesh.Mesh.triangles.Length, "16x8 quads, two triangles each");
            Assert.Greater(mesh.Mesh.vertexCount, 0);
        }
    }

    [Test]
    public void DomeMeshNormalsPointInward()
    {
        Mesh mesh = ProceduralSkyController.CreateDomeMesh(16, 8);
        try
        {
            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            Assert.AreEqual(verts.Length, norms.Length);
            int checkedSamples = 0;
            for (int i = 0; i < verts.Length; i += 7)
            {
                Vector3 outward = verts[i].normalized;
                float dot = Vector3.Dot(norms[i], outward);
                Assert.Less(dot, 0f, $"vertex {i}: normal must oppose the outward radial direction");
                checkedSamples++;
            }

            Assert.Greater(checkedSamples, 10, "geometry sampling must cover the dome");
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void DomeMeshTrianglesFaceInterior()
    {
        Mesh mesh = ProceduralSkyController.CreateDomeMesh(16, 8);
        try
        {
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            int sampled = 0;
            for (int t = 0; t < tris.Length; t += 3 * 5)
            {
                Vector3 a = verts[tris[t]];
                Vector3 b = verts[tris[t + 1]];
                Vector3 c = verts[tris[t + 2]];
                Vector3 crossNormal = Vector3.Cross(b - a, c - a);
                if (crossNormal.sqrMagnitude < 1e-12f)
                {
                    continue; // degenerate pole triangles duplicate the pole vertex
                }

                Vector3 outward = (a + b + c) * (1f / 3f);
                // Unity renders clockwise-wound triangles front-facing, so interior
                // visibility requires the right-hand cross normal to point outward
                // while the declared vertex normals point inward.
                Assert.Greater(Vector3.Dot(crossNormal, outward), 0f,
                    "winding must invert the sphere so faces look at the camera inside");
                sampled++;
            }

            Assert.Greater(sampled, 10);
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void DomeMeshIsDeterministic()
    {
        Mesh a = ProceduralSkyController.CreateDomeMesh(16, 8);
        Mesh b = ProceduralSkyController.CreateDomeMesh(16, 8);
        try
        {
            Assert.AreEqual(a.vertices, b.vertices);
            Assert.AreEqual(a.triangles, b.triangles);
            Assert.AreEqual(a.normals, b.normals);
        }
        finally
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }
    }

    [Test]
    public void StarTextureIsDeterministicAndOpaqueSomewhere()
    {
        Texture2D a = ProceduralSkyController.CreateStarTexture(64, 42u, 40);
        Texture2D b = ProceduralSkyController.CreateStarTexture(64, 42u, 40);
        Texture2D c = ProceduralSkyController.CreateStarTexture(64, 43u, 40);
        try
        {
            Color32[] pa = a.GetPixels32();
            Color32[] pb = b.GetPixels32();
            Color32[] pc = c.GetPixels32();
            Assert.AreEqual(pa.Length, pb.Length);
            int litA = 0;
            for (int i = 0; i < pa.Length; i++)
            {
                Assert.AreEqual(pa[i].a, pb[i].a, "identical seeds must produce identical pixels");
                Assert.AreEqual(pa[i].r, pb[i].r);
                if (pa[i].a > 0) litA++;
            }

            Assert.Greater(litA, 0, "the star field must contain stars");
            Assert.AreNotEqual(pa, pc, "different seeds must decorrelate the star field");
        }
        finally
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(c);
        }
    }

    [Test]
    public void ControllerBuildsExpectedChildren()
    {
        ProceduralSkyController sky = CreateSky();
        try
        {
            sky.EnsureBuilt();
            Assert.AreEqual(4, sky.transform.childCount, "SkyDome + StarDome + SunBillboard + MoonBillboard");
            bool foundDome = false;
            bool foundStars = false;
            bool foundSun = false;
            bool foundMoon = false;
            foreach (Transform child in sky.transform)
            {
                if (child.name == "SkyDome") foundDome = true;
                if (child.name == "StarDome") foundStars = true;
                if (child.name == "SunBillboard") foundSun = true;
                if (child.name == "MoonBillboard") foundMoon = true;
                Assert.NotNull(child.GetComponent<MeshFilter>(), child.name + " needs geometry");
                Assert.NotNull(child.GetComponent<MeshRenderer>(), child.name + " needs a renderer");
            }

            Assert.IsTrue(foundDome && foundStars && foundSun && foundMoon, "all four sky children must exist");
        }
        finally
        {
            Object.DestroyImmediate(sky.gameObject);
        }
    }

    [Test]
    public void DomeRendersWithGeneratedGradientTexture()
    {
        ProceduralSkyController sky = CreateSky();
        try
        {
            sky.ForceRefresh();
            Transform dome = sky.transform.Find("SkyDome");
            Assert.NotNull(dome);
            var renderer = dome.GetComponent<MeshRenderer>();
            Assert.NotNull(renderer.sharedMaterial, "dome material must exist");
            Assert.NotNull(renderer.sharedMaterial.mainTexture, "dome material must use the generated gradient texture");
            var filter = dome.GetComponent<MeshFilter>();
            Assert.AreEqual((16 + 1) * (8 + 1), filter.sharedMesh.vertexCount);
            Assert.Greater(dome.localScale.x, 0f, "dome must be scaled to the dome radius");
        }
        finally
        {
            Object.DestroyImmediate(sky.gameObject);
        }
    }

    [Test]
    public void NullSafeWithoutEnvironmentSimulation()
    {
        ProceduralSkyController sky = CreateSky();
        try
        {
            Assert.IsNull(Object.FindFirstObjectByType<EnvironmentSimulation>(), "test requires a sim-less scene");
            Assert.DoesNotThrow(sky.ForceRefresh);
            SkyControllerState state = sky.GetState();
            Assert.AreEqual(ProceduralSkyController.NeutralHour, state.hourOfDay, 0.001f, "neutral noon baseline");
            Assert.AreEqual(ProceduralSkyController.NeutralSunElevationDeg, state.sunElevationDeg, 0.001f);
            Assert.AreEqual(ProceduralSkyController.NeutralSunAzimuthDeg, state.sunAzimuthDeg, 0.001f);
            Transform sun = sky.transform.Find("SunBillboard");
            Assert.NotNull(sun);
            Assert.Greater(sun.localPosition.y, 0f, "baseline sun sits above the horizon");
        }
        finally
        {
            Object.DestroyImmediate(sky.gameObject);
        }
    }

    [Test]
    public void ControllerReadsEnvironmentSimulation()
    {
        ProceduralSkyController sky = CreateSky();
        var simGo = new GameObject("Sim");
        var sim = simGo.AddComponent<EnvironmentSimulation>();
        try
        {
            sky.ForceRefresh();
            SkyControllerState state = sky.GetState();
            Assert.IsNotNull(sky.Simulation, "controller must cache the simulation");
            // EditMode never runs Update, so the sim reports its untouched defaults;
            // the controller must mirror exactly those sanitized values.
            Assert.AreEqual(Mathf.Repeat(sim.CurrentHour, 24f), state.hourOfDay, 0.01f);
            Assert.AreEqual(Mathf.Clamp(sim.SunElevation, -90f, 90f), state.sunElevationDeg, 0.01f);
            Transform sun = sky.transform.Find("SunBillboard");
            Assert.NotNull(sun);
            Assert.AreEqual(sky.DomeRadius * 0.985f, sun.localPosition.magnitude, 0.01f,
                "sun billboard always orbits on the dome sphere");
            sky.SetDomeRadius(120f);
            sky.ForceRefresh();
            Assert.AreEqual(120f * 0.985f, sun.localPosition.magnitude, 0.01f,
                "orbit follows the dome radius");
        }
        finally
        {
            Object.DestroyImmediate(sky.gameObject);
            Object.DestroyImmediate(simGo);
        }
    }

    [Test]
    public void SetDomeRadiusRescalesDomeChildren()
    {
        ProceduralSkyController sky = CreateSky();
        try
        {
            sky.ForceRefresh();
            sky.SetDomeRadius(500f);
            Assert.AreEqual(500f, sky.DomeRadius, 0.001f);
            Transform dome = sky.transform.Find("SkyDome");
            Transform stars = sky.transform.Find("StarDome");
            Assert.AreEqual(500f, dome.localScale.x, 0.001f);
            Assert.Greater(stars.localScale.x, dome.localScale.x, "star dome sits just outside the sky dome");
        }
        finally
        {
            Object.DestroyImmediate(sky.gameObject);
        }
    }

    [Test]
    public void SetDomeRadiusRejectsDegenerateValues()
    {
        ProceduralSkyController sky = CreateSky();
        try
        {
            sky.SetDomeRadius(float.NaN);
            Assert.GreaterOrEqual(sky.DomeRadius, 5f, "NaN radius clamps to the minimum");
            sky.SetDomeRadius(-50f);
            Assert.GreaterOrEqual(sky.DomeRadius, 5f);
        }
        finally
        {
            Object.DestroyImmediate(sky.gameObject);
        }
    }

    [Test]
    public void ForceRefreshRepeatedDoesNotThrowOrLeakChildren()
    {
        ProceduralSkyController sky = CreateSky();
        try
        {
            for (int i = 0; i < 5; i++)
            {
                Assert.DoesNotThrow(sky.ForceRefresh);
            }

            Assert.AreEqual(4, sky.transform.childCount, "EnsureBuilt must stay idempotent");
        }
        finally
        {
            Object.DestroyImmediate(sky.gameObject);
        }
    }

    [Test]
    public void TickIsSafeWithoutSimulation()
    {
        ProceduralSkyController sky = CreateSky();
        try
        {
            Assert.DoesNotThrow(() => sky.Tick(0f));
            Assert.DoesNotThrow(() => sky.Tick(1f / 60f));
        }
        finally
        {
            Object.DestroyImmediate(sky.gameObject);
        }
    }

    [Test]
    public void RefreshIntervalConstantsMatchSpec()
    {
        Assert.AreEqual(0.5f, ProceduralSkyController.SimLookupIntervalSeconds, 0.0001f, "sim cache 0.5s");
        Assert.AreEqual(0.25f, ProceduralSkyController.GradientIntervalSeconds, 0.0001f, "gradient repaint max 4x/sec");
        Assert.AreEqual(1f, AtmosphereTintBridge.LookupIntervalSeconds, 0.0001f, "bridge cache 1s");
    }

    [Test]
    public void DirectionConversionMatchesSimulationConvention()
    {
        Vector3 d = ProceduralSkyController.DirFromAngles(30f, 90f);
        Assert.AreEqual(1f, d.magnitude, 0.0001f);
        Assert.AreEqual(0.5f, d.y, 0.001f, "elevation 30 gives sin(30) up component");
        Assert.AreEqual(0f, d.z, 0.001f, "azimuth 90 has no +Z component");
        Vector3 east = ProceduralSkyController.DirFromAngles(0f, 90f);
        Assert.Greater(east.x, 0.99f, "azimuth 90 at the horizon points along +X");
        Vector3 horizon = ProceduralSkyController.DirFromAngles(0f, 0f);
        Assert.AreEqual(0f, horizon.y, 0.0001f);
        Assert.Greater(horizon.z, 0.99f, "azimuth 0 points along +Z");
    }

    [Test]
    public void BridgePaletteEvaluationRespectsBiomeBias()
    {
        var go = new GameObject("Bridge");
        var bridge = go.AddComponent<AtmosphereTintBridge>();
        try
        {
            bridge.BiomeFogBiasOverride = 0.0f;
            bridge.ForceRefresh();
            Color clearFog;
            Color clearAmbient;
            float clearDensity;
            bridge.EvaluatePalette(out clearFog, out clearAmbient, out clearDensity);

            bridge.BiomeFogBiasOverride = 1.0f;
            bridge.ForceRefresh();
            Color dustyFog;
            Color dustyAmbient;
            float dustyDensity;
            bridge.EvaluatePalette(out dustyFog, out dustyAmbient, out dustyDensity);

            Assert.Greater(dustyDensity, clearDensity, "fog density must grow with biome bias");
            Assert.GreaterOrEqual(clearDensity, 0f);
            Assert.LessOrEqual(dustyDensity, 0.2f, "density stays within atmosphere limits");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BridgeIsNullOrSafeInEmptyScene()
    {
        var go = new GameObject("Bridge");
        var bridge = go.AddComponent<AtmosphereTintBridge>();
        try
        {
            Assert.IsNull(Object.FindFirstObjectByType<EnvironmentSimulation>());
            Assert.DoesNotThrow(bridge.ForceRefresh);
            Assert.AreEqual(AtmosphereTintBridge.NeutralHour, bridge.Hour, 0.001f);
            Assert.AreEqual(AtmosphereTintBridge.NeutralHumidity, bridge.Humidity, 0.001f);
            Assert.DoesNotThrow(() => bridge.Tick(0.016f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BridgeReadsSimulationAndBiomeHolder()
    {
        var bridgeGo = new GameObject("Bridge");
        var bridge = bridgeGo.AddComponent<AtmosphereTintBridge>();
        var simGo = new GameObject("Sim");
        var sim = simGo.AddComponent<EnvironmentSimulation>();
        var biomeGo = new GameObject("Biome");
        var holder = biomeGo.AddComponent<BiomeProfileHolder>();
        holder.BiomeKey = "DESERT_CHECKPOINT";
        try
        {
            sim.Humidity = 0.2f;
            bridge.ForceRefresh();
            Assert.AreEqual(0.2f, bridge.Humidity, 0.001f, "bridge consumes sim humidity");
            Color fog;
            Color ambient;
            float density;
            bridge.EvaluatePalette(out fog, out ambient, out density);
            Assert.Greater(density, 0.004f, "desert bias (0.55) raises fog density above neutral");
        }
        finally
        {
            Object.DestroyImmediate(bridgeGo);
            Object.DestroyImmediate(simGo);
            Object.DestroyImmediate(biomeGo);
        }
    }

    private readonly struct MeshScope : System.IDisposable
    {
        public readonly Mesh Mesh;

        public MeshScope(Mesh mesh)
        {
            Mesh = mesh;
        }

        public void Dispose()
        {
            if (Mesh != null) Object.DestroyImmediate(Mesh);
        }
    }
}
