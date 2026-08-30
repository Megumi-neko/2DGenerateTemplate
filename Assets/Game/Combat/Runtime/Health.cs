using System;
using UnityEngine;

namespace Game.Combat
{
    [AddComponentMenu("Game/Combat/Health")]
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        private bool isDead;

        public event Action<Health, float> Damaged;
        public event Action<Health> Died;
        public event Action<Health> Changed;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float NormalizedHealth => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
        public bool IsDead => isDead;

        private void Awake()
        {
            maxHealth = SanitizeMaximum(maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            isDead = currentHealth <= 0f;
        }

        private void OnValidate()
        {
            maxHealth = SanitizeMaximum(maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        public void ResetHealth(float newMaxHealth)
        {
            maxHealth = SanitizeMaximum(newMaxHealth);
            currentHealth = maxHealth;
            isDead = false;
            Changed?.Invoke(this);
        }

        public void IncreaseMaximumHealth(float newMaxHealth)
        {
            float previousMaxHealth = maxHealth;
            float previousDamage = Mathf.Max(0f, previousMaxHealth - currentHealth);
            maxHealth = SanitizeMaximum(newMaxHealth);
            currentHealth = Mathf.Clamp(maxHealth - previousDamage, 0f, maxHealth);
            isDead = currentHealth <= 0f;
            Changed?.Invoke(this);
        }

        public bool TakeDamage(float amount)
        {
            if (isDead || !IsFinite(amount) || amount <= 0f)
            {
                return false;
            }

            float appliedDamage = Mathf.Min(currentHealth, amount);
            currentHealth -= appliedDamage;
            Damaged?.Invoke(this, appliedDamage);
            Changed?.Invoke(this);

            if (currentHealth > 0f)
            {
                return true;
            }

            currentHealth = 0f;
            isDead = true;
            Died?.Invoke(this);
            return true;
        }

        public void Heal(float amount)
        {
            if (isDead || !IsFinite(amount) || amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            Changed?.Invoke(this);
        }

        private static float SanitizeMaximum(float value)
        {
            return IsFinite(value) ? Mathf.Max(1f, value) : 1f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
