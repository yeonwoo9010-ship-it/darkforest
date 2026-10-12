using Darkforest.Player;
using Darkforest.Rooms;
using UnityEngine;

namespace Darkforest.Enemies
{
    /// <summary>
    /// "Dog": a charger. It wanders slowly around its room. When the player stands in the same tile
    /// row or column of the same room with nothing in between, it charges straight at them until it
    /// hits a wall. Touching the player costs them one HP and stuns the dog for stunDuration seconds.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Dog : MonoBehaviour
    {
        public enum State { Wander, Charge, Rest, Stunned }

        [Header("Wander")]
        [SerializeField] private float wanderSpeed = 1.5f;
        [SerializeField] private Vector2 wanderInterval = new Vector2(1f, 2.5f);

        [Header("Charge")]
        [SerializeField] private float chargeSpeed = 9f;
        [Tooltip("Pause after a charge ends on a wall before it can wander or charge again.")]
        [SerializeField] private float restAfterCharge = 0.6f;

        [Header("Hit")]
        [SerializeField] private int damage = 1;
        [SerializeField] private float stunDuration = 2f;
        [SerializeField] private Color stunnedTint = new Color(0.55f, 0.55f, 0.7f);

        [Tooltip("Falls back to the RoomMap in the scene.")]
        [SerializeField] private RoomMap map;

        private Rigidbody2D rb;
        private SpriteRenderer sr;
        private Color baseColor = Color.white;
        private Transform player;
        private Vector2Int homeRoom;
        private bool hasRoom;
        private Vector2 moveDir;
        private float stateTimer;

        public State Current { get; private set; } = State.Wander;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                baseColor = sr.color;
        }

        private void Start()
        {
            if (map == null)
                map = FindObjectOfType<RoomMap>();
            var p = FindObjectOfType<PlayerMovement>();
            if (p != null)
                player = p.transform;

            hasRoom = map != null && map.TryGetRoomAt(transform.position, out homeRoom);
            PickWanderDirection();
        }

        private void FixedUpdate()
        {
            stateTimer -= Time.fixedDeltaTime;

            switch (Current)
            {
                case State.Wander:
                    if (TryFindChargeDirection(out Vector2 dir))
                    {
                        Current = State.Charge;
                        moveDir = dir;
                        break;
                    }
                    if (stateTimer <= 0f || !CanMove(moveDir))
                        PickWanderDirection();
                    rb.velocity = moveDir * wanderSpeed;
                    break;

                case State.Charge:
                    if (!CanMove(moveDir))
                    {
                        EnterRest();
                        break;
                    }
                    rb.velocity = moveDir * chargeSpeed;
                    break;

                case State.Rest:
                case State.Stunned:
                    rb.velocity = Vector2.zero;
                    if (stateTimer <= 0f)
                    {
                        if (sr != null) sr.color = baseColor;
                        Current = State.Wander;
                        PickWanderDirection();
                    }
                    break;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision) => HandleContact(collision);
        private void OnCollisionStay2D(Collision2D collision) => HandleContact(collision);

        private void HandleContact(Collision2D collision)
        {
            if (Current == State.Stunned)
                return;

            var health = collision.collider.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                // Stun only on a hit that lands. Touching an invincible player just ends the charge,
                // otherwise the dog would keep re-stunning itself while the player stands next to it.
                if (health.TakeDamage(damage))
                    Stun();
                else if (Current == State.Charge)
                    EnterRest();
            }
            else if (Current == State.Charge)
            {
                EnterRest(); // ran into a wall or obstacle
            }
        }

        public void Stun()
        {
            Current = State.Stunned;
            stateTimer = stunDuration;
            rb.velocity = Vector2.zero;
            if (sr != null) sr.color = stunnedTint;
        }

        private void EnterRest()
        {
            Current = State.Rest;
            stateTimer = restAfterCharge;
            rb.velocity = Vector2.zero;
        }

        // The player must be in the same room, on the same tile row or column, with a clear line.
        private bool TryFindChargeDirection(out Vector2 dir)
        {
            dir = Vector2.zero;
            if (player == null || !hasRoom)
                return false;
            if (!map.TryGetRoomAt(player.position, out var playerRoom) || playerRoom != homeRoom)
                return false;

            var grid = map.GetComponent<Grid>();
            Vector3Int myCell = grid.WorldToCell(transform.position);
            Vector3Int playerCell = grid.WorldToCell(player.position);
            Vector3Int delta = playerCell - myCell;

            if (delta.x == 0 && delta.y != 0)
                dir = new Vector2(0f, Mathf.Sign(delta.y));
            else if (delta.y == 0 && delta.x != 0)
                dir = new Vector2(Mathf.Sign(delta.x), 0f);
            else
                return false;

            return HasClearLine(player.position);
        }

        private bool HasClearLine(Vector2 to)
        {
            foreach (var hit in Physics2D.LinecastAll(rb.position, to))
            {
                if (hit.rigidbody == rb || hit.collider.GetComponentInParent<PlayerHealth>() != null)
                    continue;
                if (hit.collider.isTrigger)
                    continue;
                return false;
            }
            return true;
        }

        // Keeps the dog on its own room's floor: it never walks or charges into a wall or a passage.
        private bool CanMove(Vector2 dir)
        {
            if (dir == Vector2.zero)
                return true;
            if (!hasRoom)
                return true;
            Vector2 front = rb.position + dir * 0.55f;
            return map.TryGetRoomAt(front, out var room) && room == homeRoom;
        }

        private void PickWanderDirection()
        {
            stateTimer = Random.Range(wanderInterval.x, wanderInterval.y);
            Vector2[] options = { Vector2.up, Vector2.down, Vector2.left, Vector2.right, Vector2.zero };
            for (int i = 0; i < 8; i++)
            {
                moveDir = options[Random.Range(0, options.Length)];
                if (CanMove(moveDir))
                    return;
            }
            moveDir = Vector2.zero;
        }
    }
}
