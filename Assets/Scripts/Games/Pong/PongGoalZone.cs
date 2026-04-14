using UnityEngine;

/// <summary>
/// Place this on a GameObject with a 2D trigger collider behind each player's
/// paddle area. When the ball enters, it tells PongManager to score a goal
/// against the assigned player.
///
/// Set tableSide in the Inspector: 0 = "This Side", 1 = "That Side".
/// PongManager automatically assigns the correct playerId at session start
/// by matching tableSide. You can also set assignedPlayerId manually if you
/// need a fixed mapping.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PongGoalZone : MonoBehaviour
{
    [Tooltip("Which table side this goal belongs to. 0 = This Side, 1 = That Side. " +
             "PongManager uses this to assign the correct player at runtime.")]
    public int tableSide;

    [Tooltip("Set automatically by PongManager at session start. " +
             "You can also set this manually in the Inspector for testing.")]
    public string assignedPlayerId;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (PongManager.Instance == null) return;
        if (PongManager.Instance.BallInstance == null) return;
        if (other.gameObject != PongManager.Instance.BallInstance) return;
        if (string.IsNullOrEmpty(assignedPlayerId)) return;
        PongManager.Instance.ScoreGoal(assignedPlayerId);
    }
}
