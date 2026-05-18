using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Action3 = System.Action<string, string, string>;

/// <summary>
/// Persistent singleton that owns the top-level state machine:
///   Lobby → GameSelect → InGame → GameSelect → ...
///
/// Responsibilities:
///   - Handles all join/rejoin/disconnect logic (replaces old GameManager lobby code)
///   - During GameSelect the host picks a game (no voting) and taps Play to load it
///   - Loads/unloads game scenes and routes messages to the active IGameSession
///   - Owns the room code
/// </summary>
public class GameCoordinator : MonoBehaviour
{
    public static GameCoordinator Instance { get; private set; }

    public enum CoordinatorState { Lobby, GameSelect, InGame }

    [Header("Settings")]
    [SerializeField] private GameRegistry gameRegistry;
    [SerializeField] private int minPlayersToStart = 2;

    public static event System.Action<CoordinatorState> StateChanged;

    private CoordinatorState _currentState = CoordinatorState.Lobby;
    public CoordinatorState CurrentState
    {
        get => _currentState;
        private set
        {
            if (_currentState == value) return;
            _currentState = value;
            StateChanged?.Invoke(_currentState);
        }
    }
    public string RoomCode { get; private set; }

    private IGameSession _activeSession;
    private string _loadedSceneName;
    private Dictionary<string, Action3> _messageHandlers;

    // ── Unity Lifecycle ──────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        RoomCode = GenerateRoomCode();
    }

    private void Start()
    {
        SubscribeToEvents();

        GameLog.Divider();
        GameLog.Banner(
            $"ROOM CODE:  {RoomCode}",
            $"JOIN URL:   http://{NetworkManager.Instance.LocalIP}:{NetworkManager.Instance.Port}",
            $"GAMES:      {(gameRegistry != null ? gameRegistry.entries.Count : 0)} registered"
        );
        GameLog.Divider();
        GameLog.Game("Waiting for players to join...");
        GameLog.Game("The first player to join becomes the host and can start from their phone.");
        GameLog.Game("Press [Space] as a fallback to start game selection.");

        BroadcastCoordinatorState();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space) && CurrentState == CoordinatorState.Lobby)
            TryStartGameSelect();
#endif
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    // ── Event Wiring ─────────────────────────────

    private void SubscribeToEvents()
    {
        _messageHandlers = new Dictionary<string, Action3>
        {
            { MessageTypes.StartGame, (pid, _, __) => HandleHostStartGame(pid) },
            { MessageTypes.OpenRegistration, (pid, _, __) => HandleOpenRegistration(pid) },
            { MessageTypes.PickGame, (pid, _, json) => HandlePickGame(pid, json) },
        };

        GameEvents.JoinRequested += OnJoinRequested;
        GameEvents.RejoinRequested += OnRejoinRequested;
        GameEvents.PlayerDisconnected += OnPlayerDisconnected;
        GameEvents.GameMessageReceived += OnGameMessage;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.JoinRequested -= OnJoinRequested;
        GameEvents.RejoinRequested -= OnRejoinRequested;
        GameEvents.PlayerDisconnected -= OnPlayerDisconnected;
        GameEvents.GameMessageReceived -= OnGameMessage;
    }

    // ════════════════════════════════════════════
    //  JOIN / REJOIN / DISCONNECT
    // ════════════════════════════════════════════

    private void OnJoinRequested(string connId, string playerName, string roomCode, int tableSide, string existingPlayerId)
    {
        if (!string.Equals(roomCode, RoomCode, StringComparison.OrdinalIgnoreCase))
        {
            GameLog.Player($"Join REJECTED: \"{playerName}\" — wrong room code \"{roomCode}\"");
            GameEvents.FireJoinRejected(connId, "Invalid room code");
            return;
        }

        if (CurrentState == CoordinatorState.InGame)
        {
            string existingId = PlayerManager.Instance.FindDisconnectedByName(playerName);
            if (existingId != null)
            {
                PlayerManager.Instance.ReconnectPlayer(existingId);
                GameLog.Player($"\"{playerName}\" REJOINED via name match (id: {existingId})");
                GameEvents.FireRejoinAccepted(connId, existingId, playerName);
                _activeSession?.OnPlayerRejoined(existingId);
                GameEvents.FirePlayerListChanged();
                return;
            }

            // Mid-game-join opt-in: sessions that return true from AllowsMidGameJoin
            // (e.g. Wang Tiles) accept fresh joiners directly into the running game.
            if (_activeSession != null && _activeSession.AllowsMidGameJoin)
            {
                string midId = PlayerManager.Instance.AddPlayer(playerName, tableSide);
                GameLog.Player($"\"{playerName}\" JOINED mid-game (id: {midId}, {(tableSide == 0 ? "this side" : "that side")})  [{PlayerManager.Instance.PlayerCount} player(s)]");
                GameEvents.FireJoinAccepted(connId, midId, playerName, RoomCode);
                GameEvents.FirePlayerListChanged();
                _activeSession.OnPlayerJoinedMidGame(midId);
                return;
            }

            // Late joiner: a brand-new player arrived while a game is running.
            // Don't reject them — accept them as a regular player so they appear
            // in the lobby/game-select once the current game ends. The active
            // IGameSession only knows about the playerIds it received in
            // OnSessionStart, so this player is invisible to the running game.
            string lateId = PlayerManager.Instance.AddPlayer(playerName, tableSide);
            GameLog.Player($"\"{playerName}\" JOINED late (id: {lateId}, {(tableSide == 0 ? "this side" : "that side")}) — will play next game  [{PlayerManager.Instance.PlayerCount} player(s)]");
            GameEvents.FireJoinAccepted(connId, lateId, playerName, RoomCode);
            GameEvents.FireSendToPlayer(lateId, JsonUtility.ToJson(new LobbyNoticeMessage
            {
                message = "A game is in progress. You're in — you'll join when this round finishes."
            }));
            GameEvents.FirePlayerListChanged();
            return;
        }

        string sideLabel = tableSide == 0 ? "this side" : "that side";

        if (!string.IsNullOrEmpty(existingPlayerId) && PlayerManager.Instance.HasPlayer(existingPlayerId))
        {
            string oldName = PlayerManager.Instance.GetPlayerName(existingPlayerId);
            PlayerManager.Instance.UpdatePlayer(existingPlayerId, playerName, tableSide);
            PlayerManager.Instance.ReconnectPlayer(existingPlayerId);
            GameLog.Player($"\"{oldName}\" RE-REGISTERED as \"{playerName}\" ({sideLabel} side)  [{PlayerManager.Instance.PlayerCount} player(s)]");
            GameEvents.FireJoinAccepted(connId, existingPlayerId, playerName, RoomCode);
            GameEvents.FirePlayerListChanged();
            return;
        }

        string playerId = PlayerManager.Instance.AddPlayer(playerName, tableSide);
        GameLog.Player($"\"{playerName}\" JOINED  (id: {playerId}, {sideLabel} side)  [{PlayerManager.Instance.PlayerCount} player(s)]");
        GameEvents.FireJoinAccepted(connId, playerId, playerName, RoomCode);
        GameEvents.FirePlayerListChanged();
    }

    private void OnRejoinRequested(string connId, string playerId, string playerName)
    {
        if (PlayerManager.Instance.IsPlayerDisconnected(playerId))
        {
            PlayerManager.Instance.ReconnectPlayer(playerId);
            GameLog.Player($"\"{playerName}\" REJOINED by ID (id: {playerId})");
            GameEvents.FireRejoinAccepted(connId, playerId, playerName);
            if (CurrentState == CoordinatorState.InGame)
                _activeSession?.OnPlayerRejoined(playerId);
            GameEvents.FirePlayerListChanged();
            BroadcastCoordinatorState();
            return;
        }

        if (PlayerManager.Instance.IsPlayerConnected(playerId))
        {
            GameLog.Player($"Rejoin REJECTED: \"{playerName}\" (id: {playerId}) — already connected from another session");
            GameEvents.FireRejoinRejected(connId, "Already connected from another device");
            return;
        }

        string matchedId = PlayerManager.Instance.FindDisconnectedByName(playerName);
        if (matchedId != null)
        {
            PlayerManager.Instance.ReconnectPlayer(matchedId);
            GameLog.Player($"\"{playerName}\" REJOINED via name match (id: {matchedId})");
            GameEvents.FireRejoinAccepted(connId, matchedId, playerName);
            if (CurrentState == CoordinatorState.InGame)
                _activeSession?.OnPlayerRejoined(matchedId);
            GameEvents.FirePlayerListChanged();
            BroadcastCoordinatorState();
            return;
        }

        if (CurrentState != CoordinatorState.InGame)
        {
            string newId = PlayerManager.Instance.AddPlayer(playerName);
            GameLog.Player($"\"{playerName}\" joined fresh from stale rejoin (id: {newId})");
            GameEvents.FireJoinAccepted(connId, newId, playerName, RoomCode);
            GameEvents.FirePlayerListChanged();
            return;
        }

        // Stale rejoin during InGame: the original session was cleaned up.
        // If the active session allows mid-game joins (e.g. Wang Tiles via the
        // table QR code), bring them straight into the running game instead of
        // parking them with a "wait for next round" notice.
        if (_activeSession != null && _activeSession.AllowsMidGameJoin)
        {
            string midId = PlayerManager.Instance.AddPlayer(playerName);
            GameLog.Player($"\"{playerName}\" JOINED mid-game from stale rejoin (id: {midId})");
            GameEvents.FireJoinAccepted(connId, midId, playerName, RoomCode);
            GameEvents.FirePlayerListChanged();
            _activeSession.OnPlayerJoinedMidGame(midId);
            return;
        }

        string lateId = PlayerManager.Instance.AddPlayer(playerName);
        GameLog.Player($"\"{playerName}\" JOINED late from stale rejoin (id: {lateId}) — will play next game");
        GameEvents.FireJoinAccepted(connId, lateId, playerName, RoomCode);
        GameEvents.FireSendToPlayer(lateId, JsonUtility.ToJson(new LobbyNoticeMessage
        {
            message = "A game is in progress. You're in — you'll join when this round finishes."
        }));
        GameEvents.FirePlayerListChanged();
    }

    private void OnPlayerDisconnected(string playerId)
    {
        string name = PlayerManager.Instance.GetPlayerName(playerId);

        if (CurrentState == CoordinatorState.Lobby)
        {
            PlayerManager.Instance.RemovePlayer(playerId);
            GameLog.Player($"\"{name}\" LEFT lobby  [{PlayerManager.Instance.PlayerCount} remaining]");
        }
        else
        {
            PlayerManager.Instance.DisconnectPlayer(playerId);
            GameLog.Player($"\"{name}\" DISCONNECTED  [{PlayerManager.Instance.ActivePlayerCount} active]");
            if (CurrentState == CoordinatorState.InGame)
                _activeSession?.OnPlayerDisconnected(playerId);
        }

        GameEvents.FirePlayerListChanged();
    }

    // ════════════════════════════════════════════
    //  GAME MESSAGE ROUTING
    // ════════════════════════════════════════════

    private void OnGameMessage(string playerId, string messageType, string json)
    {
        if (_messageHandlers.TryGetValue(messageType, out var handler))
        {
            handler(playerId, messageType, json);
            return;
        }

        if (CurrentState == CoordinatorState.InGame)
        {
            _activeSession?.OnGameMessage(playerId, messageType, json);
        }
    }

    private void HandleHostStartGame(string playerId)
    {
        if (!PlayerManager.Instance.IsHost(playerId))
        {
            string name = PlayerManager.Instance.GetPlayerName(playerId);
            GameLog.Warn($"\"{name}\" tried to start but is not the host");
            return;
        }

        string hostName = PlayerManager.Instance.GetPlayerName(playerId);

        if (CurrentState == CoordinatorState.Lobby)
        {
            GameLog.Game($"Host \"{hostName}\" started game selection");
            TryStartGameSelect();
        }
        else if (CurrentState == CoordinatorState.GameSelect)
        {
            // Host no longer has a "force start" path during GameSelect — they pick a game
            // card and press Play, which arrives as a pick_game message.
            GameLog.Warn($"Host \"{hostName}\" sent start_game during GameSelect — ignoring (use Play).");
        }
    }

    private void HandleOpenRegistration(string playerId)
    {
        if (!PlayerManager.Instance.IsHost(playerId))
        {
            string name = PlayerManager.Instance.GetPlayerName(playerId);
            GameLog.Warn($"\"{name}\" tried to open registration but is not the host");
            return;
        }

        if (CurrentState != CoordinatorState.GameSelect)
        {
            GameLog.Warn("open_registration ignored — not in GameSelect state");
            return;
        }

        string hostName = PlayerManager.Instance.GetPlayerName(playerId);
        GameLog.Game($"Host \"{hostName}\" opened registration");

        CurrentState = CoordinatorState.Lobby;
        BroadcastCoordinatorState();
    }

    // ════════════════════════════════════════════
    //  GAME SELECTION
    // ════════════════════════════════════════════

    private void TryStartGameSelect()
    {
        int count = PlayerManager.Instance.ActivePlayerCount;
        if (count < minPlayersToStart)
        {
            GameLog.Game($"Cannot start: need at least {minPlayersToStart} players (have {count})");
            return;
        }

        if (gameRegistry == null || gameRegistry.entries.Count == 0)
        {
            GameLog.Warn("No games registered in GameRegistry!");
            return;
        }

        GameLog.Divider();
        GameLog.Game($"GAME SELECTION — host picks a game ({count} players)");
        GameLog.Divider();

        CurrentState = CoordinatorState.GameSelect;
        BroadcastCoordinatorState();
    }

    private void HandlePickGame(string playerId, string json)
    {
        if (CurrentState != CoordinatorState.GameSelect) return;

        if (!PlayerManager.Instance.IsHost(playerId))
        {
            string who = PlayerManager.Instance.GetPlayerName(playerId);
            GameLog.Warn($"\"{who}\" tried to pick a game but is not the host");
            return;
        }

        var msg = JsonUtility.FromJson<PickGameMessage>(json);
        if (string.IsNullOrEmpty(msg.gameId))
        {
            GameLog.Warn("pick_game ignored — missing gameId");
            return;
        }

        var entry = gameRegistry.GetEntryById(msg.gameId);
        if (entry == null)
        {
            GameLog.Warn($"pick_game ignored — unknown gameId \"{msg.gameId}\"");
            return;
        }

        if (PlayerManager.Instance.ActivePlayerCount < entry.minPlayers)
        {
            GameLog.Warn($"pick_game ignored — {entry.displayName} needs {entry.minPlayers} players");
            return;
        }

        string hostName = PlayerManager.Instance.GetPlayerName(playerId);
        GameLog.Game($"Host \"{hostName}\" picked {entry.displayName}");
        LoadGame(entry);
    }

    // ════════════════════════════════════════════
    //  SCENE MANAGEMENT
    // ════════════════════════════════════════════

    private void LoadGame(GameRegistryEntry entry)
    {
        GameLog.Divider();
        GameLog.Game($"LOADING GAME: {entry.displayName} (scene: {entry.sceneName})");
        GameLog.Divider();

        CurrentState = CoordinatorState.InGame;
        _loadedSceneName = entry.sceneName;
        SceneManager.LoadScene(entry.sceneName, LoadSceneMode.Additive);
    }

    /// <summary>
    /// Called by a game scene's IGameSession in its Start() to register with the coordinator.
    /// </summary>
    public void RegisterSession(IGameSession session)
    {
        _activeSession = session;
        GameLog.Game($"Session registered: {session.GameType}");

        var playerIds = PlayerManager.Instance.GetAllPlayerIds();
        session.OnSessionStart(playerIds.ToArray());
    }

    /// <summary>
    /// Called by the active IGameSession when the game is over.
    /// Returns to GameSelect so the host can pick the next game.
    /// </summary>
    public void OnGameEnded()
    {
        GameLog.Divider();
        GameLog.Game("Game ended — returning to game selection");
        GameLog.Divider();

        _activeSession?.OnSessionEnd();
        _activeSession = null;

        if (!string.IsNullOrEmpty(_loadedSceneName))
        {
            SceneManager.UnloadSceneAsync(_loadedSceneName);
            _loadedSceneName = null;
        }

        PlayerManager.Instance.CleanupDisconnectedPlayers();

        CurrentState = CoordinatorState.GameSelect;
        BroadcastCoordinatorState();
    }

    // ════════════════════════════════════════════
    //  STATE BROADCASTING
    // ════════════════════════════════════════════

    private void BroadcastCoordinatorState()
    {
        if (CurrentState == CoordinatorState.InGame) return;

        var msg = new GameStateMessage
        {
            state = CurrentState.ToString(),
            timer = 0,
            players = PlayerManager.Instance.GetAllPlayerInfos()
        };

        switch (CurrentState)
        {
            case CoordinatorState.Lobby:
                msg.gameType = "lobby";
                break;

            case CoordinatorState.GameSelect:
                msg.gameType = "game_select";
                msg.games = BuildGameSelectInfos();
                break;
        }

        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }

    // ── Helpers ──────────────────────────────────

    private GameSelectInfo[] BuildGameSelectInfos()
    {
        if (gameRegistry == null) return new GameSelectInfo[0];
        var list = new List<GameSelectInfo>();
        foreach (var e in gameRegistry.entries)
        {
            if (e == null) continue;
            list.Add(new GameSelectInfo
            {
                id = e.id,
                name = e.displayName,
                description = e.description,
                minPlayers = e.minPlayers,
                maxPlayers = e.maxPlayers
            });
        }
        return list.ToArray();
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        char[] code = new char[4];
        var rng = new System.Random();
        for (int i = 0; i < 4; i++)
            code[i] = chars[rng.Next(chars.Length)];
        return new string(code);
    }
}
