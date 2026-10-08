using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Darkforest.EditorTools
{
    /// <summary>
    /// "Darkforest/Create Room": builds the basic stage room in SampleScene, a 7x7 floor surrounded
    /// by a 1-tile wall ring (9x9 cells), centered on the origin. Safe to run repeatedly.
    /// Batchmode: -executeMethod Darkforest.EditorTools.RoomSetup.CreateRoom
    /// </summary>
    public static class RoomSetup
    {
        private const int FloorSize = 7;
        private const int TileSize = 32;

        private const string TileArtDir = "Assets/Art/Tiles";
        private const string FloorSpritePath = TileArtDir + "/floor.png";
        private const string WallSpritePath = TileArtDir + "/wall.png";
        private const string FloorTilePath = TileArtDir + "/FloorTile.asset";
        private const string WallTilePath = TileArtDir + "/WallTile.asset";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RoomName = "Room";

        [MenuItem("Darkforest/Create Room")]
        public static void CreateRoom()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory(TileArtDir);
            Sprite floorSprite = EnsureSprite(FloorSpritePath, DrawFloor);
            Sprite wallSprite = EnsureSprite(WallSpritePath, DrawWall);
            Tile floorTile = EnsureTile(FloorTilePath, floorSprite, Tile.ColliderType.None);
            Tile wallTile = EnsureTile(WallTilePath, wallSprite, Tile.ColliderType.Grid);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == RoomName)
                    Object.DestroyImmediate(root);
            }

            BuildRoom(floorTile, wallTile);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RoomSetup] Built {FloorSize}x{FloorSize} room in {ScenePath}.");
        }

        private static void BuildRoom(Tile floorTile, Tile wallTile)
        {
            // Cells span [-half-1, half+1]; shifting the grid by half a cell centers the room on the origin.
            var room = new GameObject(RoomName, typeof(Grid));
            room.transform.position = new Vector3(-0.5f, -0.5f, 0f);

            Tilemap floor = CreateTilemap(room.transform, "Floor", -10);
            Tilemap walls = CreateTilemap(room.transform, "Walls", -5);

            int half = FloorSize / 2;
            for (int x = -half - 1; x <= half + 1; x++)
            {
                for (int y = -half - 1; y <= half + 1; y++)
                {
                    bool isWall = Mathf.Abs(x) == half + 1 || Mathf.Abs(y) == half + 1;
                    (isWall ? walls : floor).SetTile(new Vector3Int(x, y, 0), isWall ? wallTile : floorTile);
                }
            }

            // One merged static collider for the walls, so the player slides along them smoothly.
            var wallCollider = walls.gameObject.AddComponent<TilemapCollider2D>();
            wallCollider.usedByComposite = true;
            var rb = walls.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var composite = walls.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

            floor.CompressBounds();
            walls.CompressBounds();
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            return go.GetComponent<Tilemap>();
        }

        private static Tile EnsureTile(string path, Sprite sprite, Tile.ColliderType colliderType)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.colliderType = colliderType;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        private static Sprite EnsureSprite(string path, System.Func<Texture2D> draw)
        {
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, draw().EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TileSize;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Dark basement stone: speckled brown with a faint darker edge.
        private static Texture2D DrawFloor()
        {
            var rng = new System.Random(7);
            return Draw((x, y) =>
            {
                float n = (float)rng.NextDouble() * 0.06f;
                float shade = x == 0 || y == 0 ? 0.85f : 1f;
                return new Color((0.27f + n) * shade, (0.21f + n) * shade, (0.18f + n) * shade);
            });
        }

        // Grey bricks: 8px rows, staggered joints, dark mortar.
        private static Texture2D DrawWall()
        {
            var rng = new System.Random(13);
            return Draw((x, y) =>
            {
                int row = y / 8;
                int offset = row % 2 == 0 ? 0 : 8;
                bool mortar = y % 8 == 0 || (x + offset) % 16 == 0;
                float n = (float)rng.NextDouble() * 0.05f;
                return mortar
                    ? new Color(0.10f, 0.09f, 0.10f)
                    : new Color(0.36f + n, 0.34f + n, 0.36f + n);
            });
        }

        private static Texture2D Draw(System.Func<int, int, Color> pixel)
        {
            var tex = new Texture2D(TileSize, TileSize, TextureFormat.RGBA32, false);
            var pixels = new Color[TileSize * TileSize];
            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                    pixels[y * TileSize + x] = pixel(x, y);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
