/// <summary>
/// Contract for level generators. Swap out <see cref="RoomsAndCorridorsGenerator"/> for a
/// custom generator by assigning a different implementation to <see cref="TreasureHunterManager"/>.
/// </summary>
public interface IDungeonGenerator
{
    /// <summary>
    /// Generate a new dungeon layout using the provided difficulty parameters and random seed.
    /// Must be deterministic for a given seed.
    /// </summary>
    DungeonLayout Generate(DifficultyProfile profile, int seed);
}
