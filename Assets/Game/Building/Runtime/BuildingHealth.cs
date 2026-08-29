using System;
using UnityEngine;

namespace Game.Building
{
    [AddComponentMenu("Game/Building/Building Health")]
    [DisallowMultipleComponent]
    public sealed class BuildingHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 150f;
        [SerializeField] private float currentHealth = 150f;
        private bool isDead;

        public event Action<BuildingHealth, float> Damaged;
        public event Action<BuildingHealth> Died;
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float NormalizedHealth => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
        public bool IsDead => isDead;

        private void Awake() { ResetHealth(maxHealth); }
        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        public void ResetHealth(float value)
        {
            maxHealth = Mathf.Max(1f, value);
            currentHealth = maxHealth;
            isDead = false;
        }

        public bool TakeDamage(float amount)
        {
            if (isDead || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return false;
            float applied = Mathf.Min(currentHealth, amount);
            currentHealth -= applied;
            Damaged?.Invoke(this, applied);
            if (currentHealth > 0f) return true;
            currentHealth = 0f;
            isDead = true;
            Died?.Invoke(this);
            return true;
        }

        public void Heal(float amount)
        {
            if (isDead || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }
    }
}
