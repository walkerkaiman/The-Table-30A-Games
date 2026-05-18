using UnityEngine;

/// <summary>
/// Fog-of-war overlay driven entirely by <see cref="ExplorerTorch"/> components in the scene.
///
/// The overlay is a single flat quad covering the whole map. Every LateUpdate we push the
/// position + radius of every lit torch into the <c>TreasureHunter/FogOfWar</c> shader, which
/// renders transparent inside each torch and fully opaque elsewhere. There is no persistent
/// "seen" memory — lit areas go dark again the moment the torch moves away, which matches the
/// "each player carries a point light" feel.
///
/// Scene contract:
///   • Place this component on a GameObject that has <c>MeshFilter</c> + <c>MeshRenderer</c>
///     and Unity's built-in Quad mesh. (The quad is sized by <see cref="Setup"/>; a mesh is
///     auto-assigned if missing.)
///   • Leave <c>fogMaterial</c> empty for auto-creation, or assign your own to tweak uniforms.
///   • <see cref="TreasureHunterManager"/> calls <see cref="Setup"/> after painting.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class FogOfWar : MonoBehaviour
{
    [Header("Appearance")]
    [Tooltip("Fog colour. Alpha=1 means fully opaque outside torches (recommended so items " +
             "don't leak through). Drop alpha below 1 only if you want a translucent fog.")]
    public Color fogColor = new Color(0f, 0f, 0f, 1f);

    [Tooltip("Fallback width of the soft edge (used when a torch's own softEdge is zero). " +
             "Smaller = harder edge, larger = smoother radial falloff.")]
    [Range(0.05f, 3f)]
    public float softEdgeWidth = 0.75f;

    [Header("Shader")]
    [Tooltip("The FogOfWar material (uses TreasureHunter/FogOfWar shader). Created at runtime if null.")]
    [SerializeField] private Material fogMaterial;

    private static readonly int PropTorches = Shader.PropertyToID("_Torches");
    private static readonly int PropFogColor = Shader.PropertyToID("_FogColor");
    private static readonly int PropSoftEdge = Shader.PropertyToID("_SoftEdge");

    private const int MaxTorches = 8;
    private readonly Vector4[] _torches = new Vector4[MaxTorches];
    private MeshRenderer _renderer;
    private bool _isSetup;

    // ── Unity lifecycle ──────────────────────────

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        EnsureMaterial();
        EnsureQuadMesh();
        // Ensure the fog always draws on top of every tilemap and interactable sprite.
        _renderer.sortingOrder = 1000;
    }

    private void LateUpdate()
    {
        if (!_isSetup || fogMaterial == null) return;
        UpdateTorchData();
        fogMaterial.SetVectorArray(PropTorches, _torches);
        fogMaterial.SetColor(PropFogColor, fogColor);
        fogMaterial.SetFloat(PropSoftEdge, softEdgeWidth);
    }

    // ── Public API ────────────────────────────────

    /// <summary>
    /// Called after the dungeon is painted. Sizes and positions the quad so it fully
    /// covers the map and sits just above the tilemaps / below the camera.
    /// </summary>
    public void Setup(DungeonLayout layout, float tileSize)
    {
        float w = layout.Width * tileSize;
        float h = layout.Height * tileSize;

        // Unity's built-in Quad is 1x1 centred at origin, so scaling by (w,h) makes the mesh
        // span the full map. Z = -0.5 keeps it in front of tiles and interactables (both at
        // Z=0) so the fog properly occludes them.
        transform.position = new Vector3(w * 0.5f, h * 0.5f, -0.5f);
        transform.localScale = new Vector3(w, h, 1f);

        _isSetup = true;
    }

    /// <summary>No-op kept for backwards compatibility with earlier call sites.</summary>
    public void ResetFog() { /* torch-only fog has no persistent state */ }

    // ── Internal ──────────────────────────────────

    private void UpdateTorchData()
    {
        for (int i = 0; i < MaxTorches; i++) _torches[i] = Vector4.zero;

        var torches = ExplorerTorch.ActiveTorches;
        if (torches == null || torches.Count == 0) return;

        int idx = 0;
        for (int i = 0; i < torches.Count && idx < MaxTorches; i++)
        {
            var t = torches[i];
            if (t == null || !t.isLit) continue;
            var p = t.transform.position;
            _torches[idx] = new Vector4(p.x, p.y, Mathf.Max(0.1f, t.radius), 1f);
            idx++;
        }
    }

    private void EnsureMaterial()
    {
        if (fogMaterial != null)
        {
            _renderer.sharedMaterial = fogMaterial;
            return;
        }

        var shader = Shader.Find("TreasureHunter/FogOfWar");
        if (shader == null)
        {
            GameLog.Warn("FogOfWar: Could not find TreasureHunter/FogOfWar shader. Fog disabled.");
            _renderer.enabled = false;
            return;
        }
        fogMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        _renderer.sharedMaterial = fogMaterial;
    }

    /// <summary>
    /// If the GameObject has no mesh assigned, attach Unity's built-in Quad so the overlay
    /// actually renders.
    /// </summary>
    private void EnsureQuadMesh()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null) return;
        if (mf.sharedMesh != null) return;

        var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        mf.sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(tmp);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (softEdgeWidth < 0.05f) softEdgeWidth = 0.05f;
        if (fogColor.a < 0.1f) fogColor.a = 0.1f; // Keep the fog actually visible.
    }
#endif
}
