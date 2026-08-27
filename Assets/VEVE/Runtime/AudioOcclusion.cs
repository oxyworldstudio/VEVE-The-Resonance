using UnityEngine;

namespace VEVE
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioOcclusion : MonoBehaviour
    {
        [SerializeField] private Transform listener;
        [SerializeField, Range(0f, 1f)] private float occludedVolume = 0.25f;
        private AudioSource source;

        private void Awake() => source = GetComponent<AudioSource>();

        private void Update()
        {
            if (listener == null || source == null) return;
            bool blocked = Physics.Linecast(transform.position, listener.position, out RaycastHit hit) &&
                hit.transform != listener && hit.transform.GetComponent<SmokeVolume>() == null;
            source.volume = blocked ? occludedVolume : 1f;
        }
    }
}
