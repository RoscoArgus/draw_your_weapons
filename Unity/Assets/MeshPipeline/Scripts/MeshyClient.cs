using System;
using System.Collections;
using System.Text;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;

public class MeshyClient : MonoBehaviour
{
    [Header("Meshy Settings")]
    public PipelineSecrets secrets;
    private string apiKey => secrets.meshyApiKey;

    [Tooltip("Seconds between status poll requests")]
    public float pollInterval = 5f;

    [Tooltip("Disable texturing to use only 20 credits instead of 30 during testing")]
    public bool shouldTexture = false;

    private const string BaseUrl = "https://api.meshy.ai/openapi/v1/image-to-3d";

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
            StartCoroutine(PollAndLoad(cachedTaskId, onComplete, onError));
        }
        else
        {
            StartCoroutine(SubmitAndPoll(inputTexture, hash, cache, onComplete, onError));
        }
    }

    private IEnumerator SubmitAndPoll(
        Texture2D tex,
        string hash,
        MeshyCache cache,
        Action<GameObject> onComplete,
        Action<string> onError)
    {
        byte[] pngBytes = tex.EncodeToPNG();
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
            string err = $"[MeshyClient] POST failed: {post.error} — {post.downloadHandler.text}";
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
            cache.Store(hash, taskId, tex.name);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MeshyClient] Failed to store cache for task {taskId}: {e.Message}");
        }

        yield return PollAndLoad(taskId, onComplete, onError);
    }

    private IEnumerator PollAndLoad(
        string taskId,
        Action<GameObject> onComplete,
        Action<string> onError)
    {
        Debug.Log($"[MeshyClient] Starting poll for task {taskId}");
        string url = $"{BaseUrl}/{taskId}";

        while (true)
        {
            yield return new WaitForSeconds(pollInterval);
            Debug.Log($"[MeshyClient] Polling task {taskId}...");

            using var get = UnityWebRequest.Get(url);
            get.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            get.timeout = 10;
            yield return get.SendWebRequest();

            Debug.Log($"[MeshyClient] Poll response received: {get.responseCode}");

            if (get.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[MeshyClient] Poll error: {get.error}");
                continue;
            }

            var task = JsonUtility.FromJson<TaskStatusResponse>(get.downloadHandler.text);
            Debug.Log($"[MeshyClient] Task {taskId} — {task.status} ({task.progress}%)");

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
        }
    }

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