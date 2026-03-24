using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Holds the final normalized attributes of a weapon.
/// </summary>
[System.Serializable]
public struct WeaponStats 
{
    public float Slashing;
    public float Piercing;
    public float Bluntness;
}

/// <summary>
/// Analyzes the geometric contour of a drawn weapon to calculate its 
/// physical RPG-style attributes based on its shape features.
/// </summary>
public class WeaponAnalyser : MonoBehaviour
{
    [Header("Detection Thresholds")]
    [Tooltip("Maximum inner angle (in degrees) to be considered a sharp spike.")]
    [Range(10f, 90f)] public float spikeAngleThreshold = 50f;
    
    [Tooltip("Maximum turn angle (in degrees) before an edge is no longer considered smooth/continuous.")]
    [Range(5f, 45f)] public float smoothEdgeAngleThreshold = 15f;

    [Header("Bluntness Weights")]
    public float bluntCircularityWeight = 25f;
    public float bluntInvAspectWeight = 30f;
    public float bluntTopHeavyWeight = 75f;

    [Header("Slashing Weights")]
    public float slashAspectWeight = 5f;
    public float slashSmoothEdgeWeight = 35f;
    public float slashSpikePenaltyMultiplier = 0.75f;

    [Header("Piercing Weights")]
    public float pierceSpikeBaseWeight = 4f;
    public float pierceAspectWeight = 2f;

    /// <summary>
    /// Analyses a weapon contour and maps geometric features to weapon stats.
    /// </summary>
    /// <param name="contour">List of 2D points representing the weapon's outline.</param>
    /// <returns>A WeaponStats struct with Slashing, Piercing, and Bluntness balanced to 1.0 (100%)</returns>
    public WeaponStats AnalyseShape(List<Vector2> contour)
    {
        if (contour == null || contour.Count < 3)
        {
            Debug.LogWarning("WeaponAnalyser: Contour too small to analyse.");
            return new WeaponStats { Bluntness = 1f };
        }

        float area = CalculateArea(contour);
        float perimeter = CalculatePerimeter(contour);
        float aspectRatio = CalculateAspectRatio(contour);
        aspectRatio = Mathf.Max(aspectRatio, 0.1f);

        int sharpPoints = CountSpikes(contour, spikeAngleThreshold);

        // Calculate weight distribution (Top-Heaviness)
        Vector2 centroid = CalculateCentroid(contour);
        GetBoundsInfo(contour, out Vector2 bbCenter, out float maxDimension);
        float weightOffset = maxDimension > 0 ? (Vector2.Distance(centroid, bbCenter) / maxDimension) : 0f;

        // Calculate Blade Edge Smoothness
        float smoothEdgeRatio = CalculateMaxSmoothEdgeRatio(contour, smoothEdgeAngleThreshold, perimeter);

        // BLUNTNESS:
        // Highly circular (mace head) and low aspect ratio (not long) gives huge bluntness
        // Increased by top-heaviness (center of mass far from spatial center)
        float circularity = (4f * Mathf.PI * area) / Mathf.Max(perimeter * perimeter, 0.0001f);
        float topHeavinessBonus = weightOffset * bluntTopHeavyWeight; 
        
        float rawBlunt = (circularity * bluntCircularityWeight) + (bluntInvAspectWeight / aspectRatio) + topHeavinessBonus;

        // SLASHING:
        // Driven by length (aspectRatio) and having a continuous uninterrupted blade edge
        float bladeBonus = smoothEdgeRatio * slashSmoothEdgeWeight;
        float rawSlash = (aspectRatio * slashAspectWeight) + bladeBonus; 
        
        // Penalty: Too many spikes ruin slicing capability
        if (sharpPoints > 2) 
        {
            rawSlash /= (sharpPoints * slashSpikePenaltyMultiplier); 
        }

        // PIERCING:
        // Drastically increased by the presence of sharp spikes, plus length
        float rawPierce = (Mathf.Pow(sharpPoints, 1.5f) * pierceSpikeBaseWeight) + (aspectRatio * pierceAspectWeight);

        // Normalise scores to sum to 1.0
        float total = rawBlunt + rawSlash + rawPierce;
        
        if (total == 0f) total = 1f;

        return new WeaponStats {
            Slashing = rawSlash / total,
            Piercing = rawPierce / total,
            Bluntness = rawBlunt / total
        };
    }

    /// <summary>
    /// Computes the total area of the shape using the shoelace formula
    /// </summary>
    private float CalculateArea(List<Vector2> contour)
    {
        float area = 0f;
        for (int i = 0; i < contour.Count; i++)
        {
            int next = (i + 1) % contour.Count;
            area += (contour[i].x * contour[next].y) - (contour[next].x * contour[i].y);
        }
        return Mathf.Abs(area / 2f);
    }

    /// <summary>
    /// Computes the total perimeter length of the contour
    /// </summary>
    private float CalculatePerimeter(List<Vector2> contour)
    {
        float perimeter = 0f;
        for (int i = 0; i < contour.Count; i++)
        {
            int next = (i + 1) % contour.Count;
            perimeter += Vector2.Distance(contour[i], contour[next]);
        }
        return perimeter == 0 ? 0.001f : perimeter;
    }

    /// <summary>
    /// Calculates the aspect ratio of the bounding box (longest dimension / shortest dimension)
    /// </summary>
    private float CalculateAspectRatio(List<Vector2> contour)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var p in contour)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        float width = maxX - minX;
        float height = maxY - minY;
        
        if (width == 0 || height == 0) return 1f;

        return Mathf.Max(width / height, height / width);
    }

    /// <summary>
    /// Calculates the center of mass (centroid) of the shape
    /// </summary>
    private Vector2 CalculateCentroid(List<Vector2> contour)
    {
        float cx = 0f;
        float cy = 0f;
        float signedArea = 0f;
        
        for (int i = 0; i < contour.Count; i++)
        {
            int next = (i + 1) % contour.Count;
            float cross = (contour[i].x * contour[next].y) - (contour[next].x * contour[i].y);
            signedArea += cross;
            
            cx += (contour[i].x + contour[next].x) * cross;
            cy += (contour[i].y + contour[next].y) * cross;
        }
        
        signedArea /= 2f;
        
        // Failsafe for weird shapes
        if (Mathf.Abs(signedArea) < 0.0001f) return contour[0];
        
        cx /= (6f * signedArea);
        cy /= (6f * signedArea);
        
        return new Vector2(cx, cy);
    }

    /// <summary>
    /// Gets the geometric center and the maximum dimension of the shape's bounding box
    /// </summary>
    private void GetBoundsInfo(List<Vector2> contour, out Vector2 center, out float maxDimension)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var p in contour)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        center = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
        maxDimension = Mathf.Max(maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Counts the number of sharp, convex points (spikes) pointing outwards from the contour
    /// </summary>
    private int CountSpikes(List<Vector2> contour, float maxInnerAngle)
    {
        int spikes = 0;
        int n = contour.Count;

        // Use stepping to look past tiny local vertex anomalies
        int step = Mathf.Max(1, n / 50); 

        for (int i = 0; i < n; i++)
        {
            int prev = (i - step + n) % n;
            int next = (i + step) % n;

            Vector2 current = contour[i];
            Vector2 p = contour[prev];
            Vector2 nx = contour[next];

            Vector2 dirIn = current - p;
            Vector2 dirOut = nx - current;

            if (dirIn == Vector2.zero || dirOut == Vector2.zero) continue;

            // In a clockwise contour, right turns are convex
            float cross = dirIn.x * dirOut.y - dirIn.y * dirOut.x;

            if (cross < 0) // Convex vertex
            {
                Vector2 toPrev = (p - current).normalized;
                Vector2 toNext = (nx - current).normalized;

                float dot = Mathf.Clamp(Vector2.Dot(toPrev, toNext), -1f, 1f);
                float innerAngle = Mathf.Acos(dot) * Mathf.Rad2Deg;

                if (innerAngle <= maxInnerAngle)
                {
                    spikes++;
                    i += step; // Skip a few iterations to avoid counting the same spike 
                }
            }
        }
        return spikes;
    }

    /// <summary>
    /// Finds the longest continuous straight or smooth curve along the perimeter
    /// Resets when a sharp turn is encountered
    /// </summary>
    private float CalculateMaxSmoothEdgeRatio(List<Vector2> contour, float maxTurnAngle, float totalPerimeter)
    {
        float maxEdgeLength = 0f;
        float currentEdgeLength = 0f;
        int n = contour.Count;
        
        int step = Mathf.Max(1, n / 50);

        // Run twice to ensure we seamlessly wrap around the lists start/end index
        for (int i = 0; i < n * 2; i++) 
        {
            int prev = (i - step + n) % n;
            int curr = i % n;
            int next = (i + step) % n;

            Vector2 dirIn = (contour[curr] - contour[prev]).normalized;
            Vector2 dirOut = (contour[next] - contour[curr]).normalized;

            if (dirIn != Vector2.zero && dirOut != Vector2.zero)
            {
                float dot = Mathf.Clamp(Vector2.Dot(dirIn, dirOut), -1f, 1f);
                float turnAngle = Mathf.Acos(dot) * Mathf.Rad2Deg;

                // If turn angle is small, it's a smooth/continuous edge
                if (turnAngle <= maxTurnAngle)
                {
                    currentEdgeLength += Vector2.Distance(contour[curr], contour[(curr + 1) % n]);
                    
                    if (currentEdgeLength > maxEdgeLength) 
                    {
                        maxEdgeLength = currentEdgeLength;
                    }

                    // Failsafe so perfect circles don't infinitely accumulate
                    if (maxEdgeLength >= totalPerimeter) 
                    {
                        return 1f; 
                    }
                }
                else
                {
                    // Sharp turn breaks the smooth edge
                    currentEdgeLength = 0f;
                }
            }
        }

        return Mathf.Clamp01(maxEdgeLength / totalPerimeter);
    }
}
