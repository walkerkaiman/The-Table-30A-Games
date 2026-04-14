using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HotPotatoManager : RoundBasedGameSession<HotPotatoManager.PotatoState>
{
    [Header("Potato Settings")]
    [SerializeField] private int maxStrikes = 3;
    [SerializeField] private float minRoundTime = 5f;
    [SerializeField] private float maxRoundTime = 15f;
    [SerializeField] private float preRoundTime = 3f;
    [SerializeField] private float postRoundTime = 3f;
    [SerializeField] private float passCooldown = 0.3f;

    public enum PotatoState { PreRound, Playing, Exploded, RoundResults, GameOver }

    public override string GameType => "hot_potato";
    protected override PotatoState GameOverState => PotatoState.GameOver;

    private float _roundTimer;

    private readonly Dictionary<string, int> _strikes = new Dictionary<string, int>();
    private readonly Dictionary<string, bool> _alive = new Dictionary<string, bool>();
    private readonly Dictionary<string, int> _seatIndex = new Dictionary<string, int>();
    private List<string> _ring = new List<string>();
    private string _holderId;
    private float _lastPassTime;

    private void Start()
    {
        GameCoordinator.Instance.RegisterSession(this);
    }

    protected override void Update()
    {
        base.Update();

        if (_state == PotatoState.Playing)
        {
            _roundTimer -= Time.deltaTime;
            if (_roundTimer <= 0f)
            {
                _roundTimer = 0f;
                StopTimer();
                OnPotatoExploded();
            }
        }
    }

    // ════════════════════════════════════════════
    //  Session Lifecycle
    // ════════════════════════════════════════════

    public override void OnSessionStart(string[] playerIds)
    {
        _playerIds = playerIds;
        _currentRound = 0;

        foreach (string pid in playerIds)
        {
            _strikes[pid] = 0;
            _alive[pid] = true;
        }
        BuildRing();

        GameLog.Divider();
        GameLog.Game($"HOT POTATO — {playerIds.Length} players, {maxStrikes} strikes to eliminate");
        GameLog.Divider();

        BeginNextRound();
    }

    public override void OnPlayerDisconnected(string playerId)
    {
        PlayerManager.Instance.DisconnectPlayer(playerId);
        GameEvents.FirePlayerListChanged();
    }

    public override void OnGameMessage(string playerId, string messageType, string json)
    {
        if (messageType == "potato_pass")
        {
            var msg = JsonUtility.FromJson<PotatoPassMessage>(json);
            HandlePass(playerId, msg.direction);
        }
    }

    // ════════════════════════════════════════════
    //  Game Logic
    // ════════════════════════════════════════════

    private void HandlePass(string playerId, string direction)
    {
        if (_state != PotatoState.Playing) return;
        if (playerId != _holderId) return;
        if (Time.time - _lastPassTime < passCooldown) return;

        int idx = _ring.IndexOf(playerId);
        if (idx < 0) return;

        string targetId = null;
        switch (direction)
        {
            case "left":
                targetId = _ring[(idx - 1 + _ring.Count) % _ring.Count];
                break;
            case "right":
                targetId = _ring[(idx + 1) % _ring.Count];
                break;
            case "across":
                targetId = _ring[(idx + _ring.Count / 2) % _ring.Count];
                break;
        }

        if (targetId == null || targetId == playerId) return;

        _holderId = targetId;
        _lastPassTime = Time.time;

        string fromName = PlayerManager.Instance.GetPlayerName(playerId);
        string toName = PlayerManager.Instance.GetPlayerName(targetId);
        GameLog.Round($"\"{fromName}\" passed {direction} to \"{toName}\"");

        var evt = new PotatoEventMessage { eventName = "passed", fromId = playerId, toId = targetId };
        GameEvents.FireBroadcast(JsonUtility.ToJson(evt));
        BroadcastState();
    }

    private void OnPotatoExploded()
    {
        string name = PlayerManager.Instance.GetPlayerName(_holderId);
        _strikes.TryGetValue(_holderId, out int prevStrikes);
        _strikes[_holderId] = prevStrikes + 1;
        int strikes = _strikes[_holderId];

        GameLog.Round($"BOOM! \"{name}\" caught holding the potato! ({strikes}/{maxStrikes} strikes)");

        var evt = new PotatoEventMessage { eventName = "exploded", playerId = _holderId };
        GameEvents.FireBroadcast(JsonUtility.ToJson(evt));

        if (strikes >= maxStrikes)
        {
            _alive[_holderId] = false;
            GameLog.Round($"\"{name}\" is ELIMINATED!");
            var elimEvt = new PotatoEventMessage { eventName = "eliminated", playerId = _holderId };
            GameEvents.FireBroadcast(JsonUtility.ToJson(elimEvt));
        }

        StartTimer(postRoundTime);
        TransitionTo(PotatoState.Exploded);
    }

    // ════════════════════════════════════════════
    //  Base Class Hooks
    // ════════════════════════════════════════════

    protected override void OnRoundStart(int round)
    {
        BuildRing();
        int aliveCount = _ring.Count;

        if (aliveCount <= 1)
        {
            EndGame();
            return;
        }

        var rng = new System.Random();
        _holderId = _ring[rng.Next(_ring.Count)];
        _lastPassTime = 0f;

        float roundDuration = UnityEngine.Random.Range(minRoundTime, maxRoundTime);
        _roundTimer = roundDuration;

        string holderName = PlayerManager.Instance.GetPlayerName(_holderId);
        GameLog.Round($"{aliveCount} alive, \"{holderName}\" starts with potato ({roundDuration:F1}s fuse)");

        StartTimer(preRoundTime);
        TransitionTo(PotatoState.PreRound);
    }

    protected override void OnTimerExpired()
    {
        switch (_state)
        {
            case PotatoState.PreRound:
                TransitionTo(PotatoState.Playing);
                break;
            case PotatoState.Exploded:
                TransitionTo(PotatoState.RoundResults);
                StartTimer(postRoundTime);
                break;
            case PotatoState.RoundResults:
                BeginNextRound();
                break;
            case PotatoState.GameOver:
                CompleteSession();
                break;
        }
    }

    protected override void EndGame()
    {
        string winnerId = _alive.FirstOrDefault(kvp => kvp.Value).Key;
        if (!string.IsNullOrEmpty(winnerId))
            PlayerManager.Instance.AddScore(winnerId, 1000);

        foreach (string pid in _playerIds)
        {
            _strikes.TryGetValue(pid, out int pStrikes);
            int survived = _currentRound - pStrikes;
            if (survived > 0 && pid != winnerId)
                PlayerManager.Instance.AddScore(pid, survived * 100);
        }

        var standings = Leaderboard.GetSorted();
        Leaderboard.LogStandings("HOT POTATO", standings);

        StartTimer(gameOverDisplayTime);
        TransitionTo(PotatoState.GameOver);
    }

    // ════════════════════════════════════════════
    //  Ring Management
    // ════════════════════════════════════════════

    private void BuildRing()
    {
        _ring = _playerIds.Where(pid =>
        {
            _alive.TryGetValue(pid, out bool isAlive);
            return (isAlive || !_alive.ContainsKey(pid)) && PlayerManager.Instance.IsPlayerConnected(pid);
        }).ToList();
        for (int i = 0; i < _ring.Count; i++)
            _seatIndex[_ring[i]] = i;
    }

    // ════════════════════════════════════════════
    //  Broadcast
    // ════════════════════════════════════════════

    protected override void BroadcastState()
    {
        var playerStates = new PotatoPlayerState[_playerIds.Length];
        for (int i = 0; i < _playerIds.Length; i++)
        {
            string pid = _playerIds[i];
            playerStates[i] = new PotatoPlayerState
            {
                id = pid,
                name = PlayerManager.Instance.GetPlayerName(pid),
                strikes = _strikes.TryGetValue(pid, out int s) ? s : 0,
                alive = _alive.TryGetValue(pid, out bool a) ? a : true,
                hasPotato = pid == _holderId,
                seatIndex = _seatIndex.TryGetValue(pid, out int si) ? si : i
            };
        }

        var msg = new PotatoStateMessage
        {
            state = _state.ToString(),
            timer = Mathf.CeilToInt(_roundTimer),
            players = playerStates,
            holderId = _holderId ?? "",
            round = _currentRound,
            totalRounds = 0
        };
        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }
}
