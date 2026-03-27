using UnityEngine;

public class EnemyContactDamage : MonoBehaviour
{
    [Header("Damage")]
    public float damagePerTick = 10f;
    public float damageInterval = 1f;

    private float lastDamageTime = -999f;

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;
        if (playerHealth.IsDead) return;

        if (Time.time - lastDamageTime < damageInterval)
            return;

        lastDamageTime = Time.time;
        playerHealth.TakeDamage(damagePerTick);
    }
}