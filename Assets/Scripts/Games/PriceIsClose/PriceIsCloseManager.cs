using System;
using System.Collections.Generic;
using UnityEngine;

public class PriceIsCloseManager : RoundBasedGameSession<PriceIsCloseManager.PriceState>
{
    [Header("Phase Timers")]
    [SerializeField] private float itemDisplayTime = 5f;
    [SerializeField] private float guessTimerSeconds = 30f;
    [SerializeField] private float resultsDisplayTime = 10f;

    [Header("Scoring")]
    [SerializeField] private int exactPoints = 1000;
    [SerializeField] private int within5Points = 700;
    [SerializeField] private int within10Points = 400;
    [SerializeField] private int closestBonusPoints = 200;

    public enum PriceState { ShowItem, Guess, Reveal, RoundResults, GameOver }

    public override string GameType => "price_is_close";
    protected override PriceState GameOverState => PriceState.GameOver;

    private GameContentLoader<PriceItem> _contentLoader;
    private PriceItem _currentItem;
    private readonly Dictionary<string, float> _guesses = new Dictionary<string, float>();
    private PriceGuessResult[] _currentResults;

    private void Start()
    {
        _contentLoader = new GameContentLoader<PriceItem>("price_is_close");
        _contentLoader.Load("items.json", "items");
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
        if (messageType == "submit_guess")
        {
            var msg = JsonUtility.FromJson<SubmitGuessMessage>(json);
            HandleGuess(playerId, msg.guess);
        }
    }

    // ════════════════════════════════════════════
    //  Game Logic
    // ════════════════════════════════════════════

    private void HandleGuess(string playerId, float guess)
    {
        if (_state != PriceState.Guess) return;
        if (_guesses.ContainsKey(playerId)) return;

        _guesses[playerId] = guess;
        string name = PlayerManager.Instance.GetPlayerName(playerId);
        GameLog.Round($"Guess from \"{name}\": {guess}  ({_guesses.Count}/{PlayerManager.Instance.ActivePlayerCount})");

        GameEvents.FireSendToPlayer(playerId, JsonUtility.ToJson(new ConfirmationMessage { type = MessageTypes.GuessReceived }));
        TryAutoAdvance();
    }

    // ════════════════════════════════════════════
    //  Base Class Hooks
    // ════════════════════════════════════════════

    protected override void OnRoundStart(int round)
    {
        _guesses.Clear();
        _currentResults = null;

        _currentItem = _contentLoader.HasItems
            ? _contentLoader.GetNext()
            : new PriceItem { id = "fallback", title = "Mystery Item", correctPrice = 100, unit = "$" };

        GameLog.Round($"Item: \"{_currentItem.title}\" — correct: {_currentItem.unit}{_currentItem.correctPrice}");

        StartTimer(itemDisplayTime);
        TransitionTo(PriceState.ShowItem);
    }

    protected override void OnTimerExpired()
    {
        switch (_state)
        {
            case PriceState.ShowItem:
                StartTimer(guessTimerSeconds);
                TransitionTo(PriceState.Guess);
                break;
            case PriceState.Guess:
                ScoreAndReveal();
                break;
            case PriceState.Reveal:
                StartTimer(resultsDisplayTime);
                TransitionTo(PriceState.RoundResults);
                break;
            case PriceState.RoundResults:
                BeginNextRound();
                break;
            case PriceState.GameOver:
                CompleteSession();
                break;
        }
    }

    protected override void TryAutoAdvance()
    {
        int active = PlayerManager.Instance.ActivePlayerCount;
        if (active == 0) return;
        if (_state == PriceState.Guess && _guesses.Count >= active)
        {
            StopTimer();
            ScoreAndReveal();
        }
    }

    // ════════════════════════════════════════════
    //  Scoring & Results
    // ════════════════════════════════════════════

    private void ScoreAndReveal()
    {
        float correct = _currentItem.correctPrice;
        var results = new List<PriceGuessResult>();

        float closestDiff = float.MaxValue;
        string closestId = null;

        foreach (var kvp in _guesses)
        {
            float diff = Mathf.Abs(kvp.Value - correct);
            float pct = correct > 0 ? diff / correct : (diff == 0 ? 0 : 1);

            int score = 0;
            if (diff == 0) score = exactPoints;
            else if (pct <= 0.05f) score = within5Points;
            else if (pct <= 0.10f) score = within10Points;

            if (diff < closestDiff) { closestDiff = diff; closestId = kvp.Key; }

            results.Add(new PriceGuessResult
            {
                id = kvp.Key,
                name = PlayerManager.Instance.GetPlayerName(kvp.Key),
                guess = kvp.Value,
                correctPrice = correct,
                score = score
            });
        }

        if (!string.IsNullOrEmpty(closestId))
        {
            var winner = results.Find(r => r.id == closestId);
            if (winner != null) winner.score += closestBonusPoints;
        }

        foreach (var r in results)
        {
            if (r.score > 0) PlayerManager.Instance.AddScore(r.id, r.score);
        }

        results.Sort((a, b) => b.score.CompareTo(a.score));
        _currentResults = results.ToArray();

        GameLog.Round($"── PRICE IS CLOSE RESULTS ── correct: {_currentItem.unit}{correct}");
        foreach (var r in _currentResults)
            GameLog.Round($"  \"{r.name}\" guessed {r.guess} (+{r.score} pts)");

        StartTimer(resultsDisplayTime);
        TransitionTo(PriceState.Reveal);
    }

    // ════════════════════════════════════════════
    //  Broadcast
    // ════════════════════════════════════════════

    protected override void BroadcastState()
    {
        var h = BuildHeader();
        var msg = new PriceIsCloseStateMessage
        {
            state = h.state,
            round = h.round,
            totalRounds = h.totalRounds,
            timer = h.timer,
            players = h.players,
            title = _currentItem?.title ?? "",
            description = _currentItem?.description ?? "",
            imageUrl = _currentItem?.imageUrl ?? "",
            videoUrl = _currentItem?.videoUrl ?? "",
            unit = _currentItem?.unit ?? "$"
        };

        if (_state == PriceState.Reveal || _state == PriceState.RoundResults || _state == PriceState.GameOver)
        {
            msg.correctPrice = _currentItem?.correctPrice ?? 0;
            msg.results = _currentResults;
        }

        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }
}

[Serializable]
public class PriceItem
{
    public string id;
    public string title;
    public string description;
    public float correctPrice;
    public string unit;
    public string imageUrl;
    public string videoUrl;
}

[Serializable]
public class PriceItemWrapper
{
    public PriceItem[] items;
}
