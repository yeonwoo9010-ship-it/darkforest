using System.IO;
using Darkforest.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Darkforest.EditorTools
{
    /// <summary>
    /// "Darkforest/Create Dog": generates a 1-tile placeholder sprite (replace the PNG later), builds
    /// Assets/Prefabs/Dog.prefab and puts one Dog in the start room of SampleScene. Safe to run repeatedly.
    /// Batchmode: -executeMethod Darkforest.EditorTools.DogSetup.CreateDog
    /// </summary>
    public static class DogSetup
    {
        private const string SpritePath = "Assets/Art/Placeholder/dog.png";
        private const string PrefabPath = "Assets/Prefabs/Dog.prefab";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const int Size = 32;

        // Top-left corner of the start room, away from the player's start cell and its row/column.
        private static readonly Vector3 SpawnPosition = new Vector3(-3f, 3f, 0f);

        [MenuItem("Darkforest/Create Dog")]
        public static void CreateDog()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Sprite sprite = EnsureSprite();
            GameObject prefab = BuildPrefab(sprite);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab)
                    Object.DestroyImmediate(root);
            }

            var dog = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            dog.transform.position = SpawnPosition;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DogSetup] Dog prefab at {PrefabPath}, one placed at {SpawnPosition}.");
        }

        private static Sprite EnsureSprite()
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
            importer.spritePixelsPerUnit = Size;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        // Brown rounded square with two ears, red eyes and a dark nose. Placeholder only.
        private static Texture2D DrawPlaceholder()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var body = new Color32(150, 95, 55, 255);
            var outline = new Color32(50, 30, 20, 255);
            var eye = new Color32(220, 40, 40, 255);
            var nose = new Color32(30, 20, 20, 255);
            var clear = new Color32(0, 0, 0, 0);

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Head: rounded box from (3,2) to (28,24); ears: two triangles on top.
                    bool head = InRoundedBox(x, y, 3, 2, 28, 24, 6);
                    bool headInner = InRoundedBox(x, y, 4, 3, 27, 23, 5);
                    bool leftEar = y >= 22 && y <= 30 && x >= 4 && x <= 11 && (x - 4) >= (y - 22) / 2 && (11 - x) >= (y - 22) / 2;
                    bool rightEar = y >= 22 && y <= 30 && x >= 20 && x <= 27 && (x - 20) >= (y - 22) / 2 && (27 - x) >= (y - 22) / 2;

                    Color32 c = clear;
                    if (head || leftEar || rightEar) c = outline;
                    if (headInner || (leftEar && y < 29) || (rightEar && y < 29)) c = body;
                    if ((x >= 9 && x <= 11 || x >= 20 && x <= 22) && y >= 14 && y <= 16) c = eye;
                    if (x >= 14 && x <= 17 && y >= 7 && y <= 9) c = nose;

                    pixels[y * Size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static bool InRoundedBox(int x, int y, int x0, int y0, int x1, int y1, int r)
        {
            if (x < x0 || x > x1 || y < y0 || y > y1) return false;
            int cx = Mathf.Clamp(x, x0 + r, x1 - r);
            int cy = Mathf.Clamp(y, y0 + r, y1 - r);
            return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
        }

        private static GameObject BuildPrefab(Sprite sprite)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

            var go = new GameObject("Dog");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 9; // just under the player

                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.mass = 2f;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.8f, 0.8f);

                go.AddComponent<Dog>();

                return PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
