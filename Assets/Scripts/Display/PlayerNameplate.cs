using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable "name tag" component that writes a player's display name into a Text /
/// TMP_Text component. Drop it on any player-owned GameObject (a shepherd, a pong
/// paddle, a racer, etc.) and it finds an <see cref="IPlayerIdentity"/> on itself or
/// a parent and applies the name on spawn.
///
/// The label's position, font, and all other visual styling is handled by your chosen
/// Text component — this script only owns the string (and optionally the color). That
/// keeps the component game-agnostic: a 2D game can parent a TMP_Text to the player
/// GameObject, a 3D game can use a world-space canvas, and a UI overlay can bind the
/// label to a dedicated HUD slot.
///
/// Usage:
///   1. Add a TextMeshProUGUI (or TextMeshPro world-space, or legacy UI.Text) somewhere
///      under your player prefab — author its size / font / outline / billboard etc. in
///      the Inspector or via a child Canvas.
///   2. Add PlayerNameplate to the player prefab root. The Text target is auto-found
///      in children if left blank; the identity source is auto-found on self-or-parent.
///   3. Done — when the relay calls <c>Init(...)</c> on the identity node, the label
///      updates on the next frame.
///
/// The script re-checks the identity's name each LateUpdate but does nothing unless it
/// changed, so the cost is one string compare per player per frame.
/// </summary>
[DisallowMultipleComponent]
public class PlayerNameplate : MonoBehaviour
{
    [Header("Identity source")]
    [Tooltip("Component that supplies the player identity. Leave empty to auto-find an " +
             "IPlayerIdentity on this GameObject or any parent.")]
    [SerializeField] private MonoBehaviour identitySource;

    [Header("Text targets (assign one, or let auto-find pick a child)")]
    [Tooltip("Primary TextMeshPro target. Supports both UGUI (Canvas) and 3D world-space " +
             "TMP. If null, the first TMP_Text found under this GameObject is used.")]
    [SerializeField] private TMP_Text tmpTarget;

    [Tooltip("Fallback legacy UGUI Text target for prefabs not using TextMeshPro.")]
    [SerializeField] private Text uiTextTarget;

    [Header("Display options")]
    [Tooltip("Tint the text with the player's palette color (see PlayerInputNode.Palette).")]
    [SerializeField] private bool tintWithPlayerColor = true;

    [Tooltip("String format applied to the name. Use {0} as a placeholder (e.g. \"★ {0}\" " +
             "or \"P{0}\"). Leave blank to show the name as-is.")]
    [SerializeField] private string format = "{0}";

    [Tooltip("Hide the text component(s) until the identity has a non-empty name. Avoids " +
             "flashing placeholder text on the first frame before Init() runs.")]
    [SerializeField] private bool hideUntilReady = true;

    [Header("Table orientation")]
    [Tooltip("When the owning player sits on the far side (TableSide == 1), rotate the label " +
             "180° around Z so it reads right-side up from their seat. Leave on for any top-down " +
             "table game where the two sides face each other.")]
    [SerializeField] private bool flipForFarTableSide = true;

    private IPlayerIdentity _identity;
    private string _lastAppliedName;
    private int _lastAppliedSide = -1;

    private void Awake()
    {
        ResolveIdentity();
        ResolveTargets();

        if (hideUntilReady && (_identity == null || string.IsNullOrEmpty(_identity.PlayerName)))
        {
            SetTargetsActive(false);
        }
    }

    /// <summary>Assign or override the identity source at runtime.</summary>
    public void SetIdentity(IPlayerIdentity identity)
    {
        _identity = identity;
        _lastAppliedName = null;
        ApplyIfReady();
    }

    private void LateUpdate()
    {
        // LateUpdate gives the owning relay / Init() call a chance to run first on spawn.
        // The early-out in ApplyIfReady makes the per-frame cost negligible once the name is set.
        ApplyIfReady();
    }

    // ── Internals ────────────────────────────────

    private void ApplyIfReady()
    {
        // Late-binding: PlayerInputRelay only calls AddComponent<PlayerInputNode>() AFTER Instantiate
        // when the prefab didn't bake one in. That means our Awake can run before any IPlayerIdentity
        // exists. Keep looking until we find one so the text isn't left permanently hidden.
        if (_identity == null) ResolveIdentity();
        if (_identity == null) return;

        string playerName = _identity.PlayerName;
        if (string.IsNullOrEmpty(playerName)) return;

        bool nameChanged = playerName != _lastAppliedName;
        bool sideChanged = flipForFarTableSide && _identity.TableSide != _lastAppliedSide;
        if (!nameChanged && !sideChanged) return;

        if (nameChanged)
        {
            string display = string.IsNullOrEmpty(format) ? playerName : string.Format(format, playerName);

            if (tmpTarget != null)
            {
                tmpTarget.text = display;
                if (tintWithPlayerColor) tmpTarget.color = _identity.PlayerColor;
            }
            if (uiTextTarget != null)
            {
                uiTextTarget.text = display;
                if (tintWithPlayerColor) uiTextTarget.color = _identity.PlayerColor;
            }
        }

        if (flipForFarTableSide)
        {
            ApplyTableSideFlip();
            _lastAppliedSide = _identity.TableSide;
        }

        if (hideUntilReady) SetTargetsActive(true);
        _lastAppliedName = playerName;
    }

    /// <summary>
    /// Rotates text targets 180° on Z when the owning player is on the far side of the table so
    /// the label is readable from their seat. Only the Z axis is touched — X/Y rotations set in
    /// the prefab (e.g. world-space billboard tilt) are preserved.
    /// </summary>
    private void ApplyTableSideFlip()
    {
        float zRot = _identity.TableSide == 1 ? 180f : 0f;
        ApplyZRotation(tmpTarget != null ? tmpTarget.transform : null, zRot);
        ApplyZRotation(uiTextTarget != null ? uiTextTarget.transform : null, zRot);
    }

    private static void ApplyZRotation(Transform t, float zRot)
    {
        if (t == null) return;
        Vector3 e = t.localEulerAngles;
        t.localEulerAngles = new Vector3(e.x, e.y, zRot);
    }

    private void ResolveIdentity()
    {
        if (identitySource is IPlayerIdentity explicitSource)
        {
            _identity = explicitSource;
            return;
        }

        _identity = GetComponent<IPlayerIdentity>();
        if (_identity == null) _identity = GetComponentInParent<IPlayerIdentity>();
    }

    private void ResolveTargets()
    {
        if (tmpTarget == null) tmpTarget = GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (uiTextTarget == null) uiTextTarget = GetComponentInChildren<Text>(includeInactive: true);
    }

    private void SetTargetsActive(bool active)
    {
        if (tmpTarget != null) tmpTarget.enabled = active;
        if (uiTextTarget != null) uiTextTarget.enabled = active;
    }
}
