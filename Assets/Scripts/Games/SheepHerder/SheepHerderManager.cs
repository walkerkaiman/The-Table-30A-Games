using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// IGameSession for the Sheep Herder game. Manages the collaborative variant today and is
/// structured so the competitive variant can bolt on later by changing <see cref="mode"/> and
/// assigning per-player <see cref="SheepHerderGoalZone"/> owners.
///
/// Scene contract (authored in the Unity scene ahead of time):
///   • This component lives on the "SheepHerderManager" GameObject.
///   • A <see cref="JoystickInputRelay"/> lives as a child (auto-added if missing).
///   • One or more <see cref="SheepHerderGoalZone"/> triggers exist in the scene.
///   • A <see cref="SheepSpawner"/> exists in the scene (provides sheep prefab + spawn region).
///   • A <see cref="SheepRegistry"/> exists OR is auto-created.
/// </summary>
public class SheepHerderManager : MonoBehaviour, IGameSession
{
    public static SheepHerderManager Instance { get; private set; }

    public enum Mode
    {
        /// <summary> One shared goal zone, team score. </summary>
        Collaborative,
        /// <summary> Goal zone per player, individual scores. </summary>
        Competitive,
    }

    public enum SheepHerderState
    {
        Countdown,
        Playing,
        GameOver,
    }

    [Header("Mode")]
    [SerializeField] private Mode mode = Mode.Collaborative;

    [Header("Timings")]
    [SerializeField] private float countdownSeconds = 3f;
    [SerializeField] private float gameOverDisplaySeconds = 8f;
    [SerializeField] private int broadcastEveryNFrames = 6;

    [Header("Team Score (collab only)")]
    [SerializeField] private int pointsPerSheep = 100;

    public string GameType => "sheep_herder";
    public string CurrentState => _state.ToString();
    public Mode CurrentMode => mode;

    private JoystickInputRelay _inputRelay;
    private SheepSpawner _spawner;
    private SheepRegistry _registry;

    private SheepHerderState _state;
    private float _timer;
    private int _fixedFrameCount;

    private string[] _playerIds;
    private int _teamScore;
    private int _sheepTotal;

    // ── Unity lifecycle ──────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _registry = FindAnyObjectByType<SheepRegistry>();
        if (_registry == null)
        {
            var go = new GameObject("SheepRegistry");
            go.transform.SetParent(transform);
            _registry = go.AddComponent<SheepRegistry>();
        }

        _inputRelay = GetComponentInChildren<JoystickInputRelay>();
        if (_inputRelay == null)
        {
            var go = new GameObject("JoystickInputRelay");
            go.transform.SetParent(transform);
            _inputRelay = go.AddComponent<JoystickInputRelay>();
        }

        _spawner = FindAnyObjectByType<SheepSpawner>();
        if (_spawner == null)
        {
            GameLog.Warn("SheepHerder: No SheepSpawner found in scene. Sheep can't be spawned.");
        }

        GameCoordinator.Instance.RegisterSession(this);
    }

    private void Update()
    {
        if (_state == SheepHerderState.Countdown || _state == SheepHerderState.GameOver)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) OnTimerExpired();
        }
    }

    private void FixedUpdate()
    {
        if (_state != SheepHerderState.Playing) return;
        _fixedFrameCount++;
        if (_fixedFrameCount % broadcastEveryNFrames == 0) BroadcastFullState();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ════════════════════════════════════════════
    //  IGameSession
    // ════════════════════════════════════════════

    public void OnSessionStart(string[] playerIds)
    {
        _playerIds = playerIds;
        _teamScore = 0;

        GameLog.Divider();
        GameLog.Game($"SHEEP HERDER ({mode}) — {playerIds.Length} shepherds");
        GameLog.Divider();

        _inputRelay.Initialize(playerIds);
        AssignGoalZones(playerIds);

        if (_spawner != null)
        {
            _sheepTotal = _spawner.SpawnAll();
            GameLog.Game($"Spawned {_sheepTotal} sheep");
        }
        else
        {
            _sheepTotal = _registry.Sheep.Count;
        }

        _state = SheepHerderState.Countdown;
        _timer = countdownSeconds;
        BroadcastFullState();
    }

    public void OnSessionEnd()
    {
        _state = SheepHerderState.GameOver;
        _inputRelay?.Teardown();
        _spawner?.DespawnAll();
    }

    public void OnPlayerRejoined(string playerId) => BroadcastFullState();

    public void OnPlayerDisconnected(string playerId)
    {
        PlayerManager.Instance.DisconnectPlayer(playerId);
        GameLog.Player($"\"{PlayerManager.Instance.GetPlayerName(playerId)}\" disconnected from Sheep Herder");
        GameEvents.FirePlayerListChanged();
    }

    public void OnGameMessage(string playerId, string messageType, string json)
    {
        // joystick_move is consumed by JoystickInputRelay via GameEvents directly.
    }

    // ════════════════════════════════════════════
    //  Goal zone assignment
    // ════════════════════════════════════════════

    private void AssignGoalZones(string[] playerIds)
    {
        var zones = FindObjectsByType<SheepHerderGoalZone>();
        if (zones.Length == 0)
        {
            GameLog.Warn("SheepHerder: No SheepHerderGoalZone objects in scene — sheep can't be collected!");
            return;
        }

        if (mode == Mode.Collaborative)
        {
            foreach (var z in zones) z.ownerPlayerId = "";
            return;
        }

        // Competitive: first N zones belong to first N players, remaining zones stay unassigned.
        for (int i = 0; i < zones.Length; i++)
        {
            zones[i].ownerPlayerId = i < playerIds.Length ? playerIds[i] : "";
        }
    }

    // ════════════════════════════════════════════
    //  Public scoring API
    // ════════════════════════════════════════════

    public void CollectSheep(Sheep sheep, string scoringPlayerId, int points)
    {
        if (_state != SheepHerderState.Playing || sheep == null || sheep.IsScored) return;

        sheep.MarkScored();
        _registry.UnregisterSheep(sheep);

        int awarded = points > 0 ? points : pointsPerSheep;

        if (mode == Mode.Collaborative || string.IsNullOrEmpty(scoringPlayerId))
        {
            _teamScore += awarded;
            foreach (var id in _playerIds) PlayerManager.Instance.AddScore(id, awarded);
        }
        else
        {
            PlayerManager.Instance.AddScore(scoringPlayerId, awarded);
        }

        int remaining = _registry.ActiveSheepCount;
        GameLog.Game($"Sheep collected — {remaining} remaining, team score {_teamScore}");

        BroadcastEvent("sheep_scored", scoringPlayerId, remaining);

        if (remaining <= 0)
        {
            HandleGameOver();
        }
        else
        {
            BroadcastFullState();
        }
    }

    private void HandleGameOver()
    {
        GameLog.Divider();
        GameLog.Game($"══ SHEEP HERDER COMPLETE ({mode}) — score {_teamScore} ══");
        GameLog.Divider();

        _state = SheepHerderState.GameOver;
        _timer = gameOverDisplaySeconds;
        BroadcastEvent("game_over", null, 0);
        BroadcastFullState();
    }

    // ════════════════════════════════════════════
    //  State machine
    // ════════════════════════════════════════════

    private void OnTimerExpired()
    {
        switch (_state)
        {
            case SheepHerderState.Countdown:
                _state = SheepHerderState.Playing;
                _fixedFrameCount = 0;
                GameLog.Game("SHEEP HERDER — GO!");
                BroadcastFullState();
                break;

            case SheepHerderState.GameOver:
                GameCoordinator.Instance.OnGameEnded();
                break;
        }
    }

    // ════════════════════════════════════════════
    //  Broadcasting
    // ════════════════════════════════════════════

    private void BroadcastFullState()
    {
        var msg = new SheepHerderStateMessage
        {
            state = _state.ToString(),
            timer = Mathf.CeilToInt(_timer),
            sheepRemaining = _registry != null ? _registry.ActiveSheepCount : 0,
            sheepTotal = _sheepTotal,
            teamScore = _teamScore,
            mode = mode == Mode.Collaborative ? "collab" : "competitive",
            players = PlayerManager.Instance.GetAllPlayerInfos(),
        };
        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }

    private void BroadcastEvent(string eventName, string playerId, int remaining)
    {
        var msg = new SheepHerderEventMessage
        {
            eventName = eventName,
            playerId = playerId ?? "",
            sheepRemaining = remaining,
            teamScore = _teamScore,
        };
        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }
}
