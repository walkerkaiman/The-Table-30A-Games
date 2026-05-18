using UnityEngine;

/// <summary>
/// The exit door that players must reach after all puzzles are solved.
///
/// Starts locked: the trigger collider is disabled and a locked visual is shown.
/// <see cref="TreasureHunterManager"/> calls <see cref="Unlock"/> when the final puzzle is solved.
/// Once unlocked, any non-downed explorer that enters the trigger is marked as escaped.
///
/// Prefab setup:
///   • Add a <c>Collider2D</c> with <c>isTrigger = true</c>.
///   • Assign <see cref="lockedVisual"/> and <see cref="unlockedVisual"/> child GOs (swap on unlock).
///   • Optionally assign <see cref="unlockVfx"/> for a particle burst.
///   • Optionally assign <see cref="escapedVfx"/> to play per-explorer when they pass through.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ExitDoor : MonoBehaviour
{
    [Header("State")]
    [Tooltip("Whether the door can be passed through. Set to false at session start.")]
    public bool isUnlocked = false;

    [Header("Visuals")]
    [Tooltip("Root shown while the door is locked.")]
    [SerializeField] private GameObject lockedVisual;
    [Tooltip("Root shown once unlocked.")]
    [SerializeField] private GameObject unlockedVisual;
    [Tooltip("VFX activated when the door unlocks.")]
    [SerializeField] private GameObject unlockVfx;
    [Tooltip("VFX activated each time an explorer escapes (instantiated at explorer position).")]
    [SerializeField] private GameObject escapedVfx;

    private Collider2D _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        ApplyLockState();
    }

    // ── Public API ────────────────────────────────

    /// <summary>Called by TreasureHunterManager when all puzzles are solved.</summary>
    public void Unlock()
    {
        if (isUnlocked) return;
        isUnlocked = true;
        ApplyLockState();

        if (unlockVfx != null) unlockVfx.SetActive(true);
        GameLog.Game("Exit door unlocked!");
    }

    // ── Trigger ───────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isUnlocked) return;
        var explorer = other.GetComponent<Explorer>() ?? other.GetComponentInParent<Explorer>();
        if (explorer == null || explorer.IsDown || explorer.IsEscaped) return;

        explorer.MarkEscaped();

        if (escapedVfx != null)
        {
            var vfx = Instantiate(escapedVfx, explorer.transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        // Notify escaped player.
        var msg = new TreasureHunterEventMessage
        {
            eventName = "escaped",
            playerId = explorer.PlayerId,
        };
        GameEvents.FireSendToPlayer(explorer.PlayerId, JsonUtility.ToJson(msg));

        TreasureHunterManager.Instance?.OnExplorerEscaped(explorer);
        GameLog.Game($"{explorer.PlayerName} escaped!");
    }

    // ── Helpers ───────────────────────────────────

    private void ApplyLockState()
    {
        if (_collider != null) _collider.enabled = isUnlocked;
        if (lockedVisual != null) lockedVisual.SetActive(!isUnlocked);
        if (unlockedVisual != null) unlockedVisual.SetActive(isUnlocked);
    }
}
