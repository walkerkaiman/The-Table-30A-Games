using UnityEngine;

/// <summary>
/// Passive data component attached to each per-player child of a <see cref="JoystickInputRelay"/>.
/// Stores the identity of the player plus their latest analog stick vector. The owning relay
/// drives all writes; game code reads from here.
///
/// Reuses <see cref="PlayerInputNode.GetColorForIndex"/> for per-player colors so the palette is
/// consistent across input systems.
/// </summary>
public class PlayerJoystickNode : MonoBehaviour, IJoystickInputSource, IPlayerIdentity
{
    public string PlayerId { get; private set; }
    public string PlayerName { get; private set; }
    public int PlayerIndex { get; private set; }
    public Color PlayerColor { get; private set; }

    /// <summary> 0 = near side of the table, 1 = far side. </summary>
    public int TableSide { get; private set; }

    /// <summary>
    /// Latest analog stick vector, in [-1, 1]² with |v| ≤ 1 after dead-zone + clamping.
    /// Set by the owning <see cref="JoystickInputRelay"/>.
    /// </summary>
    public Vector2 Stick { get; set; }

    /// <summary> Time.time of the last incoming joystick_move message. </summary>
    public float LastUpdateTime { get; set; }

    public void Init(string playerId, string playerName, int playerIndex, int tableSide)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        PlayerIndex = playerIndex;
        TableSide = tableSide;
        PlayerColor = PlayerInputNode.GetColorForIndex(playerIndex);
    }
}
