using UnityEngine;

namespace Darkforest.Rooms
{
    /// <summary>
    /// Layout of the room grid: columns x rows rooms, each roomSize x roomSize floor tiles.
    /// Neighboring rooms share a 1-tile wall, so rooms repeat every roomSize + 1 cells. A 1-tile
    /// passage sits in the middle of every shared wall; the outer walls are closed.
    /// The start room's center cell is (0, 0).
    /// Lives on the Grid object that holds the Floor and Walls tilemaps.
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public class RoomMap : MonoBehaviour
    {
        [SerializeField] private int columns = 7;
        [SerializeField] private int rows = 7;
        [SerializeField] private int roomSize = 7;
        [SerializeField] private Vector2Int startRoom = new Vector2Int(3, 3);

        private Grid grid;

        public int Columns => columns;
        public int Rows => rows;
        public int RoomSize => roomSize;
        public Vector2Int StartRoom => startRoom;

        /// <summary>Distance in cells between the centers of neighboring rooms.</summary>
        public int Pitch => roomSize + 1;

        private int Half => roomSize / 2;

        private Grid Grid => grid != null ? grid : grid = GetComponent<Grid>();

        public bool IsValidRoom(Vector2Int room) =>
            room.x >= 0 && room.x < columns && room.y >= 0 && room.y < rows;

        public Vector3Int RoomCenterCell(Vector2Int room) =>
            new Vector3Int((room.x - startRoom.x) * Pitch, (room.y - startRoom.y) * Pitch, 0);

        public Vector3 RoomCenterWorld(Vector2Int room) => Grid.GetCellCenterWorld(RoomCenterCell(room));

        /// <summary>The room whose cell block (floor plus its lower/left wall) contains the cell.</summary>
        public Vector2Int RoomOfCell(Vector3Int cell) => new Vector2Int(
            FloorDiv(cell.x + Half + 1, Pitch) + startRoom.x,
            FloorDiv(cell.y + Half + 1, Pitch) + startRoom.y);

        /// <summary>True only when the position is on a room's floor (not in a wall or a passage).</summary>
        public bool TryGetRoomAt(Vector3 worldPosition, out Vector2Int room)
        {
            Vector3Int cell = Grid.WorldToCell(worldPosition);
            room = RoomOfCell(cell);
            if (!IsValidRoom(room))
                return false;

            Vector3Int local = cell - RoomCenterCell(room);
            return Mathf.Abs(local.x) <= Half && Mathf.Abs(local.y) <= Half;
        }

        public Vector3Int WorldToCell(Vector3 worldPosition) => Grid.WorldToCell(worldPosition);

        public Vector3 CellCenterWorld(Vector3Int cell) => Grid.GetCellCenterWorld(cell);

        /// <summary>True if the cell is on the floor of the given room (walls and passages excluded).</summary>
        public bool IsRoomFloorCell(Vector2Int room, Vector3Int cell)
        {
            if (!IsValidRoom(room))
                return false;
            Vector3Int local = cell - RoomCenterCell(room);
            return Mathf.Abs(local.x) <= Half && Mathf.Abs(local.y) <= Half;
        }

        /// <summary>Whether the room has a passage toward dir (up/down/left/right).</summary>
        public bool HasPassage(Vector2Int room, Vector2Int dir) => IsValidRoom(room) && IsValidRoom(room + dir);

        private static int FloorDiv(int a, int b) => Mathf.FloorToInt((float)a / b);

        private void OnValidate()
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            roomSize = Mathf.Max(3, roomSize | 1); // odd, so every wall has a middle tile
            startRoom = new Vector2Int(Mathf.Clamp(startRoom.x, 0, columns - 1), Mathf.Clamp(startRoom.y, 0, rows - 1));
        }
    }
}
