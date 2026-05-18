using UnityEngine;

/// <summary>
/// Per-player explorer pawn. Wires the owning <see cref="PlayerJoystickNode"/> into the
/// <see cref="AcceleratedMover"/> for locomotion, and tracks game state (gold, downed, escaped).
///
/// Attach to the <c>ExplorerPrefab</c> alongside:
///   • <see cref="PlayerJoystickNode"/> (added by JoystickInputRelay)
///   • <see cref="AcceleratedMover"/> (XY plane, Rigidbody2D backend)
///   • <see cref="Rigidbody2D"/> (kinematic or dynamic depending on collision need)
///   • <see cref="DownedController"/> (handles the knockout/revive cycle)
///   • A child SpriteRenderer for the body visual
///   • A child SpriteRenderer (initially disabled) for the downed visual overlay
/// </summary>
[RequireComponent(typeof(PlayerJoystickNode))]
[RequireComponent(typeof(AcceleratedMover))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(DownedController))]
public class Explorer : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("Main body SpriteRenderer. Tinted with PlayerColor on spawn.")]
    [SerializeField] private SpriteRenderer bodySprite;
    [Tooltip("Additional indicator (name tag or arrow) to tint with PlayerColor.")]
    [SerializeField] private SpriteRenderer colorIndicator;

    [Header("Gameplay")]
    [Tooltip("Vision radius fed to FogOfWar (in world units). Mirrored onto the auto-attached " +
             "ExplorerTorch so you only have to tune one value.")]
    public float visionRadius = 4f;

    private ExplorerTorch _torch;

    // ── Runtime state ────────────────────────────
    public string PlayerId => _node != null ? _node.PlayerId : string.Empty;
    public string PlayerName => _node != null ? _node.PlayerName : string.Empty;
    public bool IsDown { get; set; }
    public bool IsEscaped { get; set; }
    public int GoldCarried { get; set; }
    public int TrapsTripped { get; set; }

    private PlayerJoystickNode _node;
    private AcceleratedMover _mover;
    private DownedController _downedController;

    // ── Unity lifecycle ──────────────────────────

    private void Awake()
    {
        _node = GetComponent<PlayerJoystickNode>();
        _mover = GetComponent<AcceleratedMover>();
        _downedController = GetComponent<DownedController>();
        _mover.SetSource(_node);

        // Every explorer carries its own "point light" via ExplorerTorch. Auto-add one if
        // the prefab wasn't built with it yet, so the fog system works out-of-the-box.
        _torch = GetComponent<ExplorerTorch>();
        if (_torch == null) _torch = gameObject.AddComponent<ExplorerTorch>();
        _torch.radius = visionRadius;
        _torch.isLit = true;
    }

    private void Update()
    {
        if (_torch != null && !Mathf.Approximately(_torch.radius, visionRadius))
            _torch.radius = visionRadius;
    }

    private void OnEnable()
    {
        if (ExplorerRegistry.Instance != null)
            ExplorerRegistry.Instance.Register(this);

        ApplyPlayerColor();
    }

    private void OnDisable()
    {
        if (ExplorerRegistry.Instance != null)
            ExplorerRegistry.Instance.Unregister(this);
    }

    // ── Public API ────────────────────────────────

    /// <summary>Knock this explorer down: disable movement and hand off to DownedController.</summary>
    public void KnockDown()
    {
        if (IsDown || IsEscaped) return;
        IsDown = true;
        TrapsTripped++;
        _mover.enabled = false;
        if (_torch != null) _torch.isLit = false;
        _downedController.OnKnockedDown();
        TreasureHunterManager.Instance?.OnExplorerKnockedDown();
        GameLog.Game($"Explorer {PlayerName} knocked down");
    }

    /// <summary>Called by DownedController when the revive hold completes.</summary>
    public void Revive()
    {
        if (!IsDown) return;
        IsDown = false;
        _mover.enabled = true;
        if (_torch != null) _torch.isLit = true;
        _downedController.OnRevived();
        GameLog.Game($"Explorer {PlayerName} revived");
    }

    /// <summary>Mark this explorer as having escaped through the exit door.</summary>
    public void MarkEscaped()
    {
        if (IsEscaped) return;
        IsEscaped = true;
        IsDown = false;
        _mover.enabled = false;
        if (_torch != null) _torch.isLit = false;
        GameLog.Game($"Explorer {PlayerName} escaped!");
    }

    // ── Helpers ───────────────────────────────────

    private void ApplyPlayerColor()
    {
        if (_node == null) return;
        Color c = _node.PlayerColor;
        if (bodySprite != null) bodySprite.color = c;
        if (colorIndicator != null) colorIndicator.color = c;
    }
}
