using System.Collections.Generic;
using UnityEngine;

public class MeshBuilder : MonoBehaviour
{
    [Header("Pipeline Components")]
    public DrawingExtractor drawingExtractor;
    public ContourTracer contourTracer;
    public MeshExtruder meshExtruder;

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

        // Extract, trace, and cleanup polygons in drawing before extrusion
        Texture2D binary = drawingExtractor.ProcessImage(inputTexture);
        List<Vector2> contour = contourTracer.TraceContour(binary);
        contour = EnsureClockwise(contour);
        contour = RemoveSelfIntersections(contour);

        Debug.Log($"Binary texture size: {binary.width}x{binary.height}");
        Debug.Log($"Contour point count: {contour.Count}");

        return meshExtruder.ExtrudeMesh(contour, inputTexture);
    }

    [ContextMenu("Test Build")]
    private void TestBuild()
    {
        if (testTexture != null)
            BuildFromTexture(testTexture);
    }

    private bool HasRequiredComponents()
    {
        if (drawingExtractor == null || contourTracer == null || meshExtruder == null)
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

        // Triangulation expects consistent winding order
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

        // Trim looped segments out of the contour so the polygon remains valid for extrusion
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
}
