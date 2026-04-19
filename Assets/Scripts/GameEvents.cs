using System;

/// <summary>
/// Central event bus — game-agnostic. Decouples the network layer from game logic.
///
/// Event flow:
///   Client WS → NetworkManager → [GameEvents] → GameCoordinator → IGameSession
///   IGameSession → [GameEvents.FireBroadcast] → NetworkManager → Client WS
/// </summary>
public static class GameEvents
{
    // ─────────────────────────────────────────────
    //  JOIN / REJOIN HANDSHAKE
    // ─────────────────────────────────────────────

    public static event Action<string, string, string, int, string> JoinRequested;
    public static event Action<string, string, string, string> JoinAccepted;
    public static event Action<string, string> JoinRejected;

    public static event Action<string, string, string> RejoinRequested;
    public static event Action<string, string, string> RejoinAccepted;
    public static event Action<string, string> RejoinRejected;

    // ─────────────────────────────────────────────
    //  PLAYER LIFECYCLE
    // ─────────────────────────────────────────────

    public static event Action<string> PlayerDisconnected;

    // ─────────────────────────────────────────────
    //  GENERIC GAME MESSAGE
    //  Fired by NetworkManager for any message type that is not join/rejoin.
    //  GameCoordinator routes it to the active IGameSession or handles it
    //  (e.g., pick_game during GameSelect).
    //  Args: playerId, messageType, rawJson
    // ─────────────────────────────────────────────

    public static event Action<string, string, string> GameMessageReceived;

    // ─────────────────────────────────────────────
    //  OUTBOUND NETWORK
    // ─────────────────────────────────────────────

    public static event Action<string> BroadcastMessage;
    public static event Action<string, string> SendToPlayer;
    public static event Action PlayerListChanged;

    // ─────────────────────────────────────────────
    //  TABLE DISPLAY
    //  Fired on every phase transition so the table-side display
    //  can show phase name, timer, etc. without parsing broadcast JSON.
    //  Args: gameType, phase, timer (seconds remaining)
    // ─────────────────────────────────────────────

    public static event Action<string, string, int> DisplayStateChanged;

    // ─────────────────────────────────────────────
    //  FIRE HELPERS
    // ─────────────────────────────────────────────

    public static void FireJoinRequested(string connId, string name, string roomCode, int tableSide = 0, string existingPlayerId = "")
        => JoinRequested?.Invoke(connId, name, roomCode, tableSide, existingPlayerId);

    public static void FireJoinAccepted(string connId, string playerId, string name, string roomCode)
        => JoinAccepted?.Invoke(connId, playerId, name, roomCode);

    public static void FireJoinRejected(string connId, string error)
        => JoinRejected?.Invoke(connId, error);

    public static void FireRejoinRequested(string connId, string playerId, string name)
        => RejoinRequested?.Invoke(connId, playerId, name);

    public static void FireRejoinAccepted(string connId, string playerId, string name)
        => RejoinAccepted?.Invoke(connId, playerId, name);

    public static void FireRejoinRejected(string connId, string error)
        => RejoinRejected?.Invoke(connId, error);

    public static void FirePlayerDisconnected(string playerId)
        => PlayerDisconnected?.Invoke(playerId);

    public static void FireGameMessageReceived(string playerId, string messageType, string json)
        => GameMessageReceived?.Invoke(playerId, messageType, json);

    public static void FireBroadcast(string json)
        => BroadcastMessage?.Invoke(json);

    public static void FireSendToPlayer(string playerId, string json)
        => SendToPlayer?.Invoke(playerId, json);

    public static void FirePlayerListChanged()
        => PlayerListChanged?.Invoke();

    public static void FireDisplayState(string gameType, string phase, int timer)
        => DisplayStateChanged?.Invoke(gameType, phase, timer);

    public static void ClearAll()
    {
        JoinRequested = null;
        JoinAccepted = null;
        JoinRejected = null;
        RejoinRequested = null;
        RejoinAccepted = null;
        RejoinRejected = null;
        PlayerDisconnected = null;
        GameMessageReceived = null;
        BroadcastMessage = null;
        SendToPlayer = null;
        PlayerListChanged = null;
        DisplayStateChanged = null;
    }
}
