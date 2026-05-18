using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure-data mosaic engine. Owns the placed-tile grid, the open-slot frontier, the
/// inventory of drawings that didn't fit yet, and viewport panning + offscreen culling.
///
/// Events let a renderer (<see cref="WangTilesTableDisplay"/>) react without coupling.
///
/// Coordinate convention: grid (x, y) with +X = right, +Y = up. Tile (0,0) is the seed.
/// </summary>
public class WangTileMosaic
{
    /// <summary>A drawing placed at a grid coordinate.</summary>
    public class PlacedTile
    {
        public Vector2Int gridPos;
        public WangTileDrawing drawing;
    }

    /// <summary>
    /// Axis along which the mosaic is allowed to grow indefinitely.
    /// Horizontal (default): tiles stay within the viewport's vertical band; new tiles
    /// extend the mosaic left/right and the viewport pans along X only. Best for tables
    /// that are wider than tall.
    /// Vertical: mirrored — new tiles extend up/down, viewport pans along Y only.
    /// Both: the original unconstrained behavior (mosaic can grow in any direction).
    /// </summary>
    public enum PanAxis { Horizontal, Vertical, Both }

    // ── Configuration ────────────────────────────
    private readonly int _viewportWidth;
    private readonly int _viewportHeight;
    private readonly int _cullMargin;
    private readonly int _inventoryCap;
    private readonly PanAxis _panAxis;

    // ── State ────────────────────────────────────
    private readonly Dictionary<Vector2Int, PlacedTile> _placed = new Dictionary<Vector2Int, PlacedTile>();
    private readonly HashSet<Vector2Int> _openSlots = new HashSet<Vector2Int>();
    private readonly List<WangTileDrawing> _inventory = new List<WangTileDrawing>();

    /// <summary>Center of the current viewport in grid coordinates (floats for smooth pan).</summary>
    public Vector2 ViewportCenter { get; private set; } = Vector2.zero;

    public int ViewportWidth => _viewportWidth;
    public int ViewportHeight => _viewportHeight;
    public int CullMargin => _cullMargin;
    public int PlacedCount => _placed.Count;
    public int InventoryCount => _inventory.Count;

    // ── Events ───────────────────────────────────
    public event Action<PlacedTile> TilePlaced;
    public event Action<Vector2Int> TileCulled;
    public event Action<Vector2> ViewportCenterChanged;

    public WangTileMosaic(
        int viewportWidth = 12,
        int viewportHeight = 7,
        int cullMargin = 1,
        int inventoryCap = 64,
        PanAxis panAxis = PanAxis.Horizontal)
    {
        _viewportWidth = Mathf.Max(3, viewportWidth);
        _viewportHeight = Mathf.Max(3, viewportHeight);
        _cullMargin = Mathf.Max(0, cullMargin);
        _inventoryCap = Mathf.Max(1, inventoryCap);
        _panAxis = panAxis;
    }

    /// <summary>
    /// True when the given grid position falls within the band perpendicular to the
    /// configured pan axis (i.e. the dimension along which the mosaic does NOT grow).
    /// Used to keep placement constrained to a fixed-height strip in Horizontal mode
    /// (or fixed-width strip in Vertical mode).
    /// </summary>
    private bool IsSlotInBand(Vector2Int slot)
    {
        switch (_panAxis)
        {
            case PanAxis.Horizontal:
            {
                int halfH = _viewportHeight / 2;
                return slot.y >= -halfH && slot.y <= halfH;
            }
            case PanAxis.Vertical:
            {
                int halfW = _viewportWidth / 2;
                return slot.x >= -halfW && slot.x <= halfW;
            }
            default:
                return true;
        }
    }

    // ════════════════════════════════════════════
    //  Placement API
    // ════════════════════════════════════════════

    /// <summary>
    /// Attempt to place the given drawing in the mosaic.
    /// Returns true if it was placed immediately, false if it went into the inventory.
    /// Either way, the caller can call <see cref="SweepInventory"/> to retry later
    /// (already called automatically after every successful placement).
    /// </summary>
    public bool Submit(WangTileDrawing drawing)
    {
        if (drawing == null) return false;

        if (_placed.Count == 0)
        {
            PlaceAt(Vector2Int.zero, drawing);
            return true;
        }

        if (TryFindBestSlot(drawing.shape, out Vector2Int slot))
        {
            PlaceAt(slot, drawing);
            SweepInventory();
            return true;
        }

        AddToInventory(drawing);
        return false;
    }

    /// <summary>
    /// Scan inventory once and place any drawings that now fit a newly-opened slot.
    /// Called automatically after each successful placement; safe to call manually too.
    /// </summary>
    public void SweepInventory()
    {
        if (_inventory.Count == 0) return;

        bool placedAny;
        do
        {
            placedAny = false;
            for (int i = 0; i < _inventory.Count; i++)
            {
                var d = _inventory[i];
                if (TryFindBestSlot(d.shape, out Vector2Int slot))
                {
                    _inventory.RemoveAt(i);
                    PlaceAt(slot, d);
                    placedAny = true;
                    break; // restart sweep since PlaceAt opened new slots
                }
            }
        } while (placedAny);
    }

    private void AddToInventory(WangTileDrawing d)
    {
        if (_inventory.Count >= _inventoryCap)
            _inventory.RemoveAt(0); // FIFO eviction — oldest unplaceable drawing falls out
        _inventory.Add(d);
    }

    // ════════════════════════════════════════════
    //  Slot selection (WFC core)
    // ════════════════════════════════════════════

    /// <summary>
    /// Find the open slot most appropriate for the given shape.
    /// Returns true and the chosen slot on success.
    ///
    /// Scoring: must be compatible with all placed neighbors; among compatible slots,
    /// prefer slots closer to the viewport center (so the mosaic grows visibly), and
    /// among ties prefer slots with more constraints already met (more neighbors).
    /// </summary>
    private bool TryFindBestSlot(WangTileShape shape, out Vector2Int best)
    {
        best = Vector2Int.zero;
        bool found = false;
        float bestScore = float.PositiveInfinity;

        foreach (Vector2Int slot in _openSlots)
        {
            // Defensive guard: open slots are already band-filtered in PlaceAt,
            // but skip anything that drifted out (e.g. if the seed produced one).
            if (!IsSlotInBand(slot)) continue;
            if (!IsCompatible(slot, shape)) continue;

            // Cheaper-is-better score: distance to viewport center − neighbor-count bonus.
            // Subtracting neighbor count breaks distance ties in favor of high-constraint slots
            // (tiles in pockets), which tightens the mosaic.
            float dx = slot.x - ViewportCenter.x;
            float dy = slot.y - ViewportCenter.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            int neighborCount = CountPlacedNeighbors(slot);
            float score = dist - 0.5f * neighborCount;

            if (score < bestScore)
            {
                bestScore = score;
                best = slot;
                found = true;
            }
        }
        return found;
    }

    /// <summary>
    /// True iff a tile of the given shape, placed at slot, agrees with every existing
    /// neighbor across the shared edge.
    /// </summary>
    private bool IsCompatible(Vector2Int slot, WangTileShape shape)
    {
        for (int e = 0; e < 4; e++)
        {
            WangEdge edge = (WangEdge)e;
            Vector2Int neighborPos = slot + WangTiles.Step(edge);
            if (!_placed.TryGetValue(neighborPos, out PlacedTile neighbor)) continue;

            bool mine = WangTiles.HasEndpoint(shape, edge);
            bool theirs = WangTiles.HasEndpoint(neighbor.drawing.shape, WangTiles.Opposite(edge));
            if (mine != theirs) return false;
        }
        return true;
    }

    private int CountPlacedNeighbors(Vector2Int slot)
    {
        int n = 0;
        for (int e = 0; e < 4; e++)
        {
            Vector2Int p = slot + WangTiles.Step((WangEdge)e);
            if (_placed.ContainsKey(p)) n++;
        }
        return n;
    }

    // ════════════════════════════════════════════
    //  Placement bookkeeping
    // ════════════════════════════════════════════

    private void PlaceAt(Vector2Int pos, WangTileDrawing drawing)
    {
        var tile = new PlacedTile { gridPos = pos, drawing = drawing };
        _placed[pos] = tile;
        _openSlots.Remove(pos);

        for (int e = 0; e < 4; e++)
        {
            Vector2Int neighbor = pos + WangTiles.Step((WangEdge)e);
            if (_placed.ContainsKey(neighbor)) continue;
            // Only open slots inside the perpendicular band. With PanAxis.Horizontal
            // this keeps growth strictly along ±X within the viewport's row strip.
            if (!IsSlotInBand(neighbor)) continue;
            _openSlots.Add(neighbor);
        }

        TilePlaced?.Invoke(tile);

        UpdateViewportPan(pos);
        CullOffscreen();
    }

    // ════════════════════════════════════════════
    //  Viewport pan + cull
    // ════════════════════════════════════════════

    /// <summary>
    /// After placing a tile, slide the viewport center toward it if the placement
    /// landed near (or past) a viewport edge. The viewport never lingers on empty
    /// space — it always chases the latest growth.
    /// </summary>
    private void UpdateViewportPan(Vector2Int placedAt)
    {
        float halfW = _viewportWidth * 0.5f;
        float halfH = _viewportHeight * 0.5f;
        // Trigger when the new tile is within 1 unit of (or beyond) the viewport edge.
        float triggerInsetX = halfW - 1f;
        float triggerInsetY = halfH - 1f;

        float dx = placedAt.x - ViewportCenter.x;
        float dy = placedAt.y - ViewportCenter.y;
        bool changed = false;

        if (_panAxis != PanAxis.Vertical && Mathf.Abs(dx) > triggerInsetX)
        {
            float targetX = placedAt.x + (dx > 0 ? -triggerInsetX : triggerInsetX);
            ViewportCenter = new Vector2(targetX, ViewportCenter.y);
            changed = true;
        }
        if (_panAxis != PanAxis.Horizontal && Mathf.Abs(dy) > triggerInsetY)
        {
            float targetY = placedAt.y + (dy > 0 ? -triggerInsetY : triggerInsetY);
            ViewportCenter = new Vector2(ViewportCenter.x, targetY);
            changed = true;
        }

        if (changed)
            ViewportCenterChanged?.Invoke(ViewportCenter);
    }

    /// <summary>
    /// Destroy and forget any placed tile whose grid position is outside the viewport
    /// rectangle (plus cull margin). Per design, scrolled-off tiles are truly gone.
    /// Open slots in the same region are also pruned so they don't keep the mosaic
    /// "remembering" the part that has scrolled away.
    /// </summary>
    private void CullOffscreen()
    {
        float halfW = _viewportWidth * 0.5f + _cullMargin;
        float halfH = _viewportHeight * 0.5f + _cullMargin;

        var toCull = new List<Vector2Int>();
        foreach (var kv in _placed)
        {
            float dx = kv.Key.x - ViewportCenter.x;
            float dy = kv.Key.y - ViewportCenter.y;
            if (Mathf.Abs(dx) > halfW || Mathf.Abs(dy) > halfH)
                toCull.Add(kv.Key);
        }

        foreach (Vector2Int pos in toCull)
        {
            _placed.Remove(pos);
            TileCulled?.Invoke(pos);
        }

        // Prune open slots that have drifted outside the viewport+margin AND have no
        // remaining placed neighbors. (A slot adjacent to a still-visible tile should
        // remain open even if it sits just outside the cull rect.)
        var slotsToPrune = new List<Vector2Int>();
        foreach (Vector2Int slot in _openSlots)
        {
            float dx = slot.x - ViewportCenter.x;
            float dy = slot.y - ViewportCenter.y;
            if (Mathf.Abs(dx) > halfW || Mathf.Abs(dy) > halfH)
            {
                if (CountPlacedNeighbors(slot) == 0)
                    slotsToPrune.Add(slot);
            }
        }
        foreach (Vector2Int s in slotsToPrune) _openSlots.Remove(s);
    }

    /// <summary>Clear all state — used when the session ends or restarts.</summary>
    public void Clear()
    {
        _placed.Clear();
        _openSlots.Clear();
        _inventory.Clear();
        ViewportCenter = Vector2.zero;
    }

    /// <summary>
    /// Read-only snapshot of placed tiles. Useful for the renderer or tests.
    /// </summary>
    public IReadOnlyDictionary<Vector2Int, PlacedTile> Placed => _placed;
}
