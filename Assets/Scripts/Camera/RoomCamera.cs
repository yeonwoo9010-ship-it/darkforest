using UnityEngine;

namespace Darkforest.CameraSystem
{
    /// <summary>
    /// Isaac-style fixed room camera. It always shows the same world area (viewWidth x viewHeight units,
    /// 1 unit = 1 tile) and letterboxes the window to that aspect ratio, so the player keeps the same
    /// on-screen size whatever the window or Game view size is.
    /// </summary>
    [ExecuteAlways] // keeps orthographic size in sync in Edit mode; letterboxing runs in Play mode only
    [RequireComponent(typeof(Camera))]
    public class RoomCamera : MonoBehaviour
    {
        // An Isaac 1x1 room is 13x7 floor tiles, 15x9 with walls; the view adds a margin around that.
        [Tooltip("Visible world width in units (tiles).")]
        [SerializeField] private float viewWidth = 17f;

        [Tooltip("Visible world height in units (tiles). 17 x 9.5625 is 16:9.")]
        [SerializeField] private float viewHeight = 9.5625f;

        [Tooltip("Color of the letterbox bars outside the game area.")]
        [SerializeField] private Color letterboxColor = Color.black;

        private Camera cam;
        private Camera letterboxCam;
        private int lastScreenWidth;
        private int lastScreenHeight;

        public float ViewWidth => viewWidth;
        public float ViewHeight => viewHeight;

        private void OnEnable()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            Apply();
        }

        private void OnDisable()
        {
            if (cam != null)
                cam.rect = new Rect(0f, 0f, 1f, 1f);

            if (letterboxCam != null)
            {
                if (Application.isPlaying)
                    Destroy(letterboxCam.gameObject);
                else
                    DestroyImmediate(letterboxCam.gameObject);
                letterboxCam = null;
            }
        }

        private void Update()
        {
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
                Apply();
        }

        private void OnValidate()
        {
            viewWidth = Mathf.Max(1f, viewWidth);
            viewHeight = Mathf.Max(1f, viewHeight);
            lastScreenWidth = -1; // re-apply on the next Update
        }

        // A hidden full-screen camera that renders nothing, drawn first so the bars are a solid color.
        private void EnsureLetterboxCamera()
        {
            if (letterboxCam == null)
            {
                var go = new GameObject("Letterbox Camera") { hideFlags = HideFlags.HideAndDontSave };
                letterboxCam = go.AddComponent<Camera>();
                letterboxCam.cullingMask = 0;
                letterboxCam.clearFlags = CameraClearFlags.SolidColor;
                letterboxCam.orthographic = true;
                letterboxCam.useOcclusionCulling = false;
                letterboxCam.allowHDR = false;
                letterboxCam.allowMSAA = false;
            }
            letterboxCam.depth = cam.depth - 1f;
            letterboxCam.backgroundColor = letterboxColor;
        }

        private void Apply()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            cam.orthographicSize = viewHeight * 0.5f;

            // Letterbox only in Play mode, so the viewport rect is never saved into the scene.
            if (!Application.isPlaying || lastScreenWidth <= 0 || lastScreenHeight <= 0)
                return;

            EnsureLetterboxCamera();

            float targetAspect = viewWidth / viewHeight;
            float windowAspect = (float)lastScreenWidth / lastScreenHeight;

            if (windowAspect > targetAspect)
            {
                // Window is wider: bars left and right.
                float w = targetAspect / windowAspect;
                cam.rect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            else
            {
                // Window is taller: bars top and bottom.
                float h = windowAspect / targetAspect;
                cam.rect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }
        }
    }
}
