using Darkforest.Player;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Darkforest.Rooms
{
    /// <summary>
    /// Draws a faint white outline around the floor tile the player is standing on.
    /// The outline sprite is generated at runtime, so no art asset is needed.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class TileHighlighter : MonoBehaviour
    {
        [Tooltip("Tilemap whose cells are highlighted. Only cells that have a tile are shown.")]
        [SerializeField] private Tilemap floor;

        [Tooltip("What to track. Falls back to the PlayerMovement in the scene.")]
        [SerializeField] private Transform target;

        [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 0.35f);

        [Tooltip("Outline thickness in sprite pixels (32 px = 1 tile).")]
        [SerializeField, Range(1, 4)] private int thickness = 1;

        private const int Pixels = 32;

        private SpriteRenderer sr;
        private Vector3Int currentCell = new Vector3Int(int.MinValue, int.MinValue, 0);

        /// <summary>Cell the target is on, or null when it isn't over a floor tile.</summary>
        public Vector3Int? CurrentCell => sr != null && sr.enabled ? currentCell : (Vector3Int?)null;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            sr.sprite = CreateOutlineSprite(thickness);
            sr.color = outlineColor;
            sr.enabled = false;

            if (target == null)
            {
                var player = FindObjectOfType<PlayerMovement>();
                if (player != null)
                    target = player.transform;
            }
        }

        // LateUpdate so it follows the interpolated player position of this frame.
        private void LateUpdate()
        {
            if (floor == null || target == null)
            {
                sr.enabled = false;
                return;
            }

            Vector3Int cell = floor.WorldToCell(target.position);
            bool onFloor = floor.HasTile(cell);
            sr.enabled = onFloor;
            if (!onFloor || cell == currentCell)
                return;

            currentCell = cell;
            Vector3 center = floor.GetCellCenterWorld(cell);
            transform.position = new Vector3(center.x, center.y, transform.position.z);
        }

        private static Sprite CreateOutlineSprite(int thickness)
        {
            var tex = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "TileOutline"
            };

            var pixels = new Color32[Pixels * Pixels];
            for (int y = 0; y < Pixels; y++)
            {
                for (int x = 0; x < Pixels; x++)
                {
                    bool border = x < thickness || y < thickness || x >= Pixels - thickness || y >= Pixels - thickness;
                    pixels[y * Pixels + x] = border ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, Pixels, Pixels), new Vector2(0.5f, 0.5f), Pixels);
        }
    }
}
