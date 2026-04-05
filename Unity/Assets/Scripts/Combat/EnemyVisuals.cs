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
    }
}