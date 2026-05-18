using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plain data object returned by <see cref="IDungeonGenerator.Generate"/>.
/// Contains all placement information needed by <see cref="DungeonPainter"/> and the
/// <see cref="ClueGenerator"/>.  No MonoBehaviour, no Unity references.
/// </summary>
public class DungeonLayout
{
    public enum TileType : byte
    {
        Wall = 0,
        Floor = 1,
        SpawnChamber = 2,
        ExitArea = 3,
    }

    // ── Tile grid ──────────────────────────────────
    public int Width { get; }
    public int Height { get; }
    public TileType[,] Tiles { get; }

    // ── Rooms ──────────────────────────────────────
    /// <summary>All rectangular rooms including SpawnChamber and ExitArea.</summary>
    public IReadOnlyList<RoomRect> Rooms { get; }

    // ── Special rooms ──────────────────────────────
    public RoomRect SpawnChamber { get; }
    public RoomRect ExitRoom { get; }

    // ── Anchor lists (tile coordinates) ───────────
    public IReadOnlyList<Vector2Int> TrapAnchors { get; }
    public IReadOnlyList<Vector2Int> GoldAnchors { get; }
    /// <summary>One entry per puzzle site. Each entry is the center tile of the puzzle room.</summary>
    public IReadOnlyList<Vector2Int> PuzzleAnchors { get; }
    /// <summary>Center tile of the exit room; the exit door prefab is placed here.</summary>
    public Vector2Int ExitDoorAnchor { get; }
    /// <summary>Center of the spawn chamber in tile coords; players are placed here.</summary>
    public Vector2Int SpawnAnchor { get; }

    // ── Room names (procedurally assigned for clue flavour) ───
    public IReadOnlyList<string> RoomNames { get; }

    public DungeonLayout(
        int width, int height,
        TileType[,] tiles,
        List<RoomRect> rooms,
        RoomRect spawnChamber,
        RoomRect exitRoom,
        List<Vector2Int> trapAnchors,
        List<Vector2Int> goldAnchors,
        List<Vector2Int> puzzleAnchors,
        Vector2Int exitDoorAnchor,
        Vector2Int spawnAnchor,
        List<string> roomNames)
    {
        Width = width;
        Height = height;
        Tiles = tiles;
        Rooms = rooms;
        SpawnChamber = spawnChamber;
        ExitRoom = exitRoom;
        TrapAnchors = trapAnchors;
        GoldAnchors = goldAnchors;
        PuzzleAnchors = puzzleAnchors;
        ExitDoorAnchor = exitDoorAnchor;
        SpawnAnchor = spawnAnchor;
        RoomNames = roomNames;
    }

    /// <summary>Returns true if the given tile coordinate is within the map bounds.</summary>
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>Converts a tile coordinate to a Unity world position (tile center).</summary>
    public static Vector3 TileToWorld(Vector2Int tile, float tileSize = 1f)
        => new Vector3((tile.x + 0.5f) * tileSize, (tile.y + 0.5f) * tileSize, 0f);

    /// <summary>Converts a world position back to a tile coordinate.</summary>
    public static Vector2Int WorldToTile(Vector3 world, float tileSize = 1f)
        => new Vector2Int(Mathf.FloorToInt(world.x / tileSize), Mathf.FloorToInt(world.y / tileSize));
}

/// <summary>Axis-aligned rectangle in tile coordinates.</summary>
[System.Serializable]
public struct RoomRect
{
    public int x, y, w, h;

    public RoomRect(int x, int y, int w, int h) { this.x = x; this.y = y; this.w = w; this.h = h; }

    public int CenterX => x + w / 2;
    public int CenterY => y + h / 2;
    public Vector2Int Center => new Vector2Int(CenterX, CenterY);

    public bool Intersects(RoomRect other, int margin = 1)
    {
        return !(other.x >= x + w + margin || other.x + other.w <= x - margin ||
                 other.y >= y + h + margin || other.y + other.h <= y - margin);
    }

    public bool Contains(int tx, int ty) => tx >= x && ty >= y && tx < x + w && ty < y + h;
}
