using UnityEngine;

/// <summary>
/// Stores the physical attributes of a generated XR weapon.
/// Values are normalized percentages (0.0 to 1.0).
/// </summary>
public class WeaponAttributes : MonoBehaviour
{
    [Header("Weapon Stats")]
    [Tooltip("How effective the weapon is at slashing (driven by long, smooth edges).")]
    [Range(0f, 1f)] 
    public float Slashing;

    [Tooltip("How effective the weapon is at piercing (driven by sharp spikes and narrow points).")]
    [Range(0f, 1f)] 
    public float Piercing;

    [Tooltip("How effective the weapon is at blunt impact (driven by circularity and top-heavy mass).")]
    [Range(0f, 1f)] 
    public float Bluntness;
}
