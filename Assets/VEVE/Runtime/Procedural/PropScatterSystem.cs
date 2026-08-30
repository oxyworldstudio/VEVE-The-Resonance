using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Procedural
{
    /// <summary>
    /// Functional grouping of a scattered prop, used by systems that react to props (destruction, sound, AI).
    /// </summary>
    public enum PropCategory { Barrel, Crate, Vehicle, Foliage, Debris, Sandbag, Furniture }

    /// <summary>
    /// Physical heft class of a prop. Consumed by physics, movement, and player interaction rules.
    /// </summary>
    public enum PropWeightClass { Light, Medium, Heavy }

    /// <summary>
    /// A single concrete prop placement produced by PropScatterSystem and consumed by the scene builder.
    /// </summary>
    [System.Serializable]
    public struct PropInstance
    {
        /// <summary>
        /// Palette key identifying the prop asset/material set (e.g. "prop.crate.wood").
        /// </summary>
        public string key;

        /// <summary>
        /// Grid tile the prop occupies. z carries the floor level, matching TileData.position.
        /// </summary>
        public Vector3Int tile;

        /// <summary>
        /// Vertical level index, duplicated for fast grouping during scene construction.
        /// </summary>
        public int floorLevel;

        /// <summary>
        /// Yaw rotation in degrees.
        /// </summary>
        public float rotationY;

        /// <summary>
        /// Uniform scale multiplier.
        /// </summary>
        public float scale;

        /// <summary>
        /// Heft classification from the prop definition.
        /// </summary>
        public PropWeightClass weightClass;

        /// <summary>
        /// Functional category of the prop.
        /// </summary>
        public PropCategory category;

        /// <summary>
        /// Optional room index the prop fell within, or -1 for outdoor placement.
        /// </summary>
        public int roomIndex;
    }

    /// <summary>
    /// Static description of one placeable prop and its tactical consequences.
    /// </summary>
    [System.Serializable]
    public struct PropDefinition
    {
        /// <summary>
        /// Palette key. Must be unique inside a catalogue.
        /// </summary>
        public string key;

        /// <summary>
        /// Functional category.
        /// </summary>
        public PropCategory category;

        /// <summary>
        /// Weight class assigned to instances.
        /// </summary>
        public PropWeightClass weightClass;

        /// <summary>
        /// Relative chance of this prop versus others when a placement resolves.
        /// </summary>
        public float placementWeight;

        /// <summary>
        /// When solid, the tile type used to write cover into the grid (typically CoverLow/CoverHigh).
        /// </summary>
        public TileType coverTileType;

        /// <summary>
        /// Whether the prop is a ballistic blocker once applied to the grid.
        /// </summary>
        public bool providesCover;

        /// <summary>
        /// Whether the prop blocks navigation on its tile once applied to the grid.
        /// </summary>
        public bool blocksNav;

        /// <summary>
        /// Whether the prop may only be placed on outdoor ground tiles.
        /// </summary>
        public bool outdoorOnly;

        /// <summary>
        /// Minimum conflict intensity (0-1) required for placement; 0 means always allowed.
        /// </summary>
        public float minConflictIntensity;

        /// <summary>
        /// Whether the prop requires civilian presence to be non-None.
        /// </summary>
        public bool requiresCivilians;
    }

    /// <summary>
    /// A weighted prop palette consumed by PropScatterSystem.
    /// </summary>
    [System.Serializable]
    public struct PropStyleProfile
    {
        /// <summary>
        /// Identifier for debugging and save authoring (e.g. "style.mediterranean").
        /// </summary>
        public string profileId;

        /// <summary>
        /// Prop definitions available for scattering.
        /// </summary>
        public List<PropDefinition> props;

        /// <summary>
        /// Builds the built-in catalog for a region by combining the biome table palette with default prop tables.
        /// </summary>
        /// <param name="region">Target semantic region.</param>
        /// <returns>Ready-to-use style profile.</returns>
        public static PropStyleProfile CreateForRegion(SemanticRegion region)
        {
            var profile = new PropStyleProfile
            {
                profileId = "style." + EnvironmentContextProfile.RegionToken(region),
                props = PropCatalogue.BuildDefaultsForRegion(region)
            };
            return profile;
        }
    }

    /// <summary>
    /// Default prop definitions plus helpers that translate biome palette keys into concrete PropDefinitions.
    /// </summary>
    public static class PropCatalogue
    {
        /// <summary>
        /// Returns default prop definitions referenced by a region's biome palette.
        /// </summary>
        /// <param name="region">Target semantic region.</param>
        /// <returns>Non-null list of prop definitions for the region.</returns>
        public static List<PropDefinition> BuildDefaultsForRegion(SemanticRegion region)
        {
            var defs = new List<PropDefinition>
            {
                Crate("prop.crate.wood", TileType.CoverLow, PropWeightClass.Medium, 1.0f, false),
                Barrel("prop.barrel.rusty", PropWeightClass.Medium, 0.9f),
                Debris("prop.debris.rubble", 0.8f),
                Sandbag("prop.sandbag.emplacement", 0.7f)
            };

            if (BiomeTable.TryGetForRegion(region, out var biome))
            {
                foreach (var key in biome.propPaletteKeys)
                {
                    if (TryGetFromKey(key, out var def) && !defs.Exists(d => d.key == def.key))
                    {
                        defs.Add(def);
                    }
                }
            }

            if (region != SemanticRegion.DesertCheckpoint && region != SemanticRegion.Unclassified)
            {
                defs.Add(Foliage("prop.foliage.bush", 0.6f));
            }

            return defs;
        }

        /// <summary>
        /// Resolves a palette key into a default definition.
        /// </summary>
        /// <param name="key">Palette key.</param>
        /// <param name="definition">Resulting definition when found.</param>
        /// <returns>True when the key maps to a known prop family.</returns>
        public static bool TryGetFromKey(string key, out PropDefinition definition)
        {
            definition = default;
            if (string.IsNullOrEmpty(key)) return false;

            string k = key.ToLowerInvariant();

            if (k.Contains("vehicle"))
            {
                definition = Vehicle(k);
                return true;
            }
            if (k.Contains("crate"))
            {
                bool heavy = k.Contains("ammo") || k.Contains("supply");
                definition = Crate(k, heavy ? TileType.CoverHigh : TileType.CoverLow,
                    heavy ? PropWeightClass.Medium : PropWeightClass.Light, 0.9f, false);
                return true;
            }
            if (k.Contains("barrel"))
            {
                definition = Barrel(k, k.Contains("fuel") ? PropWeightClass.Medium : PropWeightClass.Light, 0.8f);
                return true;
            }
            if (k.Contains("debris") || k.Contains("rubble"))
            {
                definition = Debris(k, 0.7f);
                return true;
            }
            if (k.Contains("sandbag") || k.Contains("barricade"))
            {
                definition = Sandbag(k, 0.6f);
                return true;
            }
            if (k.Contains("foliage") || k.Contains("bush") || k.Contains("tree") || k.Contains("shrub"))
            {
                definition = Foliage(k, 0.5f);
                return true;
            }
            if (k.Contains("furniture") || k.Contains("pallet") || k.Contains("crate.civilian"))
            {
                definition = Furniture(k);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Composes a single crate definition with common field conventions.
        /// </summary>
        /// <param name="key">Palette key.</param>
        /// <param name="coverTile">Tile type this crate converts to when providing cover.</param>
        /// <param name="weight">Weight class.</param>
        /// <param name="placementWeight">Relative placement likelihood.</param>
        /// <param name="outdoorOnly">Whether it is limited to outdoor ground tiles.</param>
        /// <returns>Composed definition.</returns>
        public static PropDefinition Crate(string key, TileType coverTile, PropWeightClass weight, float placementWeight, bool outdoorOnly)
        {
            return new PropDefinition
            {
                key = key,
                category = PropCategory.Crate,
                weightClass = weight,
                placementWeight = placementWeight,
                coverTileType = coverTile,
                providesCover = true,
                blocksNav = true,
                outdoorOnly = outdoorOnly,
                minConflictIntensity = 0f,
                requiresCivilians = false
            };
        }

        /// <summary>
        /// Composes a barrel definition. Fuel barrels never provide cover (explosive hazard) but block navigation.
        /// </summary>
        /// <param name="key">Palette key.</param>
        /// <param name="weight">Weight class.</param>
        /// <param name="placementWeight">Relative placement likelihood.</param>
        /// <returns>Composed definition.</returns>
        public static PropDefinition Barrel(string key, PropWeightClass weight, float placementWeight)
        {
            bool fuel = key.ToLowerInvariant().Contains("fuel");

            return new PropDefinition
            {
                key = key,
                category = PropCategory.Barrel,
                weightClass = weight,
                placementWeight = placementWeight,
                coverTileType = fuel ? TileType.Floor : TileType.CoverLow,
                providesCover = !fuel,
                blocksNav = true,
                outdoorOnly = false,
                minConflictIntensity = fuel ? 0.15f : 0f,
                requiresCivilians = false
            };
        }

        /// <summary>
        /// Composes a debris/rubble definition. Provides cover but stays walkable (crawlable rubble semantics).
        /// </summary>
        /// <param name="key">Palette key.</param>
        /// <param name="placementWeight">Relative placement likelihood.</param>
        /// <returns>Composed definition.</returns>
        public static PropDefinition Debris(string key, float placementWeight)
        {
            return new PropDefinition
            {
                key = key,
                category = PropCategory.Debris,
                weightClass = PropWeightClass.Light,
                placementWeight = placementWeight,
                coverTileType = TileType.Debris,
                providesCover = true,
                blocksNav = false,
                outdoorOnly = false,
                minConflictIntensity = 0.2f,
                requiresCivilians = false
            };
        }

        /// <summary>
        /// Composes a sandbag emplacement definition. Provides solid cover and blocks navigation.
        /// </summary>
        /// <param name="key">Palette key.</param>
        /// <param name="placementWeight">Relative placement likelihood.</param>
        /// <returns>Composed definition.</returns>
        public static PropDefinition Sandbag(string key, float placementWeight)
        {
            return new PropDefinition
            {
                key = key,
                category = PropCategory.Sandbag,
                weightClass = PropWeightClass.Medium,
                placementWeight = placementWeight,
                coverTileType = TileType.CoverHigh,
                providesCover = true,
                blocksNav = true,
                outdoorOnly = false,
                minConflictIntensity = 0.25f,
                requiresCivilians = false
            };
        }

        /// <summary>
        /// Composes a foliage definition. Provides low cover, never blocks navigation, outdoor only.
        /// </summary>
        /// <param name="key">Palette key.</param>
        /// <param name="placementWeight">Relative placement likelihood.</param>
        /// <returns>Composed definition.</returns>
        public static PropDefinition Foliage(string key, float placementWeight)
        {
            return new PropDefinition
            {
                key = key,
                category = PropCategory.Foliage,
                weightClass = PropWeightClass.Light,
                placementWeight = placementWeight,
                coverTileType = TileType.CoverLow,
                providesCover = true,
                blocksNav = false,
                outdoorOnly = true,
                minConflictIntensity = 0f,
                requiresCivilians = false
            };
        }

        /// <summary>
        /// Composes a vehicle definition. Provides high cover and blocks navigation.
        /// </summary>
        /// <param name="key">Palette key.</param>
        /// <returns>Composed definition.</returns>
        public static PropDefinition Vehicle(string key)
        {
            bool military = key.ToLowerInvariant().Contains("truck")
                || key.ToLowerInvariant().Contains("technical")
                || key.ToLowerInvariant().Contains("military")
                || key.ToLowerInvariant().Contains("apc");

            return new PropDefinition
            {
                key = key,
                category = PropCategory.Vehicle,
                weightClass = PropWeightClass.Heavy,
                placementWeight = military ? 0.5f : 0.4f,
                coverTileType = TileType.CoverHigh,
                providesCover = true,
                blocksNav = true,
                outdoorOnly = true,
                minConflictIntensity = military ? 0.3f : 0f,
                requiresCivilians = !military
            };
        }

        /// <summary>
        /// Composes a civilian furniture prop. Never provides cover, never blocks navigation.
        /// </summary>
        /// <param name="key">Palette key.</param>
        /// <returns>Composed definition.</returns>
        public static PropDefinition Furniture(string key)
        {
            return new PropDefinition
            {
                key = key,
                category = PropCategory.Furniture,
                weightClass = PropWeightClass.Light,
                placementWeight = 0.3f,
                coverTileType = TileType.Floor,
                providesCover = false,
                blocksNav = false,
                outdoorOnly = false,
                minConflictIntensity = 0f,
                requiresCivilians = true
            };
        }
    }

    /// <summary>
    /// Deterministic context-aware prop placement producing PropInstance data for the scene builder
    /// and optional TileData cover mutations for the tactical evaluator. Placement iterates a stable
    /// tile order and consumes a seeded System.Random derived from EnvironmentContextProfile.DeriveSeed,
    /// so identical inputs always yield byte-identical prop arrays.
    /// </summary>
    public class PropScatterSystem
    {
        /// <summary>
        /// Maximum distance in tiles from a doorway within which props are suppressed.
        /// </summary>
        public const int DoorwayKeepOutRadius = 2;

        /// <summary>
        /// Default number of tiles between two solid props to keep cover from forming walls.
        /// </summary>
        public const int MinSolidPropSpacing = 1;

        /// <summary>
        /// Scatters context props onto a generated tile grid.
        /// </summary>
        /// <param name="tiles">Tile grid indexed [x, y, floor]; unchanged unless mutatesTiles is set.</param>
        /// <param name="mapWidth">Width of the grid in tiles.</param>
        /// <param name="mapHeight">Height of the grid in tiles.</param>
        /// <param name="floorCount">Vertical floors in the grid.</param>
        /// <param name="context">Narrative profile feeding palette selection and determinism.</param>
        /// <param name="profile">Style palette to place from.</param>
        /// <param name="rooms">Rooms used for indoor/outdoor classification and context bonuses, may be null.</param>
        /// <param name="densityScale">Global density multiplier; typical range [0,2].</param>
        /// <returns>Array of prop placements in a deterministic stable order.</returns>
        public PropInstance[] Scatter(TileData[,,] tiles, int mapWidth, int mapHeight, int floorCount,
            EnvironmentContextProfile context, PropStyleProfile profile, List<Room> rooms, float densityScale)
        {
            if (tiles == null || context == null || profile.props == null || profile.props.Count == 0)
            {
                return Array.Empty<PropInstance>();
            }

            context.Normalize();
            var rng = new System.Random(context.DeriveSeed(ProfileSeed(profile)));
            var result = new List<PropInstance>();
            var solidOccupied = new HashSet<long>();

            for (int z = 0; z < floorCount; z++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    for (int x = 0; x < mapWidth; x++)
                    {
                        var tile = tiles[x, y, z];

                        if (!IsScatterableTile(tile.type)) continue;
                        if (NearDoorway(tiles, mapWidth, mapHeight, floorCount, x, y, z)) continue;
                        if (MinSolidPropSpacing > 0 && solidOccupied.Contains(TKey(x, y, z))) continue;

                        int roomIndex = RoomAt(rooms, x, y, z);
                        bool outdoor = roomIndex < 0 || tile.type == TileType.Ground || tile.type == TileType.Roof;

                        foreach (var definition in profile.props)
                        {
                            if (!PropAllowed(definition, context, outdoor, tile)) continue;

                            float chance = DefinitionChance(definition, context, outdoor, z, densityScale);
                            if (rng.NextDouble() > chance) continue;

                            if (MinSolidPropSpacing > 0 && definition.blocksNav)
                            {
                                MarkSolidNeighborhood(solidOccupied, x, y, z);
                            }

                            result.Add(new PropInstance
                            {
                                key = definition.key,
                                tile = new Vector3Int(x, y, z),
                                floorLevel = z,
                                rotationY = SnapRotation((float)(rng.NextDouble() * 360.0)),
                                scale = 0.9f + (float)rng.NextDouble() * 0.2f,
                                weightClass = definition.weightClass,
                                category = definition.category,
                                roomIndex = roomIndex
                            });
                            break;
                        }
                    }
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Writes solid prop placements back into a tile grid so downstream tactical evaluation and
        /// AI pathing see them as cover volumes. Props that are walkable (debris, foliage, furniture)
        /// only set providesCover without changing the tile type.
        /// </summary>
        /// <param name="tiles">Mutable tile grid.</param>
        /// <param name="props">Placements produced by Scatter.</param>
        /// <param name="profile">Palette used to look up definitions.</param>
        /// <returns>Number of tiles mutated.</returns>
        public int ApplyToTiles(TileData[,,] tiles, PropInstance[] props, PropStyleProfile profile)
        {
            if (tiles == null || props == null || profile.props == null) return 0;

            var lookup = new Dictionary<string, PropDefinition>(profile.props.Count);
            foreach (var definition in profile.props)
            {
                lookup[definition.key] = definition;
            }

            int mutations = 0;
            foreach (var prop in props)
            {
                int x = prop.tile.x;
                int y = prop.tile.y;
                int z = prop.tile.z;
                if (x < 0 || x >= tiles.GetLength(0)) continue;
                if (y < 0 || y >= tiles.GetLength(1)) continue;
                if (z < 0 || z >= tiles.GetLength(2)) continue;

                if (!lookup.TryGetValue(prop.key, out var definition)) continue;
                if (!definition.providesCover && !definition.blocksNav) continue;

                var tile = tiles[x, y, z];

                if (definition.providesCover)
                {
                    tile.type = definition.coverTileType;
                    tile.providesCover = true;
                }

                if (definition.blocksNav)
                {
                    tile.walkable = false;
                }

                tiles[x, y, z] = tile;
                mutations++;
            }

            return mutations;
        }

        /// <summary>
        /// Indicates whether a prop palette key resolves to a cover volume once applied.
        /// </summary>
        /// <param name="profile">Style palette to consult.</param>
        /// <param name="key">Prop palette key.</param>
        /// <returns>True when the matching definition provides cover.</returns>
        public static bool IsCoverProp(PropStyleProfile profile, string key)
        {
            if (profile.props == null || string.IsNullOrEmpty(key)) return false;
            foreach (var def in profile.props)
            {
                if (string.Equals(def.key, key, StringComparison.OrdinalIgnoreCase)) return def.providesCover;
            }
            return false;
        }

        private static bool IsScatterableTile(TileType type)
        {
            return type == TileType.Floor
                || type == TileType.Ground
                || type == TileType.CoverLow
                || type == TileType.CoverHigh
                || type == TileType.Debris
                || type == TileType.Roof;
        }

        private static bool NearDoorway(TileData[,,] tiles, int mapWidth, int mapHeight, int floorCount, int x, int y, int z)
        {
            int r = DoorwayKeepOutRadius;
            int xMax = Math.Min(mapWidth - 1, x + r);
            int yMax = Math.Min(mapHeight - 1, y + r);
            int xMin = Math.Max(0, x - r);
            int yMin = Math.Max(0, y - r);

            for (int cx = xMin; cx <= xMax; cx++)
            {
                for (int cy = yMin; cy <= yMax; cy++)
                {
                    if (tiles[cx, cy, z].type == TileType.Door)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static int RoomAt(List<Room> rooms, int x, int y, int z)
        {
            if (rooms == null) return -1;
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i];
                if (r.floorLevel != z) continue;
                if (x >= r.bounds.xMin && x < r.bounds.xMax && y >= r.bounds.yMin && y < r.bounds.yMax)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool PropAllowed(PropDefinition definition, EnvironmentContextProfile context, bool outdoor, TileData tile)
        {
            if (definition.outdoorOnly && !outdoor) return false;
            if (!outdoor && tile.type == TileType.Roof) return definition.category == PropCategory.Debris || definition.category == PropCategory.Foliage;
            if (tile.type == TileType.Ground && !outdoor) return false;
            if (context.NormalizedConflictIntensity < definition.minConflictIntensity) return false;
            if (definition.requiresCivilians && context.civilianPresence == CivilianPresenceLevel.None) return false;
            return true;
        }

        private static float DefinitionChance(PropDefinition definition, EnvironmentContextProfile context, bool outdoor, int floorIndex, float densityScale)
        {
            float chance = definition.placementWeight * Mathf.Clamp01(densityScale) * 0.05f;

            float conflict = context.NormalizedConflictIntensity;

            switch (definition.category)
            {
                case PropCategory.Debris:
                    chance *= Mathf.Lerp(0.3f, 2.0f, conflict);
                    break;
                case PropCategory.Sandbag:
                    chance *= outdoor ? Mathf.Lerp(0.05f, 2.4f, conflict) : Mathf.Lerp(0.0f, 1.4f, conflict);
                    break;
                case PropCategory.Foliage:
                    chance *= outdoor ? VectorDensityForFloor(context, floorIndex) : 0.0f;
                    break;
                case PropCategory.Vehicle:
                    chance *= context.NormalizedConflictIntensity >= 0.4f ? 0.8f : 1.4f;
                    break;
                case PropCategory.Furniture:
                    chance *= Mathf.Lerp(0.2f, 1.6f, (float)((int)context.civilianPresence) / 3f);
                    break;
            }

            return Mathf.Clamp01(chance);
        }

        private static float VectorDensityForFloor(EnvironmentContextProfile context, int floorIndex)
        {
            if (BiomeTable.TryGetForRegion(context.region, out var biome))
            {
                return floorIndex == 0 ? biome.vegetationDensity : biome.vegetationDensity * 0.4f;
            }
            return floorIndex == 0 ? 0.35f : 0.1f;
        }

        private static void MarkSolidNeighborhood(HashSet<long> set, int x, int y, int z)
        {
            for (int dx = -MinSolidPropSpacing; dx <= MinSolidPropSpacing; dx++)
            {
                for (int dy = -MinSolidPropSpacing; dy <= MinSolidPropSpacing; dy++)
                {
                    set.Add(TKey(x + dx, y + dy, z));
                }
            }
        }

        private static float SnapRotation(float degrees)
        {
            if (degrees < 0f || float.IsNaN(degrees) || float.IsInfinity(degrees)) return 0f;

            int step = 15;
            float snapped = Mathf.Round(degrees / step) * step;
            return snapped >= 360f ? snapped - 360f : snapped;
        }

        private static long TKey(int x, int y, int z)
        {
            unchecked
            {
                long hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + z;
                return hash;
            }
        }

        private static int ProfileSeed(PropStyleProfile profile)
        {
            return EnvironmentContextProfile.StableStringHash(profile.profileId);
        }
    }
}
