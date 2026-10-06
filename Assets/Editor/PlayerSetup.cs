using System.IO;
using Darkforest.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Darkforest.EditorTools
{
    /// <summary>
    /// "Darkforest/Create Player": builds the placeholder sprite, the Player prefab,
    /// and places it in SampleScene. Safe to run repeatedly.
    /// Batchmode: -executeMethod Darkforest.EditorTools.PlayerSetup.CreatePlayer
    /// </summary>
    public static class PlayerSetup
    {
        private const string SpritePath = "Assets/Art/Placeholder/player.png";
        private const string PrefabPath = "Assets/Prefabs/Player.prefab";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const int SpriteSize = 32;

        private static readonly Color CameraBackground = new Color(0.07f, 0.06f, 0.08f);

        [MenuItem("Darkforest/Create Player")]
        public static void CreatePlayer()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Sprite sprite = EnsurePlaceholderSprite();
            GameObject prefab = BuildPrefab(sprite);
            PlaceInScene(prefab);

            Debug.Log($"[PlayerSetup] Player prefab at {PrefabPath}, placed in {ScenePath}.");
        }

        private static Sprite EnsurePlaceholderSprite()
        {
            if (!File.Exists(SpritePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
                File.WriteAllBytes(SpritePath, DrawPlaceholder().EncodeToPNG());
                AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        // Pale round head with two dark eyes, roughly Isaac-shaped.
        private static Texture2D DrawPlaceholder()
        {
            var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            var body = new Color32(232, 214, 200, 255);
            var outline = new Color32(60, 40, 40, 255);
            var eye = new Color32(30, 20, 25, 255);
            var clear = new Color32(0, 0, 0, 0);

            Vector2 center = new Vector2(15.5f, 15.5f);
            const float radius = 14f;
            var pixels = new Color32[SpriteSize * SpriteSize];
            for (int y = 0; y < SpriteSize; y++)
            {
                for (int x = 0; x < SpriteSize; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    Color32 c = d > radius ? clear : d > radius - 1.5f ? outline : body;

                    bool leftEye = Vector2.Distance(new Vector2(x, y), new Vector2(10.5f, 14f)) < 2.6f;
                    bool rightEye = Vector2.Distance(new Vector2(x, y), new Vector2(20.5f, 14f)) < 2.6f;
                    if (leftEye || rightEye) c = eye;

                    pixels[y * SpriteSize + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static GameObject BuildPrefab(Sprite sprite)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

            var go = new GameObject("Player");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 10;

                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

                var col = go.AddComponent<CircleCollider2D>();
                col.radius = 0.4f;

                go.AddComponent<PlayerMovement>();

                return PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void PlaceInScene(GameObject prefab)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Replace any earlier instance so re-running stays idempotent.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab)
                    Object.DestroyImmediate(root);
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.position = Vector3.zero;

            Camera cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = CameraBackground;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
