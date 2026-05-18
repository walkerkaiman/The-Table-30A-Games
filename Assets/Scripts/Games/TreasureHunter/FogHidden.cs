using UnityEngine;

/// <summary>
/// Hides all <see cref="SpriteRenderer"/>s on this GameObject (and its children) until any
/// active <see cref="ExplorerTorch"/> comes within discovery range.
///
/// This is the "items and icons should not be seen before a explorer discovers it" rule:
/// procedurally-spawned traps, gold, puzzles, and the exit door are all tagged with this
/// component so they stay invisible in the dark — even if the fog overlay itself isn't
/// perfectly opaque.
///
/// Once visible, items remain visible while still within torch range and fade back out when
/// every torch moves away. (If you prefer "discover once, visible forever", set
/// <see cref="persistOnceDiscovered"/> to true in the inspector.)
/// </summary>
[DisallowMultipleComponent]
public class FogHidden : MonoBehaviour
{
    [Tooltip("Extra distance (beyond the torch's own radius) before this object is revealed. " +
             "Positive = reveal earlier, negative = only reveal when closer.")]
    public float extraRevealMargin = 0f;

    [Tooltip("If true, once revealed the object stays visible for the rest of the run. " +
             "If false (default), items fade back into darkness when no torch is near.")]
    public bool persistOnceDiscovered = false;

    private SpriteRenderer[] _renderers;
    private bool _visible;
    private bool _everDiscovered;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        SetRenderersEnabled(false);
    }

    private void OnEnable()
    {
        // Start hidden on every enable so pooled / reused objects don't leak visibility.
        _visible = false;
        SetRenderersEnabled(false);
    }

    private void LateUpdate()
    {
        if (persistOnceDiscovered && _everDiscovered)
        {
            if (!_visible) SetRenderersEnabled(true);
            return;
        }

        bool inRange = IsInAnyTorchRange();
        if (inRange != _visible)
        {
            _visible = inRange;
            SetRenderersEnabled(inRange);
            if (inRange) _everDiscovered = true;
        }
    }

    private bool IsInAnyTorchRange()
    {
        var torches = ExplorerTorch.ActiveTorches;
        if (torches == null || torches.Count == 0) return false;

        var pos = transform.position;
        for (int i = 0; i < torches.Count; i++)
        {
            var t = torches[i];
            if (t == null || !t.isLit) continue;
            var d = pos - t.transform.position;
            float r = t.radius + extraRevealMargin;
            if (r <= 0f) continue;
            if (d.x * d.x + d.y * d.y <= r * r) return true;
        }
        return false;
    }

    private void SetRenderersEnabled(bool on)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].enabled = on;
    }

    /// <summary>
    /// Convenience: add a FogHidden to <paramref name="go"/> if it doesn't already have one.
    /// Used by <see cref="ProceduralDungeonAssets"/> when building runtime-fallback objects.
    /// </summary>
    public static void EnsureOn(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponent<FogHidden>() == null) go.AddComponent<FogHidden>();
    }
}
