using UnityEngine;
using System.Collections.Generic;

namespace VEVE.Procedural
{
    /// <summary>
    /// Comprehensive tile type enumeration supporting indoor, outdoor, and vertical traversal elements.
    /// </summary>
    public enum TileType { Empty, Floor, Wall, CoverLow, CoverHigh, Door, Window, StairsUp, StairsDown, Roof, Ground, Water, Bridge, Ladder, Vent, Debris }

    /// <summary>
    /// Data associated with a single tile in the procedural grid.
    /// </summary>
    [System.Serializable]
    public struct TileData
    {
        /// <summary>
        /// Classification of the tile.
        /// </summary>
        public TileType type;

        /// <summary>
        /// Grid position of the tile.
        /// </summary>
        public Vector3Int position;

        /// <summary>
        /// Indicates whether the tile can be traversed.
        /// </summary>
        public bool walkable;

        /// <summary>
        /// Indicates whether the tile provides ballistic cover.
        /// </summary>
        public bool providesCover;

        /// <summary>
        /// Material integrity from 0 to 1, affecting destructibility.
        /// </summary>
        public float materialIntegrity;

        /// <summary>
        /// Vertical floor level this tile belongs to.
        /// </summary>
        public int floorLevel;

        /// <summary>
        /// Procedural noise value used for terrain variation.
        /// </summary>
        public float noiseValue;

        /// <summary>
        /// Indicates whether this tile forms the boundary edge of a room or outdoor area.
        /// </summary>
        public bool isEdge;
    }

    /// <summary>
    /// Represents a procedurally generated room with tactical metadata.
    /// </summary>
    [System.Serializable]
    public struct Room
    {
        /// <summary>
        /// Bounds of the room in grid coordinates.
        /// </summary>
        public RectInt bounds;

        /// <summary>
        /// Human-readable room type classification.
        /// </summary>
        public string roomType;

        /// <summary>
        /// Center position of the room.
        /// </summary>
        public Vector2Int center;

        /// <summary>
        /// Vertical floor level of the room.
        /// </summary>
        public int floorLevel;

        /// <summary>
        /// Heuristic value indicating tactical importance.
        /// </summary>
        public float tacticalValue;

        /// <summary>
        /// Indicates whether the room has windows.
        /// </summary>
        public bool hasWindows;

        /// <summary>
        /// Indicates whether the room has doors.
        /// </summary>
        public bool hasDoors;

        /// <summary>
        /// Precomputed cover positions within the room.
        /// </summary>
        public List<Vector2Int> coverPositions;

        /// <summary>
        /// Precomputed valid spawn positions within the room.
        /// </summary>
        public List<Vector2Int> spawnPositions;
    }

    /// <summary>
    /// Functional purpose assigned to a room during generation.
    /// </summary>
    public enum RoomPurpose { Living, Office, Storage, Entry, Utility, Lobby, Corridor, Stairwell, Armory, ServerRoom, Laboratory, Hangar }

    /// <summary>
    /// Configuration template for a specific room purpose.
    /// </summary>
    [System.Serializable]
    public struct RoomConfiguration
    {
        /// <summary>
        /// Functional purpose of the room.
        /// </summary>
        public RoomPurpose purpose;

        /// <summary>
        /// Density of cover placement within the room.
        /// </summary>
        public float coverDensity;

        /// <summary>
        /// Weight used when selecting room purposes during generation.
        /// </summary>
        public float spawnWeight;

        /// <summary>
        /// Minimum room size in tiles.
        /// </summary>
        public int minSize;

        /// <summary>
        /// Maximum room size in tiles.
        /// </summary>
        public int maxSize;

        /// <summary>
        /// Indicates whether windows are required for this room type.
        /// </summary>
        public bool requireWindows;

        /// <summary>
        /// Indicates whether doors are required for this room type.
        /// </summary>
        public bool requireDoors;

        /// <summary>
        /// Allowed tile types for this room configuration.
        /// </summary>
        public List<TileType> allowedTileTypes;
    }

    /// <summary>
    /// Geometric shape of a building footprint.
    /// </summary>
    public enum BuildingShape { Rectangle, LShape, TShape, UShape, Linear }

    /// <summary>
    /// Configuration for a multi-floor building.
    /// </summary>
    [System.Serializable]
    public struct BuildingConfig
    {
        /// <summary>
        /// Unique identifier for the building.
        /// </summary>
        public string buildingId;

        /// <summary>
        /// Origin point of the building footprint on the ground plane.
        /// </summary>
        public Vector2Int footprintOrigin;

        /// <summary>
        /// Number of floors above ground.
        /// </summary>
        public int floors;

        /// <summary>
        /// Height of each floor in grid units.
        /// </summary>
        public int floorHeight;

        /// <summary>
        /// Shape of the building footprint.
        /// </summary>
        public BuildingShape shape;

        /// <summary>
        /// Room templates to sample from for this building.
        /// </summary>
        public List<RoomConfiguration> roomTemplates;

        /// <summary>
        /// Indicates whether a roof should be generated.
        /// </summary>
        public bool generateRoof;

        /// <summary>
        /// Indicates whether a basement level should be generated.
        /// </summary>
        public bool generateBasement;

        /// <summary>
        /// Heuristic tactical priority for this building.
        /// </summary>
        public float tacticalPriority;
    }

    /// <summary>
    /// Classification of an outdoor procedural area.
    /// </summary>
    public enum OutdoorType { Courtyard, Street, Park, OpenField, Rooftop, ParkingLot }

    /// <summary>
    /// Configuration for an outdoor procedural area.
    /// </summary>
    [System.Serializable]
    public struct OutdoorAreaConfig
    {
        /// <summary>
        /// Classification of the outdoor area.
        /// </summary>
        public OutdoorType type;

        /// <summary>
        /// Bounds of the outdoor area on the ground plane.
        /// </summary>
        public RectInt bounds;

        /// <summary>
        /// Density of natural and artificial cover.
        /// </summary>
        public float coverDensity;

        /// <summary>
        /// Indicates whether vehicles are placed in the area.
        /// </summary>
        public bool hasVehicles;

        /// <summary>
        /// Indicates whether debris is scattered in the area.
        /// </summary>
        public bool hasDebris;

        /// <summary>
        /// Base visibility multiplier for the area.
        /// </summary>
        public float visibility;

        /// <summary>
        /// Heuristic tactical priority for the area.
        /// </summary>
        public float tacticalPriority;
    }

    /// <summary>
    /// A tactically significant position identified during map generation.
    /// </summary>
    [System.Serializable]
    public struct TacticalPosition
    {
        /// <summary>
        /// World position of the tactical point.
        /// </summary>
        public Vector3Int position;

        /// <summary>
        /// Classification of the tactical position.
        /// </summary>
        public TacticalPositionType type;

        /// <summary>
        /// Estimated threat level from this position.
        /// </summary>
        public float threatLevel;

        /// <summary>
        /// Estimated protection level offered by this position.
        /// </summary>
        public float protectionLevel;

        /// <summary>
        /// Visibility score from this position.
        /// </summary>
        public float visibilityScore;

        /// <summary>
        /// Adjacent cover positions within line of sight.
        /// </summary>
        public List<Vector3Int> adjacentCover;
    }

    /// <summary>
    /// Tactical classification of a significant map position.
    /// </summary>
    public enum TacticalPositionType { SniperNest, AmbushPoint, Overwatch, RallyPoint, BreachPoint, SuppressiveFirePosition, FlankingRoute }

    /// <summary>
    /// Configuration for procedural map generation.
    /// </summary>
    [System.Serializable]
    public struct MapGenerationSettings
    {
        /// <summary>
        /// Width of the map in grid units.
        /// </summary>
        public int mapWidth;

        /// <summary>
        /// Height of the map in grid units.
        /// </summary>
        public int mapHeight;

        /// <summary>
        /// Minimum room size in tiles.
        /// </summary>
        public int minRoomSize;

        /// <summary>
        /// Maximum room size in tiles.
        /// </summary>
        public int maxRoomSize;

        /// <summary>
        /// Maximum number of rooms to attempt to place.
        /// </summary>
        public int maxRooms;

        /// <summary>
        /// Width of corridors in tiles.
        /// </summary>
        public int corridorWidth;

        /// <summary>
        /// Density of cover placement from 0 to 1.
        /// </summary>
        public float coverDensity;

        /// <summary>
        /// Indicates whether windows should be generated.
        /// </summary>
        public bool enableWindows;

        /// <summary>
        /// Indicates whether doors should be generated.
        /// </summary>
        public bool enableDoors;

        /// <summary>
        /// Seed for random generation. Zero uses a random seed.
        /// </summary>
        public int seed;

        /// <summary>
        /// Returns default generation settings.
        /// </summary>
        /// <returns>Default MapGenerationSettings.</returns>
        public static MapGenerationSettings Default()
        {
            return new MapGenerationSettings
            {
                mapWidth = 64,
                mapHeight = 64,
                minRoomSize = 6,
                maxRoomSize = 14,
                maxRooms = 12,
                corridorWidth = 2,
                coverDensity = 0.3f,
                enableWindows = true,
                enableDoors = true,
                seed = 0
            };
        }
    }
}
