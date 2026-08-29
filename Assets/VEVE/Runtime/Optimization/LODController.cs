using UnityEngine;
using System.Collections.Generic;

namespace VEVE.Optimization
{
    public enum LODLevel { LOD0, LOD1, LOD2, LOD3, LOD4, Culled }

    [System.Serializable]
    public struct LODConfiguration
    {
        public LODLevel level;
        public float screenRelativeTransitionHeight;
        public int trianglePercentage;
        public float fadeTransitionWidth;
        public bool useOcclusionCulling;
    }

    public sealed class LODController : MonoBehaviour
    {
        [SerializeField] private LODConfiguration[] lodLevels = new LODConfiguration[5];
        [SerializeField] private float cullDistance = 500f;
        [SerializeField] private bool enableImpostors = true;
        [SerializeField] private float impostorDistance = 200f;

        private Renderer[] renderers;
        private LODGroup lodGroup;
        private int currentLOD;

        private void Start()
        {
            renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            lodGroup = gameObject.AddComponent<LODGroup>();
            var lods = new LOD[lodLevels.Length];

            for (int i = 0; i < lodLevels.Length; i++)
            {
                var rendererList = new List<Renderer>();
                if (i == 0)
                {
                    rendererList.AddRange(renderers);
                }
                lods[i] = new LOD(lodLevels[i].screenRelativeTransitionHeight, rendererList.ToArray());
            }

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }

        private void Update()
        {
            // LOD level is managed automatically by Unity's LODGroup based on camera distance
        }
    }
}
