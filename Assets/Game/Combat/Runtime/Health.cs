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
