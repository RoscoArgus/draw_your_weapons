using UnityEngine;

public class EnemyContactDamage : MonoBehaviour
{
    [Header("Damage")]
    public float damagePerTick = 10f;
    public float damageInterval = 1f;

    private float lastDamageTime = -999f;

    /// <summary>
    /// Applies periodic contact damage to the player while inside the collider
    /// </summary>
    /// <param name="other">Triggered collider</param>
    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }
        if (!other.TryGetComponent<PlayerHealth>(out PlayerHealth playerHealth) || playerHealth.IsDead)
        {
            return;
        }

        if (Time.time - lastDamageTime < damageInterval)
        {
            return;
        }
        lastDamageTime = Time.time;
        playerHealth.TakeDamage(damagePerTick);
    }
}
