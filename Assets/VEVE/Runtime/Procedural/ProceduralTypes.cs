using UnityEngine;
using System.Collections.Generic;

namespace VEVE.Procedural
{
    public enum TileType { Empty, Floor, Wall, Cover, Door, Window, Stairs }

    [System.Serializable]
    public struct TileData
    {
        public TileType type;
        public Vector3Int position;
        public bool walkable;
        public bool providesCover;
        public float materialIntegrity;
    }

    [System.Serializable]
    public struct Room
    {
        public RectInt bounds;
        public string roomType;
        public Vector2Int center;
    }
}
