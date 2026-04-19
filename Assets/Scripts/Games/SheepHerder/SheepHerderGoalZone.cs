using UnityEngine;

/// <summary>
/// Trigger volume that "collects" any sheep that fully enters it. Works with both the
/// cooperative single-team mode and the future competitive per-player mode — just set
/// <see cref="ownerPlayerId"/> for competitive zones (leave empty in collab).
///
/// Requires a <see cref="Collider2D"/> with <c>isTrigger = true</c> on the same GameObject. The
/// sheep prefab is expected to have a <see cref="Rigidbody2D"/> (already required by
/// <see cref="Sheep"/>) so Unity will actually fire OnTriggerEnter2D.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SheepHerderGoalZone : MonoBehaviour
{
    [Tooltip("For competitive mode: the player this zone scores for. Leave empty for the shared " +
             "collab goal.")]
    public string ownerPlayerId = "";

    [Tooltip("Points awarded per sheep collected.")]
    public int pointsPerSheep = 100;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var sheep = other.GetComponentInParent<Sheep>();
        if (sheep == null || sheep.IsScored) return;

        if (SheepHerderManager.Instance != null)
        {
            SheepHerderManager.Instance.CollectSheep(sheep, ownerPlayerId, pointsPerSheep);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is BoxCollider2D box) Gizmos.DrawCube(box.offset, box.size);
        else if (col is CircleCollider2D circle) Gizmos.DrawWireSphere(circle.offset, circle.radius);
        else Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
#endif
}
