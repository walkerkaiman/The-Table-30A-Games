using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Derives <see cref="ClueFact"/> atoms from a finished <see cref="DungeonLayout"/>, then
/// distributes them across all players so that:
///   • No single player has the complete picture.
///   • Every clue is held by exactly one player (no redundancy).
///   • Each player receives between <see cref="cluesPerPlayerMin"/> and
///     <see cref="cluesPerPlayerMax"/> clues.
///
/// Called once per session by <see cref="TreasureHunterManager"/> during the Briefing phase.
/// </summary>
public class ClueGenerator
{
    public int cluesPerPlayerMin = 1;
    public int cluesPerPlayerMax = 3;

    private static readonly string[] DirectionNames = { "north", "east", "south", "west" };
    private static readonly Color[] PlateColors =
    {
        Color.red, new Color(1f, 0.65f, 0f), Color.yellow,
        Color.green, Color.cyan, Color.blue,
        new Color(0.6f, 0f, 0.8f), Color.magenta,
    };

    private static readonly string[] PlateColorNames =
    {
        "Red", "Orange", "Yellow", "Green", "Cyan", "Blue", "Purple", "Pink",
    };

    /// <summary>
    /// Generate a list of per-player clue-fact lists. Index matches playerIds array.
    /// </summary>
    public List<List<ClueFact>> Generate(DungeonContext ctx, string[] playerIds, System.Random rng)
    {
        var allFacts = CollectFacts(ctx, rng);
        allFacts.Shuffle(rng);

        var assignment = new List<List<ClueFact>>(playerIds.Length);
        for (int i = 0; i < playerIds.Length; i++) assignment.Add(new List<ClueFact>());

        // Round-robin distribution so every player gets roughly equal facts.
        int playerIdx = 0;
        foreach (var fact in allFacts)
        {
            assignment[playerIdx].Add(fact);
            playerIdx = (playerIdx + 1) % playerIds.Length;
        }

        // Clamp each player to max clues.
        for (int i = 0; i < assignment.Count; i++)
        {
            while (assignment[i].Count > cluesPerPlayerMax)
                assignment[i].RemoveAt(assignment[i].Count - 1);

            // Ensure minimum.
            if (assignment[i].Count < cluesPerPlayerMin && allFacts.Count > 0)
            {
                while (assignment[i].Count < cluesPerPlayerMin)
                    assignment[i].Add(allFacts[rng.Next(allFacts.Count)]);
            }
        }

        return assignment;
    }

    // ── Fact collection ───────────────────────────

    private List<ClueFact> CollectFacts(DungeonContext ctx, System.Random rng)
    {
        var facts = new List<ClueFact>();
        var layout = ctx.Layout;

        // Exit location fact.
        facts.Add(new ClueFact
        {
            factType = ClueFact.FactType.ExitLocation,
            roomName = RoomNameForTile(layout, layout.ExitDoorAnchor),
            direction = DirectionFromSpawn(layout, layout.ExitDoorAnchor),
        });

        // Trap warnings (one per trap anchor, up to 4 to keep clue set manageable).
        int trapWarningLimit = Mathf.Min(layout.TrapAnchors.Count, 4);
        for (int i = 0; i < trapWarningLimit; i++)
        {
            var anchor = layout.TrapAnchors[i];
            facts.Add(new ClueFact
            {
                factType = ClueFact.FactType.TrapWarning,
                roomName = RoomNameForTile(layout, anchor),
                direction = DirectionFromSpawn(layout, anchor),
                tilePosition = anchor,
            });
        }

        // Gold location hints (one per gold anchor, up to 3).
        int goldLimit = Mathf.Min(layout.GoldAnchors.Count, 3);
        for (int i = 0; i < goldLimit; i++)
        {
            var anchor = layout.GoldAnchors[i];
            facts.Add(new ClueFact
            {
                factType = ClueFact.FactType.GoldLocation,
                roomName = RoomNameForTile(layout, anchor),
                direction = DirectionFromSpawn(layout, anchor),
            });
        }

        // Pressure plate colour sequence facts.
        foreach (var anchor in layout.PuzzleAnchors)
        {
            var colors = BuildColorSequence(ctx, rng);
            facts.Add(new ClueFact
            {
                factType = ClueFact.FactType.PlateColorOrder,
                roomName = RoomNameForTile(layout, anchor),
                colorSequence = colors,
            });
        }

        return facts;
    }

    // ── Helpers ───────────────────────────────────

    private static string RoomNameForTile(DungeonLayout layout, Vector2Int tile)
    {
        for (int i = 0; i < layout.Rooms.Count; i++)
        {
            var r = layout.Rooms[i];
            if (r.Contains(tile.x, tile.y) && i < layout.RoomNames.Count)
                return layout.RoomNames[i];
        }
        return "a dark corridor";
    }

    private static string DirectionFromSpawn(DungeonLayout layout, Vector2Int tile)
    {
        int dx = tile.x - layout.SpawnAnchor.x;
        int dy = tile.y - layout.SpawnAnchor.y;
        if (Mathf.Abs(dx) >= Mathf.Abs(dy))
            return dx >= 0 ? "east" : "west";
        return dy >= 0 ? "north" : "south";
    }

    private static string BuildColorSequence(DungeonContext ctx, System.Random rng)
    {
        int count = 2 + ctx.LevelIndex; // escalate with difficulty
        count = Mathf.Clamp(count, 2, PlateColorNames.Length);
        var names = new List<string>(PlateColorNames);
        names.Shuffle(rng);
        return string.Join(", ", names.GetRange(0, count).ToArray());
    }
}
