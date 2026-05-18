using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wang Tiles session: a continuous, solo-paced drawing game.
/// Each player repeatedly gets a random tile config to draw between two dot anchors;
/// when they submit, the drawing is placed in a shared mosaic via a true WFC matcher.
/// Host taps End Game whenever the tapestry looks good — the current viewport is
/// snapshotted and a QR code shown so anyone can download the result.
///
/// Implements <see cref="IGameSession.AllowsMidGameJoin"/> so a join QR can be shown
/// on the table during play.
/// </summary>
public class WangTilesManager : MonoBehaviour, IGameSession
{
    [Header("Mosaic")]
    [SerializeField] private int viewportWidth = 12;
    [SerializeField] private int viewportHeight = 7;
    [SerializeField] private int inventoryCap = 64;
    [SerializeField] private int cullMargin = 1;
    [Tooltip("Direction the mosaic scrolls when it fills up. Horizontal = along the " +
             "length of a wide table (default); Vertical = grows up/down; Both = " +
             "unconstrained growth in any direction.")]
    [SerializeField] private WangTileMosaic.PanAxis panAxis = WangTileMosaic.PanAxis.Horizontal;

    [Header("Per-Tile Random Color")]
    [Tooltip("Minimum WCAG contrast ratio between the tile color and the table background. " +
             "4.5 = WCAG-AA for normal text; 7 = AAA. Higher values keep tiles legible on " +
             "lighter backgrounds at the cost of variety.")]
    [SerializeField] private float minContrastRatio = 4.5f;
    [Tooltip("Minimum HSV saturation for random tile colors. Higher = more vivid.")]
    [Range(0f, 1f)]
    [SerializeField] private float minSaturation = 0.7f;
    [Tooltip("Minimum HSV value (brightness) for random tile colors.")]
    [Range(0f, 1f)]
    [SerializeField] private float minBrightness = 0.8f;
    [Tooltip("Override the table background color used for contrast checks. " +
             "Leave at default to read it from the main camera's clear color at runtime.")]
    [SerializeField] private bool overrideBackgroundColor = false;
    [SerializeField] private Color backgroundColorOverride = new Color(0.04f, 0.05f, 0.12f, 1f);

    [Header("End Game")]
    [Tooltip("How long the QR / download URL stays on screen before returning to game select.")]
    [SerializeField] private float showingQrSeconds = 30f;

    [Header("Snapshot")]
    [SerializeField] private int snapshotWidth = 1920;
    [SerializeField] private int snapshotHeight = 1080;

    public string GameType => MessageTypes.GameTypeWangTiles;
    public string CurrentState => _state.ToString();

    public enum Phase { Painting, Ending, ShowingQR }

    private Phase _state = Phase.Painting;

    private WangTileMosaic _mosaic;
    private readonly Dictionary<string, WangTileShape> _assignment = new Dictionary<string, WangTileShape>();
    private readonly Dictionary<string, List<WangTileStroke>> _strokeBuffers = new Dictionary<string, List<WangTileStroke>>();
    /// <summary>Color of the tile currently assigned to each player (re-rolled each new tile).</summary>
    private readonly Dictionary<string, string> _currentTileColor = new Dictionary<string, string>();
    private readonly List<string> _playerOrder = new List<string>();
    private readonly System.Random _rng = new System.Random();

    private float _stateTimer;
    private bool _timerActive;
    private string _snapshotImageUrl;
    private Camera _snapshotCamera;

    /// <summary>Fired by phase transitions so corner-of-screen UI (join QR) can react.</summary>
    public static event System.Action<Phase> PhaseChanged;

    public WangTileMosaic Mosaic => _mosaic;

    // ════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════

    private void Start()
    {
        _mosaic = new WangTileMosaic(viewportWidth, viewportHeight, cullMargin, inventoryCap, panAxis);
        _snapshotCamera = Camera.main;
        WangTilesTableDisplay.NotifyMosaicReady(_mosaic);
        GameCoordinator.Instance.RegisterSession(this);
    }

    private void Update()
    {
        if (!_timerActive) return;
        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f)
        {
            _timerActive = false;
            OnTimerExpired();
        }
    }

    // ════════════════════════════════════════════
    //  IGameSession
    // ════════════════════════════════════════════

    public bool AllowsMidGameJoin => _state == Phase.Painting;

    public void OnSessionStart(string[] playerIds)
    {
        GameLog.Divider();
        GameLog.Game($"WANG TILES — {playerIds.Length} player(s), continuous drawing");
        GameLog.Divider();

        _state = Phase.Painting;
        _playerOrder.Clear();
        _currentTileColor.Clear();
        _assignment.Clear();
        _strokeBuffers.Clear();

        foreach (string pid in playerIds)
            RegisterPlayer(pid);

        BroadcastPhaseChange();

        // Send each currently-connected player their first assignment.
        foreach (string pid in playerIds)
        {
            if (PlayerManager.Instance.IsPlayerConnected(pid))
                AssignAndSend(pid);
        }
    }

    public void OnSessionEnd()
    {
        _timerActive = false;
        _mosaic = null;
    }

    public void OnPlayerRejoined(string playerId)
    {
        if (_state == Phase.Painting)
        {
            // Either give them their existing assignment or a fresh one.
            if (!_assignment.ContainsKey(playerId))
                _assignment[playerId] = WangTiles.RandomShape();
            if (!_currentTileColor.ContainsKey(playerId))
                _currentTileColor[playerId] = PickTileColor();
            SendAssignment(playerId);
        }
        else if (_state == Phase.ShowingQR)
        {
            SendShowingQrTo(playerId);
        }
    }

    public void OnPlayerDisconnected(string playerId)
    {
        // Drop any buffered strokes — they'd otherwise hang around until session end.
        // Keep their assignment + tile color in case they reconnect; OnPlayerRejoined
        // will reuse them so the player returns to the same in-flight tile.
        _strokeBuffers.Remove(playerId);
    }

    public void OnPlayerJoinedMidGame(string playerId)
    {
        if (_state != Phase.Painting) return;
        RegisterPlayer(playerId);
        AssignAndSend(playerId);
        GameLog.Round($"Mid-game join: \"{PlayerManager.Instance.GetPlayerName(playerId)}\" → started drawing");
    }

    public void OnGameMessage(string playerId, string messageType, string json)
    {
        switch (messageType)
        {
            case MessageTypes.DrawStroke:
                HandleStroke(playerId, json);
                break;
            case MessageTypes.SubmitDrawing:
                HandleSubmitDrawing(playerId);
                break;
            case MessageTypes.EndGame:
                HandleEndGame(playerId);
                break;
        }
    }

    // ════════════════════════════════════════════
    //  Message Handlers
    // ════════════════════════════════════════════

    private void HandleStroke(string playerId, string json)
    {
        if (_state != Phase.Painting) return;

        var msg = JsonUtility.FromJson<DrawStrokeMessage>(json);
        if (msg.points == null || msg.points.Length < 4) return;

        if (!_strokeBuffers.TryGetValue(playerId, out var buf))
        {
            buf = new List<WangTileStroke>();
            _strokeBuffers[playerId] = buf;
        }

        buf.Add(new WangTileStroke
        {
            points = msg.points,
            color = !string.IsNullOrEmpty(msg.color) ? msg.color : ColorFor(playerId),
            timestamps = msg.timestamps
        });
    }

    private void HandleSubmitDrawing(string playerId)
    {
        if (_state != Phase.Painting) return;
        if (!_assignment.TryGetValue(playerId, out WangTileShape shape)) return;

        WangTileStroke[] strokes = _strokeBuffers.TryGetValue(playerId, out var buf)
            ? buf.ToArray()
            : new WangTileStroke[0];
        _strokeBuffers.Remove(playerId);

        var drawing = new WangTileDrawing
        {
            shape = shape,
            strokes = strokes,
            playerId = playerId,
            playerName = PlayerManager.Instance.GetPlayerName(playerId),
            color = ColorFor(playerId),
            submittedAtMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        bool placed = _mosaic.Submit(drawing);
        string status = placed ? "placed" : $"queued in inventory ({_mosaic.InventoryCount})";
        GameLog.Round($"\"{drawing.playerName}\" submitted a {shape} tile — {status}");

        // Ack the phone so it can clear its canvas immediately
        GameEvents.FireSendToPlayer(playerId, JsonUtility.ToJson(new ConfirmationMessage
        {
            type = MessageTypes.DrawingReceived
        }));

        // Hand them their next assignment right away
        AssignAndSend(playerId);
    }

    private void HandleEndGame(string playerId)
    {
        if (!PlayerManager.Instance.IsHost(playerId))
        {
            GameLog.Warn($"\"{PlayerManager.Instance.GetPlayerName(playerId)}\" tried end_game but is not the host");
            return;
        }

        if (_state != Phase.Painting)
        {
            GameLog.Warn("end_game ignored — game already ending");
            return;
        }

        GameLog.Divider();
        GameLog.Game("Host ended Wang Tiles — capturing snapshot...");
        GameLog.Divider();

        _state = Phase.Ending;
        BroadcastPhaseChange();

        // Defer one frame so any in-flight stroke renders have committed to the camera.
        StartCoroutine(CaptureSnapshotAndShowQr());
    }

    private System.Collections.IEnumerator CaptureSnapshotAndShowQr()
    {
        yield return new WaitForEndOfFrame();

        string url = WangTilesSnapshot.Capture(_snapshotCamera, snapshotWidth, snapshotHeight);
        _snapshotImageUrl = url ?? "";

        _state = Phase.ShowingQR;
        _stateTimer = showingQrSeconds;
        _timerActive = true;

        BroadcastPhaseChange();
        BroadcastShowingQr();

        // Local table-side display can pick up the QR via the static event too.
        WangTilesEndScreen.NotifySnapshotReady(_snapshotImageUrl);
    }

    private void OnTimerExpired()
    {
        if (_state == Phase.ShowingQR)
            GameCoordinator.Instance.OnGameEnded();
    }

    // ════════════════════════════════════════════
    //  Player bookkeeping
    // ════════════════════════════════════════════

    private void RegisterPlayer(string playerId)
    {
        if (_playerOrder.Contains(playerId)) return;
        _playerOrder.Add(playerId);
        if (!_assignment.ContainsKey(playerId))
            _assignment[playerId] = WangTiles.RandomShape();
        if (!_currentTileColor.ContainsKey(playerId))
            _currentTileColor[playerId] = PickTileColor();
    }

    /// <summary>Returns the hex color of the tile the player is CURRENTLY drawing.</summary>
    private string ColorFor(string playerId)
    {
        return _currentTileColor.TryGetValue(playerId, out string c) ? c : "#ffffff";
    }

    private void AssignAndSend(string playerId)
    {
        _assignment[playerId] = WangTiles.RandomShape();
        _currentTileColor[playerId] = PickTileColor();
        SendAssignment(playerId);
    }

    // ════════════════════════════════════════════
    //  Random tile-color picker (high-contrast vs background)
    // ════════════════════════════════════════════

    /// <summary>
    /// Roll a random vivid color, biased into the configured saturation/brightness range,
    /// and verify it meets the <see cref="minContrastRatio"/> against the table background
    /// (WCAG relative-luminance formula). Falls back to pure white or near-black if no
    /// candidate passes within a few attempts — guaranteeing a usable color in all cases.
    /// </summary>
    private string PickTileColor()
    {
        Color bg = GetBackgroundColor();
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float h = (float)_rng.NextDouble();
            float s = Mathf.Lerp(minSaturation, 1f, (float)_rng.NextDouble());
            float v = Mathf.Lerp(minBrightness, 1f, (float)_rng.NextDouble());
            Color c = Color.HSVToRGB(h, s, v);
            if (ContrastRatio(c, bg) >= minContrastRatio)
                return ToHex(c);
        }
        // Couldn't satisfy contrast within budget — pick the extreme that maximizes it.
        float bgL = RelativeLuminance(bg);
        return ToHex(bgL < 0.5f ? Color.white : new Color(0.05f, 0.05f, 0.08f));
    }

    private Color GetBackgroundColor()
    {
        if (overrideBackgroundColor) return backgroundColorOverride;
        if (_snapshotCamera != null) return _snapshotCamera.backgroundColor;
        return backgroundColorOverride;
    }

    private static float ContrastRatio(Color a, Color b)
    {
        float la = RelativeLuminance(a);
        float lb = RelativeLuminance(b);
        float light = Mathf.Max(la, lb);
        float dark  = Mathf.Min(la, lb);
        return (light + 0.05f) / (dark + 0.05f);
    }

    private static float RelativeLuminance(Color c)
    {
        return 0.2126f * Linearize(c.r) + 0.7152f * Linearize(c.g) + 0.0722f * Linearize(c.b);
    }

    private static float Linearize(float v)
    {
        return v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
    }

    private static string ToHex(Color c)
    {
        return string.Format("#{0:X2}{1:X2}{2:X2}",
            Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
            Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
            Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f));
    }

    private void SendAssignment(string playerId)
    {
        if (!_assignment.TryGetValue(playerId, out WangTileShape shape)) return;

        WangTiles.GetEndpointEdges(shape, out WangEdge a, out WangEdge b);
        Vector2 pa = WangTiles.AnchorPosition(shape, a);
        Vector2 pb = WangTiles.AnchorPosition(shape, b);

        var msg = new WangTilesStateMessage
        {
            state = Phase.Painting.ToString(),
            timer = 0,
            players = PlayerManager.Instance.GetAllPlayerInfos(),
            playerColor = ColorFor(playerId),
            assignment = new WangTileAssignment
            {
                shape = WangTiles.Name(shape),
                anchors = new[] { pa.x, pa.y, pb.x, pb.y }
            }
        };
        GameEvents.FireSendToPlayer(playerId, JsonUtility.ToJson(msg));
    }

    private void BroadcastShowingQr()
    {
        foreach (string pid in PlayerManager.Instance.GetAllPlayerIds())
        {
            if (PlayerManager.Instance.IsPlayerConnected(pid))
                SendShowingQrTo(pid);
        }
    }

    private void SendShowingQrTo(string playerId)
    {
        var msg = new WangTilesStateMessage
        {
            state = Phase.ShowingQR.ToString(),
            timer = Mathf.CeilToInt(_stateTimer),
            players = PlayerManager.Instance.GetAllPlayerInfos(),
            playerColor = ColorFor(playerId),
            imageUrl = _snapshotImageUrl,
        };
        GameEvents.FireSendToPlayer(playerId, JsonUtility.ToJson(msg));
    }

    private void BroadcastPhaseChange()
    {
        PhaseChanged?.Invoke(_state);
        GameEvents.FireDisplayState(GameType, CurrentState, Mathf.CeilToInt(_stateTimer));
    }
}
