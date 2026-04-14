using System;
using System.Collections.Generic;
using UnityEngine;

public class CaptionContestManager : RoundBasedGameSession<CaptionContestManager.CCState>
{
    [Header("Phase Timers")]
    [SerializeField] private float imageDisplayTime = 5f;
    [SerializeField] private float captionTimerSeconds = 45f;
    [SerializeField] private float voteTimerSeconds = 20f;
    [SerializeField] private float resultsDisplayTime = 8f;

    [Header("Scoring")]
    [SerializeField] private int pointsPerVote = 100;

    public enum CCState { ShowImage, WriteCaption, Vote, RoundResults, GameOver }

    public override string GameType => "caption_contest";
    protected override CCState GameOverState => CCState.GameOver;

    private GameContentLoader<CaptionItem> _contentLoader;
    private CaptionItem _currentItem;

    private readonly Dictionary<string, string> _captions = new Dictionary<string, string>();
    private readonly Dictionary<string, string> _captionVotes = new Dictionary<string, string>();
    private CaptionResultInfo[] _currentResults;

    private void Start()
    {
        _contentLoader = new GameContentLoader<CaptionItem>("caption_contest");
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
        switch (messageType)
        {
            case "submit_caption":
                HandleCaption(playerId, JsonUtility.FromJson<SubmitCaptionMessage>(json).caption);
                break;
            case "caption_vote":
                HandleVote(playerId, JsonUtility.FromJson<CaptionVoteMessage>(json).captionId);
                break;
        }
    }

    // ════════════════════════════════════════════
    //  Game Logic
    // ════════════════════════════════════════════

    private void HandleCaption(string playerId, string caption)
    {
        if (_state != CCState.WriteCaption) return;
        if (_captions.ContainsKey(playerId)) return;
        string cleaned = caption?.Trim();
        if (string.IsNullOrEmpty(cleaned)) return;

        _captions[playerId] = cleaned;
        string name = PlayerManager.Instance.GetPlayerName(playerId);
        GameLog.Round($"Caption from \"{name}\"  ({_captions.Count}/{PlayerManager.Instance.ActivePlayerCount})");

        GameEvents.FireSendToPlayer(playerId, JsonUtility.ToJson(new ConfirmationMessage { type = MessageTypes.CaptionReceived }));
        TryAutoAdvance();
    }

    private void HandleVote(string voterId, string captionId)
    {
        if (_state != CCState.Vote) return;
        if (_captionVotes.ContainsKey(voterId)) return;
        if (captionId == voterId) return;
        if (!_captions.ContainsKey(captionId)) return;

        _captionVotes[voterId] = captionId;
        string name = PlayerManager.Instance.GetPlayerName(voterId);
        GameLog.Round($"Vote from \"{name}\"  ({_captionVotes.Count}/{PlayerManager.Instance.ActivePlayerCount})");

        GameEvents.FireSendToPlayer(voterId, JsonUtility.ToJson(new ConfirmationMessage { type = MessageTypes.CaptionVoteReceived }));
        TryAutoAdvance();
    }

    // ════════════════════════════════════════════
    //  Base Class Hooks
    // ════════════════════════════════════════════

    protected override void OnRoundStart(int round)
    {
        _captions.Clear();
        _captionVotes.Clear();
        _currentResults = null;

        _currentItem = _contentLoader.HasItems
            ? _contentLoader.GetNext()
            : new CaptionItem { id = "fallback", imageUrl = "", category = "general" };

        GameLog.Round($"Image: {_currentItem.imageUrl}");

        StartTimer(imageDisplayTime);
        TransitionTo(CCState.ShowImage);
    }

    protected override void OnTimerExpired()
    {
        switch (_state)
        {
            case CCState.ShowImage:
                StartTimer(captionTimerSeconds);
                TransitionTo(CCState.WriteCaption);
                break;
            case CCState.WriteCaption:
                StartTimer(voteTimerSeconds);
                TransitionTo(CCState.Vote);
                break;
            case CCState.Vote:
                TallyAndShowResults();
                break;
            case CCState.RoundResults:
                BeginNextRound();
                break;
            case CCState.GameOver:
                CompleteSession();
                break;
        }
    }

    protected override void TryAutoAdvance()
    {
        int active = PlayerManager.Instance.ActivePlayerCount;
        if (active == 0) return;

        if (_state == CCState.WriteCaption && _captions.Count >= active)
        {
            StopTimer();
            StartTimer(voteTimerSeconds);
            TransitionTo(CCState.Vote);
        }
        else if (_state == CCState.Vote && _captionVotes.Count >= active)
        {
            StopTimer();
            TallyAndShowResults();
        }
    }

    // ════════════════════════════════════════════
    //  Scoring & Results
    // ════════════════════════════════════════════

    private void TallyAndShowResults()
    {
        var voteCounts = new Dictionary<string, int>();
        foreach (var kvp in _captions) voteCounts[kvp.Key] = 0;
        foreach (var kvp in _captionVotes)
        {
            if (voteCounts.ContainsKey(kvp.Value))
                voteCounts[kvp.Value]++;
        }

        var results = new List<CaptionResultInfo>();
        foreach (var kvp in _captions)
        {
            voteCounts.TryGetValue(kvp.Key, out int votes);
            int earned = votes * pointsPerVote;
            if (earned > 0) PlayerManager.Instance.AddScore(kvp.Key, earned);

            results.Add(new CaptionResultInfo
            {
                id = kvp.Key,
                name = PlayerManager.Instance.GetPlayerName(kvp.Key),
                caption = kvp.Value,
                votes = votes,
                score = earned
            });
        }
        results.Sort((a, b) => b.votes.CompareTo(a.votes));
        _currentResults = results.ToArray();

        GameLog.Round("── CAPTION CONTEST RESULTS ──");
        foreach (var r in _currentResults)
            GameLog.Round($"  \"{r.name}\" — \"{r.caption}\" — {r.votes} vote(s) (+{r.score} pts)");

        StartTimer(resultsDisplayTime);
        TransitionTo(CCState.RoundResults);
    }

    // ════════════════════════════════════════════
    //  Broadcast
    // ════════════════════════════════════════════

    protected override void BroadcastState()
    {
        var h = BuildHeader();
        var msg = new CaptionContestStateMessage
        {
            state = h.state,
            round = h.round,
            totalRounds = h.totalRounds,
            timer = h.timer,
            players = h.players,
            imageUrl = _currentItem?.imageUrl ?? ""
        };

        if (_state == CCState.Vote)
        {
            var list = new List<CaptionInfo>();
            foreach (var kvp in _captions)
                list.Add(new CaptionInfo { id = kvp.Key, text = kvp.Value });
            msg.captions = list.ToArray();
        }

        if (_state == CCState.RoundResults || _state == CCState.GameOver)
            msg.results = _currentResults;

        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }
}

[Serializable]
public class CaptionItem
{
    public string id;
    public string imageUrl;
    public string category;
}

[Serializable]
public class CaptionItemWrapper
{
    public CaptionItem[] items;
}
