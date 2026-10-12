using System.IO;
using Darkforest.CameraSystem;
using Darkforest.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Darkforest.EditorTools
{
    /// <summary>
    /// "Darkforest/Create Map": builds the basic stage map in SampleScene, a 7x7 grid of 7x7 rooms.
    /// Neighboring rooms share a 1-tile wall with a 1-tile passage in its middle (the 4th tile of
    /// each side). The outer walls are closed. The start room (3,3) is centered on the origin.
    /// Also sets up the tile highlight and the room-following camera. Safe to run repeatedly.
    /// Batchmode: -executeMethod Darkforest.EditorTools.RoomSetup.CreateMap
    /// </summary>
    public static class RoomSetup
    {
        private const int MapColumns = 7;
        private const int MapRows = 7;
        private const int RoomSize = 7;
        private const int TileSize = 32;

        private const string TileArtDir = "Assets/Art/Tiles";
        private const string FloorSpritePath = TileArtDir + "/floor.png";
        private const string WallSpritePath = TileArtDir + "/wall.png";
        private const string FloorTilePath = TileArtDir + "/FloorTile.asset";
        private const string WallTilePath = TileArtDir + "/WallTile.asset";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string MapName = "Map";
        private const string LegacyRoomName = "Room"; // the single room from the first version

        [MenuItem("Darkforest/Create Map")]
        public static void CreateMap()
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
                if (root.name == MapName || root.name == LegacyRoomName)
                    Object.DestroyImmediate(root);
            }

            RoomMap map = BuildMap(floorTile, wallTile);
            SetupCameraFollow(map);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RoomSetup] Built {MapColumns}x{MapRows} map of {RoomSize}x{RoomSize} rooms in {ScenePath}.");
        }

        private static RoomMap BuildMap(Tile floorTile, Tile wallTile)
        {
            // Shifting the grid by half a cell puts the center of cell (0,0), the start room's center, on the origin.
            var root = new GameObject(MapName, typeof(Grid), typeof(RoomMap));
            root.transform.position = new Vector3(-0.5f, -0.5f, 0f);
            var map = root.GetComponent<RoomMap>();
            var mapSo = new SerializedObject(map);
            mapSo.FindProperty("columns").intValue = MapColumns;
            mapSo.FindProperty("rows").intValue = MapRows;
            mapSo.FindProperty("roomSize").intValue = RoomSize;
            mapSo.FindProperty("startRoom").vector2IntValue = new Vector2Int(MapColumns / 2, MapRows / 2);
            mapSo.ApplyModifiedPropertiesWithoutUndo();

            Tilemap floor = CreateTilemap(root.transform, "Floor", -10);
            Tilemap walls = CreateTilemap(root.transform, "Walls", -5);

            int half = RoomSize / 2;
            for (int rx = 0; rx < MapColumns; rx++)
            {
                for (int ry = 0; ry < MapRows; ry++)
                {
                    Vector3Int c = map.RoomCenterCell(new Vector2Int(rx, ry));

                    // Floor plus the full wall ring. Shared walls are simply written twice.
                    for (int x = -half - 1; x <= half + 1; x++)
                    {
                        for (int y = -half - 1; y <= half + 1; y++)
                        {
                            var cell = new Vector3Int(c.x + x, c.y + y, 0);
                            bool isWall = Mathf.Abs(x) == half + 1 || Mathf.Abs(y) == half + 1;
                            if (isWall)
                                walls.SetTile(cell, wallTile);
                            else
                                floor.SetTile(cell, floorTile);
                        }
                    }
                }
            }

            // Passages: open the middle tile of every wall shared by two rooms. Only right and up are
            // checked, so each pair is handled once. Outer walls have no neighbor and stay closed.
            for (int rx = 0; rx < MapColumns; rx++)
            {
                for (int ry = 0; ry < MapRows; ry++)
                {
                    var room = new Vector2Int(rx, ry);
                    Vector3Int c = map.RoomCenterCell(room);
                    if (map.HasPassage(room, Vector2Int.right))
                        OpenPassage(floor, walls, floorTile, c + new Vector3Int(half + 1, 0, 0));
                    if (map.HasPassage(room, Vector2Int.up))
                        OpenPassage(floor, walls, floorTile, c + new Vector3Int(0, half + 1, 0));
                }
            }

            // One merged static collider for all walls, so the player slides along them smoothly.
            var wallCollider = walls.gameObject.AddComponent<TilemapCollider2D>();
            wallCollider.usedByComposite = true;
            var rb = walls.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var composite = walls.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

            floor.CompressBounds();
            walls.CompressBounds();

            // Faint outline on the floor tile under the player, drawn above the floor and below the walls.
            var highlight = new GameObject("Tile Highlight", typeof(SpriteRenderer), typeof(TileHighlighter));
            highlight.transform.SetParent(root.transform, false);
            highlight.GetComponent<SpriteRenderer>().sortingOrder = -9;
            var so = new SerializedObject(highlight.GetComponent<TileHighlighter>());
            so.FindProperty("floor").objectReferenceValue = floor;
            so.ApplyModifiedPropertiesWithoutUndo();

            return map;
        }

        private static void OpenPassage(Tilemap floor, Tilemap walls, Tile floorTile, Vector3Int cell)
        {
            walls.SetTile(cell, null);
            floor.SetTile(cell, floorTile);
        }

        private static void SetupCameraFollow(RoomMap map)
        {
            Camera cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            if (cam == null)
            {
                Debug.LogWarning("[RoomSetup] No camera found; skipped RoomCameraFollow.");
                return;
            }

            var follow = cam.GetComponent<RoomCameraFollow>();
            if (follow == null)
                follow = cam.gameObject.AddComponent<RoomCameraFollow>();
            var so = new SerializedObject(follow);
            so.FindProperty("map").objectReferenceValue = map;
            so.ApplyModifiedPropertiesWithoutUndo();

            Vector3 start = map.RoomCenterWorld(map.StartRoom);
            cam.transform.position = new Vector3(start.x, start.y, cam.transform.position.z);
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
