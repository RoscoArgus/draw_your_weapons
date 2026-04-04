using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

[CreateAssetMenu(menuName = "Weapon Pipeline/Meshy Cache")]
public class MeshyCache : ScriptableObject
{
    private Dictionary<string, CacheEntry> _entries = new();
    private string FilePath => Path.Combine(Application.persistentDataPath, "meshy_cache.json");

    [Serializable]
    private class CacheEntry { public string taskId; public string textureName; }

    [Serializable]
    private class CacheFile { public List<string> keys = new(); public List<CacheEntry> values = new(); }

    private void OnEnable() => Load();

    private void Load()
    {
        _entries.Clear();
        if (!File.Exists(FilePath)) return;

        try
        {
            var json = File.ReadAllText(FilePath);
            var file = JsonUtility.FromJson<CacheFile>(json);
            for (int i = 0; i < file.keys.Count; i++)
                _entries[file.keys[i]] = file.values[i];

            Debug.Log($"[MeshyCache] Loaded {_entries.Count} cached task(s) from {FilePath}");
        }
        catch (Exception e) { Debug.LogWarning($"[MeshyCache] Failed to load: {e.Message}"); }
    }

    public void Save()
    {
        var file = new CacheFile();
        foreach (var kv in _entries) { file.keys.Add(kv.Key); file.values.Add(kv.Value); }
        File.WriteAllText(FilePath, JsonUtility.ToJson(file, true));
    }

    public bool TryGet(string hash, out string taskId)
    {
        if (_entries.TryGetValue(hash, out var entry))
        {
            taskId = entry.taskId;
            return true;
        }
        taskId = null;
        return false;
    }

    public void Store(string hash, string taskId, string textureName)
    {
        _entries[hash] = new CacheEntry { taskId = taskId, textureName = textureName };
        Save();
    }

    public bool Remove(string hash)
    {
        bool removed = _entries.Remove(hash);
        if (removed)
            Save();
        return removed;
    }

    [ContextMenu("List Cache Entries")]
    public void ListCacheEntries()
    {
        if (_entries.Count == 0)
        {
            Debug.Log("[MeshyCache] Cache is empty.");
            return;
        }

        Debug.Log($"[MeshyCache] {_entries.Count} cached entry/entries:");
        foreach (var kv in _entries)
            Debug.Log($"  {kv.Value.textureName} → Task ID: {kv.Value.taskId} (Hash: {kv.Key})");
    }

    [ContextMenu("Clear Cache")]
    public void ClearCache()
    {
        _entries.Clear();
        if (File.Exists(FilePath))
            File.Delete(FilePath);
        Debug.Log("[MeshyCache] Cache cleared.");
    }

    public static string HashTexture(Texture2D tex)
    {
        byte[] bytes = tex.EncodeToPNG();
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}