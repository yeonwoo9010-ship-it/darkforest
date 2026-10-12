using Darkforest.Player;
using Darkforest.Rooms;
using UnityEngine;

namespace Darkforest.CameraSystem
{
    /// <summary>
    /// Isaac-style room camera movement: the camera stays centered on the room the player is in
    /// and slides to the next room once the player steps onto its floor.
    /// </summary>
    public class RoomCameraFollow : MonoBehaviour
    {
        [SerializeField] private RoomMap map;

        [Tooltip("What to track. Falls back to the PlayerMovement in the scene.")]
        [SerializeField] private Transform target;

        [Tooltip("Seconds the camera takes to slide to a neighboring room.")]
        [SerializeField] private float slideDuration = 0.3f;

        [Tooltip("Cover everything outside the current room (Isaac shows only one room at a time).")]
        [SerializeField] private bool maskOutsideRoom = true;

        [SerializeField] private Color maskColor = Color.black;

        public Vector2Int CurrentRoom { get; private set; }

        /// <summary>Raised when the player enters a different room (old room, new room).</summary>
        public event System.Action<Vector2Int, Vector2Int> RoomChanged;

        private void Start()
        {
            if (map == null)
                map = FindObjectOfType<RoomMap>();
            if (target == null)
            {
                var player = FindObjectOfType<PlayerMovement>();
                if (player != null)
                    target = player.transform;
            }
            if (map == null || target == null)
            {
                enabled = false;
                return;
            }

            CurrentRoom = map.TryGetRoomAt(target.position, out var room) ? room : map.StartRoom;
            transform.position = CameraPositionFor(CurrentRoom);

            if (maskOutsideRoom)
                CreateMask();
        }

        // Four black bars around a window the size of one room plus its walls. They are children of the
        // camera, so the window lines up with the room once the camera settles.
        private void CreateMask()
        {
            var tex = Texture2D.whiteTexture;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);

            float window = (map.RoomSize + 2) * map.transform.lossyScale.x;
            const float bar = 100f;
            float offset = (window + bar) * 0.5f;

            CreateBar(sprite, new Vector2(-offset, 0f), new Vector2(bar, window + 2f * bar));
            CreateBar(sprite, new Vector2(offset, 0f), new Vector2(bar, window + 2f * bar));
            CreateBar(sprite, new Vector2(0f, offset), new Vector2(window, bar));
            CreateBar(sprite, new Vector2(0f, -offset), new Vector2(window, bar));
        }

        private void CreateBar(Sprite sprite, Vector2 localPos, Vector2 size)
        {
            var go = new GameObject("Room Mask");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(localPos.x, localPos.y, 1f); // in front of the near clip plane
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = maskColor;
            sr.sortingOrder = 1000;
        }

        private void LateUpdate()
        {
            if (map.TryGetRoomAt(target.position, out var room) && room != CurrentRoom)
            {
                var previous = CurrentRoom;
                CurrentRoom = room;
                RoomChanged?.Invoke(previous, room);
            }

            Vector3 goal = CameraPositionFor(CurrentRoom);
            float speed = map.Pitch * map.transform.lossyScale.x / Mathf.Max(0.01f, slideDuration);
            transform.position = Vector3.MoveTowards(transform.position, goal, speed * Time.deltaTime);
        }

        private Vector3 CameraPositionFor(Vector2Int room)
        {
            Vector3 center = map.RoomCenterWorld(room);
            return new Vector3(center.x, center.y, transform.position.z);
        }
    }
}
