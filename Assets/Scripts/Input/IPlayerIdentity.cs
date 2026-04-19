using UnityEngine;

/// <summary>
/// Shared surface for any component that carries per-player identity (id, display name,
/// palette index, table side). Implemented by <see cref="PlayerInputNode"/> and
/// <see cref="PlayerJoystickNode"/> today; add to any future input node so the same
/// reusable display components (nameplates, color-tint visuals, etc.) can consume them
/// without caring which input style the game uses.
/// </summary>
public interface IPlayerIdentity
{
    string PlayerId { get; }
    string PlayerName { get; }
    int PlayerIndex { get; }
    Color PlayerColor { get; }
    /// <summary> 0 = near side of the table, 1 = far side. </summary>
    int TableSide { get; }
}
