using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class PendingMeshyUpgrade : MonoBehaviour
{
    [Header("Upgrade State")]
    [SerializeField] private bool isUpgradeReady;
    [SerializeField] private bool isJobStarted;

    [Header("Ready Emissive Glow")]
    [SerializeField] private Color glowColor = new Color(0.6f, 0.1f, 1f, 1f);
    [SerializeField, Min(0f)] private float glowIntensity = 1.35f;
    [SerializeField, Min(0.5f)] private float rimPower = 2.4f;
    [SerializeField, Min(0f)] private float pulseSpeed = 2f;
    [SerializeField, Min(0f)] private float pulseAmount = 0.25f;

    private MeshBuilder _meshBuilder;
    private Texture2D _sourceTexture;
    private GameObject _pendingMeshyModel;
    private readonly List<OverlayEntry> _overlayEntries = new();

    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");

    private struct OverlayEntry
    {
        public Renderer Renderer;
        public int BaseMaterialCount;
        public Material OverlayMaterial;
    }

    public bool IsUpgradeReady => isUpgradeReady && _meshBuilder != null && _pendingMeshyModel != null;
    public bool IsJobStarted => isJobStarted;

    /// <summary>
    /// Stores mesh builder and source texture used for Meshy upgrade
    /// </summary>
    /// <param name="builder">Mesh builder</param>
    /// <param name="sourceTexture">Source texture</param>
    public void Initialise(MeshBuilder builder, Texture2D sourceTexture)
    {
        _meshBuilder = builder;
        _sourceTexture = sourceTexture;
    }

    /// <summary>
    /// Starts a Meshy generation job for currently held weapon, if not already active
    /// </summary>
    public void StartMeshyJob()
    {
        if (isJobStarted)
        {
            return;
        }
        if (_meshBuilder == null || _sourceTexture == null)
        {
            return;
        }

        isJobStarted = true;

        _meshBuilder.meshyClient.GenerateFromTexture(
            _sourceTexture,
            _meshBuilder.meshyCache,
            onComplete: meshyModel =>
            {
                if (this == null || gameObject == null)
                {
                    if (meshyModel != null)
                    {
                        Destroy(meshyModel);
                    }
                    return;
                }

                _pendingMeshyModel = meshyModel;
                _pendingMeshyModel.SetActive(false);
                isUpgradeReady = true;

                ApplyReadyGlow();
            },
            onError: err =>
            {
                isJobStarted = false; // allow retry
            }
        );
    }

    /// <summary>
    /// Swaps this weapon to the prepared Meshy model when ready
    /// </summary>
    public void UpgradeToMeshyModel()
    {
        if (!IsUpgradeReady)
        {
            return;
        }

        var model = _pendingMeshyModel;
        _pendingMeshyModel = null;
        isUpgradeReady = false;
        ClearReadyVisuals();

        _meshBuilder.UpgradeToMeshyModel(gameObject, model);
    }

    private void Update()
    {
        if (!isUpgradeReady || _overlayEntries.Count == 0)
        {
            return;
        }

        float t = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        float pulseStrength = glowIntensity * t;
        for (int i = 0; i < _overlayEntries.Count; i++)
    {
            ApplyOverlayMaterial(_overlayEntries[i].OverlayMaterial, glowColor, pulseStrength);
    }
    }

    /// <summary>
    /// Adds an emissive overlay so weapons glow when upgrade is ready
    /// </summary>
    private void ApplyReadyGlow()
    {
        ClearReadyVisuals();

        Shader overlayShader = Shader.Find("MeshPipeline/MeshyEmissiveRim");
        if (overlayShader == null)
        {
            return;
        }

        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
            {
                continue;
            }
            var baseMaterials = renderer.materials;
            if (baseMaterials == null || baseMaterials.Length == 0)
            {
                continue;
            }

            var overlayMat = new Material(overlayShader) { name = "MeshyUpgradeEmissiveRimMat" };
            ApplyOverlayMaterial(overlayMat, glowColor, glowIntensity);

            var materialsWithOverlay = new Material[baseMaterials.Length + 1];
            for (int i = 0; i < baseMaterials.Length; i++)
            {
                materialsWithOverlay[i] = baseMaterials[i];
            }
            materialsWithOverlay[baseMaterials.Length] = overlayMat;
            renderer.materials = materialsWithOverlay;

            _overlayEntries.Add(new OverlayEntry
            {
                Renderer = renderer,
                BaseMaterialCount = baseMaterials.Length,
                OverlayMaterial = overlayMat
            });
        }
    }

    /// <summary>
    /// Applies rim and glow values to an overlay material
    /// </summary>
    /// <param name="material">Overlay material</param>
    /// <param name="color">Overlay tint color</param>
    /// <param name="strength">Overlay intensity value</param>
    private void ApplyOverlayMaterial(Material material, Color color, float strength)
    {
        if (material == null)
        {
            return;
        }
        if (material.HasProperty(RimColorId))
        {
            material.SetColor(RimColorId, color);
        }
        if (material.HasProperty(RimPowerId))
        {
            material.SetFloat(RimPowerId, rimPower);
        }
        if (material.HasProperty(GlowStrengthId))
        {
            material.SetFloat(GlowStrengthId, strength);
        }
    }

    /// <summary>
    /// Removes upgrade glow overlays and cleans up materials
    /// </summary>
    private void ClearReadyVisuals()
    {
        foreach (var entry in _overlayEntries)
        {
            if (entry.Renderer != null)
            {
                var current = entry.Renderer.materials;
                if (current.Length > entry.BaseMaterialCount)
                {
                    var base_ = new Material[entry.BaseMaterialCount];
                    for (int m = 0; m < entry.BaseMaterialCount; m++)
                    {
                        base_[m] = current[m];
                    }
                    entry.Renderer.materials = base_;
                }
            }
            if (entry.OverlayMaterial != null)
            {
                Destroy(entry.OverlayMaterial);
            }
        }
        _overlayEntries.Clear();
    }

    private void OnDestroy()
    {
        ClearReadyVisuals();
        if (_pendingMeshyModel != null)
        {
            Destroy(_pendingMeshyModel);
        }
    }
}
