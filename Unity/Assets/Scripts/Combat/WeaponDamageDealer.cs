using System.Collections.Generic;
using UnityEngine;

public class WeaponDamageDealer : MonoBehaviour
{
    public float baseDamage = 25f;
    public float hitCooldown = 0.35f;

    private WeaponAttributes weaponAttributes;
    private readonly Dictionary<EnemyHealth, float> lastHitTimes = new();

    /// <summary>
    /// Applies weapon damage when colliding with an enemy
    /// </summary>
    /// <param name="collision">Collision data</param>
    private void OnCollisionEnter(Collision collision) => TryDamage(collision.gameObject);
    /// <summary>
    /// Applies weapon damage when an enemy trigger is entered
    /// </summary>
    /// <param name="other">Collider that triggered the overlap callback</param>
    private void OnTriggerEnter(Collider other) => TryDamage(other.gameObject);

    /// <summary>
    /// Damages a valid enemy target if cooled down
    /// </summary>
    /// <param name="targetObject">Target enemy</param>
    private void TryDamage(GameObject targetObject)
    {
        EnemyHealth enemy = targetObject.GetComponent<EnemyHealth>();
        if (enemy == null || enemy.IsDead)
        {
            return;
        }

        if (weaponAttributes == null)
        {
            weaponAttributes = GetComponent<WeaponAttributes>()
                ?? GetComponentInParent<WeaponAttributes>();
        }
        if (weaponAttributes == null)
        {
            return;
        }

        if (lastHitTimes.TryGetValue(enemy, out float lastHitTime))
        {
            if (Time.time - lastHitTime < hitCooldown)
            {
                return;
            }
        }
        enemy.TakeHit(weaponAttributes, baseDamage);
        lastHitTimes[enemy] = Time.time;
    }
}
