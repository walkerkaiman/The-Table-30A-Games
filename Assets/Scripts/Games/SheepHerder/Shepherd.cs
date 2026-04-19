using UnityEngine;

/// <summary>
/// Glue component bolted onto every spawned shepherd prefab. Does two things:
///   • Wires the owning <see cref="PlayerJoystickNode"/> into the local <see cref="AcceleratedMover"/>
///     so the node's phone stick drives movement.
///   • Registers/unregisters with the <see cref="SheepRegistry"/> so sheep know where to flee from.
///
/// The actual locomotion (max speed, acceleration, facing) lives on AcceleratedMover and is
/// authored in the shepherd prefab's Inspector. Visual / audio bits should be children of the
/// prefab and can read PlayerColor/PlayerName off the joystick node for per-player identity.
/// </summary>
[RequireComponent(typeof(PlayerJoystickNode))]
[RequireComponent(typeof(AcceleratedMover))]
public class Shepherd : MonoBehaviour
{
    private PlayerJoystickNode _node;
    private AcceleratedMover _mover;

    public PlayerJoystickNode Node => _node;

    private void Awake()
    {
        _node = GetComponent<PlayerJoystickNode>();
        _mover = GetComponent<AcceleratedMover>();
        _mover.SetSource(_node);
    }

    private void OnEnable()
    {
        if (SheepRegistry.Instance != null)
            SheepRegistry.Instance.RegisterShepherd(transform);
    }

    private void OnDisable()
    {
        if (SheepRegistry.Instance != null)
            SheepRegistry.Instance.UnregisterShepherd(transform);
    }
}
