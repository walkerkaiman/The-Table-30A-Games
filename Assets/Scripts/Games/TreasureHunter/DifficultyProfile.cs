using UnityEngine;

/// <summary>
/// Inspector-tunable parameters that control how large and dangerous a generated dungeon is.
/// Assigned to <see cref="TreasureHunterManager"/> and passed into the generator each level.
/// For multi-level runs the manager can lerp between a <c>easyProfile</c> and <c>hardProfile</c>
/// based on <c>_levelIndex</c>.
/// </summary>
[System.Serializable]
public class DifficultyProfile
{
    [Tooltip("Width of the generated tile map in cells.")]
    public int mapWidth = 40;

    [Tooltip("Height of the generated tile map in cells.")]
    public int mapHeight = 30;

    [Tooltip("Minimum number of rooms to carve.")]
    public int minRooms = 5;

    [Tooltip("Maximum number of rooms to carve.")]
    public int maxRooms = 10;

    [Tooltip("Minimum side length of a room (in tiles).")]
    public int roomMinSize = 4;

    [Tooltip("Maximum side length of a room (in tiles).")]
    public int roomMaxSize = 8;

    [Tooltip("Number of traps to scatter through the dungeon (exact, clamped to available floor).")]
    public int trapCount = 5;

    [Tooltip("Number of gold piles to place.")]
    public int goldPileCount = 8;

    [Tooltip("Number of pressure-plate puzzle sites to generate (v1: 1 puzzle unlocks the exit).")]
    public int puzzleCount = 1;

    [Tooltip("Ratio of extra corridors added beyond the minimum spanning tree (0 = no loops, 1 = many loops).")]
    [Range(0f, 1f)]
    public float loopFactor = 0.3f;
}
