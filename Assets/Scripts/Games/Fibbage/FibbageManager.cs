using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FibbageManager : RoundBasedGameSession<FibbageManager.FibbageState>
{
    [Header("Phase Timers")]
    [SerializeField] private float promptDisplayTime = 5f;
    [SerializeField] private float bluffTimerSeconds = 45f;
    [SerializeField] private float voteTimerSeconds = 20f;
    [SerializeField] private float resultsDisplayTime = 8f;

    [Header("Scoring")]
    [SerializeField] private int truthPoints = 1000;
    [SerializeField] private int fooledPoints = 500;
    [SerializeField] private int bestBluffBonus = 250;

    public enum FibbageState { ShowPrompt, WriteBluff, Vote, RoundResults, GameOver }

    public override string GameType => "fibbage";
    protected override FibbageState GameOverState => FibbageState.GameOver;

    private GameContentLoader<FibbagePrompt> _contentLoader;
    private FibbagePrompt _currentPrompt;

    private readonly Dictionary<string, string> _bluffs = new Dictionary<string, string>();
    private List<FibbageChoiceInfo> _choices = new List<FibbageChoiceInfo>();
    private readonly Dictionary<string, int> _votes = new Dictionary<string, int>();

    private void Start()
    {
        _contentLoader = new GameContentLoader<FibbagePrompt>("fibbage");
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
            case "submit_bluff":
                HandleBluff(playerId, JsonUtility.FromJson<SubmitBluffMessage>(json).bluff);
                break;
            case "fibbage_vote":
                HandleVote(playerId, JsonUtility.FromJson<FibbageVoteMessage>(json).choiceIndex);
                break;
        }
    }

    // ════════════════════════════════════════════
    //  Game Logic
    // ════════════════════════════════════════════

    private void HandleBluff(string playerId, string bluff)
    {
        if (_state != FibbageState.WriteBluff) return;
        if (_bluffs.ContainsKey(playerId)) return;

        string cleaned = bluff?.Trim();
        if (string.IsNullOrEmpty(cleaned)) return;

        _bluffs[playerId] = cleaned;
        string name = PlayerManager.Instance.GetPlayerName(playerId);
        GameLog.Round($"Bluff from \"{name}\"  ({_bluffs.Count}/{PlayerManager.Instance.ActivePlayerCount})");

        GameEvents.FireSendToPlayer(playerId, JsonUtility.ToJson(new ConfirmationMessage { type = MessageTypes.BluffReceived }));
        TryAutoAdvance();
    }

    private void HandleVote(string playerId, int choiceIndex)
    {
        if (_state != FibbageState.Vote) return;
        if (_votes.ContainsKey(playerId)) return;
        if (choiceIndex < 0 || choiceIndex >= _choices.Count) return;

        if (_choices[choiceIndex].authorId == playerId) return;

        _votes[playerId] = choiceIndex;
        string name = PlayerManager.Instance.GetPlayerName(playerId);
        GameLog.Round($"Vote from \"{name}\"  ({_votes.Count}/{PlayerManager.Instance.ActivePlayerCount})");

        GameEvents.FireSendToPlayer(playerId, JsonUtility.ToJson(new ConfirmationMessage { type = MessageTypes.FibbageVoteReceived }));
        TryAutoAdvance();
    }

    // ════════════════════════════════════════════
    //  Base Class Hooks
    // ════════════════════════════════════════════

    protected override void OnRoundStart(int round)
    {
        _bluffs.Clear();
        _votes.Clear();
        _choices.Clear();

        _currentPrompt = _contentLoader.HasItems
            ? _contentLoader.GetNext()
            : new FibbagePrompt { text = "The first thing ever sold on eBay was ___.", truth = "a broken laser pointer" };

        GameLog.Round($"Prompt: \"{_currentPrompt.text}\"  Truth: \"{_currentPrompt.truth}\"");

        StartTimer(promptDisplayTime);
        TransitionTo(FibbageState.ShowPrompt);
    }

    protected override void OnTimerExpired()
    {
        switch (_state)
        {
            case FibbageState.ShowPrompt:
                StartTimer(bluffTimerSeconds);
                TransitionTo(FibbageState.WriteBluff);
                break;
            case FibbageState.WriteBluff:
                BuildChoices();
                StartTimer(voteTimerSeconds);
                TransitionTo(FibbageState.Vote);
                break;
            case FibbageState.Vote:
                TallyAndShowResults();
                break;
            case FibbageState.RoundResults:
                BeginNextRound();
                break;
            case FibbageState.GameOver:
                CompleteSession();
                break;
        }
    }

    protected override void TryAutoAdvance()
    {
        int active = PlayerManager.Instance.ActivePlayerCount;
        if (active == 0) return;

        if (_state == FibbageState.WriteBluff && _bluffs.Count >= active)
        {
            StopTimer();
            BuildChoices();
            StartTimer(voteTimerSeconds);
            TransitionTo(FibbageState.Vote);
        }
        else if (_state == FibbageState.Vote && _votes.Count >= active)
        {
            StopTimer();
            TallyAndShowResults();
        }
    }

    // ════════════════════════════════════════════
    //  Scoring & Results
    // ════════════════════════════════════════════

    private void BuildChoices()
    {
        _choices.Clear();
        _choices.Add(new FibbageChoiceInfo { text = _currentPrompt.truth, isTruth = true, authorId = "" });

        foreach (var kvp in _bluffs)
            _choices.Add(new FibbageChoiceInfo { text = kvp.Value, isTruth = false, authorId = kvp.Key });

        _choices.Shuffle();
    }

    private void TallyAndShowResults()
    {
        var fooledCounts = new Dictionary<string, int>();
        foreach (var kvp in _bluffs) fooledCounts[kvp.Key] = 0;

        int totalVoters = _votes.Count;

        foreach (var kvp in _votes)
        {
            var choice = _choices[kvp.Value];
            if (choice.isTruth)
            {
                PlayerManager.Instance.AddScore(kvp.Key, truthPoints);
            }
            else
            {
                if (!string.IsNullOrEmpty(choice.authorId) && fooledCounts.ContainsKey(choice.authorId))
                    fooledCounts[choice.authorId]++;
            }
        }

        bool noneFoundTruth = totalVoters > 0 && !_votes.Values.Any(vi => _choices[vi].isTruth);

        int maxFooled = fooledCounts.Count > 0 ? fooledCounts.Values.Max() : 0;
        foreach (var kvp in fooledCounts)
        {
            int earned = kvp.Value * fooledPoints;
            if (noneFoundTruth && kvp.Value == maxFooled && maxFooled > 0)
                earned += bestBluffBonus;
            if (earned > 0)
                PlayerManager.Instance.AddScore(kvp.Key, earned);
        }

        GameLog.Round("── FIBBAGE RESULTS ──");
        foreach (string pid in _playerIds)
        {
            string n = PlayerManager.Instance.GetPlayerName(pid);
            bool picked = _votes.TryGetValue(pid, out int vi) && _choices[vi].isTruth;
            int fooled = fooledCounts.ContainsKey(pid) ? fooledCounts[pid] : 0;
            GameLog.Round($"  \"{n}\" — picked truth: {picked}, fooled: {fooled}");
        }

        StartTimer(resultsDisplayTime);
        TransitionTo(FibbageState.RoundResults);
    }

    // ════════════════════════════════════════════
    //  Broadcast
    // ════════════════════════════════════════════

    protected override void BroadcastState()
    {
        var h = BuildHeader();
        var msg = new FibbageStateMessage
        {
            state = h.state,
            round = h.round,
            totalRounds = h.totalRounds,
            timer = h.timer,
            players = h.players,
            prompt = _currentPrompt?.text ?? ""
        };

        if (_state == FibbageState.Vote)
        {
            var stripped = new FibbageChoiceInfo[_choices.Count];
            for (int i = 0; i < _choices.Count; i++)
                stripped[i] = new FibbageChoiceInfo { text = _choices[i].text };
            msg.choices = stripped;
        }

        if (_state == FibbageState.RoundResults || _state == FibbageState.GameOver)
        {
            msg.choices = _choices.ToArray();
            msg.results = BuildResultEntries();
        }

        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }

    private FibbageResultEntry[] BuildResultEntries()
    {
        var entries = new List<FibbageResultEntry>();
        foreach (string pid in _playerIds)
        {
            var e = new FibbageResultEntry
            {
                id = pid,
                name = PlayerManager.Instance.GetPlayerName(pid),
                totalScore = PlayerManager.Instance.GetAllPlayerInfos().FirstOrDefault(p => p.id == pid)?.score ?? 0,
                bluff = _bluffs.ContainsKey(pid) ? _bluffs[pid] : "",
                pickedTruth = _votes.TryGetValue(pid, out int vi) && _choices[vi].isTruth,
                fooledCount = 0
            };

            foreach (var v in _votes)
            {
                if (v.Value >= 0 && v.Value < _choices.Count && _choices[v.Value].authorId == pid)
                    e.fooledCount++;
            }

            e.pointsThisRound = (e.pickedTruth ? truthPoints : 0) + e.fooledCount * fooledPoints;
            entries.Add(e);
        }
        return entries.ToArray();
    }
}

[Serializable]
public class FibbagePrompt
{
    public string id;
    public string text;
    public string truth;
    public string category;
}

[Serializable]
public class FibbagePromptWrapper
{
    public FibbagePrompt[] prompts;
}
