/// <summary>
/// Contract that every game type must implement.
/// The GameCoordinator holds a reference to the active IGameSession
/// and forwards player events and messages to it.
///
/// The session communicates outward via GameEvents.FireBroadcast / FireSendToPlayer,
/// and signals completion by calling GameCoordinator.Instance.OnGameEnded().
///
/// <b>Disconnect ownership:</b> GameCoordinator is responsible for calling
/// PlayerManager.DisconnectPlayer and FirePlayerListChanged. By the time
/// OnPlayerDisconnected reaches the session, the player is already marked
/// disconnected. Sessions should only adjust their own game state (e.g.
/// auto-advance, rebuild ring) — they must NOT call DisconnectPlayer again.
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

    /// <summary>
    /// A player's WebSocket disconnected mid-game.
    /// The player is already marked disconnected in PlayerManager by the time this fires.
    /// Do NOT call PlayerManager.DisconnectPlayer here — adjust game state only.
    /// </summary>
    void OnPlayerDisconnected(string playerId);

    /// <summary> A game-specific message from a player (not join/rejoin). </summary>
    void OnGameMessage(string playerId, string messageType, string json);

    /// <summary>
    /// True if the session is willing to admit new players mid-game. Games that opt in
    /// should also implement <see cref="OnPlayerJoinedMidGame"/>.
    /// Default false — most games freeze their roster at <see cref="OnSessionStart"/>.
    /// </summary>
    bool AllowsMidGameJoin => false;

    /// <summary>
    /// Called when a brand-new player joins while the session is running and
    /// <see cref="AllowsMidGameJoin"/> is true. The player has already been added to
    /// <see cref="PlayerManager"/> and received their join handshake by the time this
    /// fires; the session just needs to bring them into the game (e.g. send their
    /// first assignment).
    /// </summary>
    void OnPlayerJoinedMidGame(string playerId) { }
}
