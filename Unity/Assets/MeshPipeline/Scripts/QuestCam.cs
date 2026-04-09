using Meta.XR;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

using Debug = UnityEngine.Debug;

public class QuestCam : MonoBehaviour
{
    [Header("Pipeline")]
    public MeshBuilder meshBuilder;

    [Header("Camera")]
    public PassthroughCameraAccess cameraAccess;
    public Vector2Int requestedResolution = new Vector2Int(1280, 960);

    [Header("Capture")]
    public int captureWidth = 1024;
    public int captureHeight = 1024;

    [Range(0.4f, 1.0f)]
    public float cropFraction = 0.75f;

    [Range(0f, 0.15f)]
    public float borderFraction = 0.04f;

    [Header("UI")]
    public Renderer previewQuad;


    private InputAction _captureAction;
    private bool _isCapturing;
    private bool _cameraReady;
    private float _lastCaptureTime = -99f;
    private const float kCooldown = 1.5f;

    private void Awake()
    {
        _captureAction = new InputAction(
            name: "Capture",
            binding: "<XRController>{RightHand}/triggerPressed"
        );
        _captureAction.performed += OnTriggerPressed;

        cameraAccess.CameraPosition = PassthroughCameraAccess.CameraPositionType.Left;
        cameraAccess.RequestedResolution = requestedResolution;
    }

    private void OnEnable() => _captureAction.Enable();

    private void OnDisable() => _captureAction.Disable();

    private void OnDestroy()
    {
        _captureAction.performed -= OnTriggerPressed;
        _captureAction.Dispose();
    }

    private void Update()
    {
        if (!cameraAccess.IsPlaying)
        {
            return;
        }

        if (!_cameraReady)
        {
            _cameraReady = true;
            Debug.Log($"[QuestCam] Camera ready. Resolution: {cameraAccess.CurrentResolution}");
        }

        if (previewQuad != null)
        {
            previewQuad.material.mainTexture = cameraAccess.GetTexture();
        }
    }

    /// <summary>
    /// Starts capture when the camera is ready and cooldown has elapsed
    /// </summary>
    /// <param name="context">Input action callback context</param>
    private void OnTriggerPressed(InputAction.CallbackContext context)
    {
        if (!_cameraReady)
        {
            Debug.LogWarning("[QuestCam] Camera not ready yet."); 
            return;
        }
        if (_isCapturing)
        {
            return;
        }
        if (Time.time - _lastCaptureTime < kCooldown)
        {
            return;
        }

        _lastCaptureTime = Time.time;
        StartCoroutine(CaptureAndProcess());
    }

    /// <summary>
    /// Captures a frame, crops it, and sends it through the mesh pipeline
    /// </summary>
    private IEnumerator CaptureAndProcess()
    {
        _isCapturing = true;
        Debug.Log("[QuestCam] Capturing...");

        yield return new WaitForEndOfFrame();

        Vector2Int res = cameraAccess.CurrentResolution;
        Texture2D fullFrame = BltToTexture(cameraAccess.GetTexture(), res.x, res.y);
        if (fullFrame == null)
        {
            _isCapturing = false; 
            yield break;
        }

        yield return null;

        Texture2D frame = CentreCrop(fullFrame, captureWidth, captureHeight, cropFraction);
        Destroy(fullFrame);

        if (borderFraction > 0f)
        {
            int pad = Mathf.Max(2, Mathf.RoundToInt(
                Mathf.Min(captureWidth, captureHeight) * borderFraction));
            Color[] px = frame.GetPixels();
            for (int y = 0; y < captureHeight; y++)
            {
                for (int x = 0; x < captureWidth; x++)
                {
                    if (x < pad || x >= captureWidth - pad ||
                        y < pad || y >= captureHeight - pad)
                    {
                        px[y * captureWidth + x] = Color.white;
                    }
                }
            }
            frame.SetPixels(px);
            frame.Apply();
        }

        yield return null;

        if (meshBuilder != null)
        {
            meshBuilder.BuildFromTexture(frame);
        }
        else
            Debug.LogWarning("[QuestCam] No MeshBuilder assigned.");

        _isCapturing = false;
    }

    /// <summary>
    /// Crops the centered portion of a texture into a new output texture
    /// </summary>
    /// <param name="source">Source texture</param>
    /// <param name="outputWidth">Output width in pixels</param>
    /// <param name="outputHeight">Output height in pixels</param>
    /// <param name="fraction">Fraction of the centered source image to keep</param>
    /// <returns>Processed texture</returns>
    private static Texture2D CentreCrop(Texture source, int outputWidth, int outputHeight, float fraction)
    {
        fraction = Mathf.Clamp(fraction, 0.1f, 1.0f);
        float sourceWidth = source.width * fraction;
        float sourceHeight = source.height * fraction;
        float sourceX = (source.width - sourceWidth) * 0.5f;
        float sourceY = (source.height - sourceHeight) * 0.5f;
        float u0 = sourceX / source.width;
        float v0 = sourceY / source.height;
        float u1 = (sourceX + sourceWidth) / source.width;
        float v1 = (sourceY + sourceHeight) / source.height;

        RenderTexture rt = RenderTexture.Gettemp(outputWidth, outputHeight, 0, RenderTextureFormat.ARGB32);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(source, rt, new Vector2(u1 - u0, v1 - v0), new Vector2(u0, v0));
        RenderTexture.active = rt;
        Texture2D result = new Texture2D(outputWidth, outputHeight, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, outputWidth, outputHeight), 0, 0);
        result.Apply();
        RenderTexture.active = prev;
        RenderTexture.Releasetemp(rt);
        return result;
    }

    /// <summary>
    /// Copies a source texture into a readable Texture2D
    /// </summary>
    /// <param name="source">Source texture</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <returns>Processed texture</returns>
    private static Texture2D BltToTexture(Texture source, int width, int height)
    {
        RenderTexture rt = RenderTexture.Gettemp(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        RenderTexture.active = prev;
        RenderTexture.Releasetemp(rt);
        return result;
    }

    /// <summary>
    /// Draws debug GUI elements when enabled
    /// </summary>
    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };

        if (_isCapturing)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(12, 12, 300, 28), "Processing...", style);
        }
        else if (_cameraReady)
        {
            GUI.color = new Color(0.2f, 1f, 0.4f);
            GUI.Label(new Rect(12, 12, 380, 28), "Live:  right trigger to capture", style);
        }
        else if (cameraAccess != null && cameraAccess.IsPlaying)
        {
            GUI.color = new Color(1f, 0.8f, 0.0f);
            GUI.Label(new Rect(12, 12, 280, 28), "Camera warming up...", style);
        }
        else
        {
            GUI.color = new Color(1f, 0.5f, 0.2f);
            GUI.Label(new Rect(12, 12, 280, 28), "Waiting for camera...", style);
        }

        GUI.color = Color.white;
    }
}
