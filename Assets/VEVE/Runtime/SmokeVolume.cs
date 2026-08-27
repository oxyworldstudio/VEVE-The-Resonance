using UnityEngine;

namespace VEVE
{
    public sealed class SmokeVolume : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float density = 0.7f;
        public float Density => density;

        public bool ReducesVisibility(Vector3 observer, Vector3 target)
        {
            return Vector3.Distance(observer, transform.position) < transform.localScale.x &&
                Vector3.Distance(target, transform.position) < transform.localScale.x;
        }
    }
}
