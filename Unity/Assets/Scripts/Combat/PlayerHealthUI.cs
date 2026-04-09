using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("References")]
    public PlayerHealth playerHealth;
    public Slider healthSlider;
    public TMP_Text healthText;

    private void Start()
    {
        Refresh();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }

    /// <summary>
    /// Refreshes UI when player health changes
    /// </summary>
    /// <param name="current">Current health value</param>
    /// <param name="max">Maximum health value</param>
    private void HandleHealthChanged(float current, float max)
    {
        Refresh();
    }

    /// <summary>
    /// Updates the health slider and label based on current player health
    /// </summary>
    private void Refresh()
    {
        if (playerHealth == null)
        {
            return;
        }

        if (healthSlider != null)
        {
            if (!Mathf.Approximately(healthSlider.maxValue, playerHealth.MaxHealth))
            {
                healthSlider.maxValue = playerHealth.MaxHealth;
            }
            healthSlider.value = playerHealth.CurrentHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"HP: {Mathf.CeilToInt(playerHealth.CurrentHealth)} / {Mathf.CeilToInt(playerHealth.MaxHealth)}";
        }
    }
}
