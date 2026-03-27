using System.Collections.Generic;
using UnityEngine;

public class PendingMeshyUpgrade : MonoBehaviour
{
    [Header("Upgrade State")]
    [SerializeField] private bool isUpgradeReady;

    [Header("Ready Emissive Glow")]
    [SerializeField] private Color glowColor = new Color(0.6f, 0.1f, 1f, 1f);
    [SerializeField, Min(0f)] private float glowIntensity = 1.35f;
    [SerializeField, Min(0.5f)] private float rimPower = 2.4f;
    [SerializeField, Min(0f)] private float pulseSpeed = 2f;
    [SerializeField, Min(0f)] private float pulseAmount = 0.25f;

    private MeshBuilder meshBuilder;
    private GameObject pendingMeshyModel;
    private readonly List<OverlayEntry> overlayEntries = new();

    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");

    private struct OverlayEntry
    {
        public Renderer Renderer;
        public int BaseMaterialCount;
        public Material OverlayMaterial;
    }

    public bool IsUpgradeReady => isUpgradeReady && meshBuilder != null && pendingMeshyModel != null;

    public void SetPendingUpgrade(MeshBuilder builder, GameObject meshyModel, Color readyGlowColor)
    {
        meshBuilder = builder;
        pendingMeshyModel = meshyModel;
        glowColor = readyGlowColor;
        isUpgradeReady = meshBuilder != null && pendingMeshyModel != null;

        if (pendingMeshyModel != null)
            pendingMeshyModel.SetActive(false);

        if (isUpgradeReady)
            ApplyReadyGlow();
    }

    public void UpgradeToMeshyModel()
    {
        if (!IsUpgradeReady)
        {
            Debug.LogWarning("[PendingMeshyUpgrade] Upgrade is not ready yet.", this);
            return;
        }

        var meshyModel = pendingMeshyModel;
        pendingMeshyModel = null;
        isUpgradeReady = false;
        ClearReadyVisuals();

        meshBuilder.UpgradeToMeshyModel(gameObject, meshyModel);
    }

    private void Update()
    {
        if (!isUpgradeReady || overlayEntries.Count == 0)
            return;

        float t = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        float pulseStrength = glowIntensity * t;
        for (int i = 0; i < overlayEntries.Count; i++)
            ApplyOverlayMaterial(overlayEntries[i].OverlayMaterial, glowColor, pulseStrength);
    }

    private void ApplyReadyGlow()
    {
        ClearReadyVisuals();

        Shader overlayShader = Shader.Find("MeshPipeline/MeshyEmissiveRim");
        if (overlayShader == null)
        {
            Debug.LogWarning("[PendingMeshyUpgrade] MeshPipeline/MeshyEmissiveRim shader not found for emissive glow effect.", this);
            return;
        }

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            var baseMaterials = renderer.materials;
            if (baseMaterials == null || baseMaterials.Length == 0)
                continue;

            var overlayMaterial = new Material(overlayShader)
            {
                name = "MeshyUpgradeEmissiveRimMat"
            };
            ApplyOverlayMaterial(overlayMaterial, glowColor, glowIntensity);

            // Append overlay material to layered on top of base materials
            var materialsWithOverlay = new Material[baseMaterials.Length + 1];
            for (int i = 0; i < baseMaterials.Length; i++)
                materialsWithOverlay[i] = baseMaterials[i];
            materialsWithOverlay[baseMaterials.Length] = overlayMaterial;

            renderer.materials = materialsWithOverlay;

            overlayEntries.Add(new OverlayEntry
            {
                Renderer = renderer,
                BaseMaterialCount = baseMaterials.Length,
                OverlayMaterial = overlayMaterial
            });
        }
    }

    private void ApplyOverlayMaterial(Material material, Color color, float strength)
    {
        if (material == null)
            return;

        if (material.HasProperty(RimColorId))
            material.SetColor(RimColorId, color);
        if (material.HasProperty(RimPowerId))
            material.SetFloat(RimPowerId, rimPower);
        if (material.HasProperty(GlowStrengthId))
            material.SetFloat(GlowStrengthId, strength);
    }

    private void ClearReadyVisuals()
    {
        for (int i = 0; i < overlayEntries.Count; i++)
        {
            var entry = overlayEntries[i];

            if (entry.Renderer != null)
            {
                var currentMaterials = entry.Renderer.materials;
                if (currentMaterials.Length > entry.BaseMaterialCount)
                {
                    var baseMaterials = new Material[entry.BaseMaterialCount];
                    for (int m = 0; m < entry.BaseMaterialCount; m++)
                        baseMaterials[m] = currentMaterials[m];
                    entry.Renderer.materials = baseMaterials;
                }
            }

            if (entry.OverlayMaterial != null)
                Destroy(entry.OverlayMaterial);
        }

        overlayEntries.Clear();
    }

    private void OnDestroy()
    {
        ClearReadyVisuals();

        if (pendingMeshyModel != null)
            Destroy(pendingMeshyModel);
    }
}
