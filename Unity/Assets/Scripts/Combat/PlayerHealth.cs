using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public bool debugLogs = true;

    private float currentHealth;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public float HealthPercent => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Reduces player health and triggers death when it reaches zero
    /// </summary>
    /// <param name="amount">Damage value</param>
    public void TakeDamage(float amount)
    {
        if (isDead)
        {
            return;
        }
        if (amount <= 0f)
        {
            return;
        }

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        if (debugLogs)
        {
            Debug.Log($"Player took {amount:F1} damage. HP: {currentHealth:F1}/{maxHealth:F1}");
        }
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Increases player health by provided amount up to a maximum value
    /// </summary>
    /// <param name="amount">Health value</param>
    public void Heal(float amount)
    {
        if (isDead)
        {
            return;
        }
        if (amount <= 0f)
        {
            return;
        }

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }

    /// <summary>
    /// Resets death state and restores full health
    /// </summary>
    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Marks the player as dead and logs the event
    /// </summary>
    private void Die()
    {
        if (isDead)
        {
            return;
        }
        isDead = true;

        if (debugLogs)
        {
            Debug.Log("Player died.");
        }
    }
}
