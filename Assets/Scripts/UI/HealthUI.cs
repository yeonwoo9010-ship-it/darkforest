using Darkforest.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Darkforest.UI
{
    /// <summary>
    /// Top-left life icon. Shows one of the stage sprites by remaining health:
    /// stages[0] = full (life1) ... last = empty (life4).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class HealthUI : MonoBehaviour
    {
        [Tooltip("Falls back to the PlayerHealth in the scene.")]
        [SerializeField] private PlayerHealth health;

        [Tooltip("Full to empty, e.g. life1, life2, life3, life4.")]
        [SerializeField] private Sprite[] stages;

        private Image image;

        private void Awake()
        {
            image = GetComponent<Image>();
            if (health == null)
                health = FindObjectOfType<PlayerHealth>();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.HealthChanged += Show;
                Show(health.CurrentHealth, health.MaxHealth);
            }
        }

        private void OnDisable()
        {
            if (health != null)
                health.HealthChanged -= Show;
        }

        private void Show(int current, int max)
        {
            if (stages == null || stages.Length == 0 || max <= 0)
                return;

            // Lost fraction mapped onto the frames: with 4 frames and max 3, each hit moves one frame.
            float lost = 1f - (float)current / max;
            int index = Mathf.Clamp(Mathf.RoundToInt(lost * (stages.Length - 1)), 0, stages.Length - 1);
            image.sprite = stages[index];
        }
    }
}
