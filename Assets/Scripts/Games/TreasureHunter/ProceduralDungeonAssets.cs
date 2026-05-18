using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Runtime generator for placeholder art so Treasure Hunter is playable without any
/// hand-authored sprites, tile assets, or prefabs. All functions return objects that
/// are safe to use in play mode and are cached so repeated calls don't allocate.
///
/// Pipeline:
///   • <see cref="GetFloorTile"/> / <see cref="GetWallTile"/> etc. return a <c>Tile</c>
///     with a procedural solid-color sprite. Wall tiles use <c>Tile.ColliderType.Grid</c>
///     so the existing TilemapCollider2D picks them up.
///   • <see cref="BuildTrapObject"/>, <see cref="BuildGoldObject"/>,
///     <see cref="BuildPressurePlatePuzzleObject"/>, and <see cref="BuildExitDoorObject"/>
///     return fully-wired GameObjects (SpriteRenderer + trigger Collider2D + behaviour
///     component) so the clue system has real, visible targets on the shared map.
///
/// Designers can replace any of these with hand-authored prefabs/tiles via the
/// DungeonPainter inspector at any point; the painter only falls back to these when
/// the corresponding inspector field is empty.
/// </summary>
public static class ProceduralDungeonAssets
{
    // ── Cached tile singletons ───────────────────────────────────────────────

    private static Tile _floorTile;
    private static Tile _wallTile;
    private static Tile _spawnTile;
    private static Tile _exitTile;

    // ── Cached sprites for interactables ─────────────────────────────────────

    private static Sprite _trapSprite;
    private static Sprite _goldSprite;
    private static Sprite _plateSprite;
    private static Sprite _exitDoorLockedSprite;
    private static Sprite _exitDoorUnlockedSprite;
    private static Sprite _downedSprite;

    private const int TilePixelSize = 32;
    private const int IconPixelSize = 64;
    private const float TargetPixelsPerUnit = 32f;

    // ════════════════════════════════════════════════════════════════════════
    //  TILES
    // ════════════════════════════════════════════════════════════════════════

    public static Tile GetFloorTile()
    {
        // Warm dark-brown floor with a subtle grid line. Plenty of contrast against the
        // light-gray walls so you can see the room outlines at a glance.
        if (_floorTile == null)
            _floorTile = BuildTile("floor_proc",
                new Color32(0x3A, 0x2F, 0x25, 255),
                new Color32(0x2E, 0x25, 0x1D, 255),
                gridPattern: true,
                collider: Tile.ColliderType.None);
        return _floorTile;
    }

    public static Tile GetWallTile()
    {
        // Light stone-gray walls on purpose — they stand out against the dark floor so it
        // is visually obvious that walls exist and are painted correctly. Designers can
        // recolor by assigning a real wall Tile in the inspector.
        if (_wallTile == null)
            _wallTile = BuildTile("wall_proc",
                new Color32(0xA8, 0xA8, 0xAE, 255),
                new Color32(0x70, 0x6E, 0x72, 255),
                gridPattern: true,
                collider: Tile.ColliderType.Grid);
        return _wallTile;
    }

    public static Tile GetSpawnTile()
    {
        if (_spawnTile == null)
            _spawnTile = BuildTile("spawn_proc",
                new Color32(0x3A, 0x5E, 0x48, 255),
                new Color32(0x2F, 0x4D, 0x3A, 255),
                gridPattern: true,
                collider: Tile.ColliderType.None);
        return _spawnTile;
    }

    public static Tile GetExitTile()
    {
        if (_exitTile == null)
            _exitTile = BuildTile("exit_proc",
                new Color32(0x70, 0x5A, 0x1C, 255),
                new Color32(0x58, 0x46, 0x15, 255),
                gridPattern: true,
                collider: Tile.ColliderType.None);
        return _exitTile;
    }

    private static Tile BuildTile(string name, Color32 primary, Color32 secondary,
        bool gridPattern, Tile.ColliderType collider)
    {
        var tex = new Texture2D(TilePixelSize, TilePixelSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name + "_tex",
        };

        var pixels = new Color32[TilePixelSize * TilePixelSize];
        for (int y = 0; y < TilePixelSize; y++)
        for (int x = 0; x < TilePixelSize; x++)
        {
            // Subtle grid lines along the tile border so the map reads as a grid.
            bool border = gridPattern && (x == 0 || y == 0 || x == TilePixelSize - 1 || y == TilePixelSize - 1);
            pixels[y * TilePixelSize + x] = border ? secondary : primary;
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, TilePixelSize, TilePixelSize),
            new Vector2(0.5f, 0.5f),
            TilePixelSize); // pixelsPerUnit=TilePixelSize so sprite spans 1 world unit

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.name = name;
        tile.sprite = sprite;
        tile.colliderType = collider;
        tile.color = Color.white;
        return tile;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  INTERACTABLE PROCEDURAL SPRITES
    // ════════════════════════════════════════════════════════════════════════

    private static Sprite GetTrapSprite()
    {
        if (_trapSprite == null) _trapSprite = BuildIconSprite("trap_icon", DrawTrap);
        return _trapSprite;
    }

    private static Sprite GetGoldSprite()
    {
        if (_goldSprite == null) _goldSprite = BuildIconSprite("gold_icon", DrawCoin);
        return _goldSprite;
    }

    private static Sprite GetPlateSprite()
    {
        if (_plateSprite == null) _plateSprite = BuildIconSprite("plate_icon", DrawPlate);
        return _plateSprite;
    }

    private static Sprite GetExitDoorLockedSprite()
    {
        if (_exitDoorLockedSprite == null) _exitDoorLockedSprite = BuildIconSprite("door_locked_icon", (p, s) => DrawDoor(p, s, locked: true));
        return _exitDoorLockedSprite;
    }

    private static Sprite GetExitDoorUnlockedSprite()
    {
        if (_exitDoorUnlockedSprite == null) _exitDoorUnlockedSprite = BuildIconSprite("door_unlocked_icon", (p, s) => DrawDoor(p, s, locked: false));
        return _exitDoorUnlockedSprite;
    }

    public static Sprite GetDownedSprite()
    {
        if (_downedSprite == null) _downedSprite = BuildIconSprite("downed_icon", DrawDowned);
        return _downedSprite;
    }

    private delegate void DrawIcon(Color32[] pixels, int size);

    private static Sprite BuildIconSprite(string name, DrawIcon draw)
    {
        int size = IconPixelSize;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = name + "_tex",
        };
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
        draw(pixels, size);
        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            TargetPixelsPerUnit); // icons span ~2 tiles wide
    }

    // ── Pixel-art helpers ───────────────────────────────────────────────────

    private static void DrawTrap(Color32[] pixels, int size)
    {
        // Red X with spiked border.
        var red = new Color32(200, 40, 40, 255);
        var dark = new Color32(90, 15, 15, 255);
        int c = size / 2;
        int thickness = size / 10;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Circular clip
            int dx = x - c, dy = y - c;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > size * 0.48f) continue;

            bool diag1 = Mathf.Abs((x - 4) - (y - 4)) <= thickness;
            bool diag2 = Mathf.Abs((x - 4) - (size - 4 - y)) <= thickness;
            bool onX = diag1 || diag2;

            if (onX)
                pixels[y * size + x] = red;
            else
                pixels[y * size + x] = dark;
        }
    }

    private static void DrawCoin(Color32[] pixels, int size)
    {
        var gold = new Color32(255, 205, 60, 255);
        var dark = new Color32(180, 130, 20, 255);
        var sparkle = new Color32(255, 240, 180, 255);
        int c = size / 2;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int dx = x - c, dy = y - c;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > size * 0.48f) continue;
            if (d > size * 0.40f) { pixels[y * size + x] = dark; continue; }
            pixels[y * size + x] = gold;

            // Sparkle in upper-left
            int sxdx = x - size * 3 / 10, sydy = y - size * 7 / 10;
            if (sxdx * sxdx + sydy * sydy < (size / 12f) * (size / 12f))
                pixels[y * size + x] = sparkle;
        }
    }

    private static void DrawPlate(Color32[] pixels, int size)
    {
        var edge = new Color32(40, 60, 120, 255);
        var face = new Color32(80, 120, 210, 255);
        var glyph = new Color32(200, 225, 255, 255);
        int margin = size / 8;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool inBox = x >= margin && y >= margin && x < size - margin && y < size - margin;
            bool isEdge = x == margin || y == margin || x == size - margin - 1 || y == size - margin - 1;
            if (!inBox) continue;
            pixels[y * size + x] = isEdge ? edge : face;

            // Inner rune (triangle)
            int cx = size / 2, cy = size / 2;
            int relX = x - cx, relY = y - cy;
            if (Mathf.Abs(relX) + Mathf.Abs(relY) < size / 5)
                pixels[y * size + x] = glyph;
        }
    }

    private static void DrawDoor(Color32[] pixels, int size, bool locked)
    {
        Color32 body = locked ? new Color32(110, 70, 30, 255) : new Color32(60, 170, 90, 255);
        Color32 trim = new Color32(30, 20, 10, 255);
        Color32 accent = locked ? new Color32(200, 200, 60, 255) : new Color32(220, 255, 220, 255);

        int margin = size / 10;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool inDoor = x >= margin && y >= margin && x < size - margin && y < size - (locked ? margin : 0);
            if (!inDoor) continue;
            bool onEdge = x == margin || y == margin || x == size - margin - 1;
            pixels[y * size + x] = onEdge ? trim : body;
        }

        // Lock handle in the middle-right
        int hx = size * 7 / 10;
        int hy = size / 2;
        int hr = size / 10;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int dx = x - hx, dy = y - hy;
            if (dx * dx + dy * dy < hr * hr)
                pixels[y * size + x] = accent;
        }
    }

    private static void DrawDowned(Color32[] pixels, int size)
    {
        // Skull/X face.
        var bg = new Color32(210, 40, 40, 230);
        var eye = new Color32(255, 255, 255, 255);
        int c = size / 2;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int dx = x - c, dy = y - c;
            if (dx * dx + dy * dy > (size * 0.45f) * (size * 0.45f)) continue;
            pixels[y * size + x] = bg;
        }
        int eyeR = size / 10;
        DrawCircle(pixels, size, c - size / 6, c + size / 12, eyeR, eye);
        DrawCircle(pixels, size, c + size / 6, c + size / 12, eyeR, eye);
    }

    private static void DrawCircle(Color32[] pixels, int size, int cx, int cy, int r, Color32 color)
    {
        for (int y = cy - r; y <= cy + r; y++)
        for (int x = cx - r; x <= cx + r; x++)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) continue;
            int dx = x - cx, dy = y - cy;
            if (dx * dx + dy * dy <= r * r) pixels[y * size + x] = color;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  INTERACTABLE GAMEOBJECTS (fallbacks used by DungeonPainter)
    // ════════════════════════════════════════════════════════════════════════

    public static GameObject BuildTrapObject(string name)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetTrapSprite();
        sr.sortingOrder = 10;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.45f;
        var trap = go.AddComponent<Trap>();
        trap.armed = true;
        trap.resetAfterSeconds = 0f;
        trap.revealOnTrip = true;
        FogHidden.EnsureOn(go);
        return go;
    }

    public static GameObject BuildGoldObject(string name, int value = 10)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetGoldSprite();
        sr.sortingOrder = 10;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.35f;
        var gold = go.AddComponent<GoldPickup>();
        gold.goldValue = value;
        gold.goldPolicy = GoldPickup.GoldPolicy.TeamPool;
        FogHidden.EnsureOn(go);
        return go;
    }

    /// <summary>
    /// Build a pressure-plate puzzle with N child plates arranged in a row at the
    /// given world position. The puzzle is solved when all plates are pressed simultaneously.
    /// </summary>
    public static GameObject BuildPressurePlatePuzzleObject(string name, int requiredPlates = 2, float plateSpacing = 1.1f)
    {
        var root = new GameObject(name);
        var puzzle = root.AddComponent<PressurePlatePuzzle>();
        puzzle.requiredPlates = requiredPlates;
        puzzle.solveHoldSeconds = 0f;
        puzzle.plateColorOrder = new Color[requiredPlates];

        float startX = -((requiredPlates - 1) * plateSpacing) * 0.5f;
        var sprite = GetPlateSprite();
        var paletteHues = new float[] { 0.0f, 0.12f, 0.33f, 0.55f, 0.75f, 0.92f };

        for (int i = 0; i < requiredPlates; i++)
        {
            var plateGo = new GameObject($"Plate_{i}");
            plateGo.transform.SetParent(root.transform, false);
            plateGo.transform.localPosition = new Vector3(startX + i * plateSpacing, 0f, 0f);

            var sr = plateGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 9;
            Color c = Color.HSVToRGB(paletteHues[i % paletteHues.Length], 0.8f, 0.9f);
            sr.color = c;

            var col = plateGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.9f, 0.9f);

            var plate = plateGo.AddComponent<PressurePlate>();
            plate.plateColor = c;
            puzzle.plateColorOrder[i] = c;

            // Each plate is individually hidden until an explorer passes nearby.
            FogHidden.EnsureOn(plateGo);
        }

        return root;
    }

    public static GameObject BuildExitDoorObject(string name)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetExitDoorLockedSprite();
        sr.sortingOrder = 11;

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.3f, 1.5f);

        var door = go.AddComponent<ExitDoor>();
        door.isUnlocked = false;

        // Attach an observer that swaps the sprite when the door unlocks.
        var swapper = go.AddComponent<ExitDoorSpriteSwap>();
        swapper.door = door;
        swapper.renderer = sr;
        swapper.lockedSprite = GetExitDoorLockedSprite();
        swapper.unlockedSprite = GetExitDoorUnlockedSprite();

        FogHidden.EnsureOn(go);
        return go;
    }
}

/// <summary>
/// Tiny helper that swaps an <see cref="ExitDoor"/>'s SpriteRenderer sprite between
/// its locked and unlocked procedural icons whenever the state changes. Used only by
/// the procedurally-built exit door fallback so designers can still hand-author a
/// prefab with richer visuals.
/// </summary>
public class ExitDoorSpriteSwap : MonoBehaviour
{
    public ExitDoor door;
    public new SpriteRenderer renderer;
    public Sprite lockedSprite;
    public Sprite unlockedSprite;

    private bool _lastUnlocked;

    private void LateUpdate()
    {
        if (door == null || renderer == null) return;
        if (door.isUnlocked == _lastUnlocked) return;
        _lastUnlocked = door.isUnlocked;
        renderer.sprite = _lastUnlocked ? unlockedSprite : lockedSprite;
    }
}
