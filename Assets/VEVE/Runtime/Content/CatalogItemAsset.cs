using UnityEngine;

namespace VEVE.Content
{
    /// <summary>Category of exported catalog payload.</summary>
    public enum CatalogItemKind { Mission = 0, Weapon = 1, Gear = 2, Scope = 3 }

    /// <summary>
    /// Designer-facing serialized catalog entry produced by the C7 pipeline
    /// (Assets/VEVE/Resources/Generated). Payload uses <see cref="PayloadCodec"/>,
    /// is human-readable in the inspector, and is parsed by the matching codec.
    /// </summary>
    [CreateAssetMenu(fileName = "CatalogItem", menuName = "VEVE/Content/Catalog Item")]
    public sealed class CatalogItemAsset : ScriptableObject
    {
        [SerializeField] private CatalogItemKind kind = CatalogItemKind.Mission;
        [SerializeField] private string id;
        [TextArea(4, 12)]
        [SerializeField] private string payload;

        public CatalogItemKind Kind => kind;
        public string Id => id;
        public string Payload => payload;

        public void Configure(CatalogItemKind k, string identifier, string encodedPayload)
        {
            kind = k;
            id = identifier;
            payload = encodedPayload;
        }

        public MissionTemplate AsMission()
        {
            return kind == CatalogItemKind.Mission ? MissionPayloadCodec.Decode(payload) : default;
        }
    }
}
