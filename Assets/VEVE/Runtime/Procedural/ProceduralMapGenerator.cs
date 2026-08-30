using UnityEngine;
using System.Collections.Generic;

namespace VEVE.Procedural
{
    /// <summary>
    /// Advanced procedural map generator supporting multi-floor buildings, outdoor areas, and tactical positioning analysis.
    /// </summary>
    public class ProceduralMapGenerator
    {
        private TileData[,,] tiles;
        private List<Room> rooms;
        private List<BuildingConfig> buildings;
        private List<OutdoorAreaConfig> outdoorAreas;
        private List<TacticalPosition> tacticalPositions;
        private System.Random rng;

        /// <summary>
        /// Width of the map in grid units.
        /// </summary>
        public int MapWidth { get; private set; }

        /// <summary>
        /// Height of the map in grid units.
        /// </summary>
        public int MapHeight { get; private set; }

        /// <summary>
        /// Number of vertical floors generated.
        /// </summary>
        public int FloorCount { get; private set; }

        /// <summary>
        /// Generates a complete multi-floor map with buildings and outdoor areas.
        /// </summary>
        /// <param name="settings">Generation parameters.</param>
        /// <returns>Three-dimensional tile array [x, y, floor].</returns>
        public TileData[,,] Generate(MapGenerationSettings settings)
        {
            return Generate(settings, new List<BuildingConfig>(), new List<OutdoorAreaConfig>());
        }

        /// <summary>
        /// Generates a complete multi-floor map with specified buildings and outdoor areas.
        /// </summary>
        /// <param name="settings">Generation parameters.</param>
        /// <param name="buildingConfigs">Predefined building configurations.</param>
        /// <param name="outdoorConfigs">Predefined outdoor area configurations.</param>
        /// <returns>Three-dimensional tile array [x, y, floor].</returns>
        public TileData[,,] Generate(MapGenerationSettings settings, List<BuildingConfig> buildingConfigs, List<OutdoorAreaConfig> outdoorConfigs)
        {
            if (settings.seed != 0)
                rng = new System.Random(settings.seed);
            else
                rng = new System.Random();

            MapWidth = settings.mapWidth;
            MapHeight = settings.mapHeight;
            FloorCount = 3;

            tiles = new TileData[MapWidth, MapHeight, FloorCount];
            rooms = new List<Room>();
            buildings = buildingConfigs != null ? new List<BuildingConfig>(buildingConfigs) : new List<BuildingConfig>();
            outdoorAreas = outdoorConfigs != null ? new List<OutdoorAreaConfig>(outdoorConfigs) : new List<OutdoorAreaConfig>();
            tacticalPositions = new List<TacticalPosition>();

            InitializeTiles(settings);
            GenerateOutdoorAreas(settings);
            GenerateBuildings(settings);
            ConnectOutdoorToBuildings(settings);
            PlaceStairsAndVerticalAccess(settings);
            PlaceWallsAndEdges(settings);
            AnalyzeTacticalPositions(settings);

            return tiles;
        }

        /// <summary>
        /// Returns all generated rooms across all floors.
        /// </summary>
        /// <returns>List of generated rooms.</returns>
        public List<Room> GetRooms()
        {
            return new List<Room>(rooms);
        }

        /// <summary>
        /// Returns all generated buildings.
        /// </summary>
        /// <returns>List of building configurations.</returns>
        public List<BuildingConfig> GetBuildings()
        {
            return new List<BuildingConfig>(buildings);
        }

        /// <summary>
        /// Returns all generated outdoor areas.
        /// </summary>
        /// <returns>List of outdoor area configurations.</returns>
        public List<OutdoorAreaConfig> GetOutdoorAreas()
        {
            return new List<OutdoorAreaConfig>(outdoorAreas);
        }

        /// <summary>
        /// Returns all identified tactical positions.
        /// </summary>
        /// <returns>List of tactical positions.</returns>
        public List<TacticalPosition> GetTacticalPositions()
        {
            return new List<TacticalPosition>(tacticalPositions);
        }

        /// <summary>
        /// Returns valid spawn points distributed across floors and rooms.
        /// </summary>
        /// <returns>List of spawn positions.</returns>
        public List<Vector3Int> GetSpawnPoints()
        {
            var spawns = new List<Vector3Int>();
            foreach (var room in rooms)
            {
                if (room.spawnPositions != null)
                {
                    foreach (var pos in room.spawnPositions)
                    {
                        spawns.Add(new Vector3Int(pos.x, pos.y, room.floorLevel));
                    }
                }
                else
                {
                    spawns.Add(new Vector3Int(room.center.x, room.center.y, room.floorLevel));
                }
            }
            return spawns;
        }

        /// <summary>
        /// Returns cover positions across all floors.
        /// </summary>
        /// <returns>List of cover positions.</returns>
        public List<Vector3Int> GetCoverPositions()
        {
            var cover = new List<Vector3Int>();
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    for (int z = 0; z < FloorCount; z++)
                    {
                        if (tiles[x, y, z].providesCover)
                        {
                            cover.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }
            return cover;
        }

        /// <summary>
        /// Returns stair positions connecting floors.
        /// </summary>
        /// <returns>List of stair positions.</returns>
        public List<Vector3Int> GetStairPositions()
        {
            var stairs = new List<Vector3Int>();
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    for (int z = 0; z < FloorCount; z++)
                    {
                        if (tiles[x, y, z].type == TileType.StairsUp || tiles[x, y, z].type == TileType.StairsDown)
                        {
                            stairs.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }
            return stairs;
        }

        private void InitializeTiles(MapGenerationSettings settings)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    for (int z = 0; z < FloorCount; z++)
                    {
                        tiles[x, y, z] = new TileData
                        {
                            type = TileType.Empty,
                            position = new Vector3Int(x, y, z),
                            walkable = false,
                            providesCover = false,
                            materialIntegrity = 0f,
                            floorLevel = z,
                            noiseValue = (float)rng.NextDouble(),
                            isEdge = false
                        };
                    }
                }
            }
        }

        private void GenerateOutdoorAreas(MapGenerationSettings settings)
        {
            if (outdoorAreas.Count == 0)
            {
                GenerateDefaultOutdoorAreas(settings);
                return;
            }

            foreach (var outdoor in outdoorAreas)
            {
                CarveOutdoorArea(outdoor, settings);
            }
        }

        private void GenerateDefaultOutdoorAreas(MapGenerationSettings settings)
        {
            int courtyardSize = Mathf.Max(8, settings.mapWidth / 6);
            var courtyard = new OutdoorAreaConfig
            {
                type = OutdoorType.Courtyard,
                bounds = new RectInt(settings.mapWidth / 2 - courtyardSize / 2, settings.mapHeight / 2 - courtyardSize / 2, courtyardSize, courtyardSize),
                coverDensity = settings.coverDensity * 0.5f,
                hasVehicles = rng.NextDouble() < 0.3f,
                hasDebris = true,
                visibility = 0.8f,
                tacticalPriority = 0.6f
            };
            outdoorAreas.Add(courtyard);
            CarveOutdoorArea(courtyard, settings);
        }

        private void CarveOutdoorArea(OutdoorAreaConfig outdoor, MapGenerationSettings settings)
        {
            for (int x = outdoor.bounds.xMin; x < outdoor.bounds.xMax; x++)
            {
                for (int y = outdoor.bounds.yMin; y < outdoor.bounds.yMax; y++)
                {
                    if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight)
                    {
                        var tile = tiles[x, y, 0];
                        tile.type = TileType.Ground;
                        tile.walkable = true;
                        tile.materialIntegrity = 0.4f;
                        tile.noiseValue = (float)rng.NextDouble();
                        tiles[x, y, 0] = tile;
                    }
                }
            }

            if (outdoor.hasDebris)
            {
                AddOutdoorDebris(outdoor, settings);
            }

            if (outdoor.hasVehicles)
            {
                AddOutdoorVehicles(outdoor, settings);
            }

            AddOutdoorCover(outdoor, settings);
        }

        private void AddOutdoorDebris(OutdoorAreaConfig outdoor, MapGenerationSettings settings)
        {
            int debrisCount = (int)(outdoor.bounds.width * outdoor.bounds.height * 0.05f);
            for (int i = 0; i < debrisCount; i++)
            {
                int x = outdoor.bounds.xMin + rng.Next(0, outdoor.bounds.width);
                int y = outdoor.bounds.yMin + rng.Next(0, outdoor.bounds.height);
                if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight && tiles[x, y, 0].type == TileType.Ground)
                {
                    var tile = tiles[x, y, 0];
                    tile.type = TileType.Debris;
                    tile.providesCover = true;
                    tile.materialIntegrity = 0.3f;
                    tiles[x, y, 0] = tile;
                }
            }
        }

        private void AddOutdoorVehicles(OutdoorAreaConfig outdoor, MapGenerationSettings settings)
        {
            int vehicleCount = rng.Next(1, 3);
            for (int i = 0; i < vehicleCount; i++)
            {
                int x = outdoor.bounds.xMin + rng.Next(1, outdoor.bounds.width - 1);
                int y = outdoor.bounds.yMin + rng.Next(1, outdoor.bounds.height - 1);
                if (x >= 0 && x < MapWidth - 1 && y >= 0 && y < MapHeight - 1)
                {
                    var tile = tiles[x, y, 0];
                    tile.type = TileType.CoverHigh;
                    tile.providesCover = true;
                    tile.materialIntegrity = 1.5f;
                    tiles[x, y, 0] = tile;

                    if (x + 1 < MapWidth)
                    {
                        var neighbor = tiles[x + 1, y, 0];
                        neighbor.type = TileType.CoverHigh;
                        neighbor.providesCover = true;
                        neighbor.materialIntegrity = 1.5f;
                        tiles[x + 1, y, 0] = neighbor;
                    }
                }
            }
        }

        private void AddOutdoorCover(OutdoorAreaConfig outdoor, MapGenerationSettings settings)
        {
            for (int x = outdoor.bounds.xMin; x < outdoor.bounds.xMax; x++)
            {
                for (int y = outdoor.bounds.yMin; y < outdoor.bounds.yMax; y++)
                {
                    if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight && tiles[x, y, 0].type == TileType.Ground)
                    {
                        if (rng.NextDouble() < outdoor.coverDensity)
                        {
                            var tile = tiles[x, y, 0];
                            tile.type = rng.NextDouble() < 0.5f ? TileType.CoverLow : TileType.CoverHigh;
                            tile.providesCover = true;
                            tile.materialIntegrity = 0.6f;
                            tiles[x, y, 0] = tile;
                        }
                    }
                }
            }
        }

        private void GenerateBuildings(MapGenerationSettings settings)
        {
            if (buildings.Count == 0)
            {
                GenerateDefaultBuildings(settings);
                return;
            }

            foreach (var building in buildings)
            {
                CarveBuilding(building, settings);
            }
        }

        private void GenerateDefaultBuildings(MapGenerationSettings settings)
        {
            int buildingCount = rng.Next(2, 5);
            for (int i = 0; i < buildingCount; i++)
            {
                var building = new BuildingConfig
                {
                    buildingId = $"Building_{i}",
                    footprintOrigin = new Vector2Int(rng.Next(4, MapWidth - 20), rng.Next(4, MapHeight - 20)),
                    floors = rng.Next(1, 4),
                    floorHeight = 1,
                    shape = (BuildingShape)rng.Next(0, 5),
                    roomTemplates = GetDefaultRoomTemplates(),
                    generateRoof = true,
                    generateBasement = rng.NextDouble() < 0.2f,
                    tacticalPriority = (float)rng.NextDouble()
                };
                buildings.Add(building);
            }
            foreach (var building in buildings)
            {
                CarveBuilding(building, settings);
            }
        }

        private List<RoomConfiguration> GetDefaultRoomTemplates()
        {
            return new List<RoomConfiguration>
            {
                new RoomConfiguration { purpose = RoomPurpose.Living, coverDensity = 0.2f, spawnWeight = 1.0f, minSize = 4, maxSize = 10, requireWindows = true, requireDoors = true, allowedTileTypes = new List<TileType> { TileType.Floor, TileType.CoverLow } },
                new RoomConfiguration { purpose = RoomPurpose.Office, coverDensity = 0.3f, spawnWeight = 1.2f, minSize = 5, maxSize = 12, requireWindows = true, requireDoors = true, allowedTileTypes = new List<TileType> { TileType.Floor, TileType.CoverLow, TileType.CoverHigh } },
                new RoomConfiguration { purpose = RoomPurpose.Storage, coverDensity = 0.4f, spawnWeight = 0.8f, minSize = 3, maxSize = 8, requireWindows = false, requireDoors = true, allowedTileTypes = new List<TileType> { TileType.Floor, TileType.CoverHigh } },
                new RoomConfiguration { purpose = RoomPurpose.Entry, coverDensity = 0.15f, spawnWeight = 0.6f, minSize = 4, maxSize = 8, requireWindows = false, requireDoors = true, allowedTileTypes = new List<TileType> { TileType.Floor } },
                new RoomConfiguration { purpose = RoomPurpose.Stairwell, coverDensity = 0.1f, spawnWeight = 0.4f, minSize = 3, maxSize = 5, requireWindows = false, requireDoors = false, allowedTileTypes = new List<TileType> { TileType.Floor, TileType.StairsUp, TileType.StairsDown } }
            };
        }

        private void CarveBuilding(BuildingConfig building, MapGenerationSettings settings)
        {
            int footprintWidth = building.shape == BuildingShape.Linear ? rng.Next(6, 12) : rng.Next(8, 18);
            int footprintDepth = building.shape == BuildingShape.Linear ? rng.Next(4, 8) : rng.Next(8, 16);

            int ox = building.footprintOrigin.x;
            int oy = building.footprintOrigin.y;

            for (int f = 0; f < building.floors; f++)
            {
                int floorY = f;
                int roomAttempts = 0;
                List<Room> floorRooms = new List<Room>();
                List<RectInt> floorRoomBounds = new List<RectInt>();

                while (floorRooms.Count < 4 && roomAttempts < 20)
                {
                    roomAttempts++;
                    var template = PickRoomTemplate(building.roomTemplates);
                    int rw = rng.Next(template.minSize, template.maxSize + 1);
                    int rh = rng.Next(template.minSize, template.maxSize + 1);
                    int rx = ox + rng.Next(1, Mathf.Max(2, footprintWidth - rw - 1));
                    int ry = oy + rng.Next(1, Mathf.Max(2, footprintDepth - rh - 1));
                    var newBounds = new RectInt(rx, ry, rw, rh);

                    bool overlaps = false;
                    foreach (var existing in floorRoomBounds)
                    {
                        if (RectIntOverlap(newBounds, existing, 1))
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (!overlaps)
                    {
                        floorRoomBounds.Add(newBounds);
                        var room = new Room
                        {
                            bounds = newBounds,
                            roomType = template.purpose.ToString(),
                            center = new Vector2Int(rx + rw / 2, ry + rh / 2),
                            floorLevel = floorY,
                            tacticalValue = 0.5f + (template.spawnWeight * 0.1f),
                            hasWindows = template.requireWindows && rng.NextDouble() < 0.7f,
                            hasDoors = template.requireDoors,
                            coverPositions = new List<Vector2Int>(),
                            spawnPositions = new List<Vector2Int>()
                        };
                        CarveRoomInterior(room, template, settings);
                        floorRooms.Add(room);
                        rooms.Add(room);
                    }
                }

                ConnectFloorRooms(floorRooms, settings);
            }

            if (building.generateRoof)
            {
                PlaceRoof(building, settings);
            }
        }

        private RoomConfiguration PickRoomTemplate(List<RoomConfiguration> templates)
        {
            if (templates == null || templates.Count == 0)
            {
                return new RoomConfiguration { purpose = RoomPurpose.Office, coverDensity = 0.3f, spawnWeight = 1.0f, minSize = 4, maxSize = 10 };
            }

            float totalWeight = 0f;
            foreach (var t in templates) totalWeight += t.spawnWeight;
            float roll = (float)rng.NextDouble() * totalWeight;
            float cumulative = 0f;
            foreach (var t in templates)
            {
                cumulative += t.spawnWeight;
                if (roll <= cumulative) return t;
            }
            return templates[templates.Count - 1];
        }

        private void CarveRoomInterior(Room room, RoomConfiguration template, MapGenerationSettings settings)
        {
            for (int x = room.bounds.xMin; x < room.bounds.xMax; x++)
            {
                for (int y = room.bounds.yMin; y < room.bounds.yMax; y++)
                {
                    if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight && room.floorLevel >= 0 && room.floorLevel < FloorCount)
                    {
                        var tile = tiles[x, y, room.floorLevel];
                        tile.type = TileType.Floor;
                        tile.walkable = true;
                        tile.materialIntegrity = 1f;
                        tiles[x, y, room.floorLevel] = tile;
                    }
                }
            }

            AddRoomCover(room, template, settings);
            AddRoomDoors(room, settings);
            AddRoomWindows(room, settings);
        }

        private void AddRoomCover(Room room, RoomConfiguration template, MapGenerationSettings settings)
        {
            int coverCount = (int)(room.bounds.width * room.bounds.height * template.coverDensity);
            for (int i = 0; i < coverCount; i++)
            {
                int x = room.bounds.xMin + rng.Next(1, room.bounds.width - 1);
                int y = room.bounds.yMin + rng.Next(1, room.bounds.height - 1);
                if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight && room.floorLevel >= 0 && room.floorLevel < FloorCount)
                {
                    if (tiles[x, y, room.floorLevel].type == TileType.Floor)
                    {
                        var tile = tiles[x, y, room.floorLevel];
                        tile.type = rng.NextDouble() < 0.6f ? TileType.CoverLow : TileType.CoverHigh;
                        tile.providesCover = true;
                        tile.materialIntegrity = 0.7f + ((float)rng.NextDouble() * 0.3f);
                        tiles[x, y, room.floorLevel] = tile;
                        room.coverPositions.Add(new Vector2Int(x, y));
                    }
                }
            }

            if (room.floorLevel >= 0 && room.floorLevel < FloorCount)
            {
                int spawnX = room.center.x;
                int spawnY = room.center.y;
                if (spawnX >= room.bounds.xMin && spawnX < room.bounds.xMax && spawnY >= room.bounds.yMin && spawnY < room.bounds.yMax)
                {
                    room.spawnPositions.Add(new Vector2Int(spawnX, spawnY));
                    if (spawnX + 1 < room.bounds.xMax) room.spawnPositions.Add(new Vector2Int(spawnX + 1, spawnY));
                    if (spawnX - 1 >= room.bounds.xMin) room.spawnPositions.Add(new Vector2Int(spawnX - 1, spawnY));
                    if (spawnY + 1 < room.bounds.yMax) room.spawnPositions.Add(new Vector2Int(spawnX, spawnY + 1));
                    if (spawnY - 1 >= room.bounds.yMin) room.spawnPositions.Add(new Vector2Int(spawnX, spawnY - 1));
                }
            }
        }

        private void AddRoomDoors(Room room, MapGenerationSettings settings)
        {
            if (!room.hasDoors) return;

            var edges = new List<Vector2Int>();
            for (int x = room.bounds.xMin; x < room.bounds.xMax; x++)
            {
                edges.Add(new Vector2Int(x, room.bounds.yMin));
                edges.Add(new Vector2Int(x, room.bounds.yMax - 1));
            }
            for (int y = room.bounds.yMin; y < room.bounds.yMax; y++)
            {
                edges.Add(new Vector2Int(room.bounds.xMin, y));
                edges.Add(new Vector2Int(room.bounds.xMax - 1, y));
            }

            int doorCount = Mathf.Max(1, edges.Count / 8);
            for (int i = 0; i < doorCount; i++)
            {
                var edge = edges[rng.Next(0, edges.Count)];
                int x = edge.x;
                int y = edge.y;
                if (x > 0 && x < MapWidth - 1 && y > 0 && y < MapHeight - 1 && room.floorLevel >= 0 && room.floorLevel < FloorCount)
                {
                    var tile = tiles[x, y, room.floorLevel];
                    if (tile.type == TileType.Floor || tile.type == TileType.Wall)
                    {
                        tile.type = TileType.Door;
                        tile.walkable = true;
                        tile.materialIntegrity = 0.5f;
                        tiles[x, y, room.floorLevel] = tile;
                    }
                }
            }
        }

        private void AddRoomWindows(Room room, MapGenerationSettings settings)
        {
            if (!room.hasWindows) return;

            var edges = new List<Vector2Int>();
            for (int x = room.bounds.xMin + 1; x < room.bounds.xMax - 1; x++)
            {
                edges.Add(new Vector2Int(x, room.bounds.yMin));
                edges.Add(new Vector2Int(x, room.bounds.yMax - 1));
            }
            for (int y = room.bounds.yMin + 1; y < room.bounds.yMax - 1; y++)
            {
                edges.Add(new Vector2Int(room.bounds.xMin, y));
                edges.Add(new Vector2Int(room.bounds.xMax - 1, y));
            }

            int windowCount = Mathf.Max(1, edges.Count / 10);
            for (int i = 0; i < windowCount; i++)
            {
                var edge = edges[rng.Next(0, edges.Count)];
                int x = edge.x;
                int y = edge.y;
                if (x > 1 && x < MapWidth - 2 && y > 1 && y < MapHeight - 2 && room.floorLevel >= 0 && room.floorLevel < FloorCount)
                {
                    var tile = tiles[x, y, room.floorLevel];
                    if (tile.type == TileType.Wall)
                    {
                        tile.type = TileType.Window;
                        tile.walkable = true;
                        tile.materialIntegrity = 0.4f;
                        tiles[x, y, room.floorLevel] = tile;
                    }
                }
            }
        }

        private void ConnectFloorRooms(List<Room> floorRooms, MapGenerationSettings settings)
        {
            for (int i = 0; i < floorRooms.Count - 1; i++)
            {
                ConnectRooms(floorRooms[i], floorRooms[i + 1], settings);
            }

            if (floorRooms.Count > 2 && rng.NextDouble() < 0.4f)
            {
                int a = rng.Next(0, floorRooms.Count);
                int b = rng.Next(0, floorRooms.Count);
                if (a != b) ConnectRooms(floorRooms[a], floorRooms[b], settings);
            }
        }

        private void ConnectOutdoorToBuildings(MapGenerationSettings settings)
        {
            foreach (var building in buildings)
            {
                foreach (var outdoor in outdoorAreas)
                {
                    if (RectIntOverlap(new RectInt(building.footprintOrigin.x, building.footprintOrigin.y, 6, 6), outdoor.bounds, 2))
                    {
                        var doorPos = FindEdgeBetweenBuildingAndOutdoor(building, outdoor);
                        if (doorPos.HasValue)
                        {
                            int x = doorPos.Value.x;
                            int y = doorPos.Value.y;
                            if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight)
                            {
                                var tile = tiles[x, y, 0];
                                tile.type = TileType.Door;
                                tile.walkable = true;
                                tile.materialIntegrity = 0.5f;
                                tiles[x, y, 0] = tile;
                            }
                        }
                    }
                }
            }
        }

        private Vector2Int? FindEdgeBetweenBuildingAndOutdoor(BuildingConfig building, OutdoorAreaConfig outdoor)
        {
            int ox = building.footprintOrigin.x;
            int oy = building.footprintOrigin.y;
            int w = 6;
            int d = 6;

            if (outdoor.bounds.xMin > ox + w)
            {
                return new Vector2Int(ox + w, oy + d / 2);
            }
            if (outdoor.bounds.xMax < ox)
            {
                return new Vector2Int(ox - 1, oy + d / 2);
            }
            if (outdoor.bounds.yMin > oy + d)
            {
                return new Vector2Int(ox + w / 2, oy + d);
            }
            if (outdoor.bounds.yMax < oy)
            {
                return new Vector2Int(ox + w / 2, oy - 1);
            }
            return null;
        }

        private void PlaceStairsAndVerticalAccess(MapGenerationSettings settings)
        {
            var stairwellRooms = new List<Room>();
            foreach (var room in rooms)
            {
                if (room.roomType == "Stairwell" || room.roomType == "Corridor")
                {
                    stairwellRooms.Add(room);
                }
            }

            if (stairwellRooms.Count == 0 && rooms.Count > 0)
            {
                stairwellRooms.Add(rooms[0]);
            }

            foreach (var room in stairwellRooms)
            {
                for (int f = 0; f < FloorCount - 1; f++)
                {
                    int sx = room.center.x;
                    int sy = room.center.y;
                    if (sx >= 0 && sx < MapWidth && sy >= 0 && sy < MapHeight && f >= 0 && f + 1 < FloorCount)
                    {
                        var up = tiles[sx, sy, f];
                        up.type = TileType.StairsUp;
                        up.walkable = true;
                        up.materialIntegrity = 1f;
                        tiles[sx, sy, f] = up;

                        var down = tiles[sx, sy, f + 1];
                        down.type = TileType.StairsDown;
                        down.walkable = true;
                        down.materialIntegrity = 1f;
                        tiles[sx, sy, f + 1] = down;
                    }
                }
            }

            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    for (int z = 1; z < FloorCount; z++)
                    {
                        if (tiles[x, y, z].type == TileType.Empty && tiles[x, y, z - 1].walkable)
                        {
                            var tile = tiles[x, y, z];
                            tile.type = TileType.Floor;
                            tile.walkable = true;
                            tile.materialIntegrity = 0.8f;
                            tiles[x, y, z] = tile;
                        }
                    }
                }
            }
        }

        private void PlaceRoof(BuildingConfig building, MapGenerationSettings settings)
        {
            int ox = building.footprintOrigin.x;
            int oy = building.footprintOrigin.y;
            int topFloor = building.floors - 1;
            if (topFloor < 0 || topFloor >= FloorCount) return;

            for (int x = ox; x < ox + 6 && x < MapWidth; x++)
            {
                for (int y = oy; y < oy + 6 && y < MapHeight; y++)
                {
                    if (x >= 0 && y >= 0)
                    {
                        var tile = tiles[x, y, topFloor];
                        if (tile.type == TileType.Floor || tile.type == TileType.Empty)
                        {
                            tile.type = TileType.Roof;
                            tile.walkable = true;
                            tile.materialIntegrity = 1f;
                            tiles[x, y, topFloor] = tile;
                        }
                    }
                }
            }
        }

        private void ConnectRooms(Room roomA, Room roomB, MapGenerationSettings settings)
        {
            int x = roomA.center.x;
            int y = roomA.center.y;
            int targetX = roomB.center.x;
            int targetY = roomB.center.y;
            int z = roomA.floorLevel;

            if (z != roomB.floorLevel)
            {
                z = Mathf.Min(z, roomB.floorLevel);
            }

            while (x != targetX)
            {
                CarveCorridorTile(x, y, z);
                x += x < targetX ? 1 : -1;
            }

            while (y != targetY)
            {
                CarveCorridorTile(x, y, z);
                y += y < targetY ? 1 : -1;
            }
        }

        private void CarveCorridorTile(int x, int y, int z)
        {
            if (x > 0 && x < MapWidth - 1 && y > 0 && y < MapHeight - 1 && z >= 0 && z < FloorCount)
            {
                var tile = tiles[x, y, z];
                tile.type = TileType.Floor;
                tile.walkable = true;
                tile.materialIntegrity = 0.8f;
                tiles[x, y, z] = tile;
            }
        }

        private void PlaceWallsAndEdges(MapGenerationSettings settings)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    for (int z = 0; z < FloorCount; z++)
                    {
                        var tile = tiles[x, y, z];
                        if (tile.type == TileType.Floor || tile.type == TileType.Empty)
                        {
                            if (HasAdjacentEmpty(x, y, z) || HasAdjacentDifferentLevel(x, y, z))
                            {
                                if (tile.type == TileType.Empty)
                                {
                                    tile.type = TileType.Wall;
                                    tile.walkable = false;
                                    tile.materialIntegrity = 1f;
                                }
                                tile.isEdge = true;
                                tiles[x, y, z] = tile;
                            }
                        }
                    }
                }
            }
        }

        private bool HasAdjacentEmpty(int x, int y, int z)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || nx >= MapWidth || ny < 0 || ny >= MapHeight)
                        return true;
                    if (z < 0 || z >= FloorCount)
                        return true;
                    if (tiles[nx, ny, z].type == TileType.Empty)
                        return true;
                }
            }
            return false;
        }

        private bool HasAdjacentDifferentLevel(int x, int y, int z)
        {
            if (z <= 0) return false;
            if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) return false;
            return tiles[x, y, z - 1].type == TileType.Empty;
        }

        private void AnalyzeTacticalPositions(MapGenerationSettings settings)
        {
            tacticalPositions.Clear();

            foreach (var room in rooms)
            {
                if (room.floorLevel < 0 || room.floorLevel >= FloorCount) continue;

                var center3D = new Vector3Int(room.center.x, room.center.y, room.floorLevel);
                tacticalPositions.Add(new TacticalPosition
                {
                    position = center3D,
                    type = room.roomType == "Stairwell" ? TacticalPositionType.RallyPoint : TacticalPositionType.Overwatch,
                    threatLevel = 1f - (room.tacticalValue / 2f),
                    protectionLevel = room.coverPositions.Count * 0.1f,
                    visibilityScore = room.hasWindows ? 0.7f : 0.3f,
                    adjacentCover = new List<Vector3Int>()
                });

                foreach (var cover in room.coverPositions)
                {
                    tacticalPositions.Add(new TacticalPosition
                    {
                        position = new Vector3Int(cover.x, cover.y, room.floorLevel),
                        type = TacticalPositionType.AmbushPoint,
                        threatLevel = 0.6f,
                        protectionLevel = 0.8f,
                        visibilityScore = 0.4f,
                        adjacentCover = new List<Vector3Int>()
                    });
                }
            }

            foreach (var outdoor in outdoorAreas)
            {
                var center = new Vector2Int(
                    outdoor.bounds.xMin + outdoor.bounds.width / 2,
                    outdoor.bounds.yMin + outdoor.bounds.height / 2
                );
                tacticalPositions.Add(new TacticalPosition
                {
                    position = new Vector3Int(center.x, center.y, 0),
                    type = outdoor.type == OutdoorType.OpenField ? TacticalPositionType.SniperNest : TacticalPositionType.RallyPoint,
                    threatLevel = 0.3f,
                    protectionLevel = 0.2f,
                    visibilityScore = outdoor.visibility,
                    adjacentCover = new List<Vector3Int>()
                });
            }
        }

        private bool RectIntOverlap(RectInt a, RectInt b, int padding)
        {
            return a.xMin - padding < b.xMax && a.xMax + padding > b.xMin &&
                   a.yMin - padding < b.yMax && a.yMax + padding > b.yMin;
        }
    }
}
