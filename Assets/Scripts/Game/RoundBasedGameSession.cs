using System;
using UnityEngine;

/// <summary>
/// Base class for round-based party games that share timer, state machine,
/// round flow, disconnect handling, and game-over logic.
///
/// Subclasses define their own enum for TState and implement the abstract hooks.
/// Pong (frame-driven, no rounds) intentionally does NOT inherit from this.
///
/// Extends <see cref="BaseGameSession{TState}"/> which provides timer and state
/// machine plumbing; this class adds round counting, leaderboard scoring, and
/// auto-advance hooks.
/// </summary>
public abstract class RoundBasedGameSession<TState> : BaseGameSession<TState>
    where TState : Enum
{
    [Header("Round Settings")]
    [SerializeField] protected int maxRounds = 5;
    [SerializeField] protected float gameOverDisplayTime = 12f;

    protected int _currentRound;

    // ════════════════════════════════════════════
    //  Abstract — subclasses must implement
    // ════════════════════════════════════════════

    /// <summary>The enum value representing the game-over state (e.g. GameOver).</summary>
    protected abstract TState GameOverState { get; }

    /// <summary>Called at the start of each new round after _currentRound is incremented.
    /// Subclass should clear per-round data, pick content, and TransitionTo the first phase.</summary>
    protected abstract void OnRoundStart(int round);

    // ════════════════════════════════════════════
    //  IGameSession — sensible defaults
    // ════════════════════════════════════════════

    public override void OnSessionStart(string[] playerIds)
    {
        _playerIds = playerIds;
        _currentRound = 0;

        GameLog.Divider();
        GameLog.Game($"{GameType.ToUpper()} — {playerIds.Length} players, {maxRounds} rounds");
        GameLog.Divider();

        BeginNextRound();
    }

    public override void OnPlayerDisconnected(string playerId)
    {
        TryAutoAdvance();
    }

    // ════════════════════════════════════════════
    //  Header Builder
    // ════════════════════════════════════════════

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

    // ════════════════════════════════════════════
    //  Auto-Advance Hook
    // ════════════════════════════════════════════

    /// <summary>
    /// Override to check if all expected submissions are in and auto-advance.
    /// Default does nothing (games like HotPotato have no submission auto-advance).
    /// </summary>
    protected virtual void TryAutoAdvance() { }
}
