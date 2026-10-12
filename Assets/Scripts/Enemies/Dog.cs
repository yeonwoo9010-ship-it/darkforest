using Darkforest.Player;
using Darkforest.Rooms;
using UnityEngine;

namespace Darkforest.Enemies
{
    /// <summary>
    /// "Dog": a charger that moves tile to tile (GridMover).
    /// It wanders one tile at a time around its room. If it spots the player on the same tile row or
    /// column (same room, clear line), it first finishes the tile it is moving into, then charges in
    /// that direction tile by tile until the next tile is blocked (wall, room edge or another mover).
    /// A charge that hits the player costs them one HP and stuns the dog for stunDuration seconds.
    /// </summary>
    [RequireComponent(typeof(GridMover))]
    public class Dog : MonoBehaviour
    {
        public enum State { Wander, Charge, Rest, Stunned }

        [Header("Wander")]
        [Tooltip("Tiles per second while wandering.")]
        [SerializeField] private float wanderSpeed = 2f;
        [Tooltip("Random pause between wander steps (seconds).")]
        [SerializeField] private Vector2 wanderPause = new Vector2(0.3f, 1.2f);

        [Header("Charge")]
        [Tooltip("Tiles per second while charging.")]
        [SerializeField] private float chargeSpeed = 9f;
        [Tooltip("Pause after a charge ends before it can wander or charge again.")]
        [SerializeField] private float restAfterCharge = 0.6f;

        [Header("Hit")]
        [SerializeField] private int damage = 1;
        [SerializeField] private float stunDuration = 2f;
        [SerializeField] private Color stunnedTint = new Color(0.55f, 0.55f, 0.7f);

        private GridMover mover;
        private SpriteRenderer sr;
        private Color baseColor = Color.white;
        private Transform player;
        private float timer;

        // Direction spotted while mid-step; the charge starts once the step is finished.
        private Vector2Int? pendingCharge;
        private Vector2Int chargeDir;

        public State Current { get; private set; } = State.Wander;

        private void Awake()
        {
            mover = GetComponent<GridMover>();
            sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                baseColor = sr.color;
        }

        private void Start()
        {
            var p = FindObjectOfType<PlayerMovement>();
            if (p != null)
                player = p.transform;
            timer = Random.Range(wanderPause.x, wanderPause.y);
        }

        private void FixedUpdate()
        {
            timer -= Time.fixedDeltaTime;

            switch (Current)
            {
                case State.Wander: UpdateWander(); break;
                case State.Charge: UpdateCharge(); break;
                case State.Rest:
                case State.Stunned:
                    if (timer <= 0f)
                    {
                        mover.IsPaused = false;
                        if (sr != null) sr.color = baseColor;
                        Current = State.Wander;
                        timer = Random.Range(wanderPause.x, wanderPause.y);
                    }
                    break;
            }
        }

        private void UpdateWander()
        {
            if (mover.IsMoving)
            {
                // Spotted mid-step: remember it, finish this tile first.
                if (pendingCharge == null && TryFindChargeDirection(out var spotted))
                    pendingCharge = spotted;
                return;
            }

            // Standing on a tile center.
            if (pendingCharge == null && TryFindChargeDirection(out var dir))
                pendingCharge = dir;

            if (pendingCharge != null)
            {
                chargeDir = pendingCharge.Value;
                pendingCharge = null;
                Current = State.Charge;
                UpdateCharge();
                return;
            }

            if (timer > 0f)
                return;
            timer = Random.Range(wanderPause.x, wanderPause.y);

            Vector2Int[] options = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            for (int i = 0; i < 4; i++)
            {
                if (mover.TryStep(options[Random.Range(0, options.Length)], wanderSpeed))
                    return;
            }
        }

        private void UpdateCharge()
        {
            if (mover.IsMoving)
                return;
            if (!mover.TryStep(chargeDir, chargeSpeed))
                EnterRest(); // next tile is a wall, the room edge or another mover
        }

        private void OnCollisionEnter2D(Collision2D collision) => HandleContact(collision);
        private void OnCollisionStay2D(Collision2D collision) => HandleContact(collision);

        private void HandleContact(Collision2D collision)
        {
            if (Current == State.Stunned)
                return;

            var health = collision.collider.GetComponentInParent<PlayerHealth>();
            if (health != null && health.TakeDamage(damage))
                Stun();
        }

        /// <summary>Stops on the spot for stunDuration, then finishes its tile and wanders again.</summary>
        public void Stun()
        {
            Current = State.Stunned;
            timer = stunDuration;
            pendingCharge = null;
            mover.IsPaused = true;
            if (sr != null) sr.color = stunnedTint;
        }

        private void EnterRest()
        {
            Current = State.Rest;
            timer = restAfterCharge;
        }

        // The player must be in the dog's room, on the same tile row or column, with a clear line.
        private bool TryFindChargeDirection(out Vector2Int dir)
        {
            dir = Vector2Int.zero;
            RoomMap map = mover.Map;
            if (player == null || map == null || !mover.HasRoom)
                return false;
            if (!map.TryGetRoomAt(player.position, out var playerRoom) || playerRoom != mover.HomeRoom)
                return false;

            // Measured from the tile the dog stands on (or is leaving) toward the player's tile.
            Vector3Int myCell = map.WorldToCell(transform.position);
            Vector3Int delta = map.WorldToCell(player.position) - myCell;

            if (delta.x == 0 && delta.y != 0)
                dir = new Vector2Int(0, delta.y > 0 ? 1 : -1);
            else if (delta.y == 0 && delta.x != 0)
                dir = new Vector2Int(delta.x > 0 ? 1 : -1, 0);
            else
                return false;

            return HasClearLine(player.position);
        }

        private bool HasClearLine(Vector2 to)
        {
            foreach (var hit in Physics2D.LinecastAll(transform.position, to))
            {
                if (hit.transform == transform || hit.collider.isTrigger)
                    continue;
                if (hit.collider.GetComponentInParent<PlayerHealth>() != null)
                    continue;
                return false;
            }
            return true;
        }
    }
}
