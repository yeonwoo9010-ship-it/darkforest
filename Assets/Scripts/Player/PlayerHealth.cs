using System;
using UnityEngine;

namespace Darkforest.Player
{
    /// <summary>
    /// Player hit points. Monsters and traps call TakeDamage; the HUD listens to HealthChanged.
    /// After a hit the player is invincible for a moment and blinks, like Isaac.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Tooltip("Hit points at full health. The life icon has 4 frames, so 3 = full, 2, 1, empty.")]
        [SerializeField] private int maxHealth = 3;

        [Tooltip("Seconds of invincibility after taking damage.")]
        [SerializeField] private float invincibleTime = 1f;

        [Tooltip("Sprite that blinks while invincible. Optional.")]
        [SerializeField] private SpriteRenderer blinkRenderer;

        [Header("Debug (Editor only)")]
        [Tooltip("K = take 1 damage, H = heal 1. Handy until monsters and traps exist.")]
        [SerializeField] private bool debugKeys = true;

        private float invincibleUntil;

        public int MaxHealth => maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0;
        public bool IsInvincible => Time.time < invincibleUntil;

        /// <summary>(current, max). Raised on every change, and once in Start.</summary>
        public event Action<int, int> HealthChanged;
        public event Action Died;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            if (blinkRenderer == null)
                blinkRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        /// <summary>Returns true if the damage was applied (not blocked by invincibility or death).</summary>
        public bool TakeDamage(int amount = 1)
        {
            if (amount <= 0 || IsDead || IsInvincible)
                return false;

            SetHealth(CurrentHealth - amount);
            if (IsDead)
            {
                Died?.Invoke();
                var movement = GetComponent<PlayerMovement>();
                if (movement != null)
                    movement.enabled = false;
            }
            else
            {
                invincibleUntil = Time.time + invincibleTime;
            }
            return true;
        }

        public void Heal(int amount = 1)
        {
            if (amount <= 0 || IsDead)
                return;
            SetHealth(CurrentHealth + amount);
        }

        private void SetHealth(int value)
        {
            int clamped = Mathf.Clamp(value, 0, maxHealth);
            if (clamped == CurrentHealth)
                return;
            CurrentHealth = clamped;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        private void Update()
        {
            if (blinkRenderer != null)
                blinkRenderer.enabled = !IsInvincible || Mathf.Repeat(Time.time * 10f, 1f) < 0.5f;

#if UNITY_EDITOR
            if (debugKeys)
            {
                if (Input.GetKeyDown(KeyCode.K)) TakeDamage(1);
                if (Input.GetKeyDown(KeyCode.H)) Heal(1);
            }
#endif
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            invincibleTime = Mathf.Max(0f, invincibleTime);
        }
    }
}
