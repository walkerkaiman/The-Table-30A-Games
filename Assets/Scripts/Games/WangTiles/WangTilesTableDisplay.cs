using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders the Wang Tiles mosaic on the shared table display. Each placed tile is
/// shown as a <see cref="SpriteRenderer"/> built at runtime from the player's
/// strokes by <see cref="WangTileRenderer"/>. The camera pans smoothly toward the
/// mosaic's evolving "viewport center"; tiles that scroll offscreen are destroyed.
///
/// Wires into <see cref="WangTilesManager"/> via the static OnMosaicReady event so
/// it can pick up the mosaic instance without a scene reference.
/// </summary>
public class WangTilesTableDisplay : GameTableDisplay
{
    [Header("Mosaic Rendering")]
    [Tooltip("World units per tile (square). Determines on-screen tile size.")]
    [SerializeField] private float tileWorldSize = 1f;

    [Tooltip("Resolution of each tile's rendered texture (square).")]
    [SerializeField] private int tileTextureSize = 256;

    [Tooltip("Sprites-per-unit for the runtime sprite (sized so 1 tile = tileWorldSize).")]
    [SerializeField] private float pixelsPerUnit = 256f;

    [Tooltip("Background tint applied to each tile's underlay (set alpha=0 for invisible).")]
    [SerializeField] private Color tileBackground = new Color(0f, 0f, 0f, 0f);

    [Header("Camera Pan")]
    [Tooltip("How fast the camera chases the mosaic's viewport center (units/sec).")]
    [SerializeField] private float panSpeed = 4f;

    [Tooltip("Camera reference. If unset, uses Camera.main on Start.")]
    [SerializeField] private Camera cam;

    [Header("Idle Intro")]
    [Tooltip("Optional TMP shown while the mosaic is empty (no tiles placed yet).")]
    [SerializeField] private TMPro.TextMeshPro idleHintText;

    private WangTileMosaic _mosaic;
    private readonly Dictionary<Vector2Int, GameObject> _tileObjects = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<Vector2Int, Texture2D> _tileTextures = new Dictionary<Vector2Int, Texture2D>();
    private readonly Dictionary<Vector2Int, Sprite> _tileSprites = new Dictionary<Vector2Int, Sprite>();

    private Vector3 _camTargetPos;
    private MeshRenderer _idleRenderer;

    /// <summary>
    /// Manager fires this when it has constructed the mosaic so any TableDisplay in
    /// the scene can subscribe to its events. Decouples scene-wiring order.
    /// </summary>
    public static event System.Action<WangTileMosaic> OnMosaicReady;

    protected override void Awake()
    {
        base.Awake();
        _idleRenderer = CacheRenderer(idleHintText);
        SetRendererEnabled(_idleRenderer, false);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        OnMosaicReady += AttachMosaic;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        OnMosaicReady -= AttachMosaic;
        DetachMosaic();
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            // Frame the orthographic camera to the configured viewport (assuming 16:9).
            // Use the mosaic dimensions if attached; otherwise reasonable defaults.
            int viewH = _mosaic?.ViewportHeight ?? 7;
            cam.orthographic = true;
            cam.orthographicSize = viewH * tileWorldSize * 0.5f;
            _camTargetPos = new Vector3(0f, 0f, cam.transform.position.z);
            cam.transform.position = _camTargetPos;
        }
    }

    private void Update()
    {
        if (cam == null) return;
        // Smooth chase of the mosaic's viewport center.
        Vector3 cur = cam.transform.position;
        Vector3 step = Vector3.MoveTowards(
            new Vector3(cur.x, cur.y, _camTargetPos.z),
            _camTargetPos,
            panSpeed * Time.deltaTime);
        cam.transform.position = step;
    }

    public static void NotifyMosaicReady(WangTileMosaic mosaic) => OnMosaicReady?.Invoke(mosaic);

    private void AttachMosaic(WangTileMosaic mosaic)
    {
        DetachMosaic();
        _mosaic = mosaic;
        if (_mosaic == null) return;

        _mosaic.TilePlaced += OnTilePlaced;
        _mosaic.TileCulled += OnTileCulled;
        _mosaic.ViewportCenterChanged += OnViewportCenterChanged;

        UpdateIdleHint();

        if (cam != null)
        {
            cam.orthographicSize = _mosaic.ViewportHeight * tileWorldSize * 0.5f;
        }
    }

    private void DetachMosaic()
    {
        if (_mosaic != null)
        {
            _mosaic.TilePlaced -= OnTilePlaced;
            _mosaic.TileCulled -= OnTileCulled;
            _mosaic.ViewportCenterChanged -= OnViewportCenterChanged;
        }
        ClearAllTiles();
        _mosaic = null;
    }

    private void ClearAllTiles()
    {
        foreach (var kv in _tileObjects) Destroy(kv.Value);
        _tileObjects.Clear();
        foreach (var kv in _tileTextures) Destroy(kv.Value);
        _tileTextures.Clear();
        foreach (var kv in _tileSprites) Destroy(kv.Value);
        _tileSprites.Clear();
    }

    // ════════════════════════════════════════════
    //  Mosaic event handlers
    // ════════════════════════════════════════════

    private void OnTilePlaced(WangTileMosaic.PlacedTile tile)
    {
        // Build texture + sprite for this drawing
        Texture2D tex = WangTileRenderer.AllocateTexture(tileTextureSize);

        // Optional background fill (default fully transparent so adjacent tiles blend cleanly)
        if (tileBackground.a > 0f)
        {
            var px = new Color[tileTextureSize * tileTextureSize];
            for (int i = 0; i < px.Length; i++) px[i] = tileBackground;
            tex.SetPixels(px);
        }

        Color fallback = new Color(0.39f, 1f, 0.85f, 1f); // matches the #64ffda accent
        WangTileRenderer.Render(tex, tile.drawing, fallback);
        tex.Apply();

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tileTextureSize, tileTextureSize),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit
        );

        var go = new GameObject($"WangTile {tile.gridPos.x},{tile.gridPos.y}");
        go.transform.SetParent(transform, false);
        go.transform.position = GridToWorld(tile.gridPos);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        // Tile sprite scale derives from pixelsPerUnit + texture size; assert a perfect square.
        float intendedScale = tileWorldSize * pixelsPerUnit / tileTextureSize;
        go.transform.localScale = new Vector3(intendedScale, intendedScale, 1f);

        _tileObjects[tile.gridPos] = go;
        _tileTextures[tile.gridPos] = tex;
        _tileSprites[tile.gridPos] = sprite;

        UpdateIdleHint();
    }

    private void OnTileCulled(Vector2Int pos)
    {
        if (_tileObjects.TryGetValue(pos, out GameObject go))
        {
            Destroy(go);
            _tileObjects.Remove(pos);
        }
        if (_tileTextures.TryGetValue(pos, out Texture2D t))
        {
            Destroy(t);
            _tileTextures.Remove(pos);
        }
        if (_tileSprites.TryGetValue(pos, out Sprite s))
        {
            Destroy(s);
            _tileSprites.Remove(pos);
        }
    }

    private void OnViewportCenterChanged(Vector2 newCenter)
    {
        Vector3 world = new Vector3(newCenter.x * tileWorldSize, newCenter.y * tileWorldSize, _camTargetPos.z);
        _camTargetPos = world;
    }

    private Vector3 GridToWorld(Vector2Int grid)
    {
        return new Vector3(grid.x * tileWorldSize, grid.y * tileWorldSize, 0f);
    }

    private void UpdateIdleHint()
    {
        if (_idleRenderer == null) return;
        bool empty = _mosaic != null && _mosaic.PlacedCount == 0;
        SetRendererEnabled(_idleRenderer, empty);
    }

    protected override void OnPhaseChanged(string gameType, string phase, int timer)
    {
        if (gameType != "wangtiles") return;
        // Hide phase + timer once Painting begins — the mosaic itself is the display.
        bool showHud = phase != "Painting";
        SetBaseTextVisible(showHud);
    }
}
