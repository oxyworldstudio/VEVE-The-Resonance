using NUnit.Framework;

namespace VEVE.Graphics.Tests
{
    public sealed class PipelineCompatTests
    {
        [Test]
        public void ShaderNamesMatchActiveFamily()
        {
            Assert.AreEqual(PipelineCompat.BuiltInStandard, PipelineCompat.ShaderNameFor("Built-in"));
            Assert.AreEqual(PipelineCompat.UniversalLit, PipelineCompat.ShaderNameFor("Universal"));
            Assert.AreEqual(PipelineCompat.HighDefinitionLit, PipelineCompat.ShaderNameFor("HDRP"));
            Assert.AreEqual(PipelineCompat.BuiltInStandard, PipelineCompat.ShaderNameFor("Whatever"),
                "unknown family safely falls back to built-in Standard");
        }

        [Test]
        public void ActiveResolutionIsStableAndNeverThrows()
        {
            string family = PipelineCompat.ActivePipelineFamily();
            Assert.IsNotEmpty(family);
            Assert.IsNotNull(PipelineCompat.ShaderNameFor(family));
            // Built-in default in the test realm unless a URP asset got assigned.
            Assert.IsTrue(PipelineCompat.IsUniversal == (family == "Universal"));
        }

        [Test]
        public void ApplySurfaceGivesNoErrorsOnBuiltInStandard()
        {
            UnityEngine.Shader std = UnityEngine.Shader.Find(PipelineCompat.BuiltInStandard);
            if (std == null) { Assert.Ignore("no shader in headless"); return; }
            var m = new UnityEngine.Material(std);
            PipelineCompat.ApplySurface(m, new UnityEngine.Color(0.4f, 0.5f, 0.6f), 0.7f);
            Assert.AreEqual(0.7f, m.GetFloat("_Glossiness"), 1e-4f);
            UnityEngine.Object.DestroyImmediate(m);
        }
    }
}
