using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores and manages answers and votes for the current round.
/// Pure data/logic — no event subscriptions, no network calls.
/// QuiplashManager orchestrates all interactions with this class.
/// </summary>
public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    public string CurrentPrompt { get; private set; }
    public int AnswerCount => _answers.Count;
    public int VoteCount => _votes.Count;

    private readonly Dictionary<string, string> _answers = new Dictionary<string, string>();
    private readonly Dictionary<string, string> _votes = new Dictionary<string, string>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartNewRound(string prompt)
    {
        CurrentPrompt = prompt;
        _answers.Clear();
        _votes.Clear();
    }

    // ── Answers ──────────────────────────────────

    public bool SubmitAnswer(string playerId, string answer)
    {
        if (_answers.ContainsKey(playerId)) return false;
        _answers[playerId] = answer;
        return true;
    }

    public bool AllAnswered(int playerCount)
    {
        return _answers.Count >= playerCount;
    }

    public AnswerInfo[] GetAnswersForVoting()
    {
        var list = new List<AnswerInfo>();
        foreach (var kvp in _answers)
            list.Add(new AnswerInfo { id = kvp.Key, text = kvp.Value });
        return list.ToArray();
    }

    // ── Votes ────────────────────────────────────

    public bool SubmitVote(string voterId, string answerId)
    {
        if (_votes.ContainsKey(voterId)) return false;
        if (voterId == answerId) return false;
        if (!_answers.ContainsKey(answerId)) return false;

        _votes[voterId] = answerId;
        return true;
    }

    public bool AllVoted(int playerCount)
    {
        return _votes.Count >= playerCount;
    }

    // ── Results ──────────────────────────────────

    public ResultInfo[] TallyVotes()
    {
        var voteCounts = new Dictionary<string, int>();
        foreach (string answerId in _answers.Keys)
            voteCounts[answerId] = 0;

        foreach (var kvp in _votes)
        {
            if (voteCounts.ContainsKey(kvp.Value))
                voteCounts[kvp.Value]++;
        }

        var results = new List<ResultInfo>();
        foreach (var kvp in _answers)
        {
            int vc = voteCounts.ContainsKey(kvp.Key) ? voteCounts[kvp.Key] : 0;
            results.Add(new ResultInfo
            {
                id = kvp.Key,
                name = PlayerManager.Instance.GetPlayerName(kvp.Key),
                answer = kvp.Value,
                votes = vc,
                score = vc * 100
            });
        }

        results.Sort((a, b) => b.votes.CompareTo(a.votes));
        return results.ToArray();
    }
}
