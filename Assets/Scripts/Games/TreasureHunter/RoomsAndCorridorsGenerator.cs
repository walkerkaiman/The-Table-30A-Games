using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Default <see cref="IDungeonGenerator"/> implementation.
/// Uses the classic rooms-and-corridors algorithm:
///   1. Place random non-overlapping rooms.
///   2. Build a minimum spanning tree of corridors between room centres (L-shaped tunnels).
///   3. Add extra "loop" corridors controlled by <see cref="DifficultyProfile.loopFactor"/>.
///   4. Designate the farthest room from spawn as the exit room.
///   5. Scatter trap, gold, and puzzle anchors on valid floor tiles far from spawn.
/// </summary>
public class RoomsAndCorridorsGenerator : IDungeonGenerator
{
    private static readonly string[] RoomNamePool =
    {
        "Antechamber", "Vault", "Crypt", "Sanctum", "Armory", "Library",
        "Treasury", "Grotto", "Cistern", "Chapel", "Barracks", "Dungeon",
        "Cellar", "Passage", "Atrium", "Ossuary", "Alcove", "Chamber",
    };

    public DungeonLayout Generate(DifficultyProfile profile, int seed)
    {
        var rng = new System.Random(seed);
        int w = profile.mapWidth;
        int h = profile.mapHeight;
        var tiles = new DungeonLayout.TileType[w, h];

        // ── 1. Place rooms ─────────────────────────────
        var rooms = TryPlaceRooms(profile, rng, w, h, tiles);

        if (rooms.Count < 2)
        {
            // Fallback: two manually placed rooms so the game never crashes on tiny maps.
            rooms.Clear();
            rooms.Add(new RoomRect(1, 1, 5, 5));
            rooms.Add(new RoomRect(w - 7, h - 7, 5, 5));
            CarveRoom(tiles, rooms[0]);
            CarveRoom(tiles, rooms[1]);
        }

        // ── 2. Minimum spanning tree corridors ─────────
        var corridorPairs = BuildMST(rooms, rng);
        foreach (var (a, b) in corridorPairs)
            CarveCorridorL(tiles, rooms[a].Center, rooms[b].Center, rng);

        // ── 3. Extra loop corridors ─────────────────────
        int extras = Mathf.RoundToInt(rooms.Count * profile.loopFactor);
        for (int i = 0; i < extras; i++)
        {
            int a = rng.Next(rooms.Count);
            int b = rng.Next(rooms.Count);
            if (a != b) CarveCorridorL(tiles, rooms[a].Center, rooms[b].Center, rng);
        }

        // ── 4. Designate spawn + exit rooms ────────────
        int spawnIdx = 0;
        int exitIdx = FarthestRoom(rooms, spawnIdx);
        var spawnRoom = rooms[spawnIdx];
        var exitRoom = rooms[exitIdx];

        MarkRoomTileType(tiles, spawnRoom, DungeonLayout.TileType.SpawnChamber);
        MarkRoomTileType(tiles, exitRoom, DungeonLayout.TileType.ExitArea);

        // ── 5. Scatter interactables ───────────────────
        var floorTiles = CollectFloorTiles(tiles, w, h);
        var trapAnchors = PickFarTiles(floorTiles, spawnRoom.Center, profile.trapCount, rng, minSqDist: 9f);
        var goldAnchors = PickFarTiles(floorTiles, spawnRoom.Center, profile.goldPileCount, rng, minSqDist: 4f);
        var puzzleAnchors = PickRoomCenters(rooms, spawnIdx, exitIdx, profile.puzzleCount);

        // ── 6. Room names ──────────────────────────────
        var namePool = new List<string>(RoomNamePool);
        namePool.Shuffle(rng);
        var roomNames = new List<string>();
        for (int i = 0; i < rooms.Count; i++)
            roomNames.Add(i < namePool.Count ? namePool[i] : $"Room {i + 1}");

        return new DungeonLayout(
            w, h, tiles, rooms, spawnRoom, exitRoom,
            trapAnchors, goldAnchors, puzzleAnchors,
            exitRoom.Center, spawnRoom.Center, roomNames);
    }

    // ── Room placement ─────────────────────────────────────

    private static List<RoomRect> TryPlaceRooms(DifficultyProfile p, System.Random rng, int w, int h, DungeonLayout.TileType[,] tiles)
    {
        var rooms = new List<RoomRect>();
        int attempts = p.maxRooms * 10;

        for (int iter = 0; iter < attempts && rooms.Count < p.maxRooms; iter++)
        {
            int rw = rng.Next(p.roomMinSize, p.roomMaxSize + 1);
            int rh = rng.Next(p.roomMinSize, p.roomMaxSize + 1);
            int rx = rng.Next(1, w - rw - 1);
            int ry = rng.Next(1, h - rh - 1);
            var candidate = new RoomRect(rx, ry, rw, rh);

            bool overlaps = false;
            foreach (var existing in rooms)
            {
                if (candidate.Intersects(existing)) { overlaps = true; break; }
            }
            if (overlaps) continue;

            CarveRoom(tiles, candidate);
            rooms.Add(candidate);
        }
        return rooms;
    }

    private static void CarveRoom(DungeonLayout.TileType[,] tiles, RoomRect r)
    {
        for (int x = r.x; x < r.x + r.w; x++)
            for (int y = r.y; y < r.y + r.h; y++)
                tiles[x, y] = DungeonLayout.TileType.Floor;
    }

    private static void MarkRoomTileType(DungeonLayout.TileType[,] tiles, RoomRect r, DungeonLayout.TileType t)
    {
        for (int x = r.x; x < r.x + r.w; x++)
            for (int y = r.y; y < r.y + r.h; y++)
                tiles[x, y] = t;
    }

    // ── Corridors ──────────────────────────────────────────

    private static void CarveCorridorL(DungeonLayout.TileType[,] tiles, Vector2Int a, Vector2Int b, System.Random rng)
    {
        // Randomly pick which axis to go first.
        if (rng.Next(2) == 0)
        {
            CarveHorizontal(tiles, a.x, b.x, a.y);
            CarveVertical(tiles, a.y, b.y, b.x);
        }
        else
        {
            CarveVertical(tiles, a.y, b.y, a.x);
            CarveHorizontal(tiles, a.x, b.x, b.y);
        }
    }

    private static void CarveHorizontal(DungeonLayout.TileType[,] tiles, int x1, int x2, int y)
    {
        int minX = Mathf.Min(x1, x2), maxX = Mathf.Max(x1, x2);
        for (int x = minX; x <= maxX; x++)
            if (tiles[x, y] == DungeonLayout.TileType.Wall)
                tiles[x, y] = DungeonLayout.TileType.Floor;
    }

    private static void CarveVertical(DungeonLayout.TileType[,] tiles, int y1, int y2, int x)
    {
        int minY = Mathf.Min(y1, y2), maxY = Mathf.Max(y1, y2);
        for (int y = minY; y <= maxY; y++)
            if (tiles[x, y] == DungeonLayout.TileType.Wall)
                tiles[x, y] = DungeonLayout.TileType.Floor;
    }

    // ── MST (Prim's) ───────────────────────────────────────

    private static List<(int, int)> BuildMST(List<RoomRect> rooms, System.Random rng)
    {
        var result = new List<(int, int)>();
        if (rooms.Count <= 1) return result;

        var inMST = new bool[rooms.Count];
        inMST[0] = true;
        int connected = 1;

        while (connected < rooms.Count)
        {
            int bestA = -1, bestB = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < rooms.Count; i++)
            {
                if (!inMST[i]) continue;
                for (int j = 0; j < rooms.Count; j++)
                {
                    if (inMST[j]) continue;
                    float d = Vector2Int.Distance(rooms[i].Center, rooms[j].Center);
                    if (d < bestDist) { bestDist = d; bestA = i; bestB = j; }
                }
            }
            if (bestA < 0) break;
            inMST[bestB] = true;
            result.Add((bestA, bestB));
            connected++;
        }
        return result;
    }

    // ── Helpers ────────────────────────────────────────────

    private static int FarthestRoom(List<RoomRect> rooms, int from)
    {
        int best = 0;
        float bestDist = -1f;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (i == from) continue;
            float d = Vector2Int.Distance(rooms[from].Center, rooms[i].Center);
            if (d > bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private static List<Vector2Int> CollectFloorTiles(DungeonLayout.TileType[,] tiles, int w, int h)
    {
        var list = new List<Vector2Int>(w * h / 2);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (tiles[x, y] != DungeonLayout.TileType.Wall)
                    list.Add(new Vector2Int(x, y));
        return list;
    }

    private static List<Vector2Int> PickFarTiles(List<Vector2Int> candidates, Vector2Int origin, int count, System.Random rng, float minSqDist)
    {
        // Filter to candidates beyond minSqDist from origin.
        var filtered = new List<Vector2Int>(candidates.Count);
        foreach (var t in candidates)
        {
            float dx = t.x - origin.x;
            float dy = t.y - origin.y;
            if (dx * dx + dy * dy >= minSqDist) filtered.Add(t);
        }
        filtered.Shuffle(rng);

        var picked = new List<Vector2Int>();
        for (int i = 0; i < filtered.Count && picked.Count < count; i++)
            picked.Add(filtered[i]);
        return picked;
    }

    private static List<Vector2Int> PickRoomCenters(List<RoomRect> rooms, int excludeSpawn, int excludeExit, int count)
    {
        var result = new List<Vector2Int>();
        for (int i = 0; i < rooms.Count && result.Count < count; i++)
        {
            if (i == excludeSpawn || i == excludeExit) continue;
            result.Add(rooms[i].Center);
        }
        return result;
    }
}

// Extend System.Random with Fisher-Yates shuffle for use in non-Unity contexts.
internal static class RandomExtensions
{
    internal static void Shuffle<T>(this List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
