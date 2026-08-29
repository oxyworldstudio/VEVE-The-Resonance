using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace VEVE.AI
{
    public enum SquadTacticState { Idle, Moving, Attacking, Flanking, Suppressing, Defending, Retreating }

    [System.Serializable]
    public struct SquadMember
    {
        public string id;
        public Vector3 position;
        public float health;
        public float accuracy;
        public float aggression;
        public bool isLeader;
        public SquadRole role;
    }

    public enum SquadRole { Leader, Assault, Support, Marksman, Medic }

    public class SquadManager : MonoBehaviour
    {
        [SerializeField] private int squadSize = 4;
        [SerializeField] private float squadSpacing = 3f;
        [SerializeField] private float regroupDistance = 10f;
        [SerializeField] private float suppressionThreshold = 0.3f;

        private List<SquadMember> members;
        private Vector3 squadCenter;
        private SquadTacticState currentTactic;
        private Vector3 targetPosition;
        private float averageAggression;

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
        }

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
            else
            {
                currentTactic = SquadTacticState.Attacking;
            }

            ExecuteTactic(playerPosition);
        }

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
            foreach (var member in members)
            {
                Vector3 offset = (member.position - squadCenter).normalized * squadSpacing;
                Vector3 moveTarget = targetPos + offset;
                // Movement would be handled by NavMeshAgent or custom movement
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

        public void ReportEnemySighted(Vector3 position)
        {
            currentTactic = SquadTacticState.Attacking;
            targetPosition = position;
        }

        public int AliveCount => members.Count;
        public SquadTacticState CurrentTactic => currentTactic;
        public Vector3 SquadCenter => squadCenter;
    }
}
