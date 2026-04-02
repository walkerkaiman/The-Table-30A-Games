using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads prompts from StreamingAssets/prompts.json at startup.
/// Serves them in shuffled order without repeats.
/// Falls back to hardcoded prompts if the JSON file is missing or corrupt.
/// </summary>
public class PromptDatabase : MonoBehaviour
{
    public static PromptDatabase Instance { get; private set; }

    private List<PromptData> _prompts = new List<PromptData>();
    private int _currentIndex;

    public int TotalPrompts => _prompts.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadPrompts();
    }

    public string GetNextPrompt()
    {
        if (_prompts.Count == 0)
        {
            GameLog.Warn("No prompts available — using emergency fallback");
            return "What is the meaning of life?";
        }

        if (_currentIndex >= _prompts.Count)
        {
            Shuffle();
            _currentIndex = 0;
            GameLog.Prompt("Prompt deck reshuffled");
        }

        return _prompts[_currentIndex++].text;
    }

    public void ResetShuffle()
    {
        Shuffle();
        _currentIndex = 0;
    }

    // ── Loading ──────────────────────────────────

    private void LoadPrompts()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "prompts.json");

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                var wrapper = JsonUtility.FromJson<PromptListWrapper>(json);
                if (wrapper?.prompts != null && wrapper.prompts.Length > 0)
                {
                    _prompts = new List<PromptData>(wrapper.prompts);
                    GameLog.Prompt($"Loaded {_prompts.Count} prompts from prompts.json");
                }
                else
                {
                    GameLog.Warn("prompts.json exists but contains no prompts — using fallbacks");
                    LoadFallbackPrompts();
                }
            }
            catch (Exception ex)
            {
                GameLog.Warn($"Failed to parse prompts.json: {ex.Message} — using fallbacks");
                LoadFallbackPrompts();
            }
        }
        else
        {
            GameLog.Warn($"prompts.json not found at {path} — using fallback prompts");
            LoadFallbackPrompts();
        }

        Shuffle();
    }

    private void LoadFallbackPrompts()
    {
        string[] fallbacks =
        {
            "The worst name for a pet goldfish",
            "Something you should never yell in a library",
            "The worst superpower to have on a first date",
            "A terrible slogan for a dentist",
            "The real reason the chicken crossed the road",
            "The worst thing to say at a job interview",
            "A rejected name for a breakfast cereal",
            "Something a pilot should never announce mid-flight",
            "The worst fortune cookie message",
            "A terrible name for a rock band"
        };

        _prompts.Clear();
        for (int i = 0; i < fallbacks.Length; i++)
            _prompts.Add(new PromptData { id = $"fallback_{i:D3}", text = fallbacks[i], category = "general" });

        GameLog.Prompt($"Loaded {_prompts.Count} fallback prompts");
    }

    // ── Shuffle (Fisher-Yates) ───────────────────

    private void Shuffle()
    {
        var rng = new System.Random();
        for (int i = _prompts.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var temp = _prompts[i];
            _prompts[i] = _prompts[j];
            _prompts[j] = temp;
        }
    }
}

[Serializable]
public class PromptData
{
    public string id;
    public string text;
    public string category;
}

[Serializable]
public class PromptListWrapper
{
    public PromptData[] prompts;
}
