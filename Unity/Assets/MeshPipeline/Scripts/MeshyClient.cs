using System;
using System.Collections;
using System.Text;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;

public class MeshyClient : MonoBehaviour
{
    private const string TaskNotFoundErrorPrefix = "TASK_NOT_FOUND:";

    [Header("Meshy Settings")]
    public PipelineSecrets secrets;
    private string apiKey => secrets.meshyApiKey;

    [Tooltip("Seconds between status poll requests")]
    public float pollInterval = 5f;

    [Tooltip("Disable texturing to use only 20 credits instead of 30 during testing")]
    public bool shouldTexture = false;

    private const string BaseUrl = "https://api.meshy.ai/openapi/v1/image-to-3d";

    /// <summary>
    /// Starts Meshy generation for a texture
    /// </summary>
    /// <param name="inputTexture">Source texture</param>
    /// <param name="cache">Meshy cache</param>
    /// <param name="onComplete">Callback when the model generation is successful</param>
    /// <param name="onError">Callback when generation fails</param>
    public void GenerateFromTexture(
        Texture2D inputTexture,
        MeshyCache cache,
        Action<GameObject> onComplete,
        Action<string> onError = null)
    {
        string hash = MeshyCache.HashTexture(inputTexture);

        if (cache.TryGet(hash, out string cachedTaskId))
        {
            Debug.Log($"[MeshyClient] Cache hit for hash {hash}, reusing task {cachedTaskId}");
            StartCoroutine(PollCachedTaskOrResubmit(inputTexture, hash, cache, cachedTaskId, onComplete, onError));
        }
        else
        {
            StartCoroutine(SubmitAndPoll(inputTexture, hash, cache, onComplete, onError));
        }
    }

    /// <summary>
    /// Polls a cached task and resubmits if the task no longer exists
    /// </summary>
    /// <param name="inputTexture">Source texture</param>
    /// <param name="hash">Cache key derived from the source texture</param>
    /// <param name="cache">Meshy cache</param>
    /// <param name="cachedTaskId">Previously cached Meshy task ID</param>
    /// <param name="onComplete">Callback when model generation is successful</param>
    /// <param name="onError">Callback when generation fails</param>
    /// <returns>Enumerator used by Unity to run this coroutine</returns>
    private IEnumerator PollCachedTaskOrResubmit(
        Texture2D inputTexture,
        string hash,
        MeshyCache cache,
        string cachedTaskId,
        Action<GameObject> onComplete,
        Action<string> onError)
    {
        string pollError = null;
        yield return PollAndLoad(
            cachedTaskId,
            onComplete,
            err => pollError = err,
            failOnNotFound: true);

        if (!IsTaskNotFoundError(pollError))
        {
            if (!string.IsNullOrEmpty(pollError))
            {
                onError?.Invoke(pollError);
            }
            yield break;
        }

        Debug.LogWarning($"[MeshyClient] Cached task {cachedTaskId} returned 404. Removing stale cache entry and submitting a new task.");
        cache.Remove(hash);
        yield return SubmitAndPoll(inputTexture, hash, cache, onComplete, onError);
    }

    /// <summary>
    /// Submits a new Meshy task and polls until completion.
    /// </summary>
    /// <param name="texture">Texture to encode, hash, or write to disk</param>
    /// <param name="hash">Cache key derived from the source texture</param>
    /// <param name="cache">Meshy cache</param>
    /// <param name="onComplete">Callback when model generation is successful</param>
    /// <param name="onError">Callback invoked when generation fails</param>
    /// <returns>Enumerator used by Unity to run this coroutine</returns>
    private IEnumerator SubmitAndPoll(
        Texture2D texture,
        string hash,
        MeshyCache cache,
        Action<GameObject> onComplete,
        Action<string> onError)
    {
        byte[] pngBytes = texture.EncodeToPNG();
        string b64 = Convert.ToBase64String(pngBytes);
        string dataUri = $"data:image/png;base64,{b64}";

        string body = JsonUtility.ToJson(new ImageTo3DRequest
        {
            image_url = dataUri,
            should_texture = shouldTexture,
            ai_model = "meshy-6",
            texture_prompt = shouldTexture ? "A melee weapon, game-ready, realistic materials and textures. Should reflect the textures of real-world weapons." : null
        });

        using var post = new UnityWebRequest(BaseUrl, "POST");
        post.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        post.downloadHandler = new DownloadHandlerBuffer();
        post.SetRequestHeader("Content-Type", "application/json");
        post.SetRequestHeader("Authorization", $"Bearer {apiKey}");

        yield return post.SendWebRequest();

        if (post.result != UnityWebRequest.Result.Success)
        {
            string err = $"[MeshyClient] POST failed: {post.error} â€” {post.downloadHandler.text}";
            Debug.LogError(err);
            onError?.Invoke(err);
            yield break;
        }

        var response = JsonUtility.FromJson<TaskCreatedResponse>(post.downloadHandler.text);
        string taskId = response?.result;
        if (string.IsNullOrWhiteSpace(taskId))
        {
            string err = $"[MeshyClient] POST succeeded but response did not include a valid task id. Body: {post.downloadHandler.text}";
            Debug.LogError(err);
            onError?.Invoke(err);
            yield break;
        }

        Debug.Log($"[MeshyClient] Task created: {taskId}");

        try
        {
            cache.Store(hash, taskId, texture.name);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MeshyClient] Failed to store cache for task {taskId}: {e.Message}");
        }

        yield return PollAndLoad(taskId, onComplete, onError);
    }

    /// <summary>
    /// Polls task status and loads the model when generation succeeds
    /// </summary>
    /// <param name="taskId">Meshy task ID</param>
    /// <param name="onComplete">Callback when the model generation is successful</param>
    /// <param name="onError">Callback when generation fails</param>
    /// <param name="failOnNotFound">Flag to stop polling when a task is non-existent</param>
    /// <returns>Enumerator used by Unity to run this coroutine</returns>
    private IEnumerator PollAndLoad(
        string taskId,
        Action<GameObject> onComplete,
        Action<string> onError,
        bool failOnNotFound = false)
    {
        float intervalSeconds = GetSafePollIntervalSeconds();
        Debug.Log($"[MeshyClient] Starting poll for task {taskId} (interval={intervalSeconds:F2}s, timeScale={Time.timeScale:F2})");
        string url = $"{BaseUrl}/{taskId}";
        int attempt = 0;

        while (true)
        {
            attempt++;
            Debug.Log($"[MeshyClient] Polling task {taskId} (attempt {attempt})...");

            using var get = UnityWebRequest.Get(url);
            get.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            get.timeout = 10;
            yield return get.SendWebRequest();

            Debug.Log($"[MeshyClient] Poll response received: {get.responseCode}");

            if (get.result != UnityWebRequest.Result.Success)
            {
                if (failOnNotFound && get.responseCode == 404)
                {
                    string err = $"{TaskNotFoundErrorPrefix}{taskId}";
                    Debug.LogWarning($"[MeshyClient] Task {taskId} was not found (404).");
                    onError?.Invoke(err);
                    yield break;
                }

                Debug.LogWarning($"[MeshyClient] Poll error: {get.error}. Body: {get.downloadHandler?.text}");
                yield return new WaitForSecondsRealtime(intervalSeconds);
                continue;
            }

            var task = JsonUtility.FromJson<TaskStatusResponse>(get.downloadHandler.text);
            if (task == null || string.IsNullOrEmpty(task.status))
            {
                Debug.LogWarning($"[MeshyClient] Poll response could not be parsed for task {taskId}. Body: {get.downloadHandler.text}");
                yield return new WaitForSecondsRealtime(intervalSeconds);
                continue;
            }

            Debug.Log($"[MeshyClient] Task {taskId} â€” {task.status} ({task.progress}%)");

            if (task.status == "SUCCEEDED")
            {
                yield return DownloadAndInstantiate(task.model_urls.glb, onComplete, onError);
                yield break;
            }

            if (task.status is "FAILED" or "CANCELED")
            {
                string err = $"[MeshyClient] Task {taskId} ended with status {task.status}: {task.task_error?.message}";
                Debug.LogError(err);
                onError?.Invoke(err);
                yield break;
            }

            yield return new WaitForSecondsRealtime(intervalSeconds);
        }
    }

    /// <summary>
    /// Checks if an error for a missing Meshy task
    /// </summary>
    /// <param name="error">Error message</param>
    /// <returns>True when Meshy task missing, false otherwise</returns>
    private bool IsTaskNotFoundError(string error)
    {
        return !string.IsNullOrEmpty(error) &&
               error.StartsWith(TaskNotFoundErrorPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns a clamped polling interval in seconds
    /// </summary>
    private float GetSafePollIntervalSeconds()
    {
        if (float.IsNaN(pollInterval) || float.IsInfinity(pollInterval) || pollInterval < 0.1f)
        {
            return 0.1f;
        }
        return pollInterval;
    }

    /// <summary>
    /// Downloads the GLB and instantiates it with glTFast
    /// </summary>
    /// <param name="glbUrl">URL of the GLB file to download</param>
    /// <param name="onComplete">Callback when model generation is successful</param>
    /// <param name="onError">Callback when generation fails</param>
    /// <returns>Enumerator used by Unity to run this coroutine</returns>
    private IEnumerator DownloadAndInstantiate(
        string glbUrl,
        Action<GameObject> onComplete,
        Action<string> onError)
    {
        using var glbReq = UnityWebRequest.Get(glbUrl);
        yield return glbReq.SendWebRequest();

        if (glbReq.result != UnityWebRequest.Result.Success)
        {
            string err = $"[MeshyClient] GLB download failed: {glbReq.error}";
            Debug.LogError(err);
            onError?.Invoke(err);
            yield break;
        }

        byte[] glbData = glbReq.downloadHandler.data;

        var gltf = new GltfImport();
        var loadTask = gltf.Load(glbData, new Uri(glbUrl));
        while (!loadTask.IsCompleted) yield return null;

        if (!loadTask.Result)
        {
            string err = "[MeshyClient] GLTFast failed to parse GLB";
            Debug.LogError(err);
            onError?.Invoke(err);
            yield break;
        }

        var container = new GameObject("MeshyWeapon");
        var instantiateTask = gltf.InstantiateMainSceneAsync(container.transform);
        while (!instantiateTask.IsCompleted) yield return null;

        Debug.Log("[MeshyClient] Meshy model instantiated successfully");
        onComplete?.Invoke(container);
    }

    [Serializable] private class ImageTo3DRequest
    {
        public string image_url;
        public bool should_texture;
        public string ai_model;
        public string texture_prompt;
    }

    [Serializable] private class TaskCreatedResponse  { public string result; }
    [Serializable] private class ModelUrls            { public string glb; }
    [Serializable] private class TaskError            { public string message; }

    [Serializable] private class TaskStatusResponse
    {
        public string status;
        public int progress;
        public ModelUrls model_urls;
        public TaskError task_error;
    }
}
