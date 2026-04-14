using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gartic Phone-style telephone chain game. Players alternate between writing
/// prompts and drawing them, creating chains where meaning degrades hilariously.
/// No scoring — the fun is in the reveal at the end.
/// </summary>
public class TelephoneManager : MonoBehaviour, IGameSession
{
    [Header("Timers")]
    [SerializeField] private float writeSeconds = 30f;
    [SerializeField] private float drawSeconds = 30f;
    [SerializeField] private float describeSeconds = 20f;
    [SerializeField] private float revealStepSeconds = 4f;
    [SerializeField] private float revealChainPauseSeconds = 2f;
    [SerializeField] private float doneDisplaySeconds = 5f;

    public string GameType => "telephone";
    public string CurrentState => _state.ToString();

    private enum Phase { WritePrompt, Draw, Describe, Reveal, RevealPause, Done }

    private Phase _state;
    private string[] _playerIds;
    private int _playerCount;

    private float _timer;
    private bool _timerActive;

    private List<List<ChainEntry>> _chains;
    private int _currentStep;
    private int _totalSteps;

    private readonly HashSet<string> _submitted = new HashSet<string>();
    private readonly Dictionary<string, List<TelephoneStroke>> _strokeBuffers =
        new Dictionary<string, List<TelephoneStroke>>();

    private int _revealChain;
    private int _revealEntry;

    /// <summary>
    /// Fired during reveal so the table display can render each entry
    /// without parsing broadcast JSON.
    /// Args: entry, chainIndex, totalChains, entryIndex, chainLength
    /// </summary>
    public static event System.Action<TelephoneRevealEntry, int, int, int, int> RevealEntryChanged;

    private class ChainEntry
    {
        public string playerId;
        public string playerName;
        public bool isDrawing;
        public string text;
        public TelephoneStroke[] strokes;
    }

    // ════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════

    private void Start()
    {
        GameCoordinator.Instance.RegisterSession(this);
    }

    private void Update()
    {
        if (!_timerActive) return;
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timerActive = false;
            OnTimerExpired();
        }
    }

    // ════════════════════════════════════════════
    //  IGameSession
    // ════════════════════════════════════════════

    public void OnSessionStart(string[] playerIds)
    {
        _playerIds = playerIds;
        _playerCount = playerIds.Length;
        _totalSteps = _playerCount;
        _currentStep = 0;

        _chains = new List<List<ChainEntry>>();
        for (int i = 0; i < _playerCount; i++)
            _chains.Add(new List<ChainEntry>());

        GameLog.Divider();
        GameLog.Game($"TELEPHONE — {_playerCount} players, {_totalSteps} steps per chain");
        GameLog.Divider();

        BeginStep();
    }

    public void OnSessionEnd()
    {
        _timerActive = false;
    }

    public void OnPlayerRejoined(string playerId)
    {
        SendStateToPlayer(playerId);
    }

    public void OnPlayerDisconnected(string playerId)
    {
        PlayerManager.Instance.DisconnectPlayer(playerId);
        GameEvents.FirePlayerListChanged();

        if (_state == Phase.Reveal || _state == Phase.RevealPause || _state == Phase.Done)
            return;

        if (!_submitted.Contains(playerId))
        {
            AutoSubmitForPlayer(playerId);
            CheckAllSubmitted();
        }
    }

    public void OnGameMessage(string playerId, string messageType, string json)
    {
        switch (messageType)
        {
            case MessageTypes.SubmitPrompt:
                HandleSubmitText(playerId, JsonUtility.FromJson<SubmitPromptMessage>(json).text, false);
                break;
            case MessageTypes.SubmitDescription:
                HandleSubmitText(playerId, JsonUtility.FromJson<SubmitDescriptionMessage>(json).text, true);
                break;
            case MessageTypes.DrawStroke:
                HandleStroke(playerId, json);
                break;
            case MessageTypes.SubmitDrawing:
                HandleSubmitDrawing(playerId);
                break;
        }
    }

    // ════════════════════════════════════════════
    //  Step Management
    // ════════════════════════════════════════════

    private void BeginStep()
    {
        _submitted.Clear();
        _strokeBuffers.Clear();

        if (_currentStep == 0)
        {
            _state = Phase.WritePrompt;
            GameLog.Game("Phase: Write Prompts");
            StartTimer(writeSeconds);
            BroadcastStateToAll();
        }
        else if (IsDrawStep(_currentStep))
        {
            _state = Phase.Draw;
            GameLog.Game($"Phase: Draw (step {_currentStep + 1}/{_totalSteps})");
            StartTimer(drawSeconds);
            SendPerPlayerState();
        }
        else
        {
            _state = Phase.Describe;
            GameLog.Game($"Phase: Describe (step {_currentStep + 1}/{_totalSteps})");
            StartTimer(describeSeconds);
            SendPerPlayerState();
        }

        FireTableDisplay();
    }

    private static bool IsDrawStep(int step) => step % 2 == 1;

    private int GetChainForPlayer(int playerIndex, int step)
    {
        return ((playerIndex - step) % _playerCount + _playerCount) % _playerCount;
    }

    private int PlayerIndex(string playerId) => Array.IndexOf(_playerIds, playerId);

    // ════════════════════════════════════════════
    //  Message Handlers
    // ════════════════════════════════════════════

    private void HandleSubmitText(string playerId, string text, bool isDescription)
    {
        Phase expected = isDescription ? Phase.Describe : Phase.WritePrompt;
        if (_state != expected) return;
        if (_submitted.Contains(playerId)) return;

        int pIdx = PlayerIndex(playerId);
        if (pIdx < 0) return;

        int chainIdx = GetChainForPlayer(pIdx, _currentStep);
        string name = PlayerManager.Instance.GetPlayerName(playerId);

        if (string.IsNullOrWhiteSpace(text))
            text = isDescription ? "???" : "Something funny";

        _chains[chainIdx].Add(new ChainEntry
        {
            playerId = playerId,
            playerName = name,
            isDrawing = false,
            text = text
        });

        _submitted.Add(playerId);
        string ackType = isDescription ? MessageTypes.DescriptionReceived : MessageTypes.PromptReceived;
        GameEvents.FireSendToPlayer(playerId,
            JsonUtility.ToJson(new ConfirmationMessage { type = ackType }));

        string label = isDescription ? "description" : "prompt";
        GameLog.Round($"\"{name}\" submitted {label}  ({_submitted.Count}/{_playerCount})");
        CheckAllSubmitted();
    }

    private void HandleStroke(string playerId, string json)
    {
        if (_state != Phase.Draw) return;

        var msg = JsonUtility.FromJson<DrawStrokeMessage>(json);
        if (msg.points == null || msg.points.Length < 4) return;

        if (!_strokeBuffers.ContainsKey(playerId))
            _strokeBuffers[playerId] = new List<TelephoneStroke>();

        _strokeBuffers[playerId].Add(new TelephoneStroke
        {
            points = msg.points,
            color = msg.color ?? "#ffffff"
        });
    }

    private void HandleSubmitDrawing(string playerId)
    {
        if (_state != Phase.Draw) return;
        if (_submitted.Contains(playerId)) return;

        FinalizeDrawing(playerId);
        _submitted.Add(playerId);

        GameEvents.FireSendToPlayer(playerId,
            JsonUtility.ToJson(new ConfirmationMessage { type = MessageTypes.DrawingReceived }));

        string name = PlayerManager.Instance.GetPlayerName(playerId);
        GameLog.Round($"\"{name}\" finished drawing  ({_submitted.Count}/{_playerCount})");
        CheckAllSubmitted();
    }

    private void FinalizeDrawing(string playerId)
    {
        int pIdx = PlayerIndex(playerId);
        if (pIdx < 0) return;

        int chainIdx = GetChainForPlayer(pIdx, _currentStep);
        string name = PlayerManager.Instance.GetPlayerName(playerId);

        TelephoneStroke[] strokes = _strokeBuffers.ContainsKey(playerId)
            ? _strokeBuffers[playerId].ToArray()
            : new TelephoneStroke[0];

        _chains[chainIdx].Add(new ChainEntry
        {
            playerId = playerId,
            playerName = name,
            isDrawing = true,
            strokes = strokes
        });
    }

    private void AutoSubmitForPlayer(string playerId)
    {
        int pIdx = PlayerIndex(playerId);
        if (pIdx < 0) return;

        if (_state == Phase.Draw)
        {
            FinalizeDrawing(playerId);
        }
        else
        {
            int chainIdx = GetChainForPlayer(pIdx, _currentStep);
            string name = PlayerManager.Instance.GetPlayerName(playerId);
            _chains[chainIdx].Add(new ChainEntry
            {
                playerId = playerId,
                playerName = name,
                isDrawing = false,
                text = "..."
            });
        }

        _submitted.Add(playerId);
    }

    // ════════════════════════════════════════════
    //  Auto-Advance
    // ════════════════════════════════════════════

    private void CheckAllSubmitted()
    {
        if (_submitted.Count >= _playerCount)
        {
            _timerActive = false;
            AdvanceStep();
        }
    }

    private void AdvanceStep()
    {
        foreach (string pid in _playerIds)
        {
            if (_submitted.Contains(pid)) continue;
            AutoSubmitForPlayer(pid);
        }

        _currentStep++;
        if (_currentStep >= _totalSteps)
            BeginReveal();
        else
            BeginStep();
    }

    // ════════════════════════════════════════════
    //  Timer
    // ════════════════════════════════════════════

    private void StartTimer(float seconds)
    {
        _timer = seconds;
        _timerActive = true;
    }

    private void OnTimerExpired()
    {
        switch (_state)
        {
            case Phase.WritePrompt:
            case Phase.Draw:
            case Phase.Describe:
                AdvanceStep();
                break;
            case Phase.Reveal:
                AdvanceReveal();
                break;
            case Phase.RevealPause:
                _state = Phase.Reveal;
                StartTimer(revealStepSeconds);
                BroadcastRevealState();
                break;
            case Phase.Done:
                GameCoordinator.Instance.OnGameEnded();
                break;
        }
    }

    // ════════════════════════════════════════════
    //  Reveal
    // ════════════════════════════════════════════

    private void BeginReveal()
    {
        GameLog.Divider();
        GameLog.Game("REVEAL TIME!");
        GameLog.Divider();

        _revealChain = 0;
        _revealEntry = 0;
        _state = Phase.Reveal;
        StartTimer(revealStepSeconds);
        BroadcastRevealState();
    }

    private void AdvanceReveal()
    {
        _revealEntry++;
        if (_revealEntry >= _chains[_revealChain].Count)
        {
            _revealChain++;
            _revealEntry = 0;

            if (_revealChain >= _chains.Count)
            {
                _state = Phase.Done;
                StartTimer(doneDisplaySeconds);
                BroadcastStateToAll();
                FireTableDisplay();
                return;
            }

            _state = Phase.RevealPause;
            StartTimer(revealChainPauseSeconds);
            BroadcastRevealState();
            return;
        }

        StartTimer(revealStepSeconds);
        BroadcastRevealState();
    }

    private void FireTableDisplay()
    {
        GameEvents.FireDisplayState(GameType, _state.ToString(), Mathf.CeilToInt(_timer));
    }

    // ════════════════════════════════════════════
    //  State Broadcasting
    // ════════════════════════════════════════════

    private void BroadcastStateToAll()
    {
        var msg = new TelephoneStateMessage
        {
            state = _state.ToString(),
            timer = Mathf.CeilToInt(_timer),
            step = _currentStep,
            totalSteps = _totalSteps,
            players = PlayerManager.Instance.GetAllPlayerInfos()
        };
        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }

    private void SendPerPlayerState()
    {
        for (int pIdx = 0; pIdx < _playerCount; pIdx++)
        {
            string pid = _playerIds[pIdx];
            if (!PlayerManager.Instance.IsPlayerConnected(pid)) continue;

            int chainIdx = GetChainForPlayer(pIdx, _currentStep);
            var chain = _chains[chainIdx];
            var prev = chain.Count > 0 ? chain[chain.Count - 1] : null;

            var msg = new TelephoneStateMessage
            {
                state = _state.ToString(),
                timer = Mathf.CeilToInt(_timer),
                step = _currentStep,
                totalSteps = _totalSteps,
                players = PlayerManager.Instance.GetAllPlayerInfos()
            };

            if (_state == Phase.Draw && prev != null)
                msg.assignment = prev.text ?? "";
            else if (_state == Phase.Describe && prev != null)
                msg.strokes = prev.strokes ?? new TelephoneStroke[0];

            GameEvents.FireSendToPlayer(pid, JsonUtility.ToJson(msg));
        }
    }

    private void SendStateToPlayer(string playerId)
    {
        if (_state == Phase.Reveal || _state == Phase.RevealPause)
        {
            BroadcastRevealState();
            return;
        }

        if (_state == Phase.Done || _state == Phase.WritePrompt)
        {
            BroadcastStateToAll();
            return;
        }

        int pIdx = PlayerIndex(playerId);
        if (pIdx < 0) return;

        int chainIdx = GetChainForPlayer(pIdx, _currentStep);
        var chain = _chains[chainIdx];
        var prev = chain.Count > 0 ? chain[chain.Count - 1] : null;

        var msg = new TelephoneStateMessage
        {
            state = _state.ToString(),
            timer = Mathf.CeilToInt(_timer),
            step = _currentStep,
            totalSteps = _totalSteps,
            players = PlayerManager.Instance.GetAllPlayerInfos()
        };

        if (_state == Phase.Draw && prev != null)
            msg.assignment = prev.text ?? "";
        else if (_state == Phase.Describe && prev != null)
            msg.strokes = prev.strokes ?? new TelephoneStroke[0];

        GameEvents.FireSendToPlayer(playerId, JsonUtility.ToJson(msg));
    }

    private void BroadcastRevealState()
    {
        if (_revealChain >= _chains.Count) return;

        var chain = _chains[_revealChain];
        var msg = new TelephoneStateMessage
        {
            state = _state.ToString(),
            timer = Mathf.CeilToInt(_timer),
            step = _currentStep,
            totalSteps = _totalSteps,
            players = PlayerManager.Instance.GetAllPlayerInfos(),
            chainIndex = _revealChain,
            totalChains = _chains.Count,
            chainLength = chain.Count,
            entryIndex = _revealEntry
        };

        TelephoneRevealEntry revealEntry = null;

        if (_state == Phase.Reveal && _revealEntry < chain.Count)
        {
            var ce = chain[_revealEntry];
            revealEntry = new TelephoneRevealEntry
            {
                playerName = ce.playerName,
                entryType = ce.isDrawing ? "drawing" : "text",
                content = ce.text ?? "",
                strokes = ce.strokes
            };
            msg.entry = revealEntry;
        }

        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
        FireTableDisplay();

        if (revealEntry != null)
            RevealEntryChanged?.Invoke(revealEntry, _revealChain, _chains.Count, _revealEntry, chain.Count);
    }
}
