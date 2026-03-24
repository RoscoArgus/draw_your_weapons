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
        Vector2[] uvs = new Vector2[vertexCount];

        // Build face and side geometry separately
        BuildFaceVertices(contour, originalTexture, vertices, uvs, contourPointCount);
        BuildSideVertices(contour, vertices, uvs, sideBase, contourPointCount);

        List<int> faceTriangles = BuildFaceTriangles(triangulatedIndices, contourPointCount);
        List<int> sideTriangles = BuildSideTriangles(contourPointCount, sideBase);

        // Combine everything into a single mesh with submeshes for different materials
        Mesh mesh = CreateMesh(vertices, uvs, faceTriangles, sideTriangles);
        return CreateMeshObject(mesh, originalTexture);
    }

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
                    triangleIndices[i] = triangulator.Output.Triangles[i];

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

    private void BuildFaceVertices(
        List<Vector2> contour,
        Texture2D originalTexture,
        Vector3[] vertices,
        Vector2[] uvs,
        int contourPointCount)
    {        
        // De-normalise UVs based on original texture aspect ratio and undo aspect scaling
        float aspect = GetTextureAspect(originalTexture);

        for (int i = 0; i < contourPointCount; i++)
        {
            float x = contour[i].x;
            float y = contour[i].y;

            vertices[i] = new Vector3(x, y, 0f);
            vertices[i + contourPointCount] = new Vector3(x, y, extrudeDepth);

            Vector2 uv = new Vector2(x + 0.5f, (y / aspect) + 0.5f);
            uvs[i] = uv;
            uvs[i + contourPointCount] = uv;
        }
    }

    private void BuildSideVertices(
        List<Vector2> contour,
        Vector3[] vertices,
        Vector2[] uvs,
        int sideBase,
        int contourPointCount)
    {
        // Measure each contour edge to unwrap the side UVs along perimeter
        float[] edgeLengths = new float[contourPointCount];
        for (int i = 0; i < contourPointCount; i++)
        {
            int next = (i + 1) % contourPointCount;
            edgeLengths[i] = Vector2.Distance(contour[i], contour[next]);
        }

        // Give each edge a quad to unwraps side material around the perimeter
        float uvOffset = 0f;
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

            float uvStart = uvOffset;
            float uvEnd = uvOffset + edgeLengths[i];
            uvs[vertexIndex + 0] = new Vector2(0f, uvStart);
            uvs[vertexIndex + 1] = new Vector2(0f, uvEnd);
            uvs[vertexIndex + 2] = new Vector2(1f, uvEnd);
            uvs[vertexIndex + 3] = new Vector2(1f, uvStart);

            uvOffset = uvEnd;
        }
    }

    private List<int> BuildFaceTriangles(int[] triangulatedIndices, int contourPointCount)
    {
        // Reuse vertex positions for front and back faces, but reverse winding order
        List<int> faceTriangles = new List<int>(triangulatedIndices.Length * 2);

        for (int i = 0; i < triangulatedIndices.Length; i++)
            faceTriangles.Add(triangulatedIndices[i]);

        for (int i = 0; i < triangulatedIndices.Length; i += 3)
        {
            faceTriangles.Add(triangulatedIndices[i + 2] + contourPointCount);
            faceTriangles.Add(triangulatedIndices[i + 1] + contourPointCount);
            faceTriangles.Add(triangulatedIndices[i] + contourPointCount);
        }

        return faceTriangles;
    }

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

    private Mesh CreateMesh(Vector3[] vertices, Vector2[] uvs, List<int> faceTriangles, List<int> sideTriangles)
    {
        // Use separate submeshes so the front/back and side walls can use different materials
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(faceTriangles, 0);
        mesh.SetTriangles(sideTriangles, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private GameObject CreateMeshObject(Mesh mesh, Texture2D originalTexture)
    {
        Material faceMaterial = new Material(weaponMaterial);
        if (originalTexture != null)
            faceMaterial.mainTexture = originalTexture;

        Material edgeMaterial = sideMaterial != null ? new Material(sideMaterial) : faceMaterial;

        // Build a self-contained weapon GameObject to use in-scene
        GameObject weapon = new GameObject("GeneratedWeapon");
        weapon.AddComponent<MeshFilter>().mesh = mesh;

        MeshRenderer renderer = weapon.AddComponent<MeshRenderer>();
        renderer.materials = new Material[] { faceMaterial, edgeMaterial };
        weapon.AddComponent<MeshCollider>().sharedMesh = mesh;

        return weapon;
    }

    private float GetTextureAspect(Texture2D originalTexture)
    {
        if (originalTexture == null || originalTexture.width == 0)
            return 1f;

        return (float)originalTexture.height / originalTexture.width;
    }
}
