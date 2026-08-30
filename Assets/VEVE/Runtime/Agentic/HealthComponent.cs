using UnityEngine;

namespace VEVE.Agentic
{
    /// <summary>
    /// Health component for damage and healing support.
    /// </summary>
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] protected float maxHealth = 100f;
        [SerializeField] protected float currentHealth = 100f;

        /// <summary>
        /// Gets the current health percentage (0-1).
        /// </summary>
        public float HealthPercentage => currentHealth / maxHealth;

        /// <summary>
        /// Gets or sets the current health value.
        /// </summary>
        public float CurrentHealth
        {
            get => currentHealth;
            set => currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        }

        /// <summary>
        /// Gets the maximum health value.
        /// </summary>
        public float MaxHealth => maxHealth;

        /// <summary>
        /// Applies damage to the entity.
        /// </summary>
        /// <param name="amount">Amount of damage to apply.</param>
        public virtual void TakeDamage(float amount)
        {
            currentHealth -= amount;
            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
            }
        }

        /// <summary>
        /// Heals the entity by a specified amount.
        /// </summary>
        /// <param name="amount">Amount of health to restore.</param>
        public virtual void Heal(float amount)
        {
            currentHealth += amount;
            if (currentHealth > maxHealth)
            {
                currentHealth = maxHealth;
            }
        }
    }
}
