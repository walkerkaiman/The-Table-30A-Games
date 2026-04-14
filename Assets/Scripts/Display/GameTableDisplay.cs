using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Shared base class for table-side game displays.
/// Drop this (or a subclass) into any game scene to show phase name,
/// timer, and game title on the projected table via MirroredTableCamera.
///
/// Subscribes to GameEvents.DisplayStateChanged, which fires automatically
/// from RoundBasedGameSession.TransitionTo() and manually from custom
/// IGameSession implementations like TelephoneManager.
///
/// Manages MeshRenderer visibility on all assigned TextMeshPro objects so
/// nothing renders until the first state update arrives, and renderers
/// toggle cleanly as phases change.
///
/// The timer counts down locally each frame from the value received on
/// the last phase change — close enough for a visual countdown.
/// </summary>
public class GameTableDisplay : MonoBehaviour
{
    [Header("Text References (world-space TextMeshPro)")]
    [SerializeField] protected TextMeshPro gameNameText;
    [SerializeField] protected TextMeshPro phaseText;
    [SerializeField] protected TextMeshPro timerText;

    protected string _gameType;
    protected string _phase;
    protected float _displayTimer;

    protected MeshRenderer _gameNameRenderer;
    protected MeshRenderer _phaseRenderer;
    protected MeshRenderer _timerRenderer;

    private static readonly Dictionary<string, string> GameNameOverrides = new Dictionary<string, string>
    {
        { "quiplash",       "Quiplash" },
        { "fibbage",        "Fibbage" },
        { "caption_contest","Caption Contest" },
        { "price_is_close", "The Price Is Close" },
        { "hot_potato",     "Hot Potato" },
        { "telephone",      "Telephone" },
        { "pong",           "Pong Arena" },
    };

    private static readonly Dictionary<string, string> PhaseOverrides = new Dictionary<string, string>
    {
        { "ShowPrompt",    "Get Ready..." },
        { "Answer",        "Write Your Answer!" },
        { "Voting",        "Vote!" },
        { "RoundResults",  "Results" },
        { "GameOver",      "Game Over!" },
        { "WriteBluff",    "Write a Bluff!" },
        { "Vote",          "Vote!" },
        { "ShowImage",     "Look at This!" },
        { "WriteCaption",  "Write a Caption!" },
        { "ShowItem",      "Check This Out!" },
        { "Guess",         "Make Your Guess!" },
        { "Reveal",        "The Answer!" },
        { "WritePrompt",   "Write a Prompt!" },
        { "Draw",          "Drawing..." },
        { "Describe",      "Describe the Drawing!" },
        { "RevealPause",   "Next Chain..." },
        { "Done",          "Thanks for Playing!" },
        { "PreRound",      "Get Ready..." },
        { "Playing",       "Go!" },
        { "Exploded",      "BOOM!" },
        { "Countdown",     "Get Ready..." },
        { "GoalScored",    "Goal!" },
    };

    protected virtual void Awake()
    {
        _gameNameRenderer = CacheRenderer(gameNameText);
        _phaseRenderer = CacheRenderer(phaseText);
        _timerRenderer = CacheRenderer(timerText);

        SetRendererEnabled(_gameNameRenderer, false);
        SetRendererEnabled(_phaseRenderer, false);
        SetRendererEnabled(_timerRenderer, false);
    }

    protected virtual void OnEnable()
    {
        GameEvents.DisplayStateChanged += OnDisplayStateChanged;
    }

    protected virtual void OnDisable()
    {
        GameEvents.DisplayStateChanged -= OnDisplayStateChanged;
    }

    protected virtual void Update()
    {
        if (_displayTimer > 0f)
        {
            _displayTimer -= Time.deltaTime;
            if (_displayTimer < 0f) _displayTimer = 0f;
        }

        if (timerText != null)
        {
            int t = Mathf.CeilToInt(_displayTimer);
            bool hasTime = t > 0;
            timerText.text = hasTime ? t.ToString() : "";
            SetRendererEnabled(_timerRenderer, hasTime);
        }
    }

    private void OnDisplayStateChanged(string gameType, string phase, int timer)
    {
        _gameType = gameType;
        _phase = phase;
        _displayTimer = timer;

        if (gameNameText != null)
        {
            gameNameText.text = FormatGameName(gameType);
            SetRendererEnabled(_gameNameRenderer, true);
        }

        if (phaseText != null)
        {
            phaseText.text = FormatPhase(phase);
            SetRendererEnabled(_phaseRenderer, true);
        }

        if (timerText != null)
        {
            bool hasTime = timer > 0;
            timerText.text = hasTime ? timer.ToString() : "";
            SetRendererEnabled(_timerRenderer, hasTime);
        }

        OnPhaseChanged(gameType, phase, timer);
    }

    /// <summary>Override in subclasses to react to phase changes with game-specific logic.</summary>
    protected virtual void OnPhaseChanged(string gameType, string phase, int timer) { }

    protected virtual string FormatGameName(string gameType)
    {
        return GameNameOverrides.TryGetValue(gameType, out var name) ? name : gameType;
    }

    protected virtual string FormatPhase(string phase)
    {
        return PhaseOverrides.TryGetValue(phase, out var friendly) ? friendly : phase;
    }

    // ════════════════════════════════════════════
    //  MeshRenderer Helpers
    // ════════════════════════════════════════════

    protected static MeshRenderer CacheRenderer(TextMeshPro tmp)
    {
        return tmp != null ? tmp.GetComponent<MeshRenderer>() : null;
    }

    protected static void SetRendererEnabled(MeshRenderer mr, bool enabled)
    {
        if (mr != null) mr.enabled = enabled;
    }

    /// <summary>Show or hide the base text elements (game name, phase, timer) via MeshRenderer.</summary>
    protected void SetBaseTextVisible(bool visible)
    {
        SetRendererEnabled(_gameNameRenderer, visible);
        SetRendererEnabled(_phaseRenderer, visible);
        SetRendererEnabled(_timerRenderer, visible && _displayTimer > 0f);
    }
}
