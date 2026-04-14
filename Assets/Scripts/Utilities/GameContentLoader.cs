using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Reusable loader for game content stored as JSON under StreamingAssets/GameContent/{gameId}/.
/// Loads items of type T from a JSON file, shuffles, and serves without repeats.
/// </summary>
public class GameContentLoader<T>
{
    private readonly List<T> _items = new List<T>();
    private int _currentIndex;
    private readonly string _gameId;

    public int TotalItems => _items.Count;
    public bool HasItems => _items.Count > 0;

    public GameContentLoader(string gameId)
    {
        _gameId = gameId;
    }

    /// <summary>
    /// Load items from StreamingAssets/GameContent/{gameId}/{filename}.
    /// The JSON must have a root array field matching <paramref name="arrayFieldName"/>.
    /// </summary>
    public bool Load(string filename = "items.json", string arrayFieldName = "items")
    {
        string path = Path.Combine(Application.streamingAssetsPath, "GameContent", _gameId, filename);
        if (!File.Exists(path))
        {
            GameLog.Warn($"Content file not found: {path}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            string wrapped = "{\"" + arrayFieldName + "\":" + ExtractArray(json, arrayFieldName) + "}";
            var wrapper = JsonUtility.FromJson<Wrapper>(wrapped);
            if (wrapper?.items != null && wrapper.items.Length > 0)
            {
                _items.Clear();
                _items.AddRange(wrapper.items);
                Shuffle();
                _currentIndex = 0;
                GameLog.Prompt($"[{_gameId}] Loaded {_items.Count} items from {filename}");
                return true;
            }
        }
        catch (Exception ex)
        {
            GameLog.Warn($"[{_gameId}] Failed to parse {filename}: {ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// Load raw JSON text from a content file and deserialize with JsonUtility.
    /// Use when the JSON schema is game-specific (e.g. fibbage prompts with truth fields).
    /// </summary>
    public static TRoot LoadRaw<TRoot>(string gameId, string filename) where TRoot : class
    {
        string path = Path.Combine(Application.streamingAssetsPath, "GameContent", gameId, filename);
        if (!File.Exists(path))
        {
            GameLog.Warn($"Content file not found: {path}");
            return null;
        }
        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<TRoot>(json);
        }
        catch (Exception ex)
        {
            GameLog.Warn($"[{gameId}] Failed to parse {filename}: {ex.Message}");
            return null;
        }
    }

    public T GetNext()
    {
        if (_items.Count == 0) return default;
        if (_currentIndex >= _items.Count)
        {
            Shuffle();
            _currentIndex = 0;
        }
        return _items[_currentIndex++];
    }

    public void Reset()
    {
        Shuffle();
        _currentIndex = 0;
    }

    private void Shuffle() => _items.Shuffle();

    private static string ExtractArray(string json, string fieldName)
    {
        int idx = json.IndexOf("\"" + fieldName + "\"", StringComparison.Ordinal);
        if (idx < 0) return "[]";
        int colon = json.IndexOf(':', idx);
        if (colon < 0) return "[]";
        int bracket = json.IndexOf('[', colon);
        if (bracket < 0) return "[]";
        int depth = 0;
        for (int i = bracket; i < json.Length; i++)
        {
            if (json[i] == '[') depth++;
            else if (json[i] == ']') { depth--; if (depth == 0) return json.Substring(bracket, i - bracket + 1); }
        }
        return "[]";
    }

    [Serializable]
    private class Wrapper
    {
        public T[] items;
    }
}
