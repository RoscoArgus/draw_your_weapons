using System.Collections.Generic;
using UnityEngine;

public class WeaponDamageDealer : MonoBehaviour
{
    public float baseDamage = 25f;
    public float hitCooldown = 0.35f;

    private WeaponAttributes weaponAttributes;
    private readonly Dictionary<EnemyHealth, float> lastHitTimes = new();

    private void OnCollisionEnter(Collision collision) => TryDamage(collision.gameObject);
    private void OnTriggerEnter(Collider other) => TryDamage(other.gameObject);

    private void TryDamage(GameObject targetObject)
    {
        EnemyHealth enemy = targetObject.GetComponent<EnemyHealth>();
        if (enemy == null || enemy.IsDead) return;

        if (weaponAttributes == null)
            weaponAttributes = GetComponent<WeaponAttributes>()
                ?? GetComponentInParent<WeaponAttributes>();

        if (weaponAttributes == null) return;

        if (lastHitTimes.TryGetValue(enemy, out float lastHitTime))
            if (Time.time - lastHitTime < hitCooldown) return;

        enemy.TakeHit(weaponAttributes, baseDamage);
        lastHitTimes[enemy] = Time.time;
    }
}