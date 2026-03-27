using UnityEngine;

public class EnemyVisuals : MonoBehaviour
{
    public Renderer[] targetRenderers;

    private EnemyHealth health;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>();
    }

    private void Start()
    {
        ApplyColor();
    }

    private void ApplyColor()
    {
        if (health == null) return;

        Color color = Color.white;

        switch (health.enemyType)
        {
            case EnemyType.WeakToSlashing:
                color = Color.red;
                break;
            case EnemyType.WeakToPiercing:
                color = Color.blue;
                break;
            case EnemyType.WeakToBlunt:
                color = Color.yellow;
                break;
        }

        foreach (Renderer r in targetRenderers)
        {
            if (r == null) continue;
            foreach (Material mat in r.materials)
                mat.color = color;
        }
    }
}