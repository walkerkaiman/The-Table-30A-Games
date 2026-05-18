using System;

/// <summary>
/// Contract for all puzzle site implementations (Pressure Plates, Switch Sequence, Combination
/// Lock, etc.). Every puzzle is a prefab placed by <see cref="DungeonPainter"/> at a puzzle
/// anchor and discovered by explorers during gameplay.
///
/// <see cref="TreasureHunterManager"/> registers sites via <see cref="PuzzleSiteBase.OnEnable"/>
/// and counts solved sites; when all are solved it opens the exit.
/// </summary>
public interface IPuzzleSite
{
    /// <summary>Fired once when the puzzle transitions to the solved state.</summary>
    event Action<IPuzzleSite> OnSolved;

    /// <summary>Whether this puzzle has been fully solved.</summary>
    bool IsSolved { get; }

    /// <summary>
    /// Called by TreasureHunterManager after the dungeon is laid out so the puzzle can
    /// initialise data derived from the layout (e.g. correct clue values, difficulty scale).
    /// </summary>
    void Initialize(DungeonContext ctx);
}
