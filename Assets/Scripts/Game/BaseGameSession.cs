using UnityEngine;

/// <summary>
/// Minimal MonoBehaviour base that provides timer, state machine, and IGameSession
/// wiring common to all game types. Does NOT impose round structure, leaderboards,
/// or auto-advance — those live in <see cref="RoundBasedGameSession{TState}"/>.
///
/// Games that don't fit the round model (Telephone, future sandbox games) can extend
/// this instead of reimplementing timer + state tracking.
/// </summary>
public abstract class BaseGameSession<TState> : MonoBehaviour, IGameSession
    where TState : System.Enum
{
    protected TState _state;
    protected string[] _playerIds;

    private float _timer;
    private bool _timerActive;
    private bool _timerCountsUp;
    protected int TimerCeil => Mathf.CeilToInt(_timer);
    /// <summary>
    /// Floor of the current timer for count-up displays (so a 3.9s elapsed timer reads "3").
    /// </summary>
    protected int TimerFloor => Mathf.FloorToInt(_timer);
    protected float TimerRaw => _timer;
    /// <summary>True if the timer is running in count-up mode (no expiration).</summary>
    protected bool IsTimerCountingUp => _timerActive && _timerCountsUp;

    private static readonly System.Collections.Generic.Dictionary<TState, string> _stateNames
        = new System.Collections.Generic.Dictionary<TState, string>();

    /// <summary>
    /// Returns the cached string name of the given enum value, avoiding
    /// per-call ToString() allocations on the broadcast path.
    /// </summary>
    protected static string StateToString(TState state)
    {
        if (!_stateNames.TryGetValue(state, out string name))
        {
            name = state.ToString();
            _stateNames[state] = name;
        }
        return name;
    }

    // ════════════════════════════════════════════
    //  Abstract — subclasses must implement
    // ════════════════════════════════════════════

    public abstract string GameType { get; }
    public string CurrentState => StateToString(_state);

    protected abstract void OnTimerExpired();
    protected abstract void BroadcastState();

    public abstract void OnSessionStart(string[] playerIds);
    public abstract void OnGameMessage(string playerId, string messageType, string json);

    // ════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════

    protected virtual void Update()
    {
        if (!_timerActive) return;
        if (_timerCountsUp)
        {
            // Count-up timer ticks forever — subclasses drive phase transitions themselves.
            _timer += Time.deltaTime;
            return;
        }
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timerActive = false;
            OnTimerExpired();
        }
    }

    // ════════════════════════════════════════════
    //  IGameSession defaults
    // ════════════════════════════════════════════

    public virtual void OnSessionEnd()
    {
        _timerActive = false;
        // If this session was using count-up mode, reset the shared display so the next
        // game doesn't inherit infinite count-up behaviour.
        if (_timerCountsUp)
        {
            _timerCountsUp = false;
            GameEvents.FireDisplayTimerCountUp(false);
        }
    }

    public virtual void OnPlayerRejoined(string playerId)
    {
        BroadcastState();
    }

    public virtual void OnPlayerDisconnected(string playerId) { }

    // ════════════════════════════════════════════
    //  Timer
    // ════════════════════════════════════════════

    /// <summary>
    /// Start a standard countdown. <see cref="OnTimerExpired"/> fires when it hits zero.
    /// </summary>
    protected void StartTimer(float seconds)
    {
        _timer = seconds;
        _timerActive = true;
        _timerCountsUp = false;
    }

    /// <summary>
    /// Start a count-up "elapsed" timer that never expires. Subclasses should drive phase
    /// transitions based on gameplay events (e.g. all puzzles solved) rather than the timer.
    /// Used by Treasure Hunter so the exploring/escape clock keeps running to ∞.
    /// </summary>
    protected void StartCountUpTimer(float startAt = 0f)
    {
        _timer = startAt;
        _timerActive = true;
        _timerCountsUp = true;
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
        GameEvents.FireDisplayState(GameType, CurrentState, TimerCeil);
    }

    protected void CompleteSession()
    {
        GameCoordinator.Instance.OnGameEnded();
    }
}
