using UnityEngine;

/// <summary>
/// A gold pile that awards gold when an explorer walks over it.
///
/// Two gold policies are supported (set via <see cref="goldPolicy"/>):
///   • <c>TeamPool</c>: gold goes directly into the session team pool (simplest for casual play).
///   • <c>IndividualCarried</c>: gold is stored on the explorer; if they are knocked down the
///     gold pile reappears at their position (creates risk/reward tension).
///
/// Prefab setup:
///   • Add a <c>Collider2D</c> with <c>isTrigger = true</c>.
///   • Add a <c>SpriteRenderer</c> for the visual (disabled on pick-up, re-enabled on drop).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class GoldPickup : MonoBehaviour
{
    public enum GoldPolicy
    {
        /// <summary>Gold immediately added to shared team total.</summary>
        TeamPool,
        /// <summary>Gold carried by the individual; re-drops when they are knocked down.</summary>
        IndividualCarried,
    }

    [Header("Settings")]
    [Tooltip("Amount of gold this pickup is worth.")]
    public int goldValue = 10;

    [Tooltip("Determines whether gold goes into the shared team pool or is carried by the explorer.")]
    public GoldPolicy goldPolicy = GoldPolicy.TeamPool;

    [Header("Visual")]
    [Tooltip("Optional VFX activated on pickup.")]
    [SerializeField] private GameObject sparkleVfx;

    private bool _pickedUp;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_pickedUp) return;
        var explorer = other.GetComponent<Explorer>() ?? other.GetComponentInParent<Explorer>();
        if (explorer == null || explorer.IsDown || explorer.IsEscaped) return;

        Pickup(explorer);
    }

    private void Pickup(Explorer explorer)
    {
        _pickedUp = true;
        gameObject.SetActive(false);

        if (goldPolicy == GoldPolicy.IndividualCarried)
        {
            explorer.GoldCarried += goldValue;
        }
        else
        {
            // Team pool — notify manager to add to the team total.
            TreasureHunterManager.Instance?.AddTeamGold(goldValue);
        }

        if (sparkleVfx != null)
        {
            var vfx = Instantiate(sparkleVfx, transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        // Notify the collecting player's phone.
        var msg = new TreasureHunterEventMessage
        {
            eventName = "gold_pickup",
            playerId = explorer.PlayerId,
            goldDelta = goldValue,
        };
        GameEvents.FireSendToPlayer(explorer.PlayerId, JsonUtility.ToJson(msg));

        GameLog.Game($"{explorer.PlayerName} picked up {goldValue} gold");
    }

    /// <summary>
    /// Drop this pickup at a new position (called when an IndividualCarried explorer is downed).
    /// </summary>
    public void DropAt(Vector3 position)
    {
        transform.position = position;
        _pickedUp = false;
        gameObject.SetActive(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (goldValue < 0) goldValue = 0;
    }
#endif
}
