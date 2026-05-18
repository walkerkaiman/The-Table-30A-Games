using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Consumes a <see cref="DungeonLayout"/> produced by any <see cref="IDungeonGenerator"/> and
/// renders it into a pair of Unity Tilemaps (floor layer + wall layer) then instantiates all
/// prefabs (traps, gold piles, puzzle sites, exit door, decorative torches) under a single
/// <c>spawnedRoot</c> transform so teardown is a single <c>Destroy</c> call.
///
/// Robustness:
///   • If any <c>TileBase</c> field is null OR backed by an empty <c>Tile</c>/<c>AnimatedTile</c>
///     (no assigned sprite), the painter falls back to a procedural tile from
///     <see cref="ProceduralDungeonAssets"/>. This ensures the map is always visible even
///     before a designer has imported their art pack.
///   • Same rule for interactable prefabs (traps, gold, plates, exit door) — an empty array
///     or null reference triggers a procedural fallback so clue targets are always visible on
///     the shared map.
///
/// Scene contract:
///   • Assign the two Tilemaps via the Inspector (or leave empty and the painter will create
///     them under its own transform at runtime).
///   • Call <see cref="Paint"/> once per session start (called by TreasureHunterManager).
///   • Call <see cref="Clear"/> on session end to destroy all spawned objects and clear tiles.
/// </summary>
public class DungeonPainter : MonoBehaviour
{
    [Header("Tilemaps (optional — auto-created if empty)")]
    [Tooltip("Ground / floor layer Tilemap. Auto-created if null.")]
    [SerializeField] private Tilemap floorTilemap;
    [Tooltip("Wall / solid layer Tilemap. Should have a TilemapCollider2D. Auto-created if null.")]
    [SerializeField] private Tilemap wallTilemap;

    [Header("Tiles (optional — procedural fallback used if empty)")]
    [Tooltip("If true, always use the built-in procedural tiles even when fields below are " +
             "assigned. Turn off only after you've imported a real art pack.")]
    [SerializeField] private bool forceProceduralTiles = true;
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase spawnChamberTile;
    [SerializeField] private TileBase exitAreaTile;

    [Header("Prefabs (optional — procedural fallback used if empty)")]
    [Tooltip("Prefab for traps (should have a Trap component). One picked at random per anchor.")]
    [SerializeField] private GameObject[] trapPrefabs;

    [Tooltip("Prefab for gold pickups (GoldPickup component). One per anchor.")]
    [SerializeField] private GameObject[] goldPickupPrefabs;

    [Tooltip("Decorative torch / prop prefabs placed along corridors. May be empty.")]
    [SerializeField] private GameObject[] torchPropPrefabs;

    [Tooltip("Pressure-plate puzzle prefab (PressurePlatePuzzle component). One per puzzle anchor.")]
    [SerializeField] private GameObject pressurePlatePuzzlePrefab;

    [Tooltip("Exit door prefab (ExitDoor component). Placed at the exit door anchor.")]
    [SerializeField] private GameObject exitDoorPrefab;

    [Header("Layout")]
    [Tooltip("World-space size of one tile in Unity units.")]
    public float tileSize = 1f;

    [Tooltip("Density of decorative torch spawns (0 = none, 1 = every torch-eligible floor tile).")]
    [Range(0f, 1f)]
    [SerializeField] private float torchDensity = 0.05f;

    [Header("Fallback Puzzle Settings")]
    [Tooltip("Number of plates required by the procedurally-built puzzle (used when no prefab is assigned).")]
    [SerializeField] private int proceduralPlatesPerPuzzle = 2;

    [Header("Camera Framing")]
    [Tooltip("If true, reposition Camera.main onto the map centre and size it to fit after painting. " +
             "Leave on for out-of-the-box play; turn off if you have your own camera rig.")]
    [SerializeField] private bool autoFrameCamera = true;

    [Tooltip("Extra padding in tiles around the map when auto-framing the camera.")]
    [SerializeField] private float cameraPaddingTiles = 2f;

    // Refs set after painting so other systems can find them.
    public ExitDoor SpawnedExitDoor { get; private set; }
    public IReadOnlyList<PuzzleSiteBase> SpawnedPuzzleSites => _puzzleSites;
    /// <summary>Centre of the painted map in world coordinates. Valid after <see cref="Paint"/>.</summary>
    public Vector3 MapCenterWorld { get; private set; }
    /// <summary>Full size of the painted map in world units (width, height). Valid after <see cref="Paint"/>.</summary>
    public Vector2 MapSizeWorld { get; private set; }

    private readonly List<PuzzleSiteBase> _puzzleSites = new List<PuzzleSiteBase>();
    private GameObject _spawnedRoot;
    private DungeonLayout _layout;

    // ── Public API ────────────────────────────────

    /// <summary>Paint the dungeon and spawn all interactables. Clears any previous layout first.</summary>
    public void Paint(DungeonLayout layout, System.Random rng)
    {
        Clear();
        _layout = layout;
        _puzzleSites.Clear();

        EnsureTilemaps();

        _spawnedRoot = new GameObject("DungeonSpawned");
        _spawnedRoot.transform.SetParent(transform);

        PaintTiles(layout);
        SpawnTraps(layout, rng);
        SpawnGold(layout, rng);
        SpawnPuzzles(layout);
        SpawnExitDoor(layout);
        SpawnTorches(layout, rng);

        MapSizeWorld = new Vector2(layout.Width * tileSize, layout.Height * tileSize);
        MapCenterWorld = new Vector3(MapSizeWorld.x * 0.5f, MapSizeWorld.y * 0.5f, 0f);

        if (autoFrameCamera) FrameCamera();

        GameLog.Game($"DungeonPainter: painted {layout.Width}x{layout.Height} map. " +
                     $"centre={MapCenterWorld} size={MapSizeWorld} " +
                     $"traps={layout.TrapAnchors.Count} gold={layout.GoldAnchors.Count} " +
                     $"puzzles={layout.PuzzleAnchors.Count}");
    }

    /// <summary>Destroy all spawned GameObjects and clear both tilemaps.</summary>
    public void Clear()
    {
        if (_spawnedRoot != null) Destroy(_spawnedRoot);
        _spawnedRoot = null;
        SpawnedExitDoor = null;
        _puzzleSites.Clear();
        _layout = null;

        if (floorTilemap != null) floorTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();
    }

    // ── Tile painting ─────────────────────────────

    private void PaintTiles(DungeonLayout layout)
    {
        // Resolve each tile once up-front. If forceProceduralTiles is on OR the assigned
        // TileBase has no visible sprite, use the guaranteed-visible procedural tile.
        var fFloor = !forceProceduralTiles && TileHasVisibleSprite(floorTile) ? floorTile : ProceduralDungeonAssets.GetFloorTile();
        var fWall = !forceProceduralTiles && TileHasVisibleSprite(wallTile) ? wallTile : ProceduralDungeonAssets.GetWallTile();
        var fSpawn = !forceProceduralTiles && TileHasVisibleSprite(spawnChamberTile) ? spawnChamberTile : ProceduralDungeonAssets.GetSpawnTile();
        var fExit = !forceProceduralTiles && TileHasVisibleSprite(exitAreaTile) ? exitAreaTile : ProceduralDungeonAssets.GetExitTile();

        if (wallTilemap == null) GameLog.Warn("DungeonPainter: wallTilemap is NULL. Walls will not be painted.");
        if (floorTilemap == null) GameLog.Warn("DungeonPainter: floorTilemap is NULL. Floor will not be painted.");
        if (fWall == null) GameLog.Warn("DungeonPainter: wall tile resolved to NULL (procedural fallback broken).");
        if (!(fWall is Tile twall && twall.sprite != null))
            GameLog.Warn("DungeonPainter: wall tile has no sprite! Walls will be invisible.");

        int wallCount = 0, floorCount = 0, spawnCount = 0, exitCount = 0;

        for (int x = 0; x < layout.Width; x++)
        {
            for (int y = 0; y < layout.Height; y++)
            {
                var cellPos = new Vector3Int(x, y, 0);
                switch (layout.Tiles[x, y])
                {
                    case DungeonLayout.TileType.Wall:
                        if (wallTilemap != null) { wallTilemap.SetTile(cellPos, fWall); wallCount++; }
                        break;

                    case DungeonLayout.TileType.Floor:
                        if (floorTilemap != null) { floorTilemap.SetTile(cellPos, fFloor); floorCount++; }
                        break;

                    case DungeonLayout.TileType.SpawnChamber:
                        if (floorTilemap != null) { floorTilemap.SetTile(cellPos, fSpawn); spawnCount++; }
                        break;

                    case DungeonLayout.TileType.ExitArea:
                        if (floorTilemap != null) { floorTilemap.SetTile(cellPos, fExit); exitCount++; }
                        break;
                }
            }
        }

        GameLog.Game($"DungeonPainter.PaintTiles: walls={wallCount} floor={floorCount} " +
                     $"spawn={spawnCount} exit={exitCount} (wallTileColliderType=" +
                     (fWall is Tile tw ? tw.colliderType.ToString() : "?") + ")");

        EnsureWallColliders();
    }

    /// <summary>
    /// Force the wall tilemap into a working collider configuration every paint.
    /// This is defensive: the scene may have been saved with a misconfigured
    /// CompositeCollider2D (empty paths, no merge) that silently provides no collision.
    /// </summary>
    private void EnsureWallColliders()
    {
        if (wallTilemap == null) return;

        var col = wallTilemap.GetComponent<TilemapCollider2D>();
        if (col == null) col = wallTilemap.gameObject.AddComponent<TilemapCollider2D>();
        col.enabled = true;
        col.isTrigger = false;

        var rb = wallTilemap.GetComponent<Rigidbody2D>();
        if (rb == null) rb = wallTilemap.gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.simulated = true;

        var comp = wallTilemap.GetComponent<CompositeCollider2D>();
        if (comp == null) comp = wallTilemap.gameObject.AddComponent<CompositeCollider2D>();
        comp.isTrigger = false;
        comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
        comp.generationType = CompositeCollider2D.GenerationType.Synchronous;

        // CRITICAL: wire the tilemap collider into the composite every time. If the scene
        // had a CompositeCollider2D from a previous save but the tilemap collider wasn't
        // set to "used by composite", the composite has empty paths and nothing collides.
        col.compositeOperation = Collider2D.CompositeOperation.Merge;
    }

    // ── Interactable spawning ─────────────────────

    private void SpawnTraps(DungeonLayout layout, System.Random rng)
    {
        bool hasPrefab = HasUsablePrefab(trapPrefabs);
        foreach (var anchor in layout.TrapAnchors)
        {
            GameObject go;
            if (hasPrefab)
            {
                var prefab = trapPrefabs[rng.Next(trapPrefabs.Length)];
                go = Instantiate(prefab, TileToWorld(anchor), Quaternion.identity, _spawnedRoot.transform);
            }
            else
            {
                go = ProceduralDungeonAssets.BuildTrapObject($"Trap_{anchor.x}_{anchor.y}");
                go.transform.SetParent(_spawnedRoot.transform, false);
                go.transform.position = TileToWorld(anchor);
            }
            go.name = $"Trap_{anchor.x}_{anchor.y}";
            FogHidden.EnsureOn(go);
        }
    }

    private void SpawnGold(DungeonLayout layout, System.Random rng)
    {
        bool hasPrefab = HasUsablePrefab(goldPickupPrefabs);
        foreach (var anchor in layout.GoldAnchors)
        {
            GameObject go;
            if (hasPrefab)
            {
                var prefab = goldPickupPrefabs[rng.Next(goldPickupPrefabs.Length)];
                go = Instantiate(prefab, TileToWorld(anchor), Quaternion.identity, _spawnedRoot.transform);
            }
            else
            {
                go = ProceduralDungeonAssets.BuildGoldObject($"Gold_{anchor.x}_{anchor.y}");
                go.transform.SetParent(_spawnedRoot.transform, false);
                go.transform.position = TileToWorld(anchor);
            }
            go.name = $"Gold_{anchor.x}_{anchor.y}";
            FogHidden.EnsureOn(go);
        }
    }

    private void SpawnPuzzles(DungeonLayout layout)
    {
        bool hasPrefab = pressurePlatePuzzlePrefab != null;
        foreach (var anchor in layout.PuzzleAnchors)
        {
            GameObject go;
            if (hasPrefab)
            {
                go = Instantiate(pressurePlatePuzzlePrefab, TileToWorld(anchor), Quaternion.identity, _spawnedRoot.transform);
            }
            else
            {
                go = ProceduralDungeonAssets.BuildPressurePlatePuzzleObject(
                    $"Puzzle_{anchor.x}_{anchor.y}", proceduralPlatesPerPuzzle);
                go.transform.SetParent(_spawnedRoot.transform, false);
                go.transform.position = TileToWorld(anchor);
            }
            go.name = $"Puzzle_{anchor.x}_{anchor.y}";
            var site = go.GetComponent<PuzzleSiteBase>();
            if (site != null) _puzzleSites.Add(site);
            FogHidden.EnsureOn(go);
        }
    }

    private void SpawnExitDoor(DungeonLayout layout)
    {
        GameObject go;
        if (exitDoorPrefab != null)
        {
            go = Instantiate(exitDoorPrefab, TileToWorld(layout.ExitDoorAnchor), Quaternion.identity, _spawnedRoot.transform);
        }
        else
        {
            go = ProceduralDungeonAssets.BuildExitDoorObject("ExitDoor");
            go.transform.SetParent(_spawnedRoot.transform, false);
            go.transform.position = TileToWorld(layout.ExitDoorAnchor);
        }
        go.name = "ExitDoor";
        SpawnedExitDoor = go.GetComponent<ExitDoor>();
        FogHidden.EnsureOn(go);
    }

    private void SpawnTorches(DungeonLayout layout, System.Random rng)
    {
        if (torchPropPrefabs == null || torchPropPrefabs.Length == 0 || torchDensity <= 0f) return;
        for (int x = 0; x < layout.Width; x++)
        {
            for (int y = 0; y < layout.Height; y++)
            {
                if (layout.Tiles[x, y] == DungeonLayout.TileType.Wall) continue;
                if (rng.NextDouble() > torchDensity) continue;
                var prefab = torchPropPrefabs[rng.Next(torchPropPrefabs.Length)];
                if (prefab == null) continue;
                Instantiate(prefab, TileToWorld(new Vector2Int(x, y)), Quaternion.identity, _spawnedRoot.transform);
            }
        }
    }

    // ── Camera framing ────────────────────────────

    private void FrameCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        cam.transform.position = new Vector3(MapCenterWorld.x, MapCenterWorld.y, cam.transform.position.z);
        if (cam.orthographic)
        {
            float halfHeight = MapSizeWorld.y * 0.5f + cameraPaddingTiles;
            float halfWidth = (MapSizeWorld.x * 0.5f + cameraPaddingTiles) / Mathf.Max(0.01f, cam.aspect);
            cam.orthographicSize = Mathf.Max(halfHeight, halfWidth);
        }
    }

    // ── Tilemap auto-create ───────────────────────

    /// <summary>
    /// If the floor/wall tilemaps were not assigned in the Inspector, create them under this
    /// transform. Keeps the scene-setup friction to zero — designers can still wire up
    /// authored tilemaps later without changing code.
    /// </summary>
    private void EnsureTilemaps()
    {
        if (floorTilemap == null || wallTilemap == null)
        {
            var gridGo = GetComponentInChildren<Grid>()?.gameObject;
            if (gridGo == null)
            {
                gridGo = new GameObject("Grid");
                gridGo.transform.SetParent(transform, false);
                gridGo.AddComponent<Grid>();
            }

            if (floorTilemap == null)
            {
                var go = new GameObject("FloorTilemap");
                go.transform.SetParent(gridGo.transform, false);
                floorTilemap = go.AddComponent<Tilemap>();
                go.AddComponent<TilemapRenderer>();
            }

            if (wallTilemap == null)
            {
                var go = new GameObject("WallTilemap");
                go.transform.SetParent(gridGo.transform, false);
                wallTilemap = go.AddComponent<Tilemap>();
                go.AddComponent<TilemapRenderer>();
            }
        }

        // Defense in depth: each Tilemap MUST have a Grid component on itself or an ancestor,
        // otherwise Unity won't render any tiles (SetTile silently does nothing visible). Scenes
        // saved without a Grid component on the parent will hit this safeguard.
        EnsureGridAncestor(floorTilemap);
        EnsureGridAncestor(wallTilemap);

        // Force correct sorting every paint so walls reliably draw above the floor even if the
        // scene was authored with both renderers at sortingOrder 0.
        var floorTr = floorTilemap != null ? floorTilemap.GetComponent<TilemapRenderer>() : null;
        var wallTr = wallTilemap != null ? wallTilemap.GetComponent<TilemapRenderer>() : null;
        if (floorTr != null) floorTr.sortingOrder = 0;
        if (wallTr != null) wallTr.sortingOrder = 10;
    }

    private static void EnsureGridAncestor(Tilemap tm)
    {
        if (tm == null) return;
        if (tm.GetComponentInParent<Grid>() != null) return;

        // Add a Grid component to the tilemap's parent (or the tilemap itself if root).
        var host = tm.transform.parent != null ? tm.transform.parent.gameObject : tm.gameObject;
        host.AddComponent<Grid>();
        GameLog.Warn($"DungeonPainter: added missing Grid component to '{host.name}' so tiles " +
                     $"would actually render. Save the scene to persist.");
    }

    // ── Helpers ───────────────────────────────────

    private Vector3 TileToWorld(Vector2Int tile)
    {
        return new Vector3((tile.x + 0.5f) * tileSize, (tile.y + 0.5f) * tileSize, 0f);
    }

    /// <summary>Returns the world position of the spawn chamber centre.</summary>
    public Vector3 GetSpawnWorldPosition()
    {
        return _layout != null ? TileToWorld(_layout.SpawnAnchor) : Vector3.zero;
    }

    /// <summary>
    /// True if the <paramref name="tile"/> reference will actually render a sprite.
    /// Handles both <c>Tile</c> and the Tilemap-Extras <c>AnimatedTile</c> by reflection
    /// (to avoid a hard dependency on the Extras package).
    /// </summary>
    private static bool TileHasVisibleSprite(TileBase tile)
    {
        if (tile == null) return false;
        if (tile is Tile t) return t.sprite != null;

        var type = tile.GetType();

        // AnimatedTile and related store sprites in a non-public array field.
        var animField = type.GetField("m_AnimatedSprites", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? type.GetField("m_AnimatedSprites", BindingFlags.Instance | BindingFlags.Public);
        if (animField != null)
        {
            if (animField.GetValue(tile) is Sprite[] arr)
            {
                for (int i = 0; i < arr.Length; i++)
                    if (arr[i] != null) return true;
            }
            return false;
        }

        // Rule-tile / scriptable-tile variants usually have a _DefaultSprite_ field.
        var spriteField = type.GetField("m_DefaultSprite", BindingFlags.Instance | BindingFlags.NonPublic)
                          ?? type.GetField("sprite", BindingFlags.Instance | BindingFlags.Public);
        if (spriteField != null && spriteField.GetValue(tile) is Sprite s && s != null) return true;

        // Unknown TileBase subclass — assume it renders something (let the user see what they wired up).
        return true;
    }

    private static bool HasUsablePrefab(GameObject[] arr)
    {
        if (arr == null) return false;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] != null) return true;
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (tileSize <= 0f) tileSize = 1f;
        if (proceduralPlatesPerPuzzle < 1) proceduralPlatesPerPuzzle = 1;
        if (cameraPaddingTiles < 0f) cameraPaddingTiles = 0f;
    }
#endif
}
