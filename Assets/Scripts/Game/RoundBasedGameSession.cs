using System;
using UnityEngine;

/// <summary>
/// Base class for round-based party games that share timer, state machine,
/// round flow, disconnect handling, and game-over logic.
///
/// Subclasses define their own enum for TState and implement the abstract hooks.
/// Pong (frame-driven, no rounds) intentionally does NOT inherit from this.
/// </summary>
public abstract class RoundBasedGameSession<TState> : MonoBehaviour, IGameSession
    where TState : Enum
{
    [Header("Round Settings")]
    [SerializeField] protected int maxRounds = 5;
    [SerializeField] protected float gameOverDisplayTime = 12f;

    protected TState _state;
    protected int _currentRound;
    protected string[] _playerIds;

    // ── Timer ────────────────────────────────────
    private float _timer;
    private bool _timerActive;
    protected int TimerCeil => Mathf.CeilToInt(_timer);
    protected float TimerRaw => _timer;

    // ════════════════════════════════════════════
    //  Abstract — subclasses must implement
    // ════════════════════════════════════════════

    public abstract string GameType { get; }
    public string CurrentState => _state.ToString();

    /// <summary>The enum value representing the game-over state (e.g. GameOver).</summary>
    protected abstract TState GameOverState { get; }

    /// <summary>Called each time the phase timer expires. Subclass defines phase transitions.</summary>
    protected abstract void OnTimerExpired();

    /// <summary>Serialize and broadcast the current game state to all clients.</summary>
    protected abstract void BroadcastState();

    /// <summary>Called at the start of each new round after _currentRound is incremented.
    /// Subclass should clear per-round data, pick content, and TransitionTo the first phase.</summary>
    protected abstract void OnRoundStart(int round);

    /// <summary>Route an incoming player message to game-specific handlers.</summary>
    public abstract void OnGameMessage(string playerId, string messageType, string json);

    // ════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════

    protected virtual void Update()
    {
        if (!_timerActive) return;
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timerActive = false;
            OnTimerExpired();
        }
    }

    // ════════════════════════════════════════════
    //  IGameSession — sensible defaults
    // ════════════════════════════════════════════

    public virtual void OnSessionStart(string[] playerIds)
    {
        _playerIds = playerIds;
        _currentRound = 0;

        GameLog.Divider();
        GameLog.Game($"{GameType.ToUpper()} — {playerIds.Length} players, {maxRounds} rounds");
        GameLog.Divider();

        BeginNextRound();
    }

    public virtual void OnSessionEnd()
    {
        _timerActive = false;
    }

    public virtual void OnPlayerRejoined(string playerId)
    {
        BroadcastState();
    }

    public virtual void OnPlayerDisconnected(string playerId)
    {
        PlayerManager.Instance.DisconnectPlayer(playerId);
        GameEvents.FirePlayerListChanged();
        TryAutoAdvance();
    }

    // ════════════════════════════════════════════
    //  Timer
    // ════════════════════════════════════════════

    protected void StartTimer(float seconds)
    {
        _timer = seconds;
        _timerActive = true;
    }

    protected void StopTimer()
    {
        _timerActive = false;
    }

    // ════════════════════════════════════════════
    //  State Machine
    // ════════════════════════════════════════════

    protected void TransitionTo(TState newState)
    {
        _state = newState;
        GameLog.State($"{GameType} → {newState}");
        BroadcastState();
    }

    /// <summary>
    /// Builds a pre-filled GameStateHeader with the common fields.
    /// Subclasses compose this into their game-specific message classes.
    /// </summary>
    protected GameStateHeader BuildHeader()
    {
        return new GameStateHeader
        {
            type = MessageTypes.GameState,
            gameType = GameType,
            state = CurrentState,
            timer = TimerCeil,
            round = _currentRound,
            totalRounds = maxRounds,
            players = PlayerManager.Instance.GetAllPlayerInfos()
        };
    }

    /// <summary>
    /// Broadcasts a GameStateHeader directly (when no extra fields are needed).
    /// </summary>
    protected void BroadcastHeader()
    {
        GameEvents.FireBroadcast(JsonUtility.ToJson(BuildHeader()));
    }

    // ════════════════════════════════════════════
    //  Round Flow
    // ════════════════════════════════════════════

    protected virtual void BeginNextRound()
    {
        _currentRound++;
        if (_currentRound > maxRounds)
        {
            EndGame();
            return;
        }

        GameLog.Divider();
        GameLog.Round($"══ ROUND {_currentRound} / {maxRounds} ══");

        OnRoundStart(_currentRound);
    }

    protected virtual void EndGame()
    {
        var standings = Leaderboard.GetSorted();
        Leaderboard.LogStandings(GameType.ToUpper(), standings);

        StartTimer(gameOverDisplayTime);
        TransitionTo(GameOverState);
    }

    protected void CompleteSession()
    {
        GameCoordinator.Instance.OnGameEnded();
    }

    // ════════════════════════════════════════════
    //  Auto-Advance Hook
    // ════════════════════════════════════════════

    /// <summary>
    /// Override to check if all expected submissions are in and auto-advance.
    /// Default does nothing (games like HotPotato have no submission auto-advance).
    /// </summary>
    protected virtual void TryAutoAdvance() { }
}
