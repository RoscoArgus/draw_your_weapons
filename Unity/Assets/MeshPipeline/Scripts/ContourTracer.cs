using System.Collections.Generic;
using UnityEngine;

public class ContourTracer : MonoBehaviour
{
    // Define 8-connected direction for contour tracing step
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1)
    };

    // Variable to control how much the contour is simplifed during tracing
    [Range(1, 20)]
    public int simplificationStep = 4;

    /// <summary>
    /// Trace the contour of a binary image using a modified Moore-Neighbor tracing algorithm
    /// </summary>
    /// <param name="binaryImage">Binary source texture used for contour tracing</param>
    /// <returns>List of points comprising the contour</returns>
    public List<Vector2> TraceContour(Texture2D binaryImage)
    {
        int width = binaryImage.width;
        int height = binaryImage.height;
        Color[] pixels = binaryImage.GetPixels();
        List<Vector2> contour = new List<Vector2>();

        Vector2Int start = FindStartPixel(pixels, width, height);
        if (start.x == -1)
        {
            Debug.LogWarning("No drawing found - check threshold value.");
            return contour;
        }

        Debug.Log($"Start pixel found at: {start}");

        Vector2Int current = start;
        int currentDirection = 0;
        int stepCount = 0;
        int maxSteps = width * height;
        int sampleInterval = Mathf.Max(1, simplificationStep);
        float aspect = (float)height / width;

        // Step around the contour until we reach the start point again
        do
        {
            if (stepCount % sampleInterval == 0)
            {
                contour.Add(ToContourPoint(current, width, height, aspect));
            }
            Vector2Int next;
            int nextDirection;
            if (!TryFindNextBoundaryPixel(current, currentDirection, pixels, width, height, out next, out nextDirection))
            {
                break;
            }
            current = next;
            currentDirection = nextDirection;
            stepCount++;
        }
        while (!(current == start && stepCount > 2) && stepCount < maxSteps);

        Debug.Log($"Contour point count: {contour.Count}");
        return contour;
    }

    /// <summary>
    /// Find the first black pixel in the image in raster order
    /// </summary>
    /// <param name="pixels">Array of image pixels</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <returns>Coordinate of first detected black pixel</returns>
    private Vector2Int FindStartPixel(Color[] pixels, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (IsBlack(pixels[y * width + x]))
                {
                    return new Vector2Int(x, y);
                }
            }
        }

        return new Vector2Int(-1, -1);
    }

    /// <summary>
    /// Look for the next black pixel in the contour from current position and direction
    /// </summary>
    /// <param name="current">Current contour pixel</param>
    /// <param name="currentDirection">Current direction index</param>
    /// <param name="pixels">Array of image pixels</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="next">Next boundary pixel found during contour tracing</param>
    /// <param name="nextDirection">Direction index to continue contour traversal from</param>
    /// <returns>True when pixel found, false when no pixel found</returns>
    private bool TryFindNextBoundaryPixel(
        Vector2Int current,
        int currentDirection,
        Color[] pixels,
        int width,
        int height,
        out Vector2Int next,
        out int nextDirection)
    {
        int searchDirection = (currentDirection + 6) % Directions.Length;

        for (int i = 0; i < Directions.Length; i++)
        {
            int directionIndex = (searchDirection + i) % Directions.Length;
            Vector2Int candidate = current + Directions[directionIndex];

            if (IsInBounds(candidate, width, height) && IsBlack(pixels[candidate.y * width + candidate.x]))
            {
                next = candidate;
                nextDirection = directionIndex;
                return true;
            }
        }

        next = current;
        nextDirection = currentDirection;
        return false;
    }

    /// <summary>
    /// Convert pixel coordinates to normalized contour points
    /// </summary>
    /// <param name="point">Pixel coordinate to convert</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="aspect">Aspect ratio (used to scale y coordinate)</param>
    /// <returns>Computed 2D point</returns>
    private Vector2 ToContourPoint(Vector2Int point, int width, int height, float aspect)
    {
        return new Vector2(
            (float)point.x / width - 0.5f,
            ((float)point.y / height - 0.5f) * aspect
        );
    }

    /// <summary>
    /// Checks if a given pixel is considered black
    /// </summary>
    /// <param name="color">Pixel color value</param>
    /// <returns>True when pixel is considered black, false when not</returns>
    private bool IsBlack(Color color)
    {
        return color.r < 0.1f;
    }

    /// <summary>
    /// Checks if a given point is within the bounds of the image
    /// </summary>
    /// <param name="point">Point to test</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <returns>True when the condition is satisfied; otherwise, false</returns>
    private bool IsInBounds(Vector2Int point, int width, int height)
    {
        return point.x >= 0 && point.x < width && point.y >= 0 && point.y < height;
    }
}
