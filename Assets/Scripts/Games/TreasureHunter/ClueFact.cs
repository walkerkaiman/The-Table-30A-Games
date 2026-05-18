using UnityEngine;

/// <summary>
/// An atomic piece of information derived from a <see cref="DungeonLayout"/>.
/// Each fact corresponds to one secret clue that will be formatted and sent to a player.
///
/// Fact types correspond to entries in <c>clue_templates.json</c> via the <see cref="factType"/>
/// string key. The template system replaces placeholders with the concrete values from here.
/// </summary>
[System.Serializable]
public class ClueFact
{
    public enum FactType
    {
        /// <summary>Warns of a trap in a named room or directional area.</summary>
        TrapWarning,
        /// <summary>Tells the player which room the exit is in.</summary>
        ExitLocation,
        /// <summary>Tells the player which room has gold.</summary>
        GoldLocation,
        /// <summary>Gives the colour sequence for a pressure-plate puzzle.</summary>
        PlateColorOrder,
    }

    public FactType factType;

    // Populated fields depend on factType (only used fields will be non-empty).
    public string roomName;
    public string direction;     // "north", "south", "east", "west"
    public string colorSequence; // e.g. "Red, Blue, Yellow"
    public Vector2Int tilePosition;
}
