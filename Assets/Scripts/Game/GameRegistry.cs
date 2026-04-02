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
}
