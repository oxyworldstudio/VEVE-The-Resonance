using UnityEngine;
using System.Collections.Generic;

namespace VEVE.Procedural
{
    [System.Serializable]
    public struct MapGenerationSettings
    {
        public int mapWidth;
        public int mapHeight;
        public int minRoomSize;
        public int maxRoomSize;
        public int maxRooms;
        public int corridorWidth;
        public float coverDensity;
        public bool enableWindows;
        public bool enableDoors;
        public int seed;

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

    public class ProceduralMapGenerator
    {
        private TileData[,] tiles;
        private List<Room> rooms;
        private System.Random rng;

        public TileData[,] Generate(MapGenerationSettings settings)
        {
            if (settings.seed != 0)
                rng = new System.Random(settings.seed);
            else
                rng = new System.Random();

            tiles = new TileData[settings.mapWidth, settings.mapHeight];
            rooms = new List<Room>();

            for (int x = 0; x < settings.mapWidth; x++)
            {
                for (int y = 0; y < settings.mapHeight; y++)
                {
                    tiles[x, y] = new TileData
                    {
                        type = TileType.Empty,
                        position = new Vector3Int(x, y, 0),
                        walkable = false,
                        providesCover = false,
                        materialIntegrity = 0f
                    };
                }
            }

            GenerateRooms(settings);
            GenerateCorridors(settings);
            AddCover(settings);
            if (settings.enableDoors) AddDoors(settings);
            if (settings.enableWindows) AddWindows(settings);
            PlaceWalls();

            return tiles;
        }

        private void GenerateRooms(MapGenerationSettings settings)
        {
            for (int i = 0; i < settings.maxRooms; i++)
            {
                int width = rng.Next(settings.minRoomSize, settings.maxRoomSize + 1);
                int height = rng.Next(settings.minRoomSize, settings.maxRoomSize + 1);
                int x = rng.Next(1, settings.mapWidth - width - 1);
                int y = rng.Next(1, settings.mapHeight - height - 1);

                var newRoom = new Room
                {
                    bounds = new RectInt(x, y, width, height),
                    roomType = GetRoomType(width, height),
                    center = new Vector2Int(x + width / 2, y + height / 2)
                };

                if (rooms.Count == 0)
                {
                    rooms.Add(newRoom);
                    CarveRoom(newRoom);
                    continue;
                }

                bool overlaps = false;
                foreach (var room in rooms)
                {
                    if (RectIntOverlap(newRoom.bounds, room.bounds, 2))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    rooms.Add(newRoom);
                    CarveRoom(newRoom);
                    ConnectRooms(rooms[rooms.Count - 2], newRoom);
                }
            }
        }

        private void CarveRoom(Room room)
        {
            for (int x = room.bounds.xMin; x < room.bounds.xMax; x++)
            {
                for (int y = room.bounds.yMin; y < room.bounds.yMax; y++)
                {
                    tiles[x, y].type = TileType.Floor;
                    tiles[x, y].walkable = true;
                    tiles[x, y].materialIntegrity = 1f;
                }
            }
        }

        private void ConnectRooms(Room roomA, Room roomB)
        {
            int x = roomA.center.x;
            int y = roomA.center.y;
            int targetX = roomB.center.x;
            int targetY = roomB.center.y;

            while (x != targetX)
            {
                CarveCorridorTile(x, y);
                x += x < targetX ? 1 : -1;
            }

            while (y != targetY)
            {
                CarveCorridorTile(x, y);
                y += y < targetY ? 1 : -1;
            }
        }

        private void CarveCorridorTile(int x, int y)
        {
            if (x > 0 && x < tiles.GetLength(0) - 1 && y > 0 && y < tiles.GetLength(1) - 1)
            {
                tiles[x, y].type = TileType.Floor;
                tiles[x, y].walkable = true;
            }
        }

        private void GenerateCorridors(MapGenerationSettings settings)
        {
            for (int i = 0; i < rooms.Count - 1; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    if (rng.NextDouble() < 0.3f)
                    {
                        ConnectRooms(rooms[i], rooms[j]);
                    }
                }
            }
        }

        private void AddCover(MapGenerationSettings settings)
        {
            foreach (var room in rooms)
            {
                for (int x = room.bounds.xMin + 1; x < room.bounds.xMax - 1; x++)
                {
                    for (int y = room.bounds.yMin + 1; y < room.bounds.yMax - 1; y++)
                    {
                        if (tiles[x, y].walkable && rng.NextDouble() < settings.coverDensity)
                        {
                            tiles[x, y].type = TileType.Cover;
                            tiles[x, y].providesCover = true;
                            tiles[x, y].materialIntegrity = 0.8f;
                        }
                    }
                }
            }
        }

        private void AddDoors(MapGenerationSettings settings)
        {
            foreach (var room in rooms)
            {
                for (int x = room.bounds.xMin; x < room.bounds.xMax; x++)
                {
                    TryPlaceDoor(x, room.bounds.yMin);
                    TryPlaceDoor(x, room.bounds.yMax - 1);
                }
                for (int y = room.bounds.yMin; y < room.bounds.yMax; y++)
                {
                    TryPlaceDoor(room.bounds.xMin, y);
                    TryPlaceDoor(room.bounds.xMax - 1, y);
                }
            }
        }

        private void TryPlaceDoor(int x, int y)
        {
            if (x > 0 && x < tiles.GetLength(0) - 1 && y > 0 && y < tiles.GetLength(1) - 1)
            {
                if (tiles[x, y].type == TileType.Floor && rng.NextDouble() < 0.25f)
                {
                    tiles[x, y].type = TileType.Door;
                    tiles[x, y].walkable = true;
                }
            }
        }

        private void AddWindows(MapGenerationSettings settings)
        {
            foreach (var room in rooms)
            {
                for (int x = room.bounds.xMin; x < room.bounds.xMax; x++)
                {
                    TryPlaceWindow(x, room.bounds.yMin);
                    TryPlaceWindow(x, room.bounds.yMax - 1);
                }
                for (int y = room.bounds.yMin; y < room.bounds.yMax; y++)
                {
                    TryPlaceWindow(room.bounds.xMin, y);
                    TryPlaceWindow(room.bounds.xMax - 1, y);
                }
            }
        }

        private void TryPlaceWindow(int x, int y)
        {
            if (x > 1 && x < tiles.GetLength(0) - 2 && y > 1 && y < tiles.GetLength(1) - 2)
            {
                if (tiles[x, y].type == TileType.Floor && rng.NextDouble() < 0.1f)
                {
                    tiles[x, y].type = TileType.Window;
                    tiles[x, y].walkable = true;
                    tiles[x, y].materialIntegrity = 0.5f;
                }
            }
        }

        private void PlaceWalls()
        {
            for (int x = 0; x < tiles.GetLength(0); x++)
            {
                for (int y = 0; y < tiles.GetLength(1); y++)
                {
                    if (tiles[x, y].type == TileType.Floor || tiles[x, y].type == TileType.Empty)
                    {
                        if (HasAdjacentEmpty(x, y))
                        {
                            tiles[x, y].type = TileType.Wall;
                            tiles[x, y].walkable = false;
                            tiles[x, y].materialIntegrity = 1f;
                        }
                    }
                }
            }
        }

        private bool HasAdjacentEmpty(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || nx >= tiles.GetLength(0) || ny < 0 || ny >= tiles.GetLength(1))
                        return true;
                    if (tiles[nx, ny].type == TileType.Empty)
                        return true;
                }
            }
            return false;
        }

        private string GetRoomType(int width, int height)
        {
            float area = width * height;
            if (area < 30) return "Small";
            if (area < 70) return "Medium";
            return "Large";
        }

        private bool RectIntOverlap(RectInt a, RectInt b, int padding)
        {
            return a.xMin - padding < b.xMax && a.xMax + padding > b.xMin &&
                   a.yMin - padding < b.yMax && a.yMax + padding > b.yMin;
        }

        public List<Vector2Int> GetSpawnPoints()
        {
            var spawns = new List<Vector2Int>();
            foreach (var room in rooms)
            {
                spawns.Add(room.center);
            }
            return spawns;
        }

        public List<Vector2Int> GetCoverPositions()
        {
            var cover = new List<Vector2Int>();
            for (int x = 0; x < tiles.GetLength(0); x++)
            {
                for (int y = 0; y < tiles.GetLength(1); y++)
                {
                    if (tiles[x, y].providesCover)
                    {
                        cover.Add(new Vector2Int(x, y));
                    }
                }
            }
            return cover;
        }
    }
}
