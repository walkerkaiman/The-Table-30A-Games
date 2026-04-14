using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpeedDrawManager : RoundBasedGameSession<SpeedDrawManager.DrawState>
{
    [Header("Phase Timers")]
    [SerializeField] private float planSeconds = 10f;
    [SerializeField] private float drawSeconds = 3f;
    [SerializeField] private float guessSeconds = 15f;
    [SerializeField] private float resultsDisplayTime = 8f;

    [Header("Scoring")]
    [SerializeField] private int allCorrectPoints = 300;
    [SerializeField] private int majorityCorrectPoints = 200;
    [SerializeField] private int minorityCorrectPoints = 100;
    [SerializeField] private int contributionBonus = 50;
    [SerializeField] private int minStrokesForBonus = 2;

    public enum DrawState { ShowPrompt, Plan, Draw, Guess, RoundResults, GameOver }

    public override string GameType => "speed_draw";
    protected override DrawState GameOverState => DrawState.GameOver;

    private GameContentLoader<SpeedDrawPrompt> _contentLoader;
    private SpeedDrawPrompt _currentPrompt;
    private readonly Dictionary<string, int> _strokeCounts = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _guesses = new Dictionary<string, int>();
    private SpeedDrawLabelInfo[] _labels;

    private void Start()
    {
        _contentLoader = new GameContentLoader<SpeedDrawPrompt>("speed_draw");
        _contentLoader.Load("prompts.json", "prompts");
        GameCoordinator.Instance.RegisterSession(this);
    }

    public override void OnSessionStart(string[] playerIds)
    {
        _contentLoader.Reset();
        base.OnSessionStart(playerIds);
    }

    public override void OnPlayerDisconnected(string playerId)
    {
        base.OnPlayerDisconnected(playerId);
        TryAutoAdvance();
    }

    public override void OnGameMessage(string playerId, string messageType, string json)
    {
        switch (messageType)
        {
            case "draw_stroke":
                HandleStroke(playerId, json);
                break;
            case "draw_guess":
                HandleGuess(playerId, JsonUtility.FromJson<DrawGuessMessage>(json).choiceIndex);
                break;
        }
    }

    // ════════════════════════════════════════════
    //  Game Logic
    // ════════════════════════════════════════════

    private void HandleStroke(string playerId, string json)
    {
        if (_state != DrawState.Draw) return;

        var msg = JsonUtility.FromJson<DrawStrokeMessage>(json);
        if (msg.points == null || msg.points.Length < 4) return;

        _strokeCounts.TryGetValue(playerId, out int prev);
        _strokeCounts[playerId] = prev + 1;

        var broadcast = new DrawStrokeBroadcast
        {
            playerId = playerId,
            points = msg.points,
            color = msg.color ?? "#ffffff"
        };
        GameEvents.FireBroadcast(JsonUtility.ToJson(broadcast));
    }

    private void HandleGuess(string playerId, int choiceIndex)
    {
        if (_state != DrawState.Guess) return;
        if (_guesses.ContainsKey(playerId)) return;
        if (_labels == null || choiceIndex < 0 || choiceIndex >= _labels.Length) return;

        _guesses[playerId] = choiceIndex;
        string name = PlayerManager.Instance.GetPlayerName(playerId);
        GameLog.Round($"Guess from \"{name}\"  ({_guesses.Count}/{PlayerManager.Instance.ActivePlayerCount})");

        GameEvents.FireSendToPlayer(playerId, JsonUtility.ToJson(new ConfirmationMessage { type = MessageTypes.GuessReceived }));
        TryAutoAdvance();
    }

    // ════════════════════════════════════════════
    //  Base Class Hooks
    // ════════════════════════════════════════════

    protected override void OnRoundStart(int round)
    {
        _strokeCounts.Clear();
        _guesses.Clear();
        _labels = null;

        _currentPrompt = _contentLoader.HasItems
            ? _contentLoader.GetNext()
            : new SpeedDrawPrompt { text = "A cat wearing sunglasses", decoyA = "A dog in a hat", decoyB = "A fish on a bicycle" };

        GameLog.Round($"Prompt: \"{_currentPrompt.text}\"");

        StartTimer(2f);
        TransitionTo(DrawState.ShowPrompt);
    }

    protected override void OnTimerExpired()
    {
        switch (_state)
        {
            case DrawState.ShowPrompt:
                StartTimer(planSeconds);
                TransitionTo(DrawState.Plan);
                break;
            case DrawState.Plan:
                StartTimer(drawSeconds);
                TransitionTo(DrawState.Draw);
                break;
            case DrawState.Draw:
                BuildLabels();
                StartTimer(guessSeconds);
                TransitionTo(DrawState.Guess);
                break;
            case DrawState.Guess:
                TallyAndShowResults();
                break;
            case DrawState.RoundResults:
                BeginNextRound();
                break;
            case DrawState.GameOver:
                CompleteSession();
                break;
        }
    }

    protected override void TryAutoAdvance()
    {
        int active = PlayerManager.Instance.ActivePlayerCount;
        if (active == 0) return;
        if (_state == DrawState.Guess && _guesses.Count >= active)
        {
            StopTimer();
            TallyAndShowResults();
        }
    }

    // ════════════════════════════════════════════
    //  Scoring & Results
    // ════════════════════════════════════════════

    private void BuildLabels()
    {
        var list = new List<SpeedDrawLabelInfo>
        {
            new SpeedDrawLabelInfo { text = _currentPrompt.text, isCorrect = true },
            new SpeedDrawLabelInfo { text = _currentPrompt.decoyA, isCorrect = false },
            new SpeedDrawLabelInfo { text = _currentPrompt.decoyB, isCorrect = false }
        };
        list.Shuffle();
        _labels = list.ToArray();
    }

    private void TallyAndShowResults()
    {
        int correctCount = _guesses.Values.Count(vi => _labels[vi].isCorrect);
        int totalGuesses = _guesses.Count;

        int basePoints = 0;
        if (totalGuesses > 0)
        {
            float ratio = (float)correctCount / totalGuesses;
            if (ratio >= 1f) basePoints = allCorrectPoints;
            else if (ratio > 0.5f) basePoints = majorityCorrectPoints;
            else if (correctCount > 0) basePoints = minorityCorrectPoints;
        }

        foreach (string pid in _playerIds)
        {
            if (!PlayerManager.Instance.IsPlayerConnected(pid)) continue;
            int earned = basePoints;
            _strokeCounts.TryGetValue(pid, out int strokes);
            if (strokes >= minStrokesForBonus)
                earned += contributionBonus;
            if (earned > 0)
                PlayerManager.Instance.AddScore(pid, earned);
        }

        GameLog.Round($"── SPEED DRAW RESULTS ── correct: {correctCount}/{totalGuesses}, base: {basePoints}");

        StartTimer(resultsDisplayTime);
        TransitionTo(DrawState.RoundResults);
    }

    // ════════════════════════════════════════════
    //  Broadcast
    // ════════════════════════════════════════════

    protected override void BroadcastState()
    {
        var h = BuildHeader();
        var msg = new SpeedDrawStateMessage
        {
            state = h.state,
            round = h.round,
            totalRounds = h.totalRounds,
            timer = h.timer,
            players = h.players,
            prompt = _currentPrompt?.text ?? ""
        };

        if (_state == DrawState.Guess || _state == DrawState.RoundResults || _state == DrawState.GameOver)
        {
            var stripped = new SpeedDrawLabelInfo[_labels?.Length ?? 0];
            for (int i = 0; i < stripped.Length; i++)
            {
                stripped[i] = new SpeedDrawLabelInfo { text = _labels[i].text };
                if (_state != DrawState.Guess) stripped[i].isCorrect = _labels[i].isCorrect;
            }
            msg.labels = stripped;
        }

        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }
}

[Serializable]
public class SpeedDrawPrompt
{
    public string id;
    public string text;
    public string decoyA;
    public string decoyB;
}

[Serializable]
public class SpeedDrawPromptWrapper
{
    public SpeedDrawPrompt[] prompts;
}
