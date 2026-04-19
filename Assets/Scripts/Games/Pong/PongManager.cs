using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// N-player Pong session orchestrator. Implements IGameSession.
/// Manages the game loop, ball lifecycle (spawn/move/respawn via your prefab),
/// lives, elimination, scoring, and state broadcasts.
/// Paddle visuals and movement are handled by the PlayerInputRelay.
/// Collision/bouncing is handled by Unity colliders you set up in the scene.
/// Goal detection is handled by your trigger scripts calling ScoreGoal().
/// </summary>
public class PongManager : MonoBehaviour, IGameSession
{
    public static PongManager Instance { get; private set; }

    [Header("Ball")]
    [Tooltip("Your ball prefab. Add Rigidbody2D + Collider2D for physics bouncing, or leave physics-free for manual control.")]
    [SerializeField] private GameObject ballPrefab;

    [Tooltip("Optional world-space spawn point for the ball. If empty, spawns at (0,0,0). " +
             "Use a Transform that is clear of walls/paddles so physics-overlap resolution " +
             "doesn't nudge the ball into a predictable direction on spawn.")]
    [SerializeField] private Transform ballSpawnPoint;

    [SerializeField] private float ballSpeed = 5f;
    [SerializeField] private float ballSpeedIncrement = 0.3f;
    [SerializeField] private float ballMaxSpeed = 15f;

    [Tooltip("If > 0, constrain launch angles so the ball is never launched within this many " +
             "degrees of the horizontal or vertical axes. Avoids boring near-straight lobs. " +
             "Leave at 0 for a fully uniform 360° launch.")]
    [Range(0f, 30f)]
    [SerializeField] private float minLaunchAxisDeviation = 0f;

    [Header("Game Settings")]
    [SerializeField] private int startLives = 3;
    [SerializeField] private float countdownSeconds = 3f;
    [SerializeField] private float goalPauseSeconds = 1.5f;
    [SerializeField] private float gameOverDisplaySeconds = 8f;
    [SerializeField] private int broadcastEveryNFrames = 3;

    public enum PongState { Countdown, Playing, GoalScored, GameOver }

    // IGameSession
    public string GameType => "pong";
    public string CurrentState => _state.ToString();

    public GameObject BallInstance => _ballInstance;

    private PongState _state = PongState.Countdown;
    private PlayerInputRelay _inputRelay;

    private string[] _playerIds;
    private readonly Dictionary<string, int> _playerLives = new Dictionary<string, int>();
    private readonly HashSet<string> _eliminatedPlayers = new HashSet<string>();

    private float _timer;
    private int _fixedFrameCount;
    private string _lastScoredOnId;

    private GameObject _ballInstance;
    private Vector2 _ballVelocity;
    private float _currentBallSpeed;
    private Rigidbody2D _ballRb;

    // ── Unity Lifecycle ──────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _inputRelay = GetComponentInChildren<PlayerInputRelay>();

        if (_inputRelay == null)
        {
            var relayGO = new GameObject("PlayerInputRelay");
            relayGO.transform.SetParent(transform);
            _inputRelay = relayGO.AddComponent<PlayerInputRelay>();
        }

        GameCoordinator.Instance.RegisterSession(this);
    }

    private void FixedUpdate()
    {
        if (_state == PongState.Playing)
        {
            MoveBall();

            _fixedFrameCount++;
            if (_fixedFrameCount % broadcastEveryNFrames == 0)
                BroadcastPongFrame();
        }
    }

    private void Update()
    {
        if (_state == PongState.Countdown || _state == PongState.GoalScored || _state == PongState.GameOver)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                OnTimerExpired();
        }
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

        GameLog.Divider();
        GameLog.Game($"PONG — {playerIds.Length} players, {startLives} lives each");
        GameLog.Divider();

        _playerLives.Clear();
        _eliminatedPlayers.Clear();

        foreach (string id in playerIds)
            _playerLives[id] = startLives;

        _inputRelay.Initialize(playerIds);
        AssignGoalZones(playerIds);

        _currentBallSpeed = ballSpeed;

        _state = PongState.Countdown;
        _timer = countdownSeconds;

        BroadcastFullState();
    }

    public void OnSessionEnd()
    {
        _state = PongState.GameOver;
        DestroyBall();
        _inputRelay.Teardown();
    }

    public void OnPlayerRejoined(string playerId)
    {
        BroadcastFullState();
    }

    public void OnPlayerDisconnected(string playerId)
    {
        PlayerManager.Instance.DisconnectPlayer(playerId);
        GameLog.Player($"\"{PlayerManager.Instance.GetPlayerName(playerId)}\" disconnected from Pong");
        GameEvents.FirePlayerListChanged();
    }

    public void OnGameMessage(string playerId, string messageType, string json)
    {
        // Paddle input handled by PlayerInputRelay via GameEvents.
    }

    // ════════════════════════════════════════════
    //  GOAL ZONE ASSIGNMENT
    // ════════════════════════════════════════════

    private void AssignGoalZones(string[] playerIds)
    {
        var goals = FindObjectsByType<PongGoalZone>();
        if (goals.Length == 0)
        {
            GameLog.Game("WARNING: No PongGoalZone objects found in scene — goals won't register!");
            return;
        }

        foreach (var goal in goals)
            goal.assignedPlayerId = null;

        var unassignedPlayers = new List<string>();

        foreach (string id in playerIds)
        {
            int side = PlayerManager.Instance.GetTableSide(id);
            bool assigned = false;
            foreach (var goal in goals)
            {
                if (goal.tableSide == side && string.IsNullOrEmpty(goal.assignedPlayerId))
                {
                    goal.assignedPlayerId = id;
                    string name = PlayerManager.Instance.GetPlayerName(id);
                    GameLog.Game($"Goal zone \"{goal.gameObject.name}\" → \"{name}\" (side {side})");
                    assigned = true;
                    break;
                }
            }
            if (!assigned)
                unassignedPlayers.Add(id);
        }

        foreach (string id in unassignedPlayers)
        {
            foreach (var goal in goals)
            {
                if (string.IsNullOrEmpty(goal.assignedPlayerId))
                {
                    goal.assignedPlayerId = id;
                    string name = PlayerManager.Instance.GetPlayerName(id);
                    GameLog.Game($"Goal zone \"{goal.gameObject.name}\" → \"{name}\" (fallback — no side match)");
                    break;
                }
            }
        }
    }

    // ════════════════════════════════════════════
    //  BALL LIFECYCLE
    // ════════════════════════════════════════════

    private void SpawnBall()
    {
        DestroyBall();

        if (ballPrefab == null)
        {
            GameLog.Game("WARNING: ballPrefab is not assigned on PongManager!");
            return;
        }

        Vector3 spawnPos = ballSpawnPoint != null ? ballSpawnPoint.position : Vector3.zero;
        _ballInstance = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
        _ballInstance.name = "PongBall";

        float angleDeg = PickLaunchAngleDegrees();
        float angleRad = angleDeg * Mathf.Deg2Rad;
        _ballVelocity = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * _currentBallSpeed;

        _ballRb = _ballInstance.GetComponent<Rigidbody2D>();
        if (_ballRb != null)
        {
            // Defensively force-configure in case the prefab gets edited in a way that would
            // otherwise swallow the launch (kinematic body, gravity, spin, or a stale velocity).
            _ballRb.bodyType = RigidbodyType2D.Dynamic;
            _ballRb.gravityScale = 0f;
            _ballRb.angularVelocity = 0f;
            _ballRb.linearVelocity = _ballVelocity;
        }

        GameLog.Game($"Ball spawned — angle {angleDeg:F0}°  speed {_currentBallSpeed:F1}");
    }

    /// <summary>
    /// Picks a uniformly random launch direction in degrees (0-360). If
    /// <see cref="minLaunchAxisDeviation"/> is non-zero, re-rolls until the chosen angle is at
    /// least that many degrees away from every cardinal axis (0°, 90°, 180°, 270°) so the ball
    /// never launches in a near-horizontal or near-vertical line.
    /// </summary>
    private float PickLaunchAngleDegrees()
    {
        const int maxAttempts = 16;
        float buffer = Mathf.Clamp(minLaunchAxisDeviation, 0f, 30f);

        for (int i = 0; i < maxAttempts; i++)
        {
            float a = Random.Range(0f, 360f);
            if (buffer <= 0f) return a;

            float aMod = a % 90f;
            if (aMod >= buffer && aMod <= 90f - buffer)
                return a;
        }
        return Random.Range(0f, 360f);
    }

    private void DestroyBall()
    {
        if (_ballInstance != null)
        {
            Destroy(_ballInstance);
            _ballInstance = null;
            _ballRb = null;
        }
    }

    private void MoveBall()
    {
        if (_ballInstance == null) return;
        if (_ballRb != null) return; // Rigidbody2D handles movement
        _ballInstance.transform.position += (Vector3)_ballVelocity * Time.fixedDeltaTime;
    }

    /// <summary>
    /// Destroy and re-instantiate the ball at center with a new random direction.
    /// Useful if the ball gets stuck or you need a manual reset.
    /// </summary>
    public void RespawnBall()
    {
        SpawnBall();
    }

    // ════════════════════════════════════════════
    //  PUBLIC SCORING API
    // ════════════════════════════════════════════

    /// <summary>
    /// Call this from your goal-zone trigger scripts when the ball enters a
    /// player's goal area. The manager handles lives, elimination, speed
    /// increase, state transitions, and broadcasts.
    /// </summary>
    public void ScoreGoal(string scoredOnPlayerId)
    {
        if (_state != PongState.Playing) return;
        if (string.IsNullOrEmpty(scoredOnPlayerId)) return;
        if (!_playerLives.ContainsKey(scoredOnPlayerId)) return;
        if (_eliminatedPlayers.Contains(scoredOnPlayerId)) return;

        _playerLives[scoredOnPlayerId]--;
        _lastScoredOnId = scoredOnPlayerId;
        string name = PlayerManager.Instance.GetPlayerName(scoredOnPlayerId);
        int livesLeft = _playerLives[scoredOnPlayerId];

        GameLog.Game($"GOAL on \"{name}\" — {livesLeft} lives left");

        BroadcastPongEvent("goal", null, scoredOnPlayerId, livesLeft);

        if (livesLeft <= 0)
        {
            _eliminatedPlayers.Add(scoredOnPlayerId);
            GameLog.Game($"\"{name}\" ELIMINATED");
            BroadcastPongEvent("eliminated", scoredOnPlayerId, null, 0);

            int alive = _playerIds.Count(id => !_eliminatedPlayers.Contains(id));
            if (alive <= 1)
            {
                string winnerId = _playerIds.FirstOrDefault(id => !_eliminatedPlayers.Contains(id));
                HandleWinner(winnerId);
                return;
            }
        }

        DestroyBall();

        _currentBallSpeed = Mathf.Min(_currentBallSpeed + ballSpeedIncrement, ballMaxSpeed);

        _state = PongState.GoalScored;
        _timer = goalPauseSeconds;
        BroadcastFullState();
    }

    // ════════════════════════════════════════════
    //  GAME LOGIC
    // ════════════════════════════════════════════

    private void HandleWinner(string winnerId)
    {
        string name = winnerId != null ? PlayerManager.Instance.GetPlayerName(winnerId) : "Nobody";
        GameLog.Divider();
        GameLog.Game($"══ PONG WINNER: \"{name}\" ══");
        GameLog.Divider();

        if (winnerId != null)
            PlayerManager.Instance.AddScore(winnerId, 1000);

        DestroyBall();
        BroadcastPongEvent("winner", winnerId, null, 0);

        _state = PongState.GameOver;
        _timer = gameOverDisplaySeconds;
        BroadcastFullState();
    }

    private void OnTimerExpired()
    {
        switch (_state)
        {
            case PongState.Countdown:
                _state = PongState.Playing;
                _fixedFrameCount = 0;
                SpawnBall();
                GameLog.Game("PONG — GO!");
                BroadcastFullState();
                break;

            case PongState.GoalScored:
                SpawnBall();
                _state = PongState.Playing;
                _fixedFrameCount = 0;
                BroadcastFullState();
                break;

            case PongState.GameOver:
                GameCoordinator.Instance.OnGameEnded();
                break;
        }
    }

    // ════════════════════════════════════════════
    //  BROADCASTING
    // ════════════════════════════════════════════

    private void BroadcastFullState()
    {
        var msg = new GameStateMessage
        {
            gameType = "pong",
            state = _state.ToString(),
            timer = Mathf.CeilToInt(_timer),
            players = PlayerManager.Instance.GetAllPlayerInfos()
        };
        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));

        BroadcastPongFrame();
    }

    private void BroadcastPongFrame()
    {
        var paddles = new List<PongPaddleState>();
        for (int i = 0; i < _playerIds.Length; i++)
        {
            string id = _playerIds[i];
            paddles.Add(new PongPaddleState
            {
                id = id,
                position = _inputRelay.GetRawInput(id),
                side = i,
                lives = _playerLives.ContainsKey(id) ? _playerLives[id] : 0
            });
        }

        Vector3 ballPos = _ballInstance != null ? _ballInstance.transform.position : Vector3.zero;

        var frame = new PongFrameMessage
        {
            bx = ballPos.x,
            by = ballPos.y,
            paddles = paddles.ToArray()
        };
        GameEvents.FireBroadcast(JsonUtility.ToJson(frame));
    }

    private void BroadcastPongEvent(string eventName, string playerId, string scoredOn, int livesLeft)
    {
        var msg = new PongEventMessage
        {
            eventName = eventName,
            playerId = playerId ?? "",
            scoredOn = scoredOn ?? "",
            livesLeft = livesLeft
        };
        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }
}
