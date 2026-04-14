using UnityEngine;

/// <summary>
/// Passive data component attached to each per-player child of a PlayerInputRelay.
/// Stores identity info and the latest normalized input from the player's phone.
/// The owning PlayerInputRelay drives all writes; game systems read from here.
/// </summary>
public class PlayerInputNode : MonoBehaviour
{
    private static readonly Color[] Palette =
    {
        new Color(0.39f, 1f, 0.85f),   // cyan
        new Color(1f, 0.72f, 0.29f),   // orange
        new Color(0.72f, 0.45f, 1f),   // purple
        new Color(1f, 0.42f, 0.42f),   // red
        new Color(0.42f, 1f, 0.42f),   // green
        new Color(1f, 0.84f, 0.42f),   // yellow
        new Color(0.42f, 0.73f, 1f),   // blue
        new Color(1f, 0.42f, 0.84f),   // pink
    };

    public string PlayerId { get; private set; }
    public string PlayerName { get; private set; }
    public int PlayerIndex { get; private set; }
    public Color PlayerColor { get; private set; }

    /// <summary> 0 = near side of the table, 1 = far side. </summary>
    public int TableSide { get; private set; }

    /// <summary> Primary axis input, normalized 0-1. </summary>
    public float RawInput { get; set; } = 0.5f;

    /// <summary> Two-axis input for future 2D games. X = primary, Y = secondary. </summary>
    public Vector2 RawInput2D { get; set; } = new Vector2(0.5f, 0.5f);

    /// <summary> Time.time when the last input message was applied. </summary>
    public float LastUpdateTime { get; set; }

    public void Init(string playerId, string playerName, int playerIndex, int tableSide = 0)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        PlayerIndex = playerIndex;
        TableSide = tableSide;
        PlayerColor = Palette[playerIndex % Palette.Length];
        gameObject.name = $"Player_{playerId}";
    }

    public static Color GetColorForIndex(int index) => Palette[index % Palette.Length];
}
