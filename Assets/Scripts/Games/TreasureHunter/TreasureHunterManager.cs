using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IGameSession for Treasure Hunter. Inherits timer + state-machine plumbing from
/// <see cref="BaseGameSession{TState}"/>, mirroring the structure of SheepHerderManager.
///
/// Scene contract:
///   • This component lives on the "TreasureHunterManager" GameObject.
///   • A <see cref="JoystickInputRelay"/> child is found or added automatically.
///   • A <see cref="DungeonPainter"/> exists in the scene.
///   • A <see cref="FogOfWar"/> exists in the scene (FogOfWar quad).
///   • A <see cref="ExplorerRegistry"/> is auto-created if not present.
///
/// Multi-level extension: increment <c>_levelIndex</c> and call <c>StartLevel()</c> again
/// instead of <c>CompleteSession()</c>. The state machine handles the rest.
/// </summary>
public class TreasureHunterManager : BaseGameSession<TreasureHunterManager.TreasureState>
{
    public static TreasureHunterManager Instance { get; private set; }

    public enum TreasureState
    {
        Briefing,
        Deploying,
        Exploring,
        Escape,
        Results,
    }

    // ── Inspector ─────────────────────────────────

    [Header("Phase Timings")]
    [Tooltip("Countdown at the start of the level while players memorize their clues.")]
    [SerializeField] private float briefingSeconds = 30f;

    [Tooltip("Countdown after briefing and before exploring begins (3… 2… 1… GO).")]
    [SerializeField] private float deployCountdownSeconds = 3f;

    [Tooltip("How long the Results screen stays up at the end of the round before returning to the lobby.")]
    [SerializeField] private float resultsHoldSeconds = 8f;

    [Header("Scoring")]
    [Tooltip("Maximum escape time bonus, decays linearly with elapsed seconds. " +
             "Score contribution = max(0, escapeBonusMax - elapsedSeconds * escapeBonusDecayPerSec).")]
    [SerializeField] private int escapeBonusMax = 2000;
    [SerializeField] private float escapeBonusDecayPerSec = 10f;

    [Header("Clue Distribution")]
    [SerializeField] private int cluesPerPlayerMin = 1;
    [SerializeField] private int cluesPerPlayerMax = 3;

    [Header("Difficulty")]
    [SerializeField] public DifficultyProfile defaultDifficulty = new DifficultyProfile();

    [Header("Broadcast")]
    [SerializeField] private int broadcastEveryNFrames = 6;

    [Header("Generator")]
    [Tooltip("Leave null to use the default RoomsAndCorridorsGenerator.")]
    [SerializeField] private Object generatorOverride; // MonoBehaviour implementing IDungeonGenerator

    // ── Scene refs ────────────────────────────────

    private JoystickInputRelay _inputRelay;
    private DungeonPainter _painter;
    private FogOfWar _fogOfWar;
    private ExplorerRegistry _registry;

    // ── Per-run state ─────────────────────────────

    private DungeonLayout _layout;
    private DungeonContext _ctx;
    private int _levelIndex;
    private int _teamGold;
    private int _teamTrapsTripped;
    private int _readyCount;
    private int _puzzleSitesTotal;
    private int _puzzlesSolved;
    /// <summary>
    /// Elapsed seconds since the Exploring phase began. Counts up to infinity and is used
    /// both for the shared-screen / phone timer display and for the escape-bonus scoring.
    /// </summary>
    private float _runElapsedSeconds;
    private int _fixedFrameCount;
    private readonly List<PuzzleSiteBase> _puzzleSites = new List<PuzzleSiteBase>();
    private readonly ClueGenerator _clueGenerator = new ClueGenerator();
    private readonly ClueTemplateLoader _clueTemplates = new ClueTemplateLoader();

    // Reusable frame-broadcast buffer (mirrors PongManager._paddleBuffer pattern).
    private TreasureHunterExplorerState[] _explorerBuffer;
    private TreasureHunterStateMessage _frameMsg;

    // ── IGameSession / BaseGameSession ────────────

    public override string GameType => MessageTypes.GameTypeTreasureHunter;

    // ── Unity lifecycle ──────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _registry = FindAnyObjectByType<ExplorerRegistry>();
        if (_registry == null)
        {
            var go = new GameObject("ExplorerRegistry");
            go.transform.SetParent(transform);
            _registry = go.AddComponent<ExplorerRegistry>();
        }

        _inputRelay = GetComponentInChildren<JoystickInputRelay>();
        if (_inputRelay == null)
        {
            var go = new GameObject("JoystickInputRelay");
            go.transform.SetParent(transform);
            _inputRelay = go.AddComponent<JoystickInputRelay>();
        }

        _painter = FindAnyObjectByType<DungeonPainter>();
        if (_painter == null) GameLog.Warn("TreasureHunter: No DungeonPainter found in scene.");

        _fogOfWar = FindAnyObjectByType<FogOfWar>();
        if (_fogOfWar == null) GameLog.Warn("TreasureHunter: No FogOfWar found in scene.");

        _clueGenerator.cluesPerPlayerMin = cluesPerPlayerMin;
        _clueGenerator.cluesPerPlayerMax = cluesPerPlayerMax;
        _clueTemplates.Load();

        _frameMsg = new TreasureHunterStateMessage();

        GameCoordinator.Instance.RegisterSession(this);
    }

    protected override void Update()
    {
        base.Update(); // drives count-down and count-up timers

        // Exploring + Escape share a single count-up "run clock" that keeps track of how
        // long the team has been inside the dungeon. BaseGameSession's count-up mode owns
        // the tick; we also mirror it into _runElapsedSeconds so scoring can read it
        // even after the timer is stopped in Results.
        if (IsTimerCountingUp) _runElapsedSeconds = TimerRaw;
    }

    private void FixedUpdate()
    {
        if (_state != TreasureState.Exploring && _state != TreasureState.Escape) return;
        _fixedFrameCount++;
        if (_fixedFrameCount % broadcastEveryNFrames == 0) BroadcastState();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ════════════════════════════════════════════
    //  IGameSession
    // ════════════════════════════════════════════

    public override void OnSessionStart(string[] playerIds)
    {
        _playerIds = playerIds;
        _levelIndex = 0;
        _teamGold = 0;
        _teamTrapsTripped = 0;
        _readyCount = 0;
        _fixedFrameCount = 0;
        _runElapsedSeconds = 0f;

        GameLog.Divider();
        GameLog.Game($"TREASURE HUNTER — {playerIds.Length} explorers");
        GameLog.Divider();

        StartLevel();
    }

    public override void OnSessionEnd()
    {
        base.OnSessionEnd();
        _inputRelay?.Teardown();
        _painter?.Clear();
    }

    public override void OnPlayerDisconnected(string playerId)
    {
        // Mark explorer as absent — unrevivable — but do not touch PlayerManager (coordinator owns that).
        var explorer = _registry?.GetByPlayerId(playerId);
        if (explorer != null && explorer.IsDown)
        {
            GameLog.Player($"Downed explorer {explorer.PlayerName} disconnected — marking as absent.");
        }
    }

    public override void OnGameMessage(string playerId, string messageType, string json)
    {
        if (messageType == MessageTypes.TreasureHunterBriefingReady)
            HandleBriefingReady(playerId);
        // joystick_move is consumed by JoystickInputRelay via GameEvents directly.
    }

    // ════════════════════════════════════════════
    //  Level start
    // ════════════════════════════════════════════

    private void StartLevel()
    {
        int seed = System.Environment.TickCount ^ _levelIndex;
        var rng = new System.Random(seed);

        // Generate dungeon.
        IDungeonGenerator generator = (generatorOverride as IDungeonGenerator) ?? new RoomsAndCorridorsGenerator();
        _layout = generator.Generate(defaultDifficulty, seed);
        _ctx = new DungeonContext(_layout, defaultDifficulty, seed, _levelIndex);

        // Paint dungeon.
        _painter?.Paint(_layout, rng);

        // Set up fog of war.
        if (_fogOfWar != null)
        {
            _fogOfWar.Setup(_layout, _painter != null ? _painter.tileSize : 1f);
            _fogOfWar.ResetFog();
        }

        // Register puzzle sites spawned by the painter.
        _puzzleSites.Clear();
        if (_painter != null)
        {
            foreach (var site in _painter.SpawnedPuzzleSites)
            {
                site.Initialize(_ctx);
                site.OnSolved += OnPuzzleSolved;
                _puzzleSites.Add(site);
            }
        }
        _puzzleSitesTotal = _puzzleSites.Count;
        _puzzlesSolved = 0;

        // Spawn explorers.
        _inputRelay.Initialize(_playerIds);
        PositionExplorersAtSpawn();

        // IMPORTANT ORDER:
        //   1. TransitionTo(Briefing) broadcasts the game_state message that
        //      causes every phone to activate the treasure_hunter module and
        //      show the Briefing screen.
        //   2. Only AFTER that do we send per-player th_briefing messages.
        //      If we sent them first, phones would still have activeModule=null
        //      (because no game_state had arrived yet) and would silently drop
        //      the clues.
        _readyCount = 0;
        TransitionTo(TreasureState.Briefing);
        StartTimer(briefingSeconds);

        // Generate and distribute clues to each player individually.
        var assignment = _clueGenerator.Generate(_ctx, _playerIds, rng);
        SendBriefings(assignment, rng);
    }

    private void PositionExplorersAtSpawn()
    {
        if (_layout == null || _painter == null) return;
        Vector3 spawnPos = _painter.GetSpawnWorldPosition();
        // Spread explorers slightly around spawn.
        float spread = 0.8f;
        int count = 0;
        var relay = _inputRelay;
        if (relay == null) return;
        foreach (var kvp in relay.AllNodes)
        {
            float angle = (count / (float)Mathf.Max(_playerIds.Length, 1)) * Mathf.PI * 2f;
            var offset = new Vector3(Mathf.Cos(angle) * spread, Mathf.Sin(angle) * spread, 0f);
            kvp.Value.transform.position = spawnPos + offset;
            count++;
        }
    }

    // ════════════════════════════════════════════
    //  Briefing & clues
    // ════════════════════════════════════════════

    private void SendBriefings(List<List<ClueFact>> assignment, System.Random rng)
    {
        for (int i = 0; i < _playerIds.Length; i++)
        {
            string[] clueTexts = ClueFormatter.FormatAll(
                i < assignment.Count ? assignment[i] : new List<ClueFact>(),
                _clueTemplates, rng);

            var msg = new TreasureHunterBriefingMessage
            {
                clues = clueTexts,
                briefingSeconds = briefingSeconds,
            };
            GameEvents.FireSendToPlayer(_playerIds[i], JsonUtility.ToJson(msg));
        }
    }

    private void HandleBriefingReady(string playerId)
    {
        if (_state != TreasureState.Briefing) return;
        _readyCount++;
        GameLog.Player($"[TH] {playerId} is ready ({_readyCount}/{_playerIds.Length})");
        if (_readyCount >= _playerIds.Length) BeginDeploying();
    }

    private void BeginDeploying()
    {
        StopTimer();
        TransitionTo(TreasureState.Deploying);
        StartTimer(deployCountdownSeconds);
    }

    // ════════════════════════════════════════════
    //  State machine
    // ════════════════════════════════════════════

    protected override void OnTimerExpired()
    {
        switch (_state)
        {
            case TreasureState.Briefing:
                // Briefing timed out — start deploying regardless of ready count.
                BeginDeploying();
                break;

            case TreasureState.Deploying:
                TransitionTo(TreasureState.Exploring);
                _fixedFrameCount = 0;
                _runElapsedSeconds = 0f;
                // Count-up run clock — ticks forever, no hard deadline. Phase transitions
                // are triggered by gameplay events (all puzzles solved, all explorers escaped).
                StartCountUpTimer();
                GameEvents.FireDisplayTimerCountUp(true);
                GameLog.Game("TREASURE HUNTER — Exploring started!");
                // Safeguard: with 0 puzzles wired (partially-set-up scene) the Exploring
                // phase would never end because the only exit trigger is "all puzzles
                // solved". Open Escape immediately so the round still reaches Results.
                if (_puzzleSitesTotal <= 0)
                {
                    GameLog.Warn("TreasureHunter: No puzzle sites were spawned — skipping straight to Escape.");
                    OpenEscape();
                }
                break;

            case TreasureState.Results:
                CompleteSession();
                break;

            // TreasureState.Exploring and TreasureState.Escape are driven by the count-up
            // timer, so they never "expire". Transitions happen via OnPuzzleSolved and
            // OnExplorerEscaped respectively.
        }
    }

    // ════════════════════════════════════════════
    //  Puzzle solved callback
    // ════════════════════════════════════════════

    private void OnPuzzleSolved(IPuzzleSite site)
    {
        _puzzlesSolved++;
        GameLog.Game($"[TH] Puzzle {_puzzlesSolved}/{_puzzleSitesTotal} solved");

        // Broadcast puzzle event to all phones.
        var evt = new TreasureHunterEventMessage
        {
            eventName = "puzzle_solved",
            puzzlesRemaining = _puzzleSitesTotal - _puzzlesSolved,
        };
        GameEvents.FireBroadcast(JsonUtility.ToJson(evt));

        if (_puzzlesSolved >= _puzzleSitesTotal) OpenEscape();
    }

    // ════════════════════════════════════════════
    //  Escape phase
    // ════════════════════════════════════════════

    private void OpenEscape()
    {
        if (_state == TreasureState.Escape) return;

        // Unlock exit door.
        _painter?.SpawnedExitDoor?.Unlock();

        // Notify all phones.
        var evt = new TreasureHunterEventMessage { eventName = "escape_unlocked" };
        GameEvents.FireBroadcast(JsonUtility.ToJson(evt));

        TransitionTo(TreasureState.Escape);
        // Keep the run-clock counting up; no deadline. Players can take as long as they
        // need, but the escape bonus decays with elapsed time so fast runs still win.
        if (!IsTimerCountingUp) StartCountUpTimer(_runElapsedSeconds);
        GameLog.Game("TREASURE HUNTER — Escape phase started!");
    }

    /// <summary>Called by <see cref="ExitDoor"/> when an explorer passes through.</summary>
    public void OnExplorerEscaped(Explorer explorer)
    {
        if (_state != TreasureState.Escape) return;
        BroadcastState();

        // If everyone escaped, jump straight to results.
        if (_registry != null && _registry.EscapedCount >= _playerIds.Length)
            HandleResultsPhase();
    }

    // ════════════════════════════════════════════
    //  Gold API (called by GoldPickup)
    // ════════════════════════════════════════════

    public void AddTeamGold(int amount)
    {
        _teamGold += amount;
    }

    public void OnExplorerKnockedDown()
    {
        _teamTrapsTripped++;
    }

    // ════════════════════════════════════════════
    //  Puzzle registration (called by PuzzleSiteBase)
    // ════════════════════════════════════════════

    public void RegisterPuzzleSite(PuzzleSiteBase site)
    {
        if (site != null && !_puzzleSites.Contains(site))
        {
            _puzzleSites.Add(site);
            site.OnSolved += OnPuzzleSolved;
        }
    }

    // ════════════════════════════════════════════
    //  Results
    // ════════════════════════════════════════════

    private void HandleResultsPhase()
    {
        StopTimer();

        // Tally per-player scores and store via PlayerManager.
        TallyScores();

        // Results phase uses the normal countdown clock for the auto-advance timer.
        GameEvents.FireDisplayTimerCountUp(false);

        TransitionTo(TreasureState.Results);
        StartTimer(resultsHoldSeconds);
        // Note: TransitionTo already broadcasts, and our BroadcastState override
        // routes Results through BroadcastResults() so the full leaderboard goes
        // out in a single message. No extra call needed here.

        GameLog.Divider();
        GameLog.Game($"TREASURE HUNTER COMPLETE — gold {_teamGold}, puzzles {_puzzlesSolved}/{_puzzleSitesTotal}");
        GameLog.Divider();
    }

    private void TallyScores()
    {
        if (_playerIds == null) return;

        // Escape bonus decays linearly with elapsed run time — fast runs score higher.
        int escapeBonus = _painter?.SpawnedExitDoor?.isUnlocked == true
            ? Mathf.Max(0, escapeBonusMax - Mathf.RoundToInt(_runElapsedSeconds * escapeBonusDecayPerSec))
            : 0;

        foreach (var id in _playerIds)
        {
            var explorer = _registry?.GetByPlayerId(id);
            if (explorer == null) continue;

            int score = explorer.GoldCarried;
            score += (_puzzlesSolved * 200);
            if (explorer.IsEscaped) score += 500 + escapeBonus;
            score -= explorer.TrapsTripped * 50;
            score = Mathf.Max(score, 0);

            PlayerManager.Instance.AddScore(id, score);
        }
    }

    private void BroadcastResults()
    {
        var results = BuildResultsArray();
        var msg = new TreasureHunterResultsMessage
        {
            state = "Results",
            timer = Mathf.FloorToInt(_runElapsedSeconds),
            timeRemaining = _runElapsedSeconds,
            puzzlesSolved = _puzzlesSolved,
            puzzlesTotal = _puzzleSitesTotal,
            goldTeamTotal = _teamGold,
            results = results,
            players = PlayerManager.Instance.GetAllPlayerInfos(),
        };
        GameEvents.FireBroadcast(JsonUtility.ToJson(msg));
    }

    private static int GetPlayerScore(string playerId)
    {
        var infos = PlayerManager.Instance.GetAllPlayerInfos();
        foreach (var info in infos) if (info.id == playerId) return info.score;
        return 0;
    }

    private TreasureHunterPlayerResult[] BuildResultsArray()
    {
        if (_playerIds == null) return new TreasureHunterPlayerResult[0];
        var arr = new TreasureHunterPlayerResult[_playerIds.Length];
        for (int i = 0; i < _playerIds.Length; i++)
        {
            string id = _playerIds[i];
            var explorer = _registry?.GetByPlayerId(id);
            arr[i] = new TreasureHunterPlayerResult
            {
                id = id,
                name = PlayerManager.Instance.GetPlayerName(id),
                goldCollected = explorer != null ? explorer.GoldCarried : 0,
                trapsTripped = explorer != null ? explorer.TrapsTripped : 0,
                escaped = explorer != null && explorer.IsEscaped,
                score = GetPlayerScore(id),
            };
        }
        return arr;
    }

    // ════════════════════════════════════════════
    //  Broadcasting (frame state)
    // ════════════════════════════════════════════

    protected override void BroadcastState()
    {
        if (_playerIds == null) return;

        // When the game ends, the frame message lacks the per-player results
        // payload. Route the broadcast through BroadcastResults() so the phone
        // receives the full leaderboard in a single message rather than a
        // partial frame followed by a real results message.
        if (_state == TreasureState.Results)
        {
            BroadcastResults();
            return;
        }

        // Reuse _explorerBuffer to avoid per-frame allocation (mirrors PongManager._paddleBuffer).
        int explorerCount = _registry != null ? _registry.AllExplorers.Count : 0;
        if (_explorerBuffer == null || _explorerBuffer.Length != explorerCount)
            _explorerBuffer = new TreasureHunterExplorerState[explorerCount];

        var all = _registry?.AllExplorers;
        int idx = 0;
        if (all != null)
        {
            foreach (var e in all)
            {
                if (idx >= explorerCount) break;
                if (_explorerBuffer[idx] == null) _explorerBuffer[idx] = new TreasureHunterExplorerState();
                var pos = e.transform.position;
                _explorerBuffer[idx].id = e.PlayerId;
                _explorerBuffer[idx].x = pos.x;
                _explorerBuffer[idx].y = pos.y;
                _explorerBuffer[idx].isDown = e.IsDown;
                _explorerBuffer[idx].gold = e.GoldCarried;
                _explorerBuffer[idx].escaped = e.IsEscaped;
                var dc = e.GetComponent<DownedController>();
                _explorerBuffer[idx].reviveProgress = dc != null ? dc.ReviveProgress : 0f;
                idx++;
            }
        }

        _frameMsg.state = StateToString(_state);
        _frameMsg.timer = TimerRaw;
        _frameMsg.levelIndex = _levelIndex;
        _frameMsg.puzzlesRemaining = _puzzleSitesTotal - _puzzlesSolved;
        _frameMsg.puzzlesTotal = _puzzleSitesTotal;
        _frameMsg.goldTeamTotal = _teamGold;
        _frameMsg.exitUnlocked = _painter?.SpawnedExitDoor?.isUnlocked ?? false;
        _frameMsg.trapsTripped = _teamTrapsTripped;
        _frameMsg.explorers = _explorerBuffer;

        GameEvents.FireBroadcast(JsonUtility.ToJson(_frameMsg));
    }
}
