using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// N-player Pong game session. Implements IGameSession.
/// Manages the game loop, lives, elimination, paddle input, and state broadcasts.
/// Lives in its own scene; registers with GameCoordinator on Start().
/// </summary>
public class PongManager : MonoBehaviour, IGameSession
{
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

    private PongState _state = PongState.Countdown;
    private PongArena _arena;
    private PongBall _ball;

    private string[] _playerIds;
    private readonly Dictionary<string, int> _playerLives = new Dictionary<string, int>();
    private readonly Dictionary<string, float> _paddleInput = new Dictionary<string, float>();
    private readonly HashSet<string> _eliminatedPlayers = new HashSet<string>();

    private float _timer;
    private int _fixedFrameCount;
    private string _lastScoredOnId;

    // ── Unity Lifecycle ──────────────────────────

    private void Start()
    {
        _arena = GetComponentInChildren<PongArena>();
        _ball = GetComponentInChildren<PongBall>();

        if (_arena == null)
        {
            var arenaGO = new GameObject("PongArena");
            arenaGO.transform.SetParent(transform);
            _arena = arenaGO.AddComponent<PongArena>();
        }
        if (_ball == null)
        {
            var ballGO = new GameObject("PongBall");
            ballGO.transform.SetParent(transform);
            _ball = ballGO.AddComponent<PongBall>();
        }

        GameCoordinator.Instance.RegisterSession(this);
    }

    private void FixedUpdate()
    {
        if (_state == PongState.Playing)
        {
            ApplyPaddleInput();
            _ball.Tick(Time.fixedDeltaTime);

            if (_ball.HitOccurred && !_ball.HitPaddle)
                HandleGoal(_ball.HitSideIndex);

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
        _paddleInput.Clear();
        _eliminatedPlayers.Clear();

        foreach (string id in playerIds)
        {
            _playerLives[id] = startLives;
            _paddleInput[id] = 0.5f;
        }

        _arena.Build(playerIds);
        _ball.Init(_arena);
        _ball.ResetBall();

        _state = PongState.Countdown;
        _timer = countdownSeconds;

        BroadcastFullState();
    }

    public void OnSessionEnd()
    {
        _state = PongState.GameOver;
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
        if (messageType == "paddle_move")
        {
            var msg = JsonUtility.FromJson<PaddleMoveMessage>(json);
            _paddleInput[playerId] = Mathf.Clamp01(msg.position);
        }
    }

    // ════════════════════════════════════════════
    //  GAME LOGIC
    // ════════════════════════════════════════════

    private void ApplyPaddleInput()
    {
        foreach (var kvp in _paddleInput)
        {
            int side = _arena.GetSideForPlayerId(kvp.Key);
            if (side >= 0)
                _arena.SetPaddlePosition(side, kvp.Value);
        }
    }

    private void HandleGoal(int sideIndex)
    {
        string scoredOnId = _arena.GetPlayerIdForSide(sideIndex);
        if (scoredOnId == null) return;

        if (!_playerLives.ContainsKey(scoredOnId) || _eliminatedPlayers.Contains(scoredOnId)) return;

        _playerLives[scoredOnId]--;
        _lastScoredOnId = scoredOnId;
        string name = PlayerManager.Instance.GetPlayerName(scoredOnId);
        int livesLeft = _playerLives[scoredOnId];

        GameLog.Game($"GOAL on \"{name}\" — {livesLeft} lives left");

        BroadcastPongEvent("goal", null, scoredOnId, livesLeft);

        if (livesLeft <= 0)
        {
            _eliminatedPlayers.Add(scoredOnId);
            _arena.EliminateSide(sideIndex);
            GameLog.Game($"\"{name}\" ELIMINATED");
            BroadcastPongEvent("eliminated", scoredOnId, null, 0);

            int alive = _playerIds.Count(id => !_eliminatedPlayers.Contains(id));
            if (alive <= 1)
            {
                string winnerId = _playerIds.FirstOrDefault(id => !_eliminatedPlayers.Contains(id));
                HandleWinner(winnerId);
                return;
            }
        }

        _state = PongState.GoalScored;
        _timer = goalPauseSeconds;
        _ball.IncreaseSpeed();
        BroadcastFullState();
    }

    private void HandleWinner(string winnerId)
    {
        string name = winnerId != null ? PlayerManager.Instance.GetPlayerName(winnerId) : "Nobody";
        GameLog.Divider();
        GameLog.Game($"══ PONG WINNER: \"{name}\" ══");
        GameLog.Divider();

        if (winnerId != null)
            PlayerManager.Instance.AddScore(winnerId, 1000);

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
                GameLog.Game("PONG — GO!");
                BroadcastFullState();
                break;

            case PongState.GoalScored:
                _ball.ResetBall();
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
        foreach (string id in _playerIds)
        {
            int side = _arena.GetSideForPlayerId(id);
            paddles.Add(new PongPaddleState
            {
                id = id,
                position = _paddleInput.ContainsKey(id) ? _paddleInput[id] : 0.5f,
                side = side,
                lives = _playerLives.ContainsKey(id) ? _playerLives[id] : 0
            });
        }

        var frame = new PongFrameMessage
        {
            bx = _ball.Position.x,
            by = _ball.Position.y,
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
