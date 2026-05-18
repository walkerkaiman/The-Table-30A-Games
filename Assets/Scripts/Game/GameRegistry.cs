using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catalog of all available games. Drag GameRegistryEntry assets into the list.
/// Assign this to the GameCoordinator in the Inspector.
/// (Assets > Create > Party Game > Game Registry)
/// </summary>
[CreateAssetMenu(fileName = "GameRegistry", menuName = "Party Game/Game Registry")]
public class GameRegistry : ScriptableObject
{
    public List<GameRegistryEntry> entries = new List<GameRegistryEntry>();

    public GameRegistryEntry GetEntryById(string id)
    {
        foreach (var e in entries)
        {
            if (e != null && e.id == id) return e;
        }
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            if (!string.IsNullOrEmpty(e.id) && !seen.Add(e.id))
                Debug.LogWarning($"[GameRegistry] Duplicate game id \"{e.id}\" at index {i}", this);
        }
    }
#endif
}
