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

    /// <summary>
    /// Builds an interactable weapon mesh from an input texture
    /// </summary>
    /// <param name="inputTexture">Source texture used to build the weapon mesh</param>
    /// <returns>Generated GameObject</returns>
    public GameObject BuildFromTexture(Texture2D inputTexture)
    {
        if (inputTexture == null)
        {
            Debug.LogError("No input texture was provided to MeshBuilder.");
            return null;
        }

        if (!HasRequiredComponents())
        {
            return null;
        }
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
        }

        if (extrudedWeapon != null)
        {
            // Add PendingMeshyUpgrade - stores the texture for when the job is kicked off
            var pendingUpgrade = extrudedWeapon.AddComponent<PendingMeshyUpgrade>();
            pendingUpgrade.Initialise(this, inputTexture);

            PlaceInFrontOfView(extrudedWeapon);
            MakeInteractable(extrudedWeapon);
        }

        return extrudedWeapon;
    }

    /// <summary>
    /// Replaces an extruded weapon with a Meshy model
    /// </summary>
    /// <param name="extruded">Extruded weapon object</param>
    /// <param name="meshyModel">Meshy model</param>
    public void UpgradeToMeshyModel(GameObject extruded, GameObject meshyModel)
    {
        SwapToMeshyModel(extruded, meshyModel);
    }

    /// <summary>
    /// Checks whether Meshy upgrade dependencies and API credentials are available
    /// </summary>
    private bool CanQueueMeshyUpgrade()
    {
        if (meshyClient == null || meshyCache == null)
        {
            Debug.LogWarning("[MeshBuilder] Meshy upgrade skipped: assign both MeshyClient and MeshyCache.");
            return false;
        }

        if (meshyClient.secrets == null || string.IsNullOrWhiteSpace(meshyClient.secrets.meshyApiKey))
        {
            Debug.LogWarning("[MeshBuilder] Meshy upgrade skipped: MeshyClient secrets/API key are missing.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the primary texture from a weapon renderer
    /// </summary>
    /// <param name="weapon">Weapon object</param>
    /// <returns>Processed texture</returns>
    private Texture2D GetWeaponTexture(GameObject weapon)
    {
        if (weapon == null)
        {
            return null;
        }
        var meshRenderer = weapon.GetComponent<MeshRenderer>();
        if (meshRenderer == null || meshRenderer.sharedMaterials == null || meshRenderer.sharedMaterials.Length == 0)
        {
            return null;
        }
        return meshRenderer.sharedMaterials[0].mainTexture as Texture2D;
    }

    /// <summary>
    /// Swaps extruded weapon for Meshy model, matching transformations and attributes
    /// </summary>
    /// <param name="extruded">Generated placeholder weapon object</param>
    /// <param name="meshyModel">Meshy model that should replace the placeholder object</param>
    private void SwapToMeshyModel(GameObject extruded, GameObject meshyModel)
    {
        if (extruded == null || meshyModel == null)
        {
            return;
        }

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

        var sourceAttributes = extruded.GetComponent<WeaponAttributes>();
        if (sourceAttributes != null)
        {
            var destinationAttributes = meshyModel.AddComponent<WeaponAttributes>();
            destinationAttributes.Slashing  = sourceAttributes.Slashing;
            destinationAttributes.Piercing  = sourceAttributes.Piercing;
            destinationAttributes.Bluntness = sourceAttributes.Bluntness;
        }

        meshyModel.SetActive(true);
        extruded.SetActive(false);
        Destroy(extruded);

        MakeInteractable(meshyModel);
    }

    /// <summary>
    /// Returns the largest renderer bounds dimension for a model
    /// </summary>
    /// <param name="model">Model</param>
    /// <returns>Computed floating-point value</returns>
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
    /// <summary>
    /// Builds a weapon from the assigned test texture
    /// </summary>
    private void TestBuild()
    {
        if (testTexture != null)
        {
            BuildFromTexture(testTexture);
        }
    }

    /// <summary>
    /// Verifies that all necessary pipeline component references are assigned
    /// </summary>
    private bool HasRequiredComponents()
    {
        if (drawingExtractor == null || contourTracer == null || meshExtruder == null || weaponAnalyser == null)
        {
            Debug.LogError("MeshBuilder is missing one or more pipeline component references.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Ensures contour winding is clockwise before extrusion
    /// </summary>
    /// <param name="contour">Ordered contour points</param>
    /// <returns>List of contour points</returns>
    private List<Vector2> EnsureClockwise(List<Vector2> contour)
    {
        if (contour == null || contour.Count < 3)
        {
            return contour;
        }
        if (CalculateSignedArea(contour) > 0f)
        {
            contour.Reverse();
        }
        return contour;
    }

    /// <summary>
    /// Calculates the contour signed
    /// </summary>
    /// <param name="contour">Ordered contour points</param>
    /// <returns>Computed floating-point value</returns>
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

    /// <summary>
    /// Removes intersecting contour segments by trimming loops
    /// </summary>
    /// <param name="contour">Ordered contour points</param>
    /// <returns>List of contour points</returns>
    private List<Vector2> RemoveSelfIntersections(List<Vector2> contour)
    {
        if (contour == null || contour.Count < 4)
        {
            return contour;
        }
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
                    {
                        continue;
                    }
                    Vector2 a1 = contour[i];
                    Vector2 a2 = contour[(i + 1) % contour.Count];
                    Vector2 b1 = contour[j];
                    Vector2 b2 = contour[(j + 1) % contour.Count];

                    if (!EdgesIntersect(a1, a2, b1, b2))
                    {
                        continue;
                    }
                    contour.RemoveRange(i + 1, j - i);
                    changed = true;
                    break;
                }
                if (changed)
                {
                    break;
                }
            }
        }

        Debug.Log($"Contour after self-intersection removal: {contour.Count} points");
        return contour;
    }

    /// <summary>
    /// Checks if two line segments intersect
    /// </summary>
    /// <param name="a1">Start point of the first edge segment</param>
    /// <param name="a2">End point of the first edge segment</param>
    /// <param name="b1">Start point of the second edge segment</param>
    /// <param name="b2">End point of the second edge segment</param>
    /// <returns>True when two lines intersect, false otherwise</returns>
    private bool EdgesIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        float d1x = a2.x - a1.x;
        float d1y = a2.y - a1.y;
        float d2x = b2.x - b1.x;
        float d2y = b2.y - b1.y;
        float cross = d1x * d2y - d1y * d2x;
        if (Mathf.Abs(cross) < 1e-10f)
        {
            return false;
        }
        float t = ((b1.x - a1.x) * d2y - (b1.y - a1.y) * d2x) / cross;
        float u = ((b1.x - a1.x) * d1y - (b1.y - a1.y) * d1x) / cross;
        return t > 0.001f && t < 0.999f && u > 0.001f && u < 0.999f;
    }

    /// <summary>
    /// Adds interaction, collider, and damage components to a generated weapon
    /// </summary>
    /// <param name="gameObject">GameObject to be configured</param>
    private void MakeInteractable(GameObject gameObject)
    {
        Mesh mesh = null;
        var filter = gameObject.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = gameObject.GetComponentInChildren<MeshFilter>();
        }
        if (filter != null)
        {
            mesh = filter.sharedMesh;
        }

        if (mesh == null)
        {
            Debug.LogWarning("[MeshBuilder] MakeInteractable: no MeshFilter found, skipping.");
            return;
        }

        var interaction = gameObject.GetComponent<MeshInteraction>();
        if (interaction == null)
        {
            interaction = gameObject.AddComponent<MeshInteraction>();
        }
        interaction.swingClip = swingClip;
        interaction.audioSource = sceneAudioSource;
        interaction.Initialise(mesh);

        MeshCollider hitCollider = null;
        MeshCollider[] meshColliders = gameObject.GetComponents<MeshCollider>();
        for (int index = 0; index < meshColliders.Length; index++)
        {
            MeshCollider meshCollider = meshColliders[index];
            if (meshCollider != null && meshCollider.isTrigger)
            {
                hitCollider = meshCollider;
                break;
            }
        }
        if (hitCollider == null)
        {
            hitCollider = gameObject.AddComponent<MeshCollider>();
        }
        hitCollider.sharedMesh = mesh;
        hitCollider.convex = true;
        hitCollider.isTrigger = true;

        if (gameObject.GetComponent<WeaponDamageDealer>() == null)
        {
            gameObject.AddComponent<WeaponDamageDealer>();
        }
    }

    /// <summary>
    /// Places the weapon in front of the view transform
    /// </summary>
    /// <param name="weapon">Weapon object</param>
    private void PlaceInFrontOfView(GameObject weapon)
    {
        if (weapon == null)
        {
            return;
        }
        Transform view = ResolveSpawnViewTransform();
        Vector3 forward = view.forward;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        float distance = Mathf.Max(0.1f, spawnDistance);
        Vector3 position = view.position + forward.normalized * distance + view.up * spawnVerticalOffset;
        Quaternion rotation = Quaternion.LookRotation(forward.normalized, view.up);

        weapon.transform.SetPositionAndRotation(position, rotation);
    }

    /// <summary>
    /// Resolves the spawn view from an explicit transform, main camera, or this object
    /// </summary>
    private Transform ResolveSpawnViewTransform()
    {
        if (spawnViewTransform != null)
        {
            return spawnViewTransform;
        }
        if (Camera.main != null)
        {
            return Camera.main.transform;
        }
        return transform;
    }
}
