using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Activates one of the scene's powerup GameObjects at random every
/// <see cref="spawnFrequencySeconds"/>. Only ticks while Pong is in the Playing
/// state. Skips ticking (pauses the cooldown) while any powerup is currently
/// active, so the interval is measured from when the previous powerup ends.
///
/// Each powerup GameObject is expected to handle its own "active" lifetime and
/// call <c>SetActive(false)</c> on itself when it's done — that's what lets this
/// manager pick the next one.
/// </summary>
public class PowerupManager : MonoBehaviour
{
    [Tooltip("Drag each scene powerup GameObject here (e.g. the Split powerup). " +
             "They should start inactive; the manager activates one at random on the interval, " +
             "and each powerup is expected to SetActive(false) on itself when it finishes.")]
    public List<GameObject> PowerUps;

    [Tooltip("Seconds between powerup activations. The cooldown pauses while any powerup is " +
             "active, so the real gap between spawns = this value AFTER the previous powerup ends.")]
    public float spawnFrequencySeconds = 8f;

    private float _countdown;

    private void OnEnable()
    {
        _countdown = spawnFrequencySeconds;
    }

    private void Update()
    {
        if (PongManager.Instance == null) return;
        if (PongManager.Instance.CurrentState != nameof(PongManager.PongState.Playing)) return;

        // Don't tick while a powerup is still running — avoids stacking effects.
        if (AnyPowerupActive()) return;

        _countdown -= Time.deltaTime;
        if (_countdown <= 0f)
        {
            ActivateRandomPowerup();
            _countdown = spawnFrequencySeconds;
        }
    }

    private bool AnyPowerupActive()
    {
        if (PowerUps == null) return false;
        for (int i = 0; i < PowerUps.Count; i++)
        {
            var p = PowerUps[i];
            if (p != null && p.activeSelf) return true;
        }
        return false;
    }

    private void ActivateRandomPowerup()
    {
        if (PowerUps == null || PowerUps.Count == 0) return;

        int idx = Random.Range(0, PowerUps.Count);
        var picked = PowerUps[idx];
        if (picked == null) return;

        picked.SetActive(true);
    }
}
