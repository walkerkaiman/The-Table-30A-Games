using UnityEngine;

/// <summary>
/// Describes a single game type available in the party game system.
/// Create one asset per game (Assets > Create > Party Game > Game Entry).
/// </summary>
[CreateAssetMenu(fileName = "NewGame", menuName = "Party Game/Game Entry")]
public class GameRegistryEntry : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 4)]
    public string description;
    public string sceneName;
    public int minPlayers = 2;
    public int maxPlayers = 8;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
            Debug.LogWarning($"[GameRegistryEntry] \"{name}\": id is empty", this);
        if (string.IsNullOrWhiteSpace(sceneName))
            Debug.LogWarning($"[GameRegistryEntry] \"{name}\": sceneName is empty", this);
        if (minPlayers > maxPlayers)
            Debug.LogWarning($"[GameRegistryEntry] \"{name}\": minPlayers ({minPlayers}) > maxPlayers ({maxPlayers})", this);
    }
#endif
}
