using System;
using System.Collections.Generic;
using UnityEngine;

namespace Darkforest.Rooms
{
    /// <summary>
    /// Tile-to-tile movement for monsters and objects (the player moves freely and doesn't use this).
    /// The body always travels from one cell center to a neighboring cell center. A step can't be
    /// interrupted except by Pause. Cells are reserved, so two movers never share or enter the same
    /// tile. Movers stay on their home room's floor. Uses a kinematic Rigidbody2D, so walls never
    /// push it around; it still reports contacts with the dynamic player.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class GridMover : MonoBehaviour
    {
        private static readonly Dictionary<Vector3Int, GridMover> Reserved = new Dictionary<Vector3Int, GridMover>();

        [Tooltip("Falls back to the RoomMap in the scene.")]
        [SerializeField] private RoomMap map;

        [Tooltip("Keep this mover on the floor of the room it starts in.")]
        [SerializeField] private bool stayInHomeRoom = true;

        private Rigidbody2D rb;
        private Vector3Int fromCell;
        private float speed;

        public RoomMap Map => map;
        public Vector2Int HomeRoom { get; private set; }
        public bool HasRoom { get; private set; }

        /// <summary>The cell it stands on, or is moving into.</summary>
        public Vector3Int Cell { get; private set; }

        public bool IsMoving { get; private set; }
        public bool IsPaused { get; set; }

        /// <summary>Direction of the current (or last) step.</summary>
        public Vector2Int LastDirection { get; private set; }

        public event Action<Vector3Int> Arrived;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void Start()
        {
            if (map == null)
                map = FindObjectOfType<RoomMap>();
            if (map == null)
            {
                enabled = false;
                return;
            }

            // Snap onto the nearest cell center.
            Cell = map.WorldToCell(transform.position);
            fromCell = Cell;
            Vector3 c = map.CellCenterWorld(Cell);
            rb.position = c;
            transform.position = new Vector3(c.x, c.y, transform.position.z);
            Reserve(Cell);

            HasRoom = map.TryGetRoomAt(c, out var room);
            HomeRoom = room;
        }

        private void OnDisable()
        {
            Release(Cell);
            Release(fromCell);
        }

        public bool CanEnter(Vector3Int cell)
        {
            if (map == null)
                return false;
            if (stayInHomeRoom && HasRoom && !map.IsRoomFloorCell(HomeRoom, cell))
                return false;
            return !Reserved.TryGetValue(cell, out var owner) || owner == this || owner == null;
        }

        /// <summary>Starts a one-tile step. Fails while already moving or if the tile is blocked.</summary>
        public bool TryStep(Vector2Int dir, float stepSpeed)
        {
            if (IsMoving || dir == Vector2Int.zero)
                return false;

            var next = Cell + new Vector3Int(dir.x, dir.y, 0);
            if (!CanEnter(next))
                return false;

            fromCell = Cell;
            Cell = next;
            Reserve(next);
            speed = stepSpeed;
            LastDirection = dir;
            IsMoving = true;
            return true;
        }

        private void FixedUpdate()
        {
            if (!IsMoving || IsPaused)
                return;

            Vector2 target = map.CellCenterWorld(Cell);
            Vector2 pos = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
            rb.MovePosition(pos);

            if ((pos - target).sqrMagnitude < 1e-6f)
            {
                IsMoving = false;
                if (fromCell != Cell)
                    Release(fromCell);
                fromCell = Cell;
                Arrived?.Invoke(Cell);
            }
        }

        private void Reserve(Vector3Int cell) => Reserved[cell] = this;

        private void Release(Vector3Int cell)
        {
            if (Reserved.TryGetValue(cell, out var owner) && owner == this)
                Reserved.Remove(cell);
        }

        // Domain reload may be off in the Editor, so clear reservations between Play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reserved.Clear();
    }
}
