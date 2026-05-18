/// <summary>
/// Lightweight context object passed to puzzle sites and the clue generator so they can
/// derive their configuration from the generated dungeon without holding a direct reference
/// to <see cref="TreasureHunterManager"/> (keeping the puzzle prefabs decoupled).
/// </summary>
public class DungeonContext
{
    public DungeonLayout Layout { get; }
    public DifficultyProfile Difficulty { get; }
    public int Seed { get; }
    public int LevelIndex { get; }

    public DungeonContext(DungeonLayout layout, DifficultyProfile difficulty, int seed, int levelIndex)
    {
        Layout = layout;
        Difficulty = difficulty;
        Seed = seed;
        LevelIndex = levelIndex;
    }
}
