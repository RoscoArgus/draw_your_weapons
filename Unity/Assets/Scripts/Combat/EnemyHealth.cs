using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Enemy")]
    public EnemyType enemyType;
    public float maxHealth = 100f;

    [Header("Damage Multipliers")]
    public float weaknessBonus = 2f;
    public float neutralMultiplier = 1f;
    public float resistanceMultiplier = 0.75f;

    [Header("Optional")]
    public bool destroyOnDeath = true;

    private float currentHealth;
    private bool isDead;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeHit(WeaponAttributes weaponAttributes, float baseDamage)
    {
        if (isDead || weaponAttributes == null) return;

        float slash = weaponAttributes.Slashing;
        float pierce = weaponAttributes.Piercing;
        float blunt = weaponAttributes.Bluntness;

        float matchedValue = GetMatchedTypeValue(slash, pierce, blunt);
        float otherAverage = GetOtherTypeAverage(slash, pierce, blunt);

        float multiplier;

        if (matchedValue > otherAverage)
            multiplier = Mathf.Lerp(neutralMultiplier, weaknessBonus, matchedValue);
        else if (matchedValue < otherAverage)
            multiplier = Mathf.Lerp(resistanceMultiplier, neutralMultiplier, matchedValue);
        else
            multiplier = neutralMultiplier;

        float finalDamage = baseDamage * multiplier;
        currentHealth -= finalDamage;

        Debug.Log($"{name} took {finalDamage:F1} damage. Remaining HP: {currentHealth:F1}");

        var mover = GetComponent<EnemyMover>();
        if (mover != null) mover.TriggerHitAnimation();

        if (currentHealth <= 0f)
            Die();
    }

    private float GetMatchedTypeValue(float slash, float pierce, float blunt)
    {
        switch (enemyType)
        {
            case EnemyType.WeakToSlashing: return slash;
            case EnemyType.WeakToPiercing: return pierce;
            case EnemyType.WeakToBlunt: return blunt;
            default: return 0f;
        }
    }

    private float GetOtherTypeAverage(float slash, float pierce, float blunt)
    {
        switch (enemyType)
        {
            case EnemyType.WeakToSlashing: return (pierce + blunt) / 2f;
            case EnemyType.WeakToPiercing: return (slash + blunt) / 2f;
            case EnemyType.WeakToBlunt: return (slash + pierce) / 2f;
            default: return 0f;
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        WaveManager waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null)
            waveManager.NotifyEnemyKilled(this);

        if (destroyOnDeath)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }
}