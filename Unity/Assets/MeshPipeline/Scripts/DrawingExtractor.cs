using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

using Debug = UnityEngine.Debug;
using Application = UnityEngine.Application;

public class DrawingExtractor : MonoBehaviour
{
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
        foreach (bool b in backgroundMask) if (b) bgCount++;
        float bgFraction = (float)bgCount / (width * height);

        if (bgFraction < 0.25f)
        {
            Debug.Log($"[DrawingExtractor] Flood fill covered only {bgFraction:P0} — inverting and retrying.");
            for (int i = 0; i < binary.Length; i++)
                binary[i] = binary[i].r < 0.5f ? Color.white : Color.black;
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

    private Color[] BackgroundNormalizeThreshold(Color[] pixels, int w, int h)
    {
        int blurRadius = Mathf.Max(8, (int)(w * backgroundBlurFraction));
        Color[] background = BoxBlur(pixels, w, h, blurRadius);
        Color[] result = new Color[pixels.Length];
        int inkCount = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            float lum = Luminance(pixels[i]);
            float bg = Mathf.Max(Luminance(background[i]), 0.01f);
            bool isInk = (lum / bg) < backgroundNormalizeThreshold;
            result[i] = isInk ? Color.black : Color.white;
            if (isInk) inkCount++;
        }

        Debug.Log($"[DrawingExtractor] BackgroundNormalize: ink {(float)inkCount / pixels.Length:P1}, " +
                  $"threshold {backgroundNormalizeThreshold:F2}");
        return result;
    }

    private bool[] DilateCloseAndFill(Color[] binary, int w, int h, int borderPx)
    {
        Color[] dilated = DilateBlackPixels(binary, w, h, dilateRadius);
        if (closingRadius > 0)
            dilated = MorphologicalClosing(dilated, w, h, closingRadius);
        ApplyWhiteBorder(dilated, w, h, borderPx);
        return FloodFillBackground(dilated, w, h);
    }

    private static Color[] KeepLargestInteriorRegion(Color[] pixels, int w, int h)
    {
        bool[] black = new bool[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
            black[i] = pixels[i].r < 0.5f;

        int[] labels = new int[pixels.Length];
        var componentSizes = new List<int>();
        var componentTouches = new List<bool>();
        int nextLabel = 1;
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        for (int startY = 0; startY < h; startY++)
            for (int startX = 0; startX < w; startX++)
            {
                int startIdx = startY * w + startX;
                if (!black[startIdx] || labels[startIdx] != 0) continue;

                int label = nextLabel++;
                int size = 0;
                bool borderHit = false;
                var queue = new Queue<int>();

                labels[startIdx] = label;
                queue.Enqueue(startIdx);

                while (queue.Count > 0)
                {
                    int idx = queue.Dequeue();
                    int cx = idx % w, cy = idx / w;
                    size++;

                    if (cx == 0 || cx == w - 1 || cy == 0 || cy == h - 1)
                        borderHit = true;

                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + dx[d], ny = cy + dy[d];
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        int nIdx = ny * w + nx;
                        if (!black[nIdx] || labels[nIdx] != 0) continue;
                        labels[nIdx] = label;
                        queue.Enqueue(nIdx);
                    }
                }

                componentSizes.Add(size);
                componentTouches.Add(borderHit);
            }

        int bestLabel = -1, bestSize = 0;
        for (int i = 0; i < componentSizes.Count; i++)
            if (!componentTouches[i] && componentSizes[i] > bestSize)
            { bestSize = componentSizes[i]; bestLabel = i + 1; }

        if (bestLabel == -1)
        {
            Debug.LogWarning("[DrawingExtractor] All regions touch the border — keeping largest. " +
                             "Try increasing borderFraction or keeping the drawing away from the edges.");
            for (int i = 0; i < componentSizes.Count; i++)
                if (componentSizes[i] > bestSize)
                { bestSize = componentSizes[i]; bestLabel = i + 1; }
        }

        Debug.Log($"[DrawingExtractor] Kept region {bestLabel} ({bestSize} px) " +
                  $"from {componentSizes.Count} component(s).");

        Color[] result = new Color[pixels.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = labels[i] == bestLabel ? Color.black : Color.white;
        return result;
    }

    private static Color[] MorphologicalClosing(Color[] pixels, int w, int h, int radius)
        => SquareErode(SquareDilate(pixels, w, h, radius), w, h, radius);

    private static Color[] SquareDilate(Color[] pixels, int w, int h, int radius)
    {
        bool[] src = ToBool(pixels, w * h);
        bool[] tmp = new bool[w * h];
        bool[] dst = new bool[w * h];

        for (int y = 0; y < h; y++)
        {
            int dark = 0;
            for (int kx = 0; kx < Mathf.Min(radius + 1, w); kx++)
                if (src[y * w + kx]) dark++;
            for (int x = 0; x < w; x++)
            {
                tmp[y * w + x] = dark > 0;
                int rx = x - radius, ax = x + radius + 1;
                if (rx >= 0 && src[y * w + rx]) dark--;
                if (ax < w && src[y * w + ax]) dark++;
            }
        }

        for (int x = 0; x < w; x++)
        {
            int dark = 0;
            for (int ky = 0; ky < Mathf.Min(radius + 1, h); ky++)
                if (tmp[ky * w + x]) dark++;
            for (int y = 0; y < h; y++)
            {
                dst[y * w + x] = dark > 0;
                int ry = y - radius, ay = y + radius + 1;
                if (ry >= 0 && tmp[ry * w + x]) dark--;
                if (ay < h && tmp[ay * w + x]) dark++;
            }
        }

        return FromBool(dst, w * h);
    }

    private static Color[] SquareErode(Color[] pixels, int w, int h, int radius)
    {
        bool[] src = ToBool(pixels, w * h);
        bool[] tmp = new bool[w * h];
        bool[] dst = new bool[w * h];

        for (int y = 0; y < h; y++)
        {
            int light = 0;
            for (int kx = 0; kx < Mathf.Min(radius + 1, w); kx++)
                if (!src[y * w + kx]) light++;
            for (int x = 0; x < w; x++)
            {
                int lo = Mathf.Max(0, radius - x), ro = Mathf.Max(0, (x + radius + 1) - w);
                tmp[y * w + x] = (light + lo + ro) == 0;
                int rx = x - radius, ax = x + radius + 1;
                if (rx >= 0 && !src[y * w + rx]) light--;
                if (ax < w && !src[y * w + ax]) light++;
            }
        }

        for (int x = 0; x < w; x++)
        {
            int light = 0;
            for (int ky = 0; ky < Mathf.Min(radius + 1, h); ky++)
                if (!tmp[ky * w + x]) light++;
            for (int y = 0; y < h; y++)
            {
                int to = Mathf.Max(0, radius - y), bo = Mathf.Max(0, (y + radius + 1) - h);
                dst[y * w + x] = (light + to + bo) == 0;
                int ry = y - radius, ay = y + radius + 1;
                if (ry >= 0 && !tmp[ry * w + x]) light--;
                if (ay < h && !tmp[ay * w + x]) light++;
            }
        }

        return FromBool(dst, w * h);
    }

    private static bool[] ToBool(Color[] pixels, int n)
    { bool[] b = new bool[n]; for (int i = 0; i < n; i++) b[i] = pixels[i].r < 0.5f; return b; }

    private static Color[] FromBool(bool[] b, int n)
    { Color[] p = new Color[n]; for (int i = 0; i < n; i++) p[i] = b[i] ? Color.black : Color.white; return p; }

    private static Color[] BoxBlur(Color[] pixels, int w, int h, int radius)
    {
        Color[] tmp = new Color[pixels.Length];
        Color[] out_ = new Color[pixels.Length];

        for (int y = 0; y < h; y++)
        {
            float r = 0, g = 0, b = 0; int count = 0;
            for (int kx = 0; kx <= radius; kx++)
            { r += pixels[y * w + kx].r; g += pixels[y * w + kx].g; b += pixels[y * w + kx].b; count++; }
            for (int x = 0; x < w; x++)
            {
                tmp[y * w + x] = new Color(r / count, g / count, b / count);
                int rx = x - radius, ax = x + radius + 1;
                if (rx >= 0) { r -= pixels[y * w + rx].r; g -= pixels[y * w + rx].g; b -= pixels[y * w + rx].b; count--; }
                if (ax < w) { r += pixels[y * w + ax].r; g += pixels[y * w + ax].g; b += pixels[y * w + ax].b; count++; }
            }
        }

        for (int x = 0; x < w; x++)
        {
            float r = 0, g = 0, b = 0; int count = 0;
            for (int ky = 0; ky <= radius; ky++)
            { r += tmp[ky * w + x].r; g += tmp[ky * w + x].g; b += tmp[ky * w + x].b; count++; }
            for (int y = 0; y < h; y++)
            {
                out_[y * w + x] = new Color(r / count, g / count, b / count);
                int ry = y - radius, ay = y + radius + 1;
                if (ry >= 0) { r -= tmp[ry * w + x].r; g -= tmp[ry * w + x].g; b -= tmp[ry * w + x].b; count--; }
                if (ay < h) { r += tmp[ay * w + x].r; g += tmp[ay * w + x].g; b += tmp[ay * w + x].b; count++; }
            }
        }

        return out_;
    }

    private void ApplyWhiteBorder(Color[] pixels, int w, int h, int padding)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (x < padding || x >= w - padding || y < padding || y >= h - padding)
                    pixels[y * w + x] = Color.white;
    }

    private Color[] DilateBlackPixels(Color[] pixels, int w, int h, int radius)
    {
        Color[] result = new Color[pixels.Length];
        for (int i = 0; i < result.Length; i++) result[i] = Color.white;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!IsBlack(pixels[y * w + x])) continue;
                for (int ky = -radius; ky <= radius; ky++)
                    for (int kx = -radius; kx <= radius; kx++)
                    {
                        int nx = x + kx, ny = y + ky;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                            result[ny * w + nx] = Color.black;
                    }
            }

        return result;
    }

    private bool[] FloodFillBackground(Color[] pixels, int w, int h)
    {
        bool[] mask = new bool[pixels.Length];
        Queue<int> queue = new Queue<int>();

        for (int x = 0; x < w; x++)
        {
            TryEnqueue(x, 0, w, h, pixels, mask, queue);
            TryEnqueue(x, h - 1, w, h, pixels, mask, queue);
        }
        for (int y = 0; y < h; y++)
        {
            TryEnqueue(0, y, w, h, pixels, mask, queue);
            TryEnqueue(w - 1, y, w, h, pixels, mask, queue);
        }

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int x = idx % w, y = idx / w;
            for (int d = 0; d < 4; d++)
                TryEnqueue(x + dx[d], y + dy[d], w, h, pixels, mask, queue);
        }

        return mask;
    }

    private void TryEnqueue(int x, int y, int w, int h, Color[] pixels, bool[] visited, Queue<int> queue)
    {
        if (x < 0 || x >= w || y < 0 || y >= h) return;
        int idx = y * w + x;
        if (visited[idx] || IsBlack(pixels[idx])) return;
        visited[idx] = true;
        queue.Enqueue(idx);
    }

    private static Color[] BuildFilledSilhouette(bool[] mask)
    {
        Color[] result = new Color[mask.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = mask[i] ? Color.white : Color.black;
        return result;
    }

    private static bool IsBlack(Color c) => c.r < 0.1f;
    private static float Luminance(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

    // ****** Debug helpers ******

    private void LogBlackPixelCount(Color[] pixels)
    {
        int count = 0;
        for (int i = 0; i < pixels.Length; i++) if (IsBlack(pixels[i])) count++;
        Debug.Log($"[DrawingExtractor] Black pixels after fill: {count}");
    }

    private void SaveDebugTexture(Texture2D tex, string filename)
    {
        byte[] bytes = tex.EncodeToPNG();
        string path = System.IO.Path.Combine(Application.persistentDataPath, filename);
        System.IO.File.WriteAllBytes(path, bytes);
        Debug.Log($"[DrawingExtractor] Debug saved: {path}");
    }
}