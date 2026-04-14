using UnityEngine;

public class QuiplashManager : RoundBasedGameSession<QuiplashManager.QuiplashState>
{
    [Header("Phase Timers")]
    [SerializeField] private float promptDisplayTime = 5f;
    [SerializeField] private float answerTimerSeconds = 15f;
    [SerializeField] private float voteTimerSeconds = 10f;
    [SerializeField] private float resultsDisplayTime = 10f;

    [Header("Scoring")]
    [SerializeField] private int pointsPerVote = 100;

    public enum QuiplashState { ShowPrompt, Answer, Voting, RoundResults, GameOver }

    public override string GameType => "quiplash";
    protected override QuiplashState GameOverState => QuiplashState.GameOver;

    private ResultInfo[] _currentResults;

    private void Start()
    {
        GameCoordinator.Instance.RegisterSession(this);
    }

    public override void OnSessionStart(string[] playerIds)
    {
        _currentResults = null;
        PlayerManager.Instance.ResetScores();
        PromptDatabase.Instance.ResetShuffle();
        base.OnSessionStart(playerIds);
    }

    public override void OnPlayerDisconnected(string playerId)
    {
        string name = PlayerManager.Instance.GetPlayerName(playerId);
        base.OnPlayerDisconnected(playerId);
        GameLog.Player($"\"{name}\" DISCONNECTED mid-game [{PlayerManager.Instance.ActivePlayerCount} active]");
    }

    public override void OnGameMessage(string playerId, string messageType, string json)
    {
        switch (messageType)
        {
            case "submit_answer":
                HandleAnswer(playerId, JsonUtility.FromJson<SubmitAnswerMessage>(json).answer);
                break;
            case "vote":
                HandleVote(playerId, JsonUtility.FromJson<VoteMessage>(json).answerId);
                break;
        }
    }

    // ════════════════════════════════════════════
    //  Game Logic
    // ════════════════════════════════════════════

    private void HandleAnswer(string playerId, string answer)
    {
        if (_state != QuiplashState.Answer) return;

        if (RoundManager.Instance.SubmitAnswer(playerId, answer))
        {
            string name = PlayerManager.Instance.GetPlayerName(playerId);
            int have = RoundManager.Instance.AnswerCount;
            int need = PlayerManager.Instance.ActivePlayerCount;
            GameLog.Round($"Answer from \"{name}\"  ({have}/{need})");

            GameEvents.FireSendToPlayer(playerId,
                JsonUtility.ToJson(new ConfirmationMessage { type = MessageTypes.AnswerReceived }));

            if (have >= need) GameLog.Round("All answers received!");
        }
        TryAutoAdvance();
    }

    private void HandleVote(string voterId, string answerId)
    {
        if (_state != QuiplashState.Voting) return;

        if (RoundManager.Instance.SubmitVote(voterId, answerId))
        {
            string name = PlayerManager.Instance.GetPlayerName(voterId);
            int have = RoundManager.Instance.VoteCount;
            int need = PlayerManager.Instance.ActivePlayerCount;
            GameLog.Round($"Vote from \"{name}\"  ({have}/{need})");

            GameEvents.FireSendToPlayer(voterId,
                JsonUtility.ToJson(new ConfirmationMessage { type = MessageTypes.VoteReceived }));

            if (have >= need) GameLog.Round("All votes received!");
        }
        TryAutoAdvance();
    }

    // ════════════════════════════════════════════
    //  Base Class Hooks
    // ════════════════════════════════════════════

    protected override void OnRoundStart(int round)
    {
        _currentResults = null;
        string prompt = PromptDatabase.Instance.GetNextPrompt();
        RoundManager.Instance.StartNewRound(prompt);

        GameLog.Round($"Prompt: \"{prompt}\"");

        StartTimer(promptDisplayTime);
        TransitionTo(QuiplashState.ShowPrompt);
    }

    protected override void OnTimerExpired()
    {
        switch (_state)
        {
            case QuiplashState.ShowPrompt:
                GameLog.State($"Prompt displayed — opening answers ({answerTimerSeconds}s)");
                StartTimer(answerTimerSeconds);
                TransitionTo(QuiplashState.Answer);
                break;

            case QuiplashState.Answer:
                GameLog.State("Answer timer expired — moving to voting");
                StartTimer(voteTimerSeconds);
                TransitionTo(QuiplashState.Voting);
                break;

            case QuiplashState.Voting:
                GameLog.State("Vote timer expired — tallying results");
                TallyAndShowResults();
                break;

            case QuiplashState.RoundResults:
                BeginNextRound();
                break;

            case QuiplashState.GameOver:
                CompleteSession();
                break;
        }
    }

    protected override void TryAutoAdvance()
    {
        int count = PlayerManager.Instance.ActivePlayerCount;
        if (count == 0) return;

        if (_state == QuiplashState.Answer && RoundManager.Instance.AllAnswered(count))
        {
            StopTimer();
            GameLog.State("All answers in — auto-advancing to voting");
            StartTimer(voteTimerSeconds);
            TransitionTo(QuiplashState.Voting);
        }
        else if (_state == QuiplashState.Voting && RoundManager.Instance.AllVoted(count))
        {
            StopTimer();
            GameLog.State("All votes in — auto-advancing to results");
            TallyAndShowResults();
        }
    }

    // ════════════════════════════════════════════
    //  Scoring & Results
    // ════════════════════════════════════════════

    private void TallyAndShowResults()
    {
        _currentResults = RoundManager.Instance.TallyVotes();

        GameLog.Round("── ROUND RESULTS ──");
        for (int i = 0; i < _currentResults.Length; i++)
        {
            var r = _currentResults[i];
            int earned = r.votes * pointsPerVote;
            PlayerManager.Instance.AddScore(r.id, earned);
            GameLog.Round($"  {Leaderboard.OrdinalOf(i + 1)}: \"{r.name}\" — \"{r.answer}\" — {r.votes} vote(s) (+{earned} pts)");
        }

        StartTimer(resultsDisplayTime);
        TransitionTo(QuiplashState.RoundResults);
    }

    protected override void EndGame()
    {
        var standings = Leaderboard.GetSorted();
        Leaderboard.LogStandings("QUIPLASH", standings);

        _currentResults = BuildFinalResults(standings);
        StartTimer(gameOverDisplayTime);
        TransitionTo(QuiplashState.GameOver);
    }

    // ════════════════════════════════════════════
    //  Broadcast
    // ════════════════════════════════════════════

    protected override void BroadcastState()
    {
        var h = BuildHeader();
        var msg = new GameStateMessage
        {
            gameType = h.gameType,
            state = h.state,
            round = h.round,
            totalRounds = h.totalRounds,
            timer = h.timer,
            players = h.players,
            prompt = RoundManager.Instance != null ? RoundManager.Instance.CurrentPrompt : ""
        };

        if (_state == QuiplashState.Voting)
            msg.answers = RoundManager.Instance.GetAnswersForVoting();

        if (_state == QuiplashState.RoundResults || _state == QuiplashState.GameOver)
            msg.results = _currentResults ?? RoundManager.Instance.TallyVotes();

        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }

    // ── Helpers ──────────────────────────────────

    private static ResultInfo[] BuildFinalResults(PlayerInfo[] standings)
    {
        var results = new ResultInfo[standings.Length];
        for (int i = 0; i < standings.Length; i++)
        {
            results[i] = new ResultInfo
            {
                id = standings[i].id,
                name = standings[i].name,
                score = standings[i].score
            };
        }
        return results;
    }
}
