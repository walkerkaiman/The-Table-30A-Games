using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A "point light" carried by an explorer (or any moving object) that illuminates the world
/// through the <see cref="FogOfWar"/> overlay. The torch itself has no real lighting — it just
/// defines a circle around its transform that the fog treats as lit.
///
/// Each torch self-registers with the static <see cref="ActiveTorches"/> list on enable so the
/// <see cref="FogOfWar"/> overlay and <see cref="FogHidden"/> components can iterate them
/// every frame without having to know about explorers specifically. This keeps the fog system
/// decoupled: future entities (stationary braziers, item torches, NPC companions) can also
/// carry a torch just by adding this component.
///
/// Scene contract:
///   • Attach to any transform you want to illuminate around.
///   • Set <see cref="radius"/> (world units, matches default visionRadius on Explorer).
///   • Optional <see cref="softEdge"/> controls the width of the smooth falloff ring outside
///     the torch circle.
/// </summary>
[DisallowMultipleComponent]
public class ExplorerTorch : MonoBehaviour
{
    [Tooltip("How far the torch illuminates, in world units. This is also the discovery radius " +
             "for FogHidden items.")]
    public float radius = 4f;

    [Tooltip("Width of the smooth falloff ring at the torch edge. Smaller = harder edge.")]
    [Range(0.05f, 3f)]
    public float softEdge = 0.75f;

    [Tooltip("If false, the torch is ignored by the fog overlay (e.g. downed explorer).")]
    public bool isLit = true;

    private static readonly List<ExplorerTorch> _active = new List<ExplorerTorch>(16);

    /// <summary>Read-only snapshot of every torch currently in the scene.</summary>
    public static IReadOnlyList<ExplorerTorch> ActiveTorches => _active;

    private void OnEnable() { if (!_active.Contains(this)) _active.Add(this); }
    private void OnDisable() { _active.Remove(this); }

    /// <summary>
    /// Returns true if <paramref name="worldPos"/> lies within any currently-lit torch's circle.
    /// Used by <see cref="FogHidden"/> to decide whether items are discovered.
    /// </summary>
    public static bool IsInsideAnyTorch(Vector3 worldPos)
    {
        for (int i = 0; i < _active.Count; i++)
        {
            var t = _active[i];
            if (t == null || !t.isLit) continue;
            var d = worldPos - t.transform.position;
            float r = t.radius;
            if (d.x * d.x + d.y * d.y <= r * r) return true;
        }
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (radius < 0.1f) radius = 0.1f;
        if (softEdge < 0.05f) softEdge = 0.05f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
