using UnityEngine;

/// <summary>
/// A dungeon trap that knocks down the first non-downed <see cref="Explorer"/> that enters it.
///
/// Prefab setup:
///   • Add a <c>Collider2D</c> with <c>isTrigger = true</c>.
///   • Add a <c>SpriteRenderer</c> for the visual (armed / tripped variants controlled below).
///   • Keep the prefab self-contained; <see cref="DungeonPainter"/> places it at runtime.
///
/// The trap can optionally reset after a cooldown (for reusable trap tiles).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Trap : MonoBehaviour
{
    [Header("Behaviour")]
    [Tooltip("Whether this trap is currently able to trigger.")]
    public bool armed = true;

    [Tooltip("Seconds before the trap re-arms after triggering. Set to 0 to disable reset (one-shot).")]
    public float resetAfterSeconds = 0f;

    [Tooltip("If true, a revealed marker is shown on the table after the trap is tripped (other players see it).")]
    public bool revealOnTrip = true;

    [Header("Visuals")]
    [Tooltip("Sprite shown when the trap is armed and hidden.")]
    [SerializeField] private SpriteRenderer armedSprite;

    [Tooltip("Sprite shown once the trap has been tripped (revealed state).")]
    [SerializeField] private SpriteRenderer trippedSprite;

    [Tooltip("VFX GameObject activated briefly on trigger (particles, etc.). Set inactive in prefab.")]
    [SerializeField] private GameObject triggerVfx;

    private float _resetTimer;

    private void Start()
    {
        UpdateVisuals();
    }

    private void Update()
    {
        if (armed || resetAfterSeconds <= 0f) return;
        _resetTimer -= Time.deltaTime;
        if (_resetTimer <= 0f)
        {
            armed = true;
            UpdateVisuals();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!armed) return;
        var explorer = other.GetComponent<Explorer>() ?? other.GetComponentInParent<Explorer>();
        if (explorer == null || explorer.IsDown || explorer.IsEscaped) return;

        Trigger(explorer);
    }

    private void Trigger(Explorer explorer)
    {
        armed = false;
        _resetTimer = resetAfterSeconds;

        explorer.KnockDown();

        if (triggerVfx != null) triggerVfx.SetActive(true);

        // Notify the tripped player's phone via a targeted event.
        var msg = new TreasureHunterEventMessage
        {
            eventName = "trap_tripped",
            playerId = explorer.PlayerId,
        };
        GameEvents.FireSendToPlayer(explorer.PlayerId, JsonUtility.ToJson(msg));

        UpdateVisuals();
        GameLog.Game($"Trap triggered by {explorer.PlayerName} at {transform.position}");
    }

    private void UpdateVisuals()
    {
        if (armedSprite != null) armedSprite.enabled = armed;
        if (trippedSprite != null) trippedSprite.enabled = !armed && revealOnTrip;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (resetAfterSeconds < 0f) resetAfterSeconds = 0f;
    }
#endif
}
