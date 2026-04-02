using System;
using UnityEngine;

/// <summary>
/// Quiplash game session. Implements IGameSession so GameCoordinator can manage it.
/// Owns the round flow: ShowPrompt → Answer → Voting → RoundResults → ... → GameOver.
///
/// Lives in its own scene. On Start(), registers with GameCoordinator.
/// Receives player messages through OnGameMessage() — no direct event subscriptions.
/// </summary>
public class QuiplashManager : MonoBehaviour, IGameSession
{
    [Header("Game Settings")]
    [SerializeField] private int maxRounds = 5;
    [SerializeField] private float promptDisplayTime = 5f;
    [SerializeField] private float answerTimerSeconds = 60f;
    [SerializeField] private float voteTimerSeconds = 30f;
    [SerializeField] private float resultsDisplayTime = 10f;
    [SerializeField] private float gameOverDisplayTime = 12f;
    [SerializeField] private int pointsPerVote = 100;

    public enum QuiplashState { ShowPrompt, Answer, Voting, RoundResults, GameOver }

    // IGameSession
    public string GameType => "quiplash";
    public string CurrentState => _currentState.ToString();

    private QuiplashState _currentState;
    private int _currentRound;
    private float _timer;
    private bool _timerActive;
    private ResultInfo[] _currentResults;

    // ── Unity Lifecycle ──────────────────────────

    private void Start()
    {
        GameCoordinator.Instance.RegisterSession(this);
    }

    private void Update()
    {
        if (_timerActive)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timerActive = false;
                OnTimerExpired();
            }
        }
    }

    // ════════════════════════════════════════════
    //  IGameSession
    // ════════════════════════════════════════════

    public void OnSessionStart(string[] playerIds)
    {
        GameLog.Divider();
        GameLog.Game($"QUIPLASH — {playerIds.Length} players, {maxRounds} rounds");
        GameLog.Divider();

        _currentRound = 0;
        _currentResults = null;
        PlayerManager.Instance.ResetScores();
        PromptDatabase.Instance.ResetShuffle();
        BeginNextRound();
    }

    public void OnSessionEnd()
    {
        _timerActive = false;
    }

    public void OnPlayerRejoined(string playerId)
    {
        BroadcastFullState();
    }

    public void OnPlayerDisconnected(string playerId)
    {
        string name = PlayerManager.Instance.GetPlayerName(playerId);
        PlayerManager.Instance.DisconnectPlayer(playerId);
        GameLog.Player($"\"{name}\" DISCONNECTED mid-game [{PlayerManager.Instance.ActivePlayerCount} active]");
        GameEvents.FirePlayerListChanged();
        CheckAutoAdvance();
    }

    public void OnGameMessage(string playerId, string messageType, string json)
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
    //  GAME LOGIC
    // ════════════════════════════════════════════

    private void HandleAnswer(string playerId, string answer)
    {
        if (_currentState != QuiplashState.Answer) return;

        if (RoundManager.Instance.SubmitAnswer(playerId, answer))
        {
            string name = PlayerManager.Instance.GetPlayerName(playerId);
            int have = RoundManager.Instance.AnswerCount;
            int need = PlayerManager.Instance.ActivePlayerCount;
            GameLog.Round($"Answer from \"{name}\"  ({have}/{need})");

            GameEvents.FireSendToPlayer(playerId,
                JsonUtility.ToJson(new ConfirmationMessage { type = "answer_received" }));

            if (have >= need) GameLog.Round("All answers received!");
        }
        CheckAutoAdvance();
    }

    private void HandleVote(string voterId, string answerId)
    {
        if (_currentState != QuiplashState.Voting) return;

        if (RoundManager.Instance.SubmitVote(voterId, answerId))
        {
            string name = PlayerManager.Instance.GetPlayerName(voterId);
            int have = RoundManager.Instance.VoteCount;
            int need = PlayerManager.Instance.ActivePlayerCount;
            GameLog.Round($"Vote from \"{name}\"  ({have}/{need})");

            GameEvents.FireSendToPlayer(voterId,
                JsonUtility.ToJson(new ConfirmationMessage { type = "vote_received" }));

            if (have >= need) GameLog.Round("All votes received!");
        }
        CheckAutoAdvance();
    }

    // ════════════════════════════════════════════
    //  ROUND FLOW
    // ════════════════════════════════════════════

    private void BeginNextRound()
    {
        _currentRound++;
        if (_currentRound > maxRounds)
        {
            EndGame();
            return;
        }

        _currentResults = null;
        string prompt = PromptDatabase.Instance.GetNextPrompt();
        RoundManager.Instance.StartNewRound(prompt);

        GameLog.Divider();
        GameLog.Round($"══ ROUND {_currentRound} / {maxRounds} ══");
        GameLog.Round($"Prompt: \"{prompt}\"");

        StartTimer(promptDisplayTime);
        TransitionTo(QuiplashState.ShowPrompt);
    }

    private void OnTimerExpired()
    {
        switch (_currentState)
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
                GameCoordinator.Instance.OnGameEnded();
                break;
        }
    }

    private void TallyAndShowResults()
    {
        _currentResults = RoundManager.Instance.TallyVotes();

        GameLog.Round("── ROUND RESULTS ──");
        for (int i = 0; i < _currentResults.Length; i++)
        {
            var r = _currentResults[i];
            int earned = r.votes * pointsPerVote;
            PlayerManager.Instance.AddScore(r.id, earned);
            GameLog.Round($"  {OrdinalOf(i + 1)}: \"{r.name}\" — \"{r.answer}\" — {r.votes} vote(s) (+{earned} pts)");
        }

        StartTimer(resultsDisplayTime);
        TransitionTo(QuiplashState.RoundResults);
    }

    private void EndGame()
    {
        var standings = PlayerManager.Instance.GetAllPlayerInfos();
        Array.Sort(standings, (a, b) => b.score.CompareTo(a.score));

        GameLog.Divider();
        GameLog.Game("══ QUIPLASH GAME OVER ══");
        for (int i = 0; i < standings.Length; i++)
        {
            string marker = i == 0 ? "  <<< WINNER" : "";
            GameLog.Game($"  {OrdinalOf(i + 1)}: \"{standings[i].name}\" — {standings[i].score} pts{marker}");
        }
        GameLog.Divider();

        _currentResults = BuildFinalResults(standings);
        StartTimer(gameOverDisplayTime);
        TransitionTo(QuiplashState.GameOver);
    }

    private void CheckAutoAdvance()
    {
        int count = PlayerManager.Instance.ActivePlayerCount;
        if (count == 0) return;

        if (_currentState == QuiplashState.Answer && RoundManager.Instance.AllAnswered(count))
        {
            _timerActive = false;
            GameLog.State("All answers in — auto-advancing to voting");
            StartTimer(voteTimerSeconds);
            TransitionTo(QuiplashState.Voting);
        }
        else if (_currentState == QuiplashState.Voting && RoundManager.Instance.AllVoted(count))
        {
            _timerActive = false;
            GameLog.State("All votes in — auto-advancing to results");
            TallyAndShowResults();
        }
    }

    // ════════════════════════════════════════════
    //  STATE / BROADCAST
    // ════════════════════════════════════════════

    private void TransitionTo(QuiplashState newState)
    {
        _currentState = newState;
        GameLog.State($"Quiplash → {newState}");
        BroadcastFullState();
    }

    private void BroadcastFullState()
    {
        var msg = new GameStateMessage
        {
            gameType = "quiplash",
            state = _currentState.ToString(),
            round = _currentRound,
            totalRounds = maxRounds,
            timer = Mathf.CeilToInt(_timer),
            prompt = RoundManager.Instance != null ? RoundManager.Instance.CurrentPrompt : "",
            players = PlayerManager.Instance.GetAllPlayerInfos()
        };

        if (_currentState == QuiplashState.Voting)
            msg.answers = RoundManager.Instance.GetAnswersForVoting();

        if (_currentState == QuiplashState.RoundResults || _currentState == QuiplashState.GameOver)
            msg.results = _currentResults ?? RoundManager.Instance.TallyVotes();

        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }

    // ── Helpers ──────────────────────────────────

    private void StartTimer(float seconds)
    {
        _timer = seconds;
        _timerActive = true;
    }

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

    private static string OrdinalOf(int n)
    {
        return n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th" };
    }
}
