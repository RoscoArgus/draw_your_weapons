using UnityEngine;

public class DesktopDebugWeaponSpawner : MonoBehaviour
{
    public GameObject debugWeaponPrefab;
    public Transform handAnchor;
    public KeyCode spawnKey = KeyCode.G;

    private GameObject currentWeapon;

    private void Update()
    {
        if (Input.GetKeyDown(spawnKey))
        {
            SpawnDebugWeapon();
        }
    }

    /// <summary>
    /// Spawns the debug weapon at the hand anchor position
    /// </summary>
    private void SpawnDebugWeapon()
    {
        if (debugWeaponPrefab == null || handAnchor == null)
        {
            Debug.LogWarning("Assign debugWeaponPrefab and handAnchor.");
            return;
        }

        if (currentWeapon != null)
        {
            Destroy(currentWeapon);
        }
        currentWeapon = Instantiate(debugWeaponPrefab, handAnchor.position, handAnchor.rotation, handAnchor);
    }
}
