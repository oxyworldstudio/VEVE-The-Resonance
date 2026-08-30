using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace VEVE.AI
{
    /// <summary>
    /// Represents the current tactical state of the squad.
    /// </summary>
    public enum SquadTacticState { Idle, Moving, Attacking, Flanking, Suppressing, Defending, Retreating, BoundingOverwatch }

    /// <summary>
    /// Represents the available squad formations.
    /// </summary>
    public enum FormationType { Column, Line, Wedge, Diamond }

    /// <summary>
    /// Data container for squad member state.
    /// </summary>
    [System.Serializable]
    public struct SquadMember
    {
        /// <summary>Unique identifier for the squad member.</summary>
        public string id;

        /// <summary>Current world position.</summary>
        public Vector3 position;

        /// <summary>Current health value.</summary>
        public float health;

        /// <summary>Weapon accuracy modifier.</summary>
        public float accuracy;

        /// <summary>Aggression modifier.</summary>
        public float aggression;

        /// <summary>True if this member is the squad leader.</summary>
        public bool isLeader;

        /// <summary>Assigned combat role.</summary>
        public SquadRole role;

        /// <summary>True if this member is currently providing covering fire.</summary>
        public bool isCovering;

        /// <summary>Target position for bounding movement.</summary>
        public Vector3 boundingTarget;
    }

    /// <summary>
    /// Represents the role of a squad member.
    /// </summary>
    public enum SquadRole { Leader, Assault, Support, Marksman, Medic }

    /// <summary>
    /// Manages squad behavior including formations and bounding overwatch.
    /// </summary>
    public class SquadManager : MonoBehaviour
    {
        [SerializeField] private int squadSize = 4;
        [SerializeField] private float squadSpacing = 3f;
        [SerializeField] private float regroupDistance = 10f;
        [SerializeField] private float suppressionThreshold = 0.3f;
        [SerializeField] private FormationType formation = FormationType.Column;

        private List<SquadMember> members;
        private Vector3 squadCenter;
        private SquadTacticState currentTactic;
        private Vector3 targetPosition;
        private float averageAggression;
        private int boundingIndex;
        private float boundingCooldown;

        /// <summary>
        /// Initializes the squad with the given spawn points.
        /// </summary>
        /// <param name="spawnPoints">List of positions to spawn squad members.</param>
        public void InitializeSquad(List<Vector3> spawnPoints)
        {
            members = new List<SquadMember>();
            for (int i = 0; i < squadSize && i < spawnPoints.Count; i++)
            {
                members.Add(new SquadMember
                {
                    id = $"SquadMember_{i}",
                    position = spawnPoints[i],
                    health = 100f,
                    accuracy = Random.Range(0.4f, 0.9f),
                    aggression = Random.Range(0.3f, 0.9f),
                    isLeader = i == 0,
                    role = (SquadRole)i
                });
            }

            squadCenter = new Vector3(members.Average(m => m.position.x), 0f, members.Average(m => m.position.z));
            averageAggression = members.Average(m => m.aggression);
            currentTactic = SquadTacticState.Idle;
            boundingIndex = 0;
        }

        /// <summary>
        /// Updates squad state based on player position and known enemies.
        /// </summary>
        /// <param name="playerPosition">Current player position.</param>
        /// <param name="knownEnemies">List of known enemy positions.</param>
        public void UpdateSquad(Vector3 playerPosition, List<Vector3> knownEnemies)
        {
            squadCenter = new Vector3(members.Average(m => m.position.x), 0f, members.Average(m => m.position.z));

            if (knownEnemies.Count == 0)
            {
                currentTactic = SquadTacticState.Idle;
                return;
            }

            targetPosition = knownEnemies.OrderBy(e => Vector3.Distance(e, squadCenter)).FirstOrDefault();

            float avgHealth = members.Average(m => m.health);
            if (avgHealth < suppressionThreshold)
            {
                currentTactic = SquadTacticState.Retreating;
                return;
            }

            float distanceToTarget = Vector3.Distance(squadCenter, targetPosition);

            if (distanceToTarget > 30f)
            {
                currentTactic = SquadTacticState.Moving;
            }
            else if (averageAggression > 0.6f && distanceToTarget > 15f)
            {
                currentTactic = SquadTacticState.Flanking;
            }
            else if (members.Count(m => m.role == SquadRole.Support) > 0 && distanceToTarget < 25f)
            {
                currentTactic = SquadTacticState.Suppressing;
            }
            else if (distanceToTarget < 20f && members.Count >= 3)
            {
                currentTactic = SquadTacticState.BoundingOverwatch;
            }
            else
            {
                currentTactic = SquadTacticState.Attacking;
            }

            ExecuteTactic(playerPosition);
        }

        /// <summary>
        /// Sets the current squad formation.
        /// </summary>
        /// <param name="newFormation">The formation type to use.</param>
        public void SetFormation(FormationType newFormation)
        {
            formation = newFormation;
        }

        /// <summary>
        /// Reports damage to a squad member.
        /// </summary>
        /// <param name="memberId">The ID of the member taking damage.</param>
        /// <param name="damage">Amount of damage dealt.</param>
        /// <param name="sourcePosition">Position of the damage source.</param>
        public void ReportDamage(string memberId, float damage, Vector3 sourcePosition)
        {
            int memberIndex = members.FindIndex(m => m.id == memberId);
            if (memberIndex >= 0)
            {
                var member = members[memberIndex];
                if (member.health > 0)
                {
                    member.health -= damage;
                    members[memberIndex] = member;
                    if (member.health <= 0)
                    {
                        members.RemoveAt(memberIndex);
                    }
                }

                if (member.isLeader && members.Count > 0)
                {
                    var newLeader = members[0];
                    newLeader.isLeader = true;
                    members[0] = newLeader;
                }
            }
        }

        /// <summary>
        /// Reports an enemy sighting to the squad.
        /// </summary>
        /// <param name="position">Position where the enemy was sighted.</param>
        public void ReportEnemySighted(Vector3 position)
        {
            currentTactic = SquadTacticState.Attacking;
            targetPosition = position;
        }

        /// <summary>Gets the number of alive squad members.</summary>
        public int AliveCount => members.Count;

        /// <summary>Gets the current tactical state.</summary>
        public SquadTacticState CurrentTactic => currentTactic;

        /// <summary>Gets the center of the squad.</summary>
        public Vector3 SquadCenter => squadCenter;

        /// <summary>Gets the current formation type.</summary>
        public FormationType CurrentFormation => formation;

        /// <summary>Gets the list of squad members.</summary>
        public List<SquadMember> Members => members;

        private void ExecuteTactic(Vector3 playerPosition)
        {
            switch (currentTactic)
            {
                case SquadTacticState.Idle:
                    IdleBehavior();
                    break;
                case SquadTacticState.Moving:
                    MoveBehavior(playerPosition);
                    break;
                case SquadTacticState.Attacking:
                    AttackBehavior();
                    break;
                case SquadTacticState.Flanking:
                    FlankBehavior(playerPosition);
                    break;
                case SquadTacticState.Suppressing:
                    SuppressBehavior();
                    break;
                case SquadTacticState.Defending:
                    DefendBehavior();
                    break;
                case SquadTacticState.Retreating:
                    RetreatBehavior();
                    break;
                case SquadTacticState.BoundingOverwatch:
                    BoundingOverwatchBehavior();
                    break;
            }
        }

        private void IdleBehavior()
        {
            foreach (var member in members)
            {
                // Patrol behavior handled by individual AI
            }
        }

        private void MoveBehavior(Vector3 targetPos)
        {
            List<Vector3> formationOffsets = CalculateFormationOffsets();
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                Vector3 moveTarget = targetPos + formationOffsets[i];
                // Movement would be handled by NavMeshAgent or custom movement
                member.position = Vector3.MoveTowards(member.position, moveTarget, 3f * Time.deltaTime);
                members[i] = member;
            }
        }

        private void AttackBehavior()
        {
            foreach (var member in members)
            {
                if (member.role == SquadRole.Assault || member.role == SquadRole.Marksman)
                {
                    // Direct attack on target
                }
            }
        }

        private void FlankBehavior(Vector3 playerPos)
        {
            var flanker = members.FirstOrDefault(m => m.role == SquadRole.Assault);
            if (flanker.health > 0)
            {
                Vector3 flankDirection = Vector3.Cross((targetPosition - squadCenter).normalized, Vector3.up);
                // Move flanker to flank position
            }
        }

        private void SuppressBehavior()
        {
            var supporter = members.FirstOrDefault(m => m.role == SquadRole.Support);
            if (supporter.health > 0)
            {
                // Lay suppressive fire
            }
        }

        private void DefendBehavior()
        {
            foreach (var member in members)
            {
                // Take defensive positions
            }
        }

        private void RetreatBehavior()
        {
            Vector3 retreatDirection = (squadCenter - targetPosition).normalized;
            foreach (var member in members)
            {
                Vector3 retreatPos = squadCenter + retreatDirection * 20f;
                // Move to retreat position
            }
        }

        private void BoundingOverwatchBehavior()
        {
            boundingCooldown -= Time.deltaTime;
            if (boundingCooldown <= 0f)
            {
                boundingCooldown = 3f;
                boundingIndex = (boundingIndex + 1) % members.Count;
            }

            List<Vector3> formationOffsets = CalculateFormationOffsets();
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (i == boundingIndex)
                {
                    member.isCovering = false;
                    member.boundingTarget = targetPosition + formationOffsets[i];
                    // Move bounding element
                }
                else
                {
                    member.isCovering = true;
                    member.boundingTarget = targetPosition + formationOffsets[i];
                    // Provide covering fire
                }
                members[i] = member;
            }
        }

        private List<Vector3> CalculateFormationOffsets()
        {
            List<Vector3> offsets = new List<Vector3>();
            int count = members.Count;

            switch (formation)
            {
                case FormationType.Column:
                    for (int i = 0; i < count; i++)
                        offsets.Add(new Vector3(0f, 0f, -i * squadSpacing));
                    break;

                case FormationType.Line:
                    float lineWidth = (count - 1) * squadSpacing * 0.5f;
                    for (int i = 0; i < count; i++)
                        offsets.Add(new Vector3(i * squadSpacing - lineWidth, 0f, 0f));
                    break;

                case FormationType.Wedge:
                    for (int i = 0; i < count; i++)
                    {
                        float z = -i * squadSpacing;
                        float x = i * squadSpacing * 0.5f;
                        if (i % 2 == 1) x = -x;
                        offsets.Add(new Vector3(x, 0f, z));
                    }
                    break;

                case FormationType.Diamond:
                    if (count >= 1) offsets.Add(Vector3.zero);
                    if (count >= 2) offsets.Add(new Vector3(-squadSpacing, 0f, -squadSpacing));
                    if (count >= 3) offsets.Add(new Vector3(squadSpacing, 0f, -squadSpacing));
                    if (count >= 4) offsets.Add(new Vector3(0f, 0f, -squadSpacing * 2f));
                    break;
            }

            return offsets;
        }
    }
}
