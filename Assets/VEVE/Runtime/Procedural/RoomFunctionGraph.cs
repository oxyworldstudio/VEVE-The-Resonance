using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Procedural
{
    /// <summary>
    /// Tactical/narrative function assigned to a room by the RoomFunctionGraph.
    /// </summary>
    public enum RoomFunction { CommandPost, Barracks, Storage, Medical, Comms, Residential, Industrial, Stairwell, Rooftop }

    /// <summary>
    /// Data-driven contract for a room function: size constraints, exit expectations,
    /// adjacency rules, and the prop/lighting keys implied by the function.
    /// </summary>
    [System.Serializable]
    public struct RoomFunctionSpec
    {
        /// <summary>
        /// Function this contract describes.
        /// </summary>
        public RoomFunction function;

        /// <summary>
        /// Minimum floor area in tiles for the function to fit credibly.
        /// </summary>
        public int minimumArea;

        /// <summary>
        /// Minimum side length in tiles for the function to fit credibly.
        /// </summary>
        public int minimumSide;

        /// <summary>
        /// Minimum number of exits expected for egress realism.
        /// </summary>
        public int minimumExits;

        /// <summary>
        /// Relative selection desirability when the graph assigns functions.
        /// </summary>
        public float desirability;

        /// <summary>
        /// Whether this function may only exist on the top generated floor.
        /// </summary>
        public bool requiresTopFloor;

        /// <summary>
        /// Whether placement below ground floor level 0 is physically valid.
        /// </summary>
        public bool groundFloorOnly;

        /// <summary>
        /// Semantic lighting key expected for this function.
        /// </summary>
        public string lightingKey;

        /// <summary>
        /// Prop palette keys expected by the function.
        /// </summary>
        public string[] propPaletteKeys;

        /// <summary>
        /// Functions that must never share a room boundary with this one.
        /// </summary>
        public RoomFunction[] forbiddenNeighbors;

        /// <summary>
        /// Functions that gain adjacency bonuses next to this one.
        /// </summary>
        public RoomFunction[] preferredNeighbors;
    }

    /// <summary>
    /// Result of assigning a function to one room in the supplied room list.
    /// </summary>
    [System.Serializable]
    public struct RoomFunctionAssignment
    {
        /// <summary>
        /// Index into the room list provided to AssignFunctions.
        /// </summary>
        public int roomIndex;

        /// <summary>
        /// Assigned function.
        /// </summary>
        public RoomFunction function;

        /// <summary>
        /// False when no function satisfied the constraints and a fallback was forced.
        /// </summary>
        public bool satisfied;
    }

    /// <summary>
    /// Aggregate result of a function-assignment pass.
    /// </summary>
    public sealed class RoomFunctionAssignmentResult
    {
        /// <summary>
        /// One assignment per input room, in input order.
        /// </summary>
        public RoomFunctionAssignment[] Assignments = Array.Empty<RoomFunctionAssignment>();

        /// <summary>
        /// Human-readable constraint violations discovered during assignment.
        /// </summary>
        public List<string> Violations = new List<string>();

        /// <summary>
        /// Count of rooms whose assignment could not satisfy all constraints.
        /// </summary>
        public int UnsatisfiedCount
        {
            get
            {
                int count = 0;
                if (Assignments != null)
                {
                    foreach (var assignment in Assignments)
                    {
                        if (!assignment.satisfied) count++;
                    }
                }
                return count;
            }
        }
    }

    /// <summary>
    /// Assigns tactical room functions to generated rooms with deterministic adjacency,
    /// area, and exit-rule enforcement. Pure function over supplied Room data; the generator
    /// or scene builder calls this after ProceduralMapGenerator.GetRooms().
    /// </summary>
    public static class RoomFunctionGraph
    {
        /// <summary>
        /// Room-boundary proximity in tiles considered adjacent for rule evaluation.
        /// </summary>
        public const int AdjacencyPadding = 1;

        private static readonly Dictionary<RoomFunction, RoomFunctionSpec> Specs = BuildSpecs();

        /// <summary>
        /// Returns the contract for a room function.
        /// </summary>
        /// <param name="function">Function to look up.</param>
        /// <returns>The matching spec.</returns>
        public static RoomFunctionSpec GetSpec(RoomFunction function)
        {
            return Specs[function];
        }

        /// <summary>
        /// Attempts to fetch a spec without throwing.
        /// </summary>
        /// <param name="function">Function to look up.</param>
        /// <param name="spec">Resolved spec when found.</param>
        /// <returns>True when the function is known.</returns>
        public static bool TryGetSpec(RoomFunction function, out RoomFunctionSpec spec)
        {
            return Specs.TryGetValue(function, out spec);
        }

        /// <summary>
        /// Every authored function contract.
        /// </summary>
        public static IEnumerable<RoomFunctionSpec> AllSpecs
        {
            get { return Specs.Values; }
        }

        /// <summary>
        /// Returns the regional candidate function pool for a context profile.
        /// </summary>
        /// <param name="context">Narrative context; null yields the full pool.</param>
        /// <returns>Non-null candidate array.</returns>
        public static RoomFunction[] GetCandidateFunctions(EnvironmentContextProfile context)
        {
            var semanticRegion = context != null ? context.region : SemanticRegion.Unclassified;

            switch (semanticRegion)
            {
                case SemanticRegion.MediterraneanTown:
                    return new[] { RoomFunction.Residential, RoomFunction.CommandPost, RoomFunction.Storage, RoomFunction.Medical, RoomFunction.Comms, RoomFunction.Barracks, RoomFunction.Stairwell, RoomFunction.Rooftop };
                case SemanticRegion.EasternEuropeanIndustrial:
                    return new[] { RoomFunction.Industrial, RoomFunction.Storage, RoomFunction.Comms, RoomFunction.CommandPost, RoomFunction.Barracks, RoomFunction.Medical, RoomFunction.Stairwell, RoomFunction.Rooftop };
                case SemanticRegion.DesertCheckpoint:
                    return new[] { RoomFunction.Barracks, RoomFunction.Storage, RoomFunction.Comms, RoomFunction.Medical, RoomFunction.CommandPost, RoomFunction.Stairwell, RoomFunction.Rooftop };
                case SemanticRegion.SubarcticCompound:
                    return new[] { RoomFunction.Storage, RoomFunction.Barracks, RoomFunction.Medical, RoomFunction.Comms, RoomFunction.CommandPost, RoomFunction.Industrial, RoomFunction.Stairwell, RoomFunction.Rooftop };
                case SemanticRegion.TemperateForestVillage:
                    return new[] { RoomFunction.Residential, RoomFunction.Storage, RoomFunction.Barracks, RoomFunction.Medical, RoomFunction.Comms, RoomFunction.Stairwell, RoomFunction.Rooftop };
                default:
                    return new[]
                    {
                        RoomFunction.CommandPost, RoomFunction.Barracks, RoomFunction.Storage, RoomFunction.Medical,
                        RoomFunction.Comms, RoomFunction.Residential, RoomFunction.Industrial, RoomFunction.Stairwell, RoomFunction.Rooftop
                    };
            }
        }

        /// <summary>
        /// Tests whether two functions are forbidden from sharing a wall boundary.
        /// </summary>
        /// <param name="a">First function.</param>
        /// <param name="b">Second function.</param>
        /// <returns>True when the pair breaks a compatibility rule.</returns>
        public static bool AreFunctionsAdjacentForbidden(RoomFunction a, RoomFunction b)
        {
            if (TryGetSpec(a, out var specA) && Contains(specA.forbiddenNeighbors, b)) return true;
            if (TryGetSpec(b, out var specB) && Contains(specB.forbiddenNeighbors, a)) return true;
            return false;
        }

        /// <summary>
        /// Room bounds proximity test used by both assignment and validation passes.
        /// </summary>
        /// <param name="a">First room bounds.</param>
        /// <param name="b">Second room bounds.</param>
        /// <param name="padding">Extra tiles considered adjacent beyond touching.</param>
        /// <returns>True when the rectangles are within padding tiles of each other.</returns>
        public static bool RoomsAdjacent(RectInt a, RectInt b, int padding)
        {
            return a.xMin - padding < b.xMax
                && a.xMax + padding > b.xMin
                && a.yMin - padding < b.yMax
                && a.yMax + padding > b.yMin;
        }

        /// <summary>
        /// Deterministically assigns functions to every room given the context, respecting pool,
        /// area, floor, and adjacency constraints. Same inputs always yield the same assignments.
        /// </summary>
        /// <param name="rooms">Rooms produced by the generator.</param>
        /// <param name="context">Narrative context; null uses unclassified defaults.</param>
        /// <param name="seed">Base seed combined with the context seed for local randomization.</param>
        /// <returns>Assignments and violations for the input list.</returns>
        public static RoomFunctionAssignmentResult AssignFunctions(IReadOnlyList<Room> rooms, EnvironmentContextProfile context, int seed)
        {
            var result = new RoomFunctionAssignmentResult();
            if (rooms == null || rooms.Count == 0) return result;

            if (context != null) context.Normalize();
            int derivedSeed = context != null ? context.DeriveSeed(seed) : (seed == 0 ? 1618033 : seed);
            var rng = new System.Random(derivedSeed);

            int topFloor = int.MinValue;
            foreach (var room in rooms)
            {
                if (room.floorLevel > topFloor) topFloor = room.floorLevel;
            }

            var candidates = GetCandidateFunctions(context);
            var functions = new RoomFunction?[rooms.Count];
            var usedOnFloor = new HashSet<string>();

            foreach (int index in OrderedIndices(rooms))
            {
                var room = rooms[index];
                int area = Mathf.Max(1, room.bounds.width * room.bounds.height);

                RoomFunction? bestFunction = null;
                float bestScore = float.NegativeInfinity;

                foreach (var candidate in candidates)
                {
                    var spec = GetSpec(candidate);

                    if (spec.minimumArea > area) continue;
                    if (Mathf.Min(room.bounds.width, room.bounds.height) < spec.minimumSide) continue;
                    if (spec.requiresTopFloor && room.floorLevel != topFloor) continue;
                    if (spec.groundFloorOnly && room.floorLevel != 0) continue;

                    string usageKey = candidate + "@" + room.floorLevel;
                    int usagePenalty = usedOnFloor.Contains(usageKey) ? 1 : 0;

                    bool blocked = false;
                    int preferredBonus = 0;
                    for (int other = 0; other < rooms.Count; other++)
                    {
                        if (other == index || functions[other] == null) continue;
                        var otherRoom = rooms[other];
                        if (otherRoom.floorLevel != room.floorLevel) continue;
                        if (!RoomsAdjacent(room.bounds, otherRoom.bounds, AdjacencyPadding)) continue;

                        var otherFunction = functions[other].Value;
                        if (AreFunctionsAdjacentForbidden(candidate, otherFunction))
                        {
                            blocked = true;
                            break;
                        }
                        if (Contains(spec.preferredNeighbors, otherFunction)) preferredBonus++;
                    }
                    if (blocked) continue;

                    float groundBonus = spec.groundFloorOnly && room.floorLevel == 0 ? 0.25f : 0f;
                    float score = spec.desirability
                        + preferredBonus * 0.35f
                        + groundBonus
                        - usagePenalty * 0.8f
                        + (float)rng.NextDouble() * 0.05f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestFunction = candidate;
                    }
                }

                if (bestFunction.HasValue)
                {
                    functions[index] = bestFunction.Value;
                    usedOnFloor.Add(bestFunction.Value + "@" + room.floorLevel);
                }
                else
                {
                    functions[index] = RoomFunction.Storage;
                    result.Violations.Add(string.Format("Room {0} (floor {1}, {2}x{3}): no candidate met constraints; Storage forced.",
                        index, room.floorLevel, room.bounds.width, room.bounds.height));
                }
            }

            var assignments = new RoomFunctionAssignment[rooms.Count];
            for (int i = 0; i < rooms.Count; i++)
            {
                bool satisfied = functions[i].HasValue;
                assignments[i] = new RoomFunctionAssignment
                {
                    roomIndex = i,
                    function = satisfied ? functions[i].Value : RoomFunction.Storage,
                    satisfied = satisfied
                };
            }

            result.Assignments = assignments;

            foreach (var violation in Validate(rooms, assignments))
            {
                result.Violations.Add(violation);
            }

            return result;
        }

        /// <summary>
        /// Validates an assignment set against every hard constraint: fit, floor rules,
        /// adjacency compatibility, and exit expectations.
        /// </summary>
        /// <param name="rooms">Room list the assignments were produced for.</param>
        /// <param name="assignments">Assignment array parallel to the room list.</param>
        /// <returns>Human-readable violations; empty when the layout is valid.</returns>
        public static List<string> Validate(IReadOnlyList<Room> rooms, RoomFunctionAssignment[] assignments)
        {
            var violations = new List<string>();
            if (rooms == null || assignments == null || rooms.Count != assignments.Length) return violations;

            int topFloor = int.MinValue;
            foreach (var room in rooms)
            {
                if (room.floorLevel > topFloor) topFloor = room.floorLevel;
            }

            for (int i = 0; i < assignments.Length; i++)
            {
                var room = rooms[i];
                var assignment = assignments[i];
                var spec = GetSpec(assignment.function);
                int area = Mathf.Max(1, room.bounds.width * room.bounds.height);

                if (!assignment.satisfied)
                {
                    violations.Add(string.Format("Room {0}: assignment unsatisfied (function {1}).", i, assignment.function));
                }
                if (spec.minimumArea > area)
                {
                    violations.Add(string.Format("Room {0}: {1} requires area >= {2}, got {3}.", i, assignment.function, spec.minimumArea, area));
                }
                if (Mathf.Min(room.bounds.width, room.bounds.height) < spec.minimumSide)
                {
                    violations.Add(string.Format("Room {0}: {1} requires side >= {2}.", i, assignment.function, spec.minimumSide));
                }
                if (spec.requiresTopFloor && room.floorLevel != topFloor)
                {
                    violations.Add(string.Format("Room {0}: {1} must sit on top floor ({2}), got {3}.", i, assignment.function, topFloor, room.floorLevel));
                }
                if (spec.groundFloorOnly && room.floorLevel != 0)
                {
                    violations.Add(string.Format("Room {0}: {1} must sit on the ground floor.", i, assignment.function));
                }
                if (spec.minimumExits > 0 && !room.hasDoors)
                {
                    violations.Add(string.Format("WARNING: Room {0}: {1} expects at least {2} exit(s) but room has none.", i, spec.function, spec.minimumExits));
                }
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    if (rooms[i].floorLevel != rooms[j].floorLevel) continue;
                    if (!RoomsAdjacent(rooms[i].bounds, rooms[j].bounds, AdjacencyPadding)) continue;

                    var functionA = assignments[i].function;
                    var functionB = assignments[j].function;
                    if (AreFunctionsAdjacentForbidden(functionA, functionB))
                    {
                        violations.Add(string.Format("Rooms {0} ({1}) and {2} ({3}) are adjacent but incompatible.",
                            i, functionA, j, functionB));
                    }
                }
            }

            return violations;
        }

        /// <summary>
        /// Returns the expected lighting key for a function (used by scene builder selection).
        /// </summary>
        /// <param name="function">Room function.</param>
        /// <returns>Semantic lighting key.</returns>
        public static string GetExpectedLightingKey(RoomFunction function)
        {
            return GetSpec(function).lightingKey;
        }

        /// <summary>
        /// Returns the expected prop palette keys for a function (merged into scatter profiles).
        /// </summary>
        /// <param name="function">Room function.</param>
        /// <returns>Semantic prop keys.</returns>
        public static string[] GetExpectedPropKeys(RoomFunction function)
        {
            return GetSpec(function).propPaletteKeys;
        }

        private static IEnumerable<int> OrderedIndices(IReadOnlyList<Room> rooms)
        {
            var indices = new List<int>(rooms.Count);
            for (int i = 0; i < rooms.Count; i++) indices.Add(i);
            indices.Sort((a, b) =>
            {
                int floorCompare = rooms[a].floorLevel.CompareTo(rooms[b].floorLevel);
                if (floorCompare != 0) return floorCompare;
                int areaCompare = (rooms[b].bounds.width * rooms[b].bounds.height).CompareTo(rooms[a].bounds.width * rooms[a].bounds.height);
                if (areaCompare != 0) return areaCompare;
                int yCompare = rooms[a].bounds.yMin.CompareTo(rooms[b].bounds.yMin);
                if (yCompare != 0) return yCompare;
                return rooms[a].bounds.xMin.CompareTo(rooms[b].bounds.xMin);
            });
            return indices;
        }

        private static bool Contains(RoomFunction[] array, RoomFunction value)
        {
            if (array == null) return false;
            foreach (var entry in array)
            {
                if (entry == value) return true;
            }
            return false;
        }

        private static Dictionary<RoomFunction, RoomFunctionSpec> BuildSpecs()
        {
            var specs = new Dictionary<RoomFunction, RoomFunctionSpec>();

            void Add(RoomFunctionSpec spec) { specs[spec.function] = spec; }

            Add(new RoomFunctionSpec
            {
                function = RoomFunction.CommandPost,
                minimumArea = 12,
                minimumSide = 3,
                minimumExits = 2,
                desirability = 0.9f,
                requiresTopFloor = false,
                groundFloorOnly = false,
                lightingKey = "light.warm.command",
                propPaletteKeys = new[] { "prop.crate.ammo", "prop.sandbag.emplacement", "prop.crate.wood", "prop.barrel.rusty" },
                forbiddenNeighbors = new RoomFunction[0],
                preferredNeighbors = new[] { RoomFunction.Comms, RoomFunction.Stairwell }
            });

            Add(new RoomFunctionSpec
            {
                function = RoomFunction.Barracks,
                minimumArea = 10,
                minimumSide = 3,
                minimumExits = 1,
                desirability = 0.75f,
                requiresTopFloor = false,
                groundFloorOnly = false,
                lightingKey = "light.neutral.barracks",
                propPaletteKeys = new[] { "prop.crate.wood", "prop.furniture.pallet", "prop.barrel.rusty" },
                forbiddenNeighbors = new[] { RoomFunction.Industrial },
                preferredNeighbors = new[] { RoomFunction.Medical, RoomFunction.Stairwell }
            });

            Add(new RoomFunctionSpec
            {
                function = RoomFunction.Storage,
                minimumArea = 6,
                minimumSide = 2,
                minimumExits = 1,
                desirability = 0.6f,
                requiresTopFloor = false,
                groundFloorOnly = false,
                lightingKey = "light.dim.storage",
                propPaletteKeys = new[] { "prop.crate.wood", "prop.crate.ammo", "prop.pallet.cargo", "prop.barrel.fuel" },
                forbiddenNeighbors = new[] { RoomFunction.Medical, RoomFunction.Comms, RoomFunction.Residential },
                preferredNeighbors = new[] { RoomFunction.Industrial, RoomFunction.CommandPost }
            });

            Add(new RoomFunctionSpec
            {
                function = RoomFunction.Medical,
                minimumArea = 8,
                minimumSide = 3,
                minimumExits = 2,
                desirability = 0.65f,
                requiresTopFloor = false,
                groundFloorOnly = false,
                lightingKey = "light.clinical.white",
                propPaletteKeys = new[] { "prop.furniture.cabinet", "prop.barrel.medical", "prop.crate.wood" },
                forbiddenNeighbors = new[] { RoomFunction.Industrial, RoomFunction.Storage },
                preferredNeighbors = new[] { RoomFunction.Barracks, RoomFunction.Stairwell }
            });

            Add(new RoomFunctionSpec
            {
                function = RoomFunction.Comms,
                minimumArea = 6,
                minimumSide = 3,
                minimumExits = 1,
                desirability = 0.55f,
                requiresTopFloor = false,
                groundFloorOnly = false,
                lightingKey = "light.cool.server-blue",
                propPaletteKeys = new[] { "prop.equipment.radio-rack", "prop.crate.ammo", "prop.furniture.desk" },
                forbiddenNeighbors = new[] { RoomFunction.Industrial, RoomFunction.Storage },
                preferredNeighbors = new[] { RoomFunction.CommandPost, RoomFunction.Stairwell }
            });

            Add(new RoomFunctionSpec
            {
                function = RoomFunction.Residential,
                minimumArea = 6,
                minimumSide = 3,
                minimumExits = 1,
                desirability = 0.7f,
                requiresTopFloor = false,
                groundFloorOnly = false,
                lightingKey = "light.warm.domestic",
                propPaletteKeys = new[] { "prop.furniture.pallet", "prop.crate.wood", "prop.barrel.olive" },
                forbiddenNeighbors = new[] { RoomFunction.Industrial, RoomFunction.Storage },
                preferredNeighbors = new[] { RoomFunction.Residential }
            });

            Add(new RoomFunctionSpec
            {
                function = RoomFunction.Industrial,
                minimumArea = 14,
                minimumSide = 4,
                minimumExits = 1,
                desirability = 0.5f,
                requiresTopFloor = false,
                groundFloorOnly = true,
                lightingKey = "light.flicker.industrial",
                propPaletteKeys = new[] { "prop.crate.ammo", "prop.barrel.fuel", "prop.debris.rubble", "prop.vehicle.truck" },
                forbiddenNeighbors = new[] { RoomFunction.Medical, RoomFunction.Comms, RoomFunction.Barracks, RoomFunction.Residential },
                preferredNeighbors = new[] { RoomFunction.Storage }
            });

            Add(new RoomFunctionSpec
            {
                function = RoomFunction.Stairwell,
                minimumArea = 3,
                minimumSide = 2,
                minimumExits = 1,
                desirability = 0.85f,
                requiresTopFloor = false,
                groundFloorOnly = false,
                lightingKey = "light.util.stairwell",
                propPaletteKeys = new[] { "prop.debris.rubble", "prop.crate.wood" },
                forbiddenNeighbors = new RoomFunction[0],
                preferredNeighbors = new[] { RoomFunction.CommandPost, RoomFunction.Rooftop, RoomFunction.Barracks, RoomFunction.Medical, RoomFunction.Comms, RoomFunction.Storage, RoomFunction.Residential, RoomFunction.Industrial }
            });

            Add(new RoomFunctionSpec
            {
                function = RoomFunction.Rooftop,
                minimumArea = 9,
                minimumSide = 3,
                minimumExits = 1,
                desirability = 0.6f,
                requiresTopFloor = true,
                groundFloorOnly = false,
                lightingKey = "light.rooftop.sky",
                propPaletteKeys = new[] { "prop.vehicle.sedan", "prop.barrel.rusty", "prop.sandbag.emplacement", "prop.debris.rubble", "prop.foliage.bush" },
                forbiddenNeighbors = new RoomFunction[0],
                preferredNeighbors = new[] { RoomFunction.Stairwell }
            });

            return specs;
        }
    }
}
