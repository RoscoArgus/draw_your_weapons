using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using andywiecko.BurstTriangulator;

public class MeshExtruder : MonoBehaviour
{
    public float extrudeDepth = 0.1f;
    public Material weaponMaterial;
    public Material sideMaterial;

    /// <summary>
    /// Extrudes a 2D contour into a 3D weapon mesh
    /// </summary>
    /// <param name="contour">Ordered contour points</param>
    /// <param name="originalTexture">Original texture used to derive UV mapping</param>
    /// <returns>Generated GameObject</returns>
    public GameObject ExtrudeMesh(List<Vector2> contour, Texture2D originalTexture = null)
    {
        if (contour == null || contour.Count < 3)
        {
            Debug.LogError("Contour too small to extrude.");
            return null;
        }

        int contourPointCount = contour.Count;
        int[] triangulatedIndices = TriangulateContour(contour);

        // Calculate base vertex index for the side geometry
        int sideBase = contourPointCount * 2;
        // Calculate total vertex count: front face + back face + 4 vertices per contour edge for sides
        int vertexCount = contourPointCount * 6;

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] texCoords = new Vector2[vertexCount];

        // Build face and side geometry separately
        BuildFaceVertices(contour, originalTexture, vertices, texCoords, contourPointCount);
        BuildSideVertices(contour, vertices, texCoords, sideBase, contourPointCount);

        List<int> faceTriangles = BuildFaceTriangles(triangulatedIndices, contourPointCount);
        List<int> sideTriangles = BuildSideTriangles(contourPointCount, sideBase);

        // Combine everything into a single mesh with submeshes for different materials
        Mesh mesh = CreateMesh(vertices, texCoords, faceTriangles, sideTriangles);
        return CreateMeshObject(mesh, originalTexture);
    }

    /// <summary>
    /// Triangulates the contour interior
    /// </summary>
    /// <param name="contour">Ordered contour points</param>
    /// <returns>Array of triangle indices produced by triangulation</returns>
    private int[] TriangulateContour(List<Vector2> contour)
    {
        int contourPointCount = contour.Count;

        // Copy contour points into native arrays for triangulator input
        NativeArray<double2> positions = new NativeArray<double2>(contourPointCount, Allocator.Persistent);
        NativeArray<int> constraintEdges = new NativeArray<int>(contourPointCount * 2, Allocator.Persistent);

        try
        {
            for (int i = 0; i < contourPointCount; i++)
            {
                positions[i] = new double2(contour[i].x, contour[i].y);

                constraintEdges[i * 2] = i;
                constraintEdges[i * 2 + 1] = (i + 1) % contourPointCount;
            }

            // Keep the traced outline as a hard boundary - triangulate interior
            using (Triangulator triangulator = new Triangulator(Allocator.Persistent))
            {
                triangulator.Input.Positions = positions;
                triangulator.Input.ConstraintEdges = constraintEdges;
                triangulator.Settings.RestoreBoundary = true;
                triangulator.Run();

                int triangleIndexCount = triangulator.Output.Triangles.Length;
                int[] triangleIndices = new int[triangleIndexCount];
                for (int i = 0; i < triangleIndexCount; i++)
                {
                    triangleIndices[i] = triangulator.Output.Triangles[i];
                }

                Debug.Log($"Triangle count: {triangleIndexCount / 3}");
                Debug.Log($"Triangulator status: {triangulator.Output.Status}");

                return triangleIndices;
            }
        }
        finally
        {
            positions.Dispose();
            constraintEdges.Dispose();
        }
    }

    /// <summary>
    /// Builds front and back face vertices and texture coordinates from the contour
    /// </summary>
    /// <param name="contour">Ordered contour points</param>
    /// <param name="originalTexture">Original texture used to derive UV mapping</param>
    /// <param name="vertices">Array of vertex positions for the generated mesh</param>
    /// <param name="texCoords">Array of texture coordinates for the generated mesh</param>
    /// <param name="contourPointCount">Number of points in the contour</param>
    private void BuildFaceVertices(
        List<Vector2> contour,
        Texture2D originalTexture,
        Vector3[] vertices,
        Vector2[] texCoords,
        int contourPointCount)
    {        
        // De-normalize texture coordinates based on original texture aspect ratio and undo aspect scaling
        float aspect = GetTextureAspect(originalTexture);

        for (int i = 0; i < contourPointCount; i++)
        {
            float x = contour[i].x;
            float y = contour[i].y;

            vertices[i] = new Vector3(x, y, 0f);
            vertices[i + contourPointCount] = new Vector3(x, y, extrudeDepth);

            Vector2 textureCoordinate = new Vector2(x + 0.5f, (y / aspect) + 0.5f);
            texCoords[i] = textureCoordinate;
            texCoords[i + contourPointCount] = textureCoordinate;
        }
    }

    /// <summary>
    /// Builds side-wall quad vertices and perimeter texture coordinates
    /// </summary>
    /// <param name="contour">Ordered contour points</param>
    /// <param name="vertices">Array of vertex positions for the generated mesh</param>
    /// <param name="texCoords">Array of texture coordinates for the generated mesh</param>
    /// <param name="sideBase">Starting vertex index for side-wall geometry</param>
    /// <param name="contourPointCount">Number of points in the contour</param>
    private void BuildSideVertices(
        List<Vector2> contour,
        Vector3[] vertices,
        Vector2[] texCoords,
        int sideBase,
        int contourPointCount)
    {
        // Measure each contour edge to unwrap side texture coordinates along the perimeter
        float[] edgeLengths = new float[contourPointCount];
        for (int i = 0; i < contourPointCount; i++)
        {
            int next = (i + 1) % contourPointCount;
            edgeLengths[i] = Vector2.Distance(contour[i], contour[next]);
        }

        // Give each edge a quad to unwraps side material around the perimeter
        float texCoordOffset = 0f;
        for (int i = 0; i < contourPointCount; i++)
        {
            int next = (i + 1) % contourPointCount;
            int vertexIndex = sideBase + i * 4;

            Vector2 current = contour[i];
            Vector2 nextPoint = contour[next];

            // Each edge becomes a quad
            vertices[vertexIndex + 0] = new Vector3(current.x, current.y, 0f); // Current front
            vertices[vertexIndex + 1] = new Vector3(nextPoint.x, nextPoint.y, 0f); // Next front
            vertices[vertexIndex + 2] = new Vector3(nextPoint.x, nextPoint.y, extrudeDepth); // Next back
            vertices[vertexIndex + 3] = new Vector3(current.x, current.y, extrudeDepth); // Current back

            float texCoordstart = texCoordOffset;
            float texCoordEnd = texCoordOffset + edgeLengths[i];
            texCoords[vertexIndex + 0] = new Vector2(0f, texCoordstart);
            texCoords[vertexIndex + 1] = new Vector2(0f, texCoordEnd);
            texCoords[vertexIndex + 2] = new Vector2(1f, texCoordEnd);
            texCoords[vertexIndex + 3] = new Vector2(1f, texCoordstart);

            texCoordOffset = texCoordEnd;
        }
    }

    /// <summary>
    /// Builds triangle indices for front and back faces
    /// </summary>
    /// <param name="triangulatedIndices">Triangle indices produced by triangulation</param>
    /// <param name="contourPointCount">Number of points in the contour</param>
    /// <returns>List of triangles</returns>
    private List<int> BuildFaceTriangles(int[] triangulatedIndices, int contourPointCount)
    {
        // Reuse vertex positions for front and back faces, but reverse winding order
        List<int> faceTriangles = new List<int>(triangulatedIndices.Length * 2);

        for (int i = 0; i < triangulatedIndices.Length; i++)
        {
            faceTriangles.Add(triangulatedIndices[i]);
        }

        for (int i = 0; i < triangulatedIndices.Length; i += 3)
        {
            faceTriangles.Add(triangulatedIndices[i + 2] + contourPointCount);
            faceTriangles.Add(triangulatedIndices[i + 1] + contourPointCount);
            faceTriangles.Add(triangulatedIndices[i] + contourPointCount);
        }

        return faceTriangles;
    }

    /// <summary>
    /// Builds triangle indices for side-wall quads
    /// </summary>
    /// <param name="contourPointCount">Number of points in the contour</param>
    /// <param name="sideBase">Starting vertex index for side-wall geometry</param>
    /// <returns>List of triangles</returns>
    private List<int> BuildSideTriangles(int contourPointCount, int sideBase)
    {
        // Split side quad into two triangles
        List<int> sideTriangles = new List<int>(contourPointCount * 6);

        for (int i = 0; i < contourPointCount; i++)
        {
            int vertexIndex = sideBase + i * 4;

            sideTriangles.Add(vertexIndex + 0);
            sideTriangles.Add(vertexIndex + 2);
            sideTriangles.Add(vertexIndex + 1);

            sideTriangles.Add(vertexIndex + 0);
            sideTriangles.Add(vertexIndex + 3);
            sideTriangles.Add(vertexIndex + 2);
        }

        return sideTriangles;
    }

    /// <summary>
    /// Creates a two-submesh mesh from vertex, texture-coordinate, and triangle data
    /// </summary>
    /// <param name="vertices">Array of vertex positions for the generated mesh</param>
    /// <param name="texCoords">Array of texture coordinates for the generated mesh</param>
    /// <param name="faceTriangles">Triangle indices for front and back faces</param>
    /// <param name="sideTriangles">Triangle indices for side-wall geometry</param>
    /// <returns>Generated mesh</returns>
    private Mesh CreateMesh(Vector3[] vertices, Vector2[] texCoords, List<int> faceTriangles, List<int> sideTriangles)
    {
        // Use separate submeshes so the front/back and side walls can use different materials
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = texCoords;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(faceTriangles, 0);
        mesh.SetTriangles(sideTriangles, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Creates a weapon GameObject with mesh, renderer, and collider components
    /// </summary>
    /// <param name="mesh">Mesh data</param>
    /// <param name="originalTexture">Original texture used to derive UV mapping</param>
    /// <returns>Generated GameObject</returns>
    private GameObject CreateMeshObject(Mesh mesh, Texture2D originalTexture)
    {
        Material faceMaterial = new Material(weaponMaterial);
        if (originalTexture != null)
        {
            faceMaterial.mainTexture = originalTexture;
        }
        Material edgeMaterial = sideMaterial != null ? new Material(sideMaterial) : faceMaterial;

        // Build a self-contained weapon GameObject to use in-scene
        GameObject weapon = new GameObject("GeneratedWeapon");
        weapon.AddComponent<MeshFilter>().mesh = mesh;

        MeshRenderer renderer = weapon.AddComponent<MeshRenderer>();
        renderer.materials = new Material[] { faceMaterial, edgeMaterial };
        weapon.AddComponent<MeshCollider>().sharedMesh = mesh;

        return weapon;
    }

    /// <summary>
    /// Returns the source texture aspect ratio
    /// </summary>
    /// <param name="originalTexture">Original texture used to derive UV mapping</param>
    /// <returns>Computed floating-point value</returns>
    private float GetTextureAspect(Texture2D originalTexture)
    {
        if (originalTexture == null || originalTexture.width == 0)
        {
            return 1f;
        }
        return (float)originalTexture.height / originalTexture.width;
    }
}
