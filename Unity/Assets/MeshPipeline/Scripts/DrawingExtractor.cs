using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

using Debug = UnityEngine.Debug;
using Application = UnityEngine.Application;

public class DrawingExtractor : MonoBehaviour
{
    private static readonly Vector2Int[] FloodFillDirections =
    {
        new Vector2Int(1, 0),   // E
        new Vector2Int(-1, 0),  // E
        new Vector2Int(0, 1),   // S
        new Vector2Int(0, -1),  // N
        new Vector2Int(1, 1),   // SE
        new Vector2Int(1, -1),  // NE
        new Vector2Int(-1, 1),  // SW
        new Vector2Int(-1, -1)  // NW
    };

    [Header("Background Normalisation")]
    [Range(0.05f, 0.5f)]
    public float backgroundBlurFraction = 0.15f;

    [Range(0.3f, 0.95f)]
    public float backgroundNormalizeThreshold = 0.75f;

    [Header("Dilation & Closing")]
    [Range(1, 14)]
    public int dilateRadius = 5;

    [Range(0, 40)]
    public int closingRadius = 15;

    [Header("Border")]
    [Range(0f, 0.12f)]
    public float borderFraction = 0.03f;

    /// <summary>
    /// Processes the input texture into a cleaned black-and-white silhouette
    /// </summary>
    /// <param name="input">Input texture</param>
    /// <returns>Black-and-white silhouette texture</returns>
    public Texture2D ProcessImage(Texture2D input)
    {
        if (input == null)
        {
            Debug.LogError("No input texture was provided to DrawingExtractor.");
            return null;
        }

        int width = input.width;
        int height = input.height;
        Color[] source = input.GetPixels();

        SaveDebugTexture(input, "DrawingExtractor_raw_input.png");

        Color[] binary = BackgroundNormalizeThreshold(source, width, height);
        int borderPx = Mathf.Max(dilateRadius + 2,
                               Mathf.RoundToInt(Mathf.Min(width, height) * borderFraction));

        bool[] backgroundMask = DilateCloseAndFill(binary, width, height, borderPx);

        int bgCount = 0;
        foreach (bool b in backgroundMask) {
            if (b) 
            {
                bgCount++;
            }
        }
        float bgFraction = (float)bgCount / (width * height);

        if (bgFraction < 0.25f)
        {
            Debug.Log($"[DrawingExtractor] Flood fill covered only {bgFraction:P0} â€” inverting and retrying.");
            for (int i = 0; i < binary.Length; i++)
            {
                binary[i] = binary[i].r < 0.5f ? Color.white : Color.black;
            }
            backgroundMask = DilateCloseAndFill(binary, width, height, borderPx);
        }
        else
        {
            Debug.Log($"[DrawingExtractor] Background coverage: {bgFraction:P0}");
        }

        Color[] silhouette = BuildFilledSilhouette(backgroundMask);
        silhouette = KeepLargestInteriorRegion(silhouette, width, height);
        LogBlackPixelCount(silhouette);

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.SetPixels(silhouette);
        result.Apply();

        SaveDebugTexture(result, "DrawingExtractor_debug.png");
        return result;
    }

    /// <summary>
    /// Normalizes each pixel against a box-blurred background and thresholds it to either black or white
    /// </summary>
    /// <param name="pixels">Array of pixel color values</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <returns>Array of processed pixel color values</returns>
    private Color[] BackgroundNormalizeThreshold(Color[] pixels, int width, int height)
    {
        int blurRadius = Mathf.Max(8, (int)(width * backgroundBlurFraction));
        Color[] background = BoxBlur(pixels, width, height, blurRadius);
        Color[] result = new Color[pixels.Length];
        int inkCount = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            float lum = Luminance(pixels[i]);
            float bg = Mathf.Max(Luminance(background[i]), 0.01f);
            bool isInk = (lum / bg) < backgroundNormalizeThreshold;
            result[i] = isInk ? Color.black : Color.white;
            if (isInk)
            {
                inkCount++;
            }
        }

        Debug.Log($"[DrawingExtractor] BackgroundNormalize: ink {(float)inkCount / pixels.Length:P1}, " +
                  $"threshold {backgroundNormalizeThreshold:F2}");
        return result;
    }

    /// <summary>
    /// Dilates the foreground, applies closing, and flood-fills the background mask
    /// </summary>
    /// <param name="binary">Array of binary pixel color values</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="borderPx">Border thickness in pixels</param>
    /// <returns>Array of boolean mask values</returns>
    private bool[] DilateCloseAndFill(Color[] binary, int width, int height, int borderPx)
    {
        Color[] dilated = DilateBlackPixels(binary, width, height, dilateRadius);
        if (closingRadius > 0)
        {
            dilated = MorphologicalClosing(dilated, width, height, closingRadius);
        }
        ApplyWhiteBorder(dilated, width, height, borderPx);
        return FloodFillBackground(dilated, width, height);
    }

    /// <summary>
    /// Keeps the largest black region that does not touch the image border
    /// </summary>
    /// <param name="pixels">Array of pixel color values</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <returns>Array of retained pixel color values</returns>
    private static Color[] KeepLargestInteriorRegion(Color[] pixels, int width, int height)
    {
        bool[] black = new bool[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            black[i] = IsBlack(pixels[i]);
        }

        int[] labels = new int[pixels.Length];
        var componentSizes = new List<int>();
        var componentTouches = new List<bool>();
        int nextLabel = 1;
        for (int startY = 0; startY < height; startY++)
        {
            for (int startX = 0; startX < width; startX++)
            {
                int startIndex = startY * width + startX;
                if (!black[startIndex] || labels[startIndex] != 0)
                {
                    continue;
                }

                int label = nextLabel++;
                int size = 0;
                bool borderHit = false;
                var queue = new Queue<int>();

                labels[startIndex] = label;
                queue.Enqueue(startIndex);

                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    int currentX = index % width;
                    int currentY = index / width;
                    size++;

                    if (currentX == 0 || currentX == width - 1 || currentY == 0 || currentY == height - 1)
                    {
                        borderHit = true;
                    }
                    for (int d = 0; d < FloodFillDirections.Length; d++)
                    {
                        int neighborX = currentX + FloodFillDirections[d].x;
                        int neighborY = currentY + FloodFillDirections[d].y;
                        if (neighborX < 0 || neighborX >= width || neighborY < 0 || neighborY >= height)
                        {
                            continue;
                        }
                        int neighborIndex = neighborY * width + neighborX;
                        if (!black[neighborIndex] || labels[neighborIndex] != 0)
                        {
                            continue;
                        }
                        labels[neighborIndex] = label;
                        queue.Enqueue(neighborIndex);
                    }
                }

                componentSizes.Add(size);
                componentTouches.Add(borderHit);
            }
        }

        int bestLabel = -1;
        int bestSize = 0;
        for (int i = 0; i < componentSizes.Count; i++)
        {
            if (!componentTouches[i] && componentSizes[i] > bestSize)
            {
                bestSize = componentSizes[i]; 
                bestLabel = i + 1; 
            }
        }
        if (bestLabel == -1)
        {
            Debug.LogWarning("[DrawingExtractor] All regions touch the border â€” keeping largest. " +
                             "Try increasing borderFraction or keeping the drawing away from the edges.");
            for (int i = 0; i < componentSizes.Count; i++)
            {
                if (componentSizes[i] > bestSize)
                {
                    bestSize = componentSizes[i]; 
                    bestLabel = i + 1; 
                }
            }
        }

        Debug.Log($"[DrawingExtractor] Kept region {bestLabel} ({bestSize} px) " +
                  $"from {componentSizes.Count} component(s).");

        Color[] result = new Color[pixels.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = labels[i] == bestLabel ? Color.black : Color.white;
        }
        return result;
    }

    /// <summary>
    /// Applies morphological closing to the image to close small gaps and holes
    /// </summary>
    /// <param name="pixels">Array of pixel color values</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="radius">Kernel radius in pixels</param>
    /// <returns>Array of processed pixel color values</returns>
    private static Color[] MorphologicalClosing(Color[] pixels, int width, int height, int radius)
        => SquareErode(SquareDilate(pixels, width, height, radius), width, height, radius);

    /// <summary>
    /// Dilates black pixels with a square kernel
    /// </summary>
    /// <param name="pixels">Array of pixel color values</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="radius">Kernel radius in pixels</param>
    /// <returns>Array of processed pixel color values</returns>
    private static Color[] SquareDilate(Color[] pixels, int width, int height, int radius)
    {
        bool[] source = ToBool(pixels, width * height);
        bool[] temp = new bool[width * height];
        bool[] destination = new bool[width * height];

        for (int y = 0; y < height; y++)
        {
            int dark = 0;
            for (int kx = 0; kx < Mathf.Min(radius + 1, width); kx++)
            {
                if (source[y * width + kx])
                {
                    dark++;
                }
            }
            for (int x = 0; x < width; x++)
            {
                temp[y * width + x] = dark > 0;
                int rx = x - radius;
                int ax = x + radius + 1;
                if (rx >= 0 && source[y * width + rx])
                {
                    dark--;
                }
                if (ax < width && source[y * width + ax])
                {
                    dark++;
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            int dark = 0;
            for (int ky = 0; ky < Mathf.Min(radius + 1, height); ky++)
            {
                if (temp[ky * width + x])
                {
                    dark++;
                }
            }
            for (int y = 0; y < height; y++)
            {
                destination[y * width + x] = dark > 0;
                int ry = y - radius;
                int ay = y + radius + 1;
                if (ry >= 0 && temp[ry * width + x])
                {
                    dark--;
                }
                if (ay < height && temp[ay * width + x])
                {
                    dark++;
                }
            }
        }

        return FromBool(destination, width * height);
    }

    /// <summary>
    /// Erodes black pixels with a square kernel
    /// </summary>
    /// <param name="pixels">Array of pixel color values</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="radius">Kernel radius in pixels</param>
    /// <returns>Array of processed pixel color values</returns>
    private static Color[] SquareErode(Color[] pixels, int width, int height, int radius)
    {
        bool[] source = ToBool(pixels, width * height);
        bool[] temp = new bool[width * height];
        bool[] destination = new bool[width * height];

        for (int y = 0; y < height; y++)
        {
            int light = 0;
            for (int kx = 0; kx < Mathf.Min(radius + 1, width); kx++)
            {
                if (!source[y * width + kx])
                {
                    light++;
                }
            }
            for (int x = 0; x < width; x++)
            {
                int lo = Mathf.Max(0, radius - x);
                int ro = Mathf.Max(0, (x + radius + 1) - width);
                temp[y * width + x] = (light + lo + ro) == 0;
                int rx = x - radius;
                int ax = x + radius + 1;
                if (rx >= 0 && !source[y * width + rx])
                {
                    light--;
                }
                if (ax < width && !source[y * width + ax])
                {
                    light++;
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            int light = 0;
            for (int ky = 0; ky < Mathf.Min(radius + 1, height); ky++) 
            {
                if (!temp[ky * width + x])
                {
                    light++;
                }
            }
            for (int y = 0; y < height; y++)
            {
                int to = Mathf.Max(0, radius - y);
                int bo = Mathf.Max(0, (y + radius + 1) - height);
                destination[y * width + x] = (light + to + bo) == 0;
                int ry = y - radius;
                int ay = y + radius + 1;
                if (ry >= 0 && !temp[ry * width + x])
                {
                    light--;
                }
                if (ay < height && !temp[ay * width + x])
                {
                    light++;
                }
            }
        }

        return FromBool(destination, width * height);
    }

    /// <summary>
    /// Converts black-and-white pixels into a boolean mask
    /// </summary>
    /// <param name="pixels">Array of pixel color values</param>
    /// <param name="elementCount">Number of elements to process</param>
    /// <returns>Array of boolean mask values</returns>
    private static bool[] ToBool(Color[] pixels, int elementCount)
    {
        bool[] mask = new bool[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            mask[i] = IsBlack(pixels[i]);
        }
        return mask;
    }

    /// <summary>
    /// Converts a boolean mask back to black-and-white pixels
    /// </summary>
    /// <param name="mask">Array of boolean mask values</param>
    /// <param name="elementCount">Number of elements to process</param>
    /// <returns>Array of processed pixel color values</returns>
    private static Color[] FromBool(bool[] mask, int elementCount)
    {
        Color[] pixelColors = new Color[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            pixelColors[i] = mask[i] ? Color.black : Color.white;
        }
        return pixelColors;
    }

    /// <summary>
    /// Applies a box blur to an array of pixel color values.
    /// </summary>
    /// <param name="pixels">Array of pixel color values</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="radius">Kernel radius in pixels</param>
    /// <returns>Array of processed pixel color values</returns>
    private static Color[] BoxBlur(Color[] pixels, int width, int height, int radius)
    {
        Color[] temp = new Color[pixels.Length];
        Color[] output = new Color[pixels.Length];

        for (int y = 0; y < height; y++)
        {
            float r = 0f;
            float g = 0f;
            float b = 0f;
            int count = 0;
            for (int kx = 0; kx <= radius; kx++) 
            { 
                r += pixels[y * width + kx].r; 
                g += pixels[y * width + kx].g; 
                b += pixels[y * width + kx].b; 
                count++; 
            }
            for (int x = 0; x < width; x++)
            {
                temp[y * width + x] = new Color(r / count, g / count, b / count);
                int rx = x - radius;
                int ax = x + radius + 1;
                if (rx >= 0)
                {
                    r -= pixels[y * width + rx].r; 
                    g -= pixels[y * width + rx].g; 
                    b -= pixels[y * width + rx].b; 
                    count--;
                }
                if (ax < width)
                {
                    r += pixels[y * width + ax].r; 
                    g += pixels[y * width + ax].g; 
                    b += pixels[y * width + ax].b; 
                    count++;
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            float r = 0f;
            float g = 0f;
            float b = 0f;
            int count = 0;
            for (int ky = 0; ky <= radius; ky++)
            { 
                r += temp[ky * width + x].r; 
                g += temp[ky * width + x].g; 
                b += temp[ky * width + x].b; 
                count++; 
            }
            for (int y = 0; y < height; y++)
            {
                output[y * width + x] = new Color(r / count, g / count, b / count);
                int ry = y - radius;
                int ay = y + radius + 1;
                if (ry >= 0)
                {
                    r -= temp[ry * width + x].r; 
                    g -= temp[ry * width + x].g; 
                    b -= temp[ry * width + x].b; 
                    count--;
                }
                if (ay < height)
                {
                    r += temp[ay * width + x].r; 
                    g += temp[ay * width + x].g; 
                    b += temp[ay * width + x].b; 
                    count++;
                }
            }
        }

        return output;
    }

    /// <summary>
    /// Paints a white border around the image edges
    /// </summary>
    /// <param name="pixels">Array of pixel color values</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="padding">Border padding width in pixels</param>
    private void ApplyWhiteBorder(Color[] pixels, int width, int height, int padding)
    {
        for (int y = 0; y < height; y++) 
        {
            for (int x = 0; x < width; x++)
            {
                if (x < padding || x >= width - padding || y < padding || y >= height - padding)
                {
                    pixels[y * width + x] = Color.white;
                }
            }
        }
    }

    /// <summary>
    /// Expands black pixels outward by given radius
    /// </summary>
    /// <param name="pixels">Array of pixel color values</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="radius">Kernel radius in pixels</param>
    /// <returns>Array of processed pixel color values</returns>
    private Color[] DilateBlackPixels(Color[] pixels, int width, int height, int radius)
    {
        Color[] result = new Color[pixels.Length];
        for (int i = 0; i < result.Length; i++) 
        {
            result[i] = Color.white;
        }

        for (int y = 0; y < height; y++) 
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsBlack(pixels[y * width + x]))
                {
                    continue;
                }
                for (int ky = -radius; ky <= radius; ky++) 
                {
                    for (int kx = -radius; kx <= radius; kx++)
                    {
                        int neighborX = x + kx;
                        int neighborY = y + ky;
                        if (neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height)
                        {
                            result[neighborY * width + neighborX] = Color.black;
                        }
                    }
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Flood-fills white background pixels starting from the image border
    /// </summary>
    /// <param name="pixels">Array of pixel color values</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <returns>Array of boolean mask values</returns>
    private bool[] FloodFillBackground(Color[] pixels, int width, int height)
    {
        bool[] mask = new bool[pixels.Length];
        Queue<int> queue = new Queue<int>();

        for (int x = 0; x < width; x++)
        {
            TryEnqueue(x, 0, width, height, pixels, mask, queue);
            TryEnqueue(x, height - 1, width, height, pixels, mask, queue);
        }
        for (int y = 0; y < height; y++)
        {
            TryEnqueue(0, y, width, height, pixels, mask, queue);
            TryEnqueue(width - 1, y, width, height, pixels, mask, queue);
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % width;
            int y = index / width;
            for (int d = 0; d < FloodFillDirections.Length; d++)
            {
                TryEnqueue(
                    x + FloodFillDirections[d].x,
                    y + FloodFillDirections[d].y,
                    width,
                    height,
                    pixels,
                    mask,
                    queue);
            }
        }

        return mask;
    }

    /// <summary>
    /// Queues a valid white pixel for flood fill
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="pixels">Array of pixel color values</param>
    /// <param name="visited">Array of visited-state flags for flood fill</param>
    /// <param name="queue">Queue of pixel indices pending flood-fill processing</param>
    private void TryEnqueue(int x, int y, int width, int height, Color[] pixels, bool[] visited, Queue<int> queue)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return;
        }
        int index = y * width + x;
        if (visited[index] || IsBlack(pixels[index]))
        {
            return;
        }
        visited[index] = true;
        queue.Enqueue(index);
    }

    /// <summary>
    /// Builds a black silhouette from the background mask
    /// </summary>
    /// <param name="mask">Background mask</param>
    /// <returns>Array of processed pixel color values</returns>
    private static Color[] BuildFilledSilhouette(bool[] mask)
    {
        Color[] result = new Color[mask.Length];
        for (int i = 0; i < result.Length; i++) 
        {
            result[i] = mask[i] ? Color.white : Color.black;
        }
        return result;
    }

    /// <summary>
    /// Returns true when a color sample is treated as black
    /// </summary>
    /// <param name="color">Pixel color value</param>
    /// <returns>True when color is considered black, false if not</returns>
    private static bool IsBlack(Color color) => color.r < 0.1f;

    /// <summary>
    /// Computes luminance from RGB channels
    /// </summary>
    /// <param name="color">Pixel color value</param>
    /// <returns>Computed luminance</returns>
    private static float Luminance(Color color) => color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;

    // ****** Debug helpers ******

    /// <summary>
    /// Logs how many black pixels are in the processed image
    /// </summary>
    /// <param name="pixels">Array of pixel color values</param>
    private void LogBlackPixelCount(Color[] pixels)
    {
        int count = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (IsBlack(pixels[i])) 
            {
                count++;
            }
        }
        Debug.Log($"[DrawingExtractor] Black pixels after fill: {count}");
    }

    /// <summary>
    /// Saves a debug texture as a PNG in persistent storage
    /// </summary>
    /// <param name="texture">Texture to write to disk</param>
    /// <param name="filename">Output file name</param>
    private void SaveDebugTexture(Texture2D texture, string filename)
    {
        byte[] bytes = texture.EncodeToPNG();
        string path = System.IO.Path.Combine(Application.persistentDataPath, filename);
        System.IO.File.WriteAllBytes(path, bytes);
        Debug.Log($"[DrawingExtractor] Debug saved: {path}");
    }
}
