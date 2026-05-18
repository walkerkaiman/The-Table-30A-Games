using UnityEngine;

/// <summary>
/// Manages the "knockout → revive" loop on an <see cref="Explorer"/>.
///
/// When knocked down:
///   • Shows the downed visual overlay.
///   • Each FixedUpdate scans <see cref="ExplorerRegistry"/> for a nearby, standing, still-held
///     teammate. Accumulates <c>reviveProgress</c> while the condition holds; resets when it fails.
///   • On completion calls <see cref="Explorer.Revive"/> and broadcasts a <c>revived</c> event
///     to the downed player's phone and to the reviver's phone.
///
/// Visual authoring:
///   • Assign <see cref="downedVisualRoot"/> to a child GO that is toggled on/off.
///   • Assign <see cref="reviveProgressBar"/> (optional SpriteRenderer whose transform.localScale.x
///     is driven to 0..1) so players can see revive progress.
/// </summary>
[RequireComponent(typeof(Explorer))]
public class DownedController : MonoBehaviour
{
    [Header("Revive Settings")]
    [Tooltip("Seconds a teammate must hold still next to the downed explorer to revive them.")]
    public float reviveHoldSeconds = 3f;

    [Tooltip("World-unit radius a teammate must be within to start the revive.")]
    public float reviveRadius = 1.5f;

    [Tooltip("Stick magnitude below which a teammate is considered 'holding still'.")]
    [Range(0f, 0.5f)]
    public float stillThreshold = 0.1f;

    [Header("Visuals")]
    [Tooltip("Root GameObject toggled on when the explorer is downed (skull icon, tint, etc.).")]
    [SerializeField] private GameObject downedVisualRoot;

    [Tooltip("Optional SpriteRenderer whose localScale.x (0..1) shows revive progress.")]
    [SerializeField] private SpriteRenderer reviveProgressBar;

    public float ReviveProgress { get; private set; }

    private Explorer _explorer;
    private string _reviverId;

    private void Awake()
    {
        _explorer = GetComponent<Explorer>();
    }

    private void FixedUpdate()
    {
        if (!_explorer.IsDown) return;

        float sqRadius = reviveRadius * reviveRadius;
        string closestReviverId = null;
        float closestSqDist = sqRadius + 1f;

        var all = ExplorerRegistry.Instance?.AllExplorers;
        if (all == null) return;

        foreach (var other in all)
        {
            if (other == null || other == _explorer || other.IsDown || other.IsEscaped) continue;

            float sqDist = ((Vector2)(other.transform.position - transform.position)).sqrMagnitude;
            if (sqDist > sqRadius) continue;

            // Check whether their stick is still (they are holding still).
            var node = other.GetComponent<PlayerJoystickNode>();
            if (node == null) continue;
            if (node.Stick.sqrMagnitude > stillThreshold * stillThreshold) continue;

            if (sqDist < closestSqDist) { closestSqDist = sqDist; closestReviverId = other.PlayerId; }
        }

        if (closestReviverId == null)
        {
            // No valid reviver — reset progress.
            if (ReviveProgress > 0f)
            {
                ReviveProgress = 0f;
                _reviverId = null;
                UpdateProgressBar();
            }
            return;
        }

        // If the reviver changed, reset progress.
        if (_reviverId != closestReviverId)
        {
            ReviveProgress = 0f;
            _reviverId = closestReviverId;
        }

        ReviveProgress += Time.fixedDeltaTime / reviveHoldSeconds;
        UpdateProgressBar();

        if (ReviveProgress >= 1f)
        {
            ReviveProgress = 1f;
            string reviverIdCopy = _reviverId;
            _reviverId = null;
            CompleteRevive(reviverIdCopy);
        }
    }

    // ── Called by Explorer ────────────────────────

    public void OnKnockedDown()
    {
        ReviveProgress = 0f;
        _reviverId = null;
        if (downedVisualRoot != null) downedVisualRoot.SetActive(true);
        UpdateProgressBar();
    }

    public void OnRevived()
    {
        ReviveProgress = 0f;
        _reviverId = null;
        if (downedVisualRoot != null) downedVisualRoot.SetActive(false);
        UpdateProgressBar();
    }

    // ── Internal ──────────────────────────────────

    private void CompleteRevive(string reviverId)
    {
        _explorer.Revive();

        // Broadcast th_event "revived" to both phones.
        var msg = new TreasureHunterEventMessage
        {
            eventName = "revived",
            playerId = _explorer.PlayerId,
        };
        string json = UnityEngine.JsonUtility.ToJson(msg);

        // Send targeted message to the downed player and the reviver.
        GameEvents.FireSendToPlayer(_explorer.PlayerId, json);
        if (!string.IsNullOrEmpty(reviverId))
            GameEvents.FireSendToPlayer(reviverId, json);
    }

    private void UpdateProgressBar()
    {
        if (reviveProgressBar == null) return;
        var s = reviveProgressBar.transform.localScale;
        s.x = ReviveProgress;
        reviveProgressBar.transform.localScale = s;
    }
}
