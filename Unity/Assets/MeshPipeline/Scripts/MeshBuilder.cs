using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

using Debug = UnityEngine.Debug;

public class MeshBuilder : MonoBehaviour
{
    [Header("Pipeline Components")]
    public DrawingExtractor drawingExtractor;
    public ContourTracer contourTracer;
    public MeshExtruder meshExtruder;
    public WeaponAnalyser weaponAnalyser;

    [Header("Meshy Integration")]
    public MeshyClient meshyClient;
    public MeshyCache meshyCache;

    [Header("Audio")]
    public AudioClip swingClip;
    public AudioSource sceneAudioSource;
    
    [Header("Spawn Placement")]
    public Transform spawnViewTransform;
    [Min(0.1f)] public float spawnDistance = 0.8f;
    public float spawnVerticalOffset = -0.08f;

    [Header("Test Input")]
    public Texture2D testTexture;

    public GameObject BuildFromTexture(Texture2D inputTexture)
    {
        if (inputTexture == null)
        {
            Debug.LogError("No input texture was provided to MeshBuilder.");
            return null;
        }

        if (!HasRequiredComponents())
            return null;

        Texture2D binary = drawingExtractor.ProcessImage(inputTexture);
        List<Vector2> contour = contourTracer.TraceContour(binary);
        contour = EnsureClockwise(contour);
        contour = RemoveSelfIntersections(contour);

        Debug.Log($"Binary texture size: {binary.width}x{binary.height}");
        Debug.Log($"Contour point count: {contour.Count}");

        GameObject extrudedWeapon = meshExtruder.ExtrudeMesh(contour, inputTexture);

        if (extrudedWeapon != null && weaponAnalyser != null)
        {
            WeaponStats stats = weaponAnalyser.AnalyseShape(contour);
            WeaponAttributes attributes = extrudedWeapon.AddComponent<WeaponAttributes>();
            attributes.Slashing = stats.Slashing;
            attributes.Piercing = stats.Piercing;
            attributes.Bluntness = stats.Bluntness;
            Debug.Log($"Weapon Attributes --> S: {stats.Slashing*100:F1}%, P: {stats.Piercing*100:F1}%, B: {stats.Bluntness*100:F1}%");
        }

        if (extrudedWeapon != null)
        {
            PlaceInFrontOfView(extrudedWeapon);
            MakeInteractable(extrudedWeapon);
        }

        return extrudedWeapon;
    }

    /// <summary>
    /// Replaces the extruded mesh with the Meshy-generated one, preserving
    /// WeaponAttributes and world transform.
    /// </summary>
    
    bool meshyJob = false;

    public void UpgradeToMeshyModel(GameObject extruded, GameObject meshyModel)
    {

        if (meshyJob) 
        {
            meshyJob = !meshyJob;
            if (meshyClient != null && meshyCache != null)
            {
                Texture2D inputTexture = (Texture2D)extruded.GetComponent<MeshRenderer>().materials[0].mainTexture;
                meshyClient.GenerateFromTexture(
                    inputTexture,
                    meshyCache,
                    onComplete: meshyModel =>
                    {
                        if (extruded == null)
                        {
                            Debug.LogWarning("[MeshBuilder] Extruded weapon no longer exists; discarding Meshy model.");
                            if (meshyModel != null)
                                Destroy(meshyModel);
                            return;
                        }

                        var pendingUpgrade = extruded.GetComponent<PendingMeshyUpgrade>();
                        if (pendingUpgrade == null)
                            pendingUpgrade = extruded.AddComponent<PendingMeshyUpgrade>();

                        pendingUpgrade.SetPendingUpgrade(this, meshyModel, new Color(0.6f, 0.1f, 1f, 1f));
                        Debug.Log("[MeshBuilder] Meshy model ready. Use the weapon context menu to upgrade.");
                    },
                    onError: err => Debug.LogWarning($"[MeshBuilder] Meshy generation failed, keeping extruded mesh. Reason: {err}")
                );
            }
        } 
        else 
        {
            meshyJob = !meshyJob;
            SwapToMeshyModel(extruded, meshyModel);
        }
    }

    private void SwapToMeshyModel(GameObject extruded, GameObject meshyModel)
    {
        if (extruded == null || meshyModel == null) return;

        meshyModel.transform.SetPositionAndRotation(
            extruded.transform.position,
            extruded.transform.rotation);

        // Calculate height of both models to match scale
        float extrudedHeight = GetModelHeight(extruded);
        float meshyHeight = GetModelHeight(meshyModel);

        if (meshyHeight > 0.01f)
        {
            float heightRatio = extrudedHeight / meshyHeight;
            meshyModel.transform.localScale = Vector3.one * heightRatio;
        }
        else
        {
            meshyModel.transform.localScale = extruded.transform.localScale;
        }

        var src = extruded.GetComponent<WeaponAttributes>();
        if (src != null)
        {
            var dst = meshyModel.AddComponent<WeaponAttributes>();
            dst.Slashing  = src.Slashing;
            dst.Piercing  = src.Piercing;
            dst.Bluntness = src.Bluntness;
        }

        meshyModel.SetActive(true);
        extruded.SetActive(false);
        Destroy(extruded);

        MakeInteractable(meshyModel);
    }

    private float GetModelHeight(GameObject model)
    {
        Bounds bounds = new Bounds();
        bool foundRenderer = false;

        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!foundRenderer)
            {
                bounds = renderer.bounds;
                foundRenderer = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        // Use the largest dimension of the bounding box for orientation-independent scaling
        if (foundRenderer)
        {
            Vector3 size = bounds.size;
            return Mathf.Max(size.x, size.y, size.z);
        }

        return 1f;
    }

    [ContextMenu("Test Build")]
    private void TestBuild()
    {
        if (testTexture != null)
            BuildFromTexture(testTexture);
    }

    private bool HasRequiredComponents()
    {
        if (drawingExtractor == null || contourTracer == null || meshExtruder == null || weaponAnalyser == null)
        {
            Debug.LogError("MeshBuilder is missing one or more pipeline component references.");
            return false;
        }
        return true;
    }

    private List<Vector2> EnsureClockwise(List<Vector2> contour)
    {
        if (contour == null || contour.Count < 3)
            return contour;
        if (CalculateSignedArea(contour) > 0f)
            contour.Reverse();
        return contour;
    }

    private float CalculateSignedArea(List<Vector2> contour)
    {
        float signedArea = 0f;
        for (int i = 0; i < contour.Count; i++)
        {
            int next = (i + 1) % contour.Count;
            signedArea += contour[i].x * contour[next].y;
            signedArea -= contour[next].x * contour[i].y;
        }
        return signedArea;
    }

    private List<Vector2> RemoveSelfIntersections(List<Vector2> contour)
    {
        if (contour == null || contour.Count < 4)
            return contour;

        bool changed = true;
        int remainingPasses = 10;
        while (changed && remainingPasses-- > 0)
        {
            changed = false;
            for (int i = 0; i < contour.Count; i++)
            {
                for (int j = i + 2; j < contour.Count; j++)
                {
                    if (i == 0 && j == contour.Count - 1)
                        continue;

                    Vector2 a1 = contour[i];
                    Vector2 a2 = contour[(i + 1) % contour.Count];
                    Vector2 b1 = contour[j];
                    Vector2 b2 = contour[(j + 1) % contour.Count];

                    if (!EdgesIntersect(a1, a2, b1, b2))
                        continue;
                    contour.RemoveRange(i + 1, j - i);
                    changed = true;
                    break;
                }
                if (changed)
                    break;
            }
        }

        Debug.Log($"Contour after self-intersection removal: {contour.Count} points");
        return contour;
    }

    private bool EdgesIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        float d1x = a2.x - a1.x;
        float d1y = a2.y - a1.y;
        float d2x = b2.x - b1.x;
        float d2y = b2.y - b1.y;
        float cross = d1x * d2y - d1y * d2x;
        if (Mathf.Abs(cross) < 1e-10f)
            return false;
        float t = ((b1.x - a1.x) * d2y - (b1.y - a1.y) * d2x) / cross;
        float u = ((b1.x - a1.x) * d1y - (b1.y - a1.y) * d1x) / cross;
        return t > 0.001f && t < 0.999f && u > 0.001f && u < 0.999f;
    }

    private void MakeInteractable(GameObject obj)
    {
        Mesh mesh = null;
        var filter = obj.GetComponent<MeshFilter>();
        if (filter == null) filter = obj.GetComponentInChildren<MeshFilter>();
        if (filter != null) mesh = filter.sharedMesh;

        if (mesh == null)
        {
            Debug.LogWarning("[MeshBuilder] MakeInteractable: no MeshFilter found, skipping.");
            return;
        }

        var interaction = obj.AddComponent<MeshInteraction>();
        interaction.swingClip = swingClip;
        interaction.audioSource = sceneAudioSource;
        interaction.Initialise(mesh);

        var hitCollider = obj.AddComponent<MeshCollider>();
        hitCollider.sharedMesh = mesh;
        hitCollider.convex = true;
        hitCollider.isTrigger = true;

        obj.AddComponent<WeaponDamageDealer>();
    }

    private void PlaceInFrontOfView(GameObject weapon)
    {
        if (weapon == null)
            return;

        Transform view = ResolveSpawnViewTransform();
        Vector3 forward = view.forward;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        float distance = Mathf.Max(0.1f, spawnDistance);
        Vector3 position = view.position + forward.normalized * distance + view.up * spawnVerticalOffset;
        Quaternion rotation = Quaternion.LookRotation(forward.normalized, view.up);

        weapon.transform.SetPositionAndRotation(position, rotation);
    }

    private Transform ResolveSpawnViewTransform()
    {
        if (spawnViewTransform != null)
            return spawnViewTransform;

        if (Camera.main != null)
            return Camera.main.transform;

        return transform;
    }
}