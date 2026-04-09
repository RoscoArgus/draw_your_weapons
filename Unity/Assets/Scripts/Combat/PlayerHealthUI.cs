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

    private void Update()
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
            healthSlider.maxValue = playerHealth.MaxHealth;
            healthSlider.value = playerHealth.CurrentHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"HP: {Mathf.CeilToInt(playerHealth.CurrentHealth)} / {Mathf.CeilToInt(playerHealth.MaxHealth)}";
        }
    }
}
