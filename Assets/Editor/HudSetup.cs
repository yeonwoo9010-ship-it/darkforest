using Darkforest.Player;
using Darkforest.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Darkforest.EditorTools
{
    /// <summary>
    /// "Darkforest/Create Health UI": imports Assets/Art/Ui/life1-4 as pixel-art sprites, adds
    /// PlayerHealth to the Player prefab, and builds a HUD canvas with the life icon in the top-left
    /// corner of the game view. Safe to run repeatedly.
    /// Batchmode: -executeMethod Darkforest.EditorTools.HudSetup.CreateHealthUI
    /// </summary>
    public static class HudSetup
    {
        private const string LifeSpriteDir = "Assets/Art/Ui";
        private const int LifeStages = 4;
        private const string PrefabPath = "Assets/Prefabs/Player.prefab";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string HudName = "HUD";

        // On the 1920x1080 reference: the 64 px icon drawn at 2x, 40 px from the corner.
        private const float IconSize = 128f;
        private const float Margin = 40f;

        [MenuItem("Darkforest/Create Health UI")]
        public static void CreateHealthUI()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Sprite[] stages = ImportLifeSprites();
            if (stages == null)
                return;

            AddHealthToPrefab();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == HudName)
                    Object.DestroyImmediate(root);
            }

            BuildHud(stages);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[HudSetup] Health UI created (top-left). In Play mode: K = damage, H = heal.");
        }

        private static Sprite[] ImportLifeSprites()
        {
            var stages = new Sprite[LifeStages];
            for (int i = 0; i < LifeStages; i++)
            {
                string path = $"{LifeSpriteDir}/life{i + 1}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[HudSetup] Missing {path}.");
                    return null;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();

                stages[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return stages;
        }

        private static void AddHealthToPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (root.GetComponent<PlayerHealth>() == null)
                    root.AddComponent<PlayerHealth>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildHud(Sprite[] stages)
        {
            // Screen Space - Camera keeps the HUD inside the camera's letterboxed 16:9 area.
            var hud = new GameObject(HudName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = hud.GetComponent<Canvas>();
            Camera cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            if (cam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.sortingOrder = 2000; // above the room mask (1000)
            canvas.pixelPerfect = true;

            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var icon = new GameObject("Life", typeof(RectTransform), typeof(Image), typeof(HealthUI));
            icon.transform.SetParent(hud.transform, false);
            var rect = icon.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(Margin, -Margin);
            rect.sizeDelta = new Vector2(IconSize, IconSize);

            var image = icon.GetComponent<Image>();
            image.sprite = stages[0];
            image.preserveAspect = true;
            image.raycastTarget = false;

            var so = new SerializedObject(icon.GetComponent<HealthUI>());
            var stagesProp = so.FindProperty("stages");
            stagesProp.arraySize = stages.Length;
            for (int i = 0; i < stages.Length; i++)
                stagesProp.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
