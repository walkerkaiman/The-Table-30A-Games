using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Split" powerup. When the real Pong ball exits this trigger, spawns a wave of
/// decoy balls flying in random directions, then deactivates itself so the
/// <see cref="PowerupManager"/> can pick the next powerup.
///
/// Invariants enforced here:
///   * Only the real Pong ball (<c>PongManager.Instance.BallInstance</c>) can
///     trigger the split — paddles, walls, and decoy balls are ignored.
///   * Only one trigger per activation cycle — a flag blocks a second split
///     even if multiple collider exits happen on the same frame.
///   * Decoys are spawned parentless so they survive this GameObject being
///     deactivated after the split.
///   * Any decoys still alive from a previous activation are destroyed the next
///     time this powerup activates, so decoys never accumulate across cycles.
/// </summary>
public class SplitHandler : MonoBehaviour
{
    [Tooltip("Prefab spawned as a decoy ball when the real Pong ball passes through.")]
    public GameObject falseBall;

    [Tooltip("How many decoy balls to spawn per trigger.")]
    public int splitAmount = 3;

    private readonly List<GameObject> _spawnedFakes = new List<GameObject>();
    private bool _hasTriggered;

    private void OnEnable()
    {
        // Clean up any decoys left over from the previous activation BEFORE the
        // new cycle starts, so the arena never accumulates balls across spawns.
        CleanupFakes();
        _hasTriggered = false;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_hasTriggered) return;
        if (PongManager.Instance == null) return;

        // Only react to the real ball — ignore paddles, walls, and decoys.
        if (other.gameObject != PongManager.Instance.BallInstance) return;

        _hasTriggered = true;
        SpawnFakes();
        gameObject.SetActive(false);
    }

    private void SpawnFakes()
    {
        if (falseBall == null) return;

        // Match the real ball's current speed so decoys are indistinguishable
        // at a glance. Fall back to spawnForce if the ball is missing or idle.
        float speed = 0;
        if (PongManager.Instance != null && PongManager.Instance.BallInstance != null)
        {
            var ballRb = PongManager.Instance.BallInstance.GetComponent<Rigidbody2D>();
            if (ballRb != null && ballRb.linearVelocity.sqrMagnitude > 0.0001f)
            {
                speed = ballRb.linearVelocity.magnitude;
            }
        }

        for (int i = 0; i < splitAmount; i++)
        {
            // Uniform random direction on the unit circle — each decoy flies
            // a different way, but they all share the real ball's magnitude.
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            // Parentless: decoys must outlive this GameObject being SetActive(false).
            GameObject newBall = Instantiate(falseBall, transform.position, Quaternion.identity);

            var rb = newBall.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = velocity;

            _spawnedFakes.Add(newBall);
        }
    }

    private void CleanupFakes()
    {
        for (int i = 0; i < _spawnedFakes.Count; i++)
        {
            if (_spawnedFakes[i] != null) Destroy(_spawnedFakes[i]);
        }
        _spawnedFakes.Clear();
    }
}
