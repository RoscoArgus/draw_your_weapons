using System.Collections.Generic;
using UnityEngine;

public class DrawingExtractor : MonoBehaviour
{
    private const int BorderPadding = 10;

    // Threshold for binary conversion
    [Range(0f, 1f)]
    public float threshold = 0.6f;

    // Radius for dilation step
    [Range(1, 10)]
    public int dilateRadius = 3;

    public Texture2D ProcessImage(Texture2D input)
    {
        if (input == null)
        {
            Debug.LogError("No input texture was provided to DrawingExtractor.");
            return null;
        }

        int width = input.width;
        int height = input.height;

        Color[] binaryPixels = ThresholdToBinary(input.GetPixels());

        // Apply a white border around image to ensure flood fill step correctly identified background
        ApplyWhiteBorder(binaryPixels, width, height, BorderPadding);

        // Dilate black pixels to fill gaps
        Color[] dilatedPixels = DilateBlackPixels(binaryPixels, width, height, dilateRadius);

        // Flood fill to identify background-connected pixels.
        bool[] backgroundMask = FloodFillBackground(dilatedPixels, width, height);

        // Build a silhouette texture where black pixels are the drawing and white pixels are transparent
        Color[] finalPixels = BuildFilledSilhouette(backgroundMask);
        LogBlackPixelCount(finalPixels);

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.SetPixels(finalPixels);
        result.Apply();

        SaveDebugTexture(result, "DrawingExtractor_debug.png");
        return result;
    }

    private Color[] ThresholdToBinary(Color[] pixels)
    {
        Color[] binaryPixels = new Color[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            float greyscale = pixels[i].r * 0.299f + pixels[i].g * 0.587f + pixels[i].b * 0.114f;
            binaryPixels[i] = greyscale < threshold ? Color.black : Color.white;
        }

        return binaryPixels;
    }

    private void ApplyWhiteBorder(Color[] pixels, int width, int height, int padding)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorderPixel =
                    x < padding || x >= width - padding ||
                    y < padding || y >= height - padding;

                if (isBorderPixel)
                    pixels[y * width + x] = Color.white;
            }
        }
    }

    private Color[] DilateBlackPixels(Color[] pixels, int width, int height, int radius)
    {
        Color[] dilatedPixels = new Color[pixels.Length];
        for (int i = 0; i < dilatedPixels.Length; i++)
            dilatedPixels[i] = Color.white;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsBlack(pixels[y * width + x]))
                    continue;

                for (int ky = -radius; ky <= radius; ky++)
                {
                    for (int kx = -radius; kx <= radius; kx++)
                    {
                        int nx = x + kx;
                        int ny = y + ky;

                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            dilatedPixels[ny * width + nx] = Color.black;
                    }
                }
            }
        }

        return dilatedPixels;
    }

    private bool[] FloodFillBackground(Color[] pixels, int width, int height)
    {
        bool[] backgroundMask = new bool[pixels.Length];
        Queue<int> queue = new Queue<int>();

        for (int x = 0; x < width; x++)
        {
            TryEnqueue(x, 0, width, height, pixels, backgroundMask, queue);
            TryEnqueue(x, height - 1, width, height, pixels, backgroundMask, queue);
        }

        for (int y = 0; y < height; y++)
        {
            TryEnqueue(0, y, width, height, pixels, backgroundMask, queue);
            TryEnqueue(width - 1, y, width, height, pixels, backgroundMask, queue);
        }

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % width;
            int y = index / width;

            for (int d = 0; d < 4; d++)
                TryEnqueue(x + dx[d], y + dy[d], width, height, pixels, backgroundMask, queue);
        }

        return backgroundMask;
    }

    private Color[] BuildFilledSilhouette(bool[] backgroundMask)
    {
        Color[] filledPixels = new Color[backgroundMask.Length];

        for (int i = 0; i < filledPixels.Length; i++)
            filledPixels[i] = backgroundMask[i] ? Color.white : Color.black;

        return filledPixels;
    }

    private void TryEnqueue(int x, int y, int width, int height, Color[] pixels, bool[] visited, Queue<int> queue)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        int index = y * width + x;
        if (visited[index] || IsBlack(pixels[index]))
            return;

        visited[index] = true;
        queue.Enqueue(index);
    }

    private bool IsBlack(Color pixel)
    {
        return pixel.r < 0.1f;
    }

    // ****** Debug helpers ******

    private void LogBlackPixelCount(Color[] pixels)
    {
        int blackPixelCount = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            if (IsBlack(pixels[i]))
                blackPixelCount++;
        }

        Debug.Log($"Black pixels after fill: {blackPixelCount}");
    }

    private void SaveDebugTexture(Texture2D tex, string filename)
    {
        byte[] bytes = tex.EncodeToPNG();
        string path = System.IO.Path.Combine(Application.persistentDataPath, filename);
        System.IO.File.WriteAllBytes(path, bytes);
        Debug.Log($"Debug texture saved to: {path}");
    }
}
