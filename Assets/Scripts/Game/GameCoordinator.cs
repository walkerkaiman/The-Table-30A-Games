using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent singleton that owns the top-level state machine:
///   Lobby → GameSelect → InGame → GameSelect → ...
///
/// Responsibilities:
///   - Handles all join/rejoin/disconnect logic (replaces old GameManager lobby code)
///   - Manages game voting during GameSelect
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
    [SerializeField] private float voteTimerSeconds = 15f;

    public CoordinatorState CurrentState { get; private set; } = CoordinatorState.Lobby;
    public string RoomCode { get; private set; }

    private IGameSession _activeSession;
    private Dictionary<string, string> _playerVotes = new Dictionary<string, string>();
    private float _timer;
    private bool _timerActive;
    private string _loadedSceneName;

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
        if (_timerActive)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timerActive = false;
                OnTimerExpired();
            }
        }

        if (Input.GetKeyDown(KeyCode.Space) && CurrentState == CoordinatorState.Lobby)
            TryStartGameSelect();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    // ── Event Wiring ─────────────────────────────

    private void SubscribeToEvents()
    {
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

    private void OnJoinRequested(string connId, string playerName, string roomCode)
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
            GameLog.Player($"Join REJECTED: \"{playerName}\" — game in progress");
            GameEvents.FireJoinRejected(connId, "Game in progress");
            return;
        }

        string playerId = PlayerManager.Instance.AddPlayer(playerName);
        GameLog.Player($"\"{playerName}\" JOINED  (id: {playerId})  [{PlayerManager.Instance.PlayerCount} player(s)]");
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
            GameEvents.FireRejoinRejected(connId, "Already connected");
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

        GameEvents.FireRejoinRejected(connId, "Session not found");
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
        if (messageType == "start_game")
        {
            HandleHostStartGame(playerId);
            return;
        }

        if (CurrentState == CoordinatorState.GameSelect && messageType == "game_vote")
        {
            HandleGameVote(playerId, json);
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
            GameLog.Game($"Host \"{hostName}\" forced vote to resolve");
            _timerActive = false;
            ResolveVote();
        }
    }

    // ════════════════════════════════════════════
    //  GAME SELECTION / VOTING
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
        GameLog.Game($"GAME SELECTION — {count} players voting");
        GameLog.Divider();

        CurrentState = CoordinatorState.GameSelect;
        _playerVotes.Clear();
        StartTimer(voteTimerSeconds);
        BroadcastCoordinatorState();
    }

    private void HandleGameVote(string playerId, string json)
    {
        var msg = JsonUtility.FromJson<GameVoteMessage>(json);
        if (msg == null || string.IsNullOrEmpty(msg.gameId)) return;

        var entry = gameRegistry.GetEntryById(msg.gameId);
        if (entry == null) return;

        if (PlayerManager.Instance.ActivePlayerCount < entry.minPlayers) return;

        _playerVotes[playerId] = msg.gameId;
        string name = PlayerManager.Instance.GetPlayerName(playerId);
        GameLog.Game($"\"{name}\" voted for {entry.displayName}");

        BroadcastVoteUpdate();

        if (_playerVotes.Count >= PlayerManager.Instance.ActivePlayerCount)
        {
            _timerActive = false;
            GameLog.Game("All votes in!");
            ResolveVote();
        }
    }

    private void BroadcastVoteUpdate()
    {
        var msg = new VoteUpdateMessage { votes = BuildVoteCounts() };
        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }

    private void ResolveVote()
    {
        var counts = new Dictionary<string, int>();
        foreach (var vote in _playerVotes.Values)
        {
            counts.TryGetValue(vote, out int c);
            counts[vote] = c + 1;
        }

        int maxVotes = 0;
        var candidates = new List<string>();
        foreach (var kvp in counts)
        {
            if (kvp.Value > maxVotes)
            {
                maxVotes = kvp.Value;
                candidates.Clear();
                candidates.Add(kvp.Key);
            }
            else if (kvp.Value == maxVotes)
            {
                candidates.Add(kvp.Key);
            }
        }

        string winnerId;
        if (candidates.Count == 0)
        {
            int idx = UnityEngine.Random.Range(0, gameRegistry.entries.Count);
            winnerId = gameRegistry.entries[idx].id;
        }
        else
        {
            winnerId = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        var winner = gameRegistry.GetEntryById(winnerId);
        GameLog.Game($"Vote winner: {winner.displayName}");
        LoadGame(winner);
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
    /// Returns to GameSelect for the next round of voting.
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
        PlayerManager.Instance.ResetScores();

        CurrentState = CoordinatorState.GameSelect;
        _playerVotes.Clear();
        StartTimer(voteTimerSeconds);
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
            timer = Mathf.CeilToInt(_timer),
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
                msg.voteCounts = BuildVoteCounts();
                break;
        }

        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }

    private void OnTimerExpired()
    {
        if (CurrentState == CoordinatorState.GameSelect)
        {
            GameLog.Game("Vote timer expired");
            ResolveVote();
        }
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

    private VoteCount[] BuildVoteCounts()
    {
        if (gameRegistry == null) return new VoteCount[0];
        var counts = new Dictionary<string, int>();
        foreach (var e in gameRegistry.entries)
        {
            if (e != null) counts[e.id] = 0;
        }
        foreach (var vote in _playerVotes.Values)
        {
            if (counts.ContainsKey(vote)) counts[vote]++;
        }
        var result = new VoteCount[counts.Count];
        int i = 0;
        foreach (var kvp in counts)
            result[i++] = new VoteCount { gameId = kvp.Key, count = kvp.Value };
        return result;
    }

    private void StartTimer(float seconds)
    {
        _timer = seconds;
        _timerActive = true;
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
