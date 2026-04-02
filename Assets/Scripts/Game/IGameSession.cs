/// <summary>
/// Contract that every game type must implement.
/// The GameCoordinator holds a reference to the active IGameSession
/// and forwards player events and messages to it.
///
/// The session communicates outward via GameEvents.FireBroadcast / FireSendToPlayer,
/// and signals completion by calling GameCoordinator.Instance.OnGameEnded().
/// </summary>
public interface IGameSession
{
    string GameType { get; }
    string CurrentState { get; }

    /// <summary> Called by GameCoordinator when the game scene is ready. </summary>
    void OnSessionStart(string[] playerIds);

    /// <summary> Called by GameCoordinator when the game is being torn down. </summary>
    void OnSessionEnd();

    /// <summary> A previously disconnected player has reconnected. </summary>
    void OnPlayerRejoined(string playerId);

    /// <summary> A player's WebSocket disconnected mid-game. </summary>
    void OnPlayerDisconnected(string playerId);

    /// <summary> A game-specific message from a player (not join/rejoin). </summary>
    void OnGameMessage(string playerId, string messageType, string json);
}
