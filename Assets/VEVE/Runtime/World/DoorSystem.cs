using UnityEngine;
using VEVE.World;

namespace VEVE.World
{
    /// <summary>
    /// Interactable door with lock model + kick/pick/breach paths. Interaction is E-driven
    /// through <see cref="Interact"/> (call from a raycast "use" system); all decisions are
    /// delegated to <see cref="DoorModel"/> so the physics stays testable.
    /// </summary>
    public sealed class DoorSystem : MonoBehaviour
    {
        [SerializeField] private DoorState state = DoorState.Closed;
        [SerializeField] private int lockLevel = 1;
        [SerializeField] private float integrity = 100f;
        [SerializeField] private float openAngleDegrees = 95f;

        public DoorState State => state;
        public int LockLevel => lockLevel;
        public float Integrity => integrity;

        /// <summary>Kick attempt. Returns true on a breach transition; emits loud noise always.</summary>
        public bool Kick()
        {
            if (state == DoorState.Open || state == DoorState.Breached) return false;
            float damage = DoorModel.KickDamage(lockLevel);
            integrity = Mathf.Max(0f, integrity - damage);
            TacticalSound.Emit(transform.position, DoorModel.KickNoiseLoudness);
            DoorState next = DoorModel.ResolveKick(state, integrity, state != DoorState.Locked);
            bool breached = next == DoorState.Breached;
            if (breached)
            {
                state = next;
                SwingToOpen();
            }
            return breached;
        }

        /// <summary>Instant unjam of a simple lock: only for lockLevel==0 doors (jiggle).</summary>
        public bool TryJiggle()
        {
            if (state != DoorState.Locked || lockLevel > 0) return false;
            state = DoorState.Closed;
            return true;
        }

        /// <summary>Complete a pick (call after the UI timer has elapsed with <see cref="PickSeconds"/>).</summary>
        public bool PickComplete()
        {
            if (state != DoorState.Locked) return false;
            state = DoorState.Closed;
            return true;
        }

        public float PickSeconds(bool hasKit) => DoorModel.PickSeconds(lockLevel, hasKit);

        /// <summary>Explosive breach: chargeKg of C4.</summary>
        public bool Breach(float chargeKg)
        {
            if (state == DoorState.Open || state == DoorState.Breached) return false;
            integrity = Mathf.Max(0f, integrity - DoorModel.BreachDamage(chargeKg));
            state = DoorState.Breached;
            SwingToOpen();
            return true;
        }

        /// <summary>Open an unlocked (Closed) door.</summary>
        public bool Open()
        {
            switch (state)
            {
                case DoorState.Open: return true;
                case DoorState.Closed:
                    state = DoorState.Open;
                    SwingToOpen();
                    return true;
                default: return false;
            }
        }

        /// <summary>Close an open door (a breached one cannot be closed again).</summary>
        public bool Close()
        {
            if (state != DoorState.Open) return false;
            state = DoorState.Closed;
            transform.rotation = Quaternion.Euler(transform.eulerAngles.x, baseYaw, transform.eulerAngles.z);
            return true;
        }

        private float baseYaw;

        private void Awake()
        {
            baseYaw = transform.eulerAngles.y;
        }

        private void SwingToOpen()
        {
            transform.rotation = Quaternion.Euler(transform.eulerAngles.x, baseYaw + openAngleDegrees, transform.eulerAngles.z);
        }

        /// <summary>E key context prompt text (used by the interact system / HUD).</summary>
        public string Prompt
        {
            get
            {
                switch (state)
                {
                    case DoorState.Locked: return lockLevel == 0 ? "E jiggle / R rotate / kick" : "E pick (" + PickSeconds(false).ToString("0.0") + "s)";
                    case DoorState.Closed: return "E open";
                    default: return string.Empty;
                }
            }
        }
    }
}
