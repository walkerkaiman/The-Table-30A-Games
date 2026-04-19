using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D joystick-style input relay. Analog to <see cref="PlayerInputRelay"/> but geared at games
/// where each player's phone sends a continuous 2D vector (direction + magnitude) rather than a
/// single 0-1 axis. Spawns one child GameObject per player at Initialize() time and keeps its
/// <see cref="PlayerJoystickNode"/> component fed with the latest vector.
///
/// Unlike the paddle relay, this class does NOT move transforms for you — the joystick is an
/// intent, not a position. Game code attaches an <see cref="AcceleratedMover"/> (or custom
/// controller) to the spawned prefab and reads <c>PlayerJoystickNode.Stick</c> each frame.
///
/// The relay listens for incoming messages of type <c>joystick_move</c> (configurable). Expected
/// JSON shape: <c>{"type":"joystick_move","x":0.0,"y":1.0}</c> where x/y are in [-1, 1] and the
/// vector length represents stick magnitude. Values outside the unit disc are clamped.
/// </summary>
public class JoystickInputRelay : MonoBehaviour
{
    public enum SpawnPlane
    {
        /// <summary> Ring lies on the XY plane (top-down 2D). </summary>
        XY,
        /// <summary> Ring lies on the XZ plane (top-down 3D). </summary>
        XZ,
    }

    [Header("Node Spawning")]
    [Tooltip("Prefab instantiated per player. Typically holds a visual (shepherd mesh) + " +
             "a movement controller such as AcceleratedMover. A PlayerJoystickNode is added " +
             "automatically if the prefab doesn't already have one.")]
    [SerializeField] private GameObject playerNodePrefab;

    [Header("Message Filtering")]
    [Tooltip("Incoming message types this relay intercepts.")]
    [SerializeField] private string[] listenMessageTypes = { "joystick_move" };

    [Header("Spawn Layout")]
    [Tooltip("Plane the spawn ring is drawn on. Use XY for top-down 2D games, XZ for top-down 3D.")]
    [SerializeField] private SpawnPlane spawnPlane = SpawnPlane.XY;

    [Tooltip("World-space origin the players spawn around.")]
    [SerializeField] private Vector3 spawnCenter = Vector3.zero;

    [Tooltip("Radius (world units) of the circle players are arranged on at spawn time.")]
    [SerializeField] private float spawnRadius = 2f;

    [Tooltip("Offset applied per table side. This Side (0) nodes are placed at -sideOffset, " +
             "That Side (1) at +sideOffset. Use (0,N,0) / (0,0,N) depending on how your table is split.")]
    [SerializeField] private Vector3 sideOffset = Vector3.zero;

    [Header("Dead Zone")]
    [Tooltip("Inputs with magnitude below this are clamped to zero — filters phone sensor noise.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float deadZone = 0.1f;

    private readonly Dictionary<string, PlayerJoystickNode> _nodes = new Dictionary<string, PlayerJoystickNode>();
    private HashSet<string> _listenSet;
    private bool _initialized;

    public IReadOnlyDictionary<string, PlayerJoystickNode> AllNodes => _nodes;

    // ── Unity lifecycle ──────────────────────────

    private void Awake()
    {
        RebuildListenSet();
    }

    private void OnEnable()
    {
        GameEvents.GameMessageReceived += OnGameMessage;
    }

    private void OnDisable()
    {
        GameEvents.GameMessageReceived -= OnGameMessage;
    }

    // ── Public API ───────────────────────────────

    /// <summary>Spawn one child node per player. Safe to call multiple times.</summary>
    public void Initialize(string[] playerIds)
    {
        if (_initialized) Teardown();

        for (int i = 0; i < playerIds.Length; i++)
        {
            string id = playerIds[i];
            string playerName = PlayerManager.Instance.GetPlayerName(id);
            int tableSide = PlayerManager.Instance.GetTableSide(id);

            GameObject go;
            if (playerNodePrefab != null)
            {
                go = Instantiate(playerNodePrefab, transform);
            }
            else
            {
                go = new GameObject();
                go.transform.SetParent(transform);
            }

            var node = go.GetComponent<PlayerJoystickNode>();
            if (node == null) node = go.AddComponent<PlayerJoystickNode>();

            node.Init(id, playerName, i, tableSide);
            go.transform.position = ComputeSpawnPosition(i, playerIds.Length, tableSide);
            go.name = $"Joystick_{playerName}_{id}";

            _nodes[id] = node;
        }

        _initialized = true;
        GameLog.Game($"JoystickInputRelay initialized — {playerIds.Length} player node(s)");
    }

    public void Teardown()
    {
        foreach (var node in _nodes.Values)
        {
            if (node != null && node.gameObject != null) Destroy(node.gameObject);
        }
        _nodes.Clear();
        _initialized = false;
    }

    public PlayerJoystickNode GetNode(string playerId)
    {
        _nodes.TryGetValue(playerId, out var node);
        return node;
    }

    public Vector2 GetStick(string playerId)
    {
        return _nodes.TryGetValue(playerId, out var node) ? node.Stick : Vector2.zero;
    }

    // ── Message handling ─────────────────────────

    private void OnGameMessage(string playerId, string messageType, string json)
    {
        if (!_initialized) return;
        if (!_listenSet.Contains(messageType)) return;
        if (!_nodes.TryGetValue(playerId, out var node)) return;

        float x = ExtractFloat(json, "x");
        float y = ExtractFloat(json, "y");

        var stick = new Vector2(x, y);
        if (stick.sqrMagnitude < deadZone * deadZone)
        {
            stick = Vector2.zero;
        }
        else if (stick.sqrMagnitude > 1f)
        {
            stick.Normalize();
        }

        node.Stick = stick;
        node.LastUpdateTime = Time.time;
    }

    // ── Helpers ──────────────────────────────────

    private Vector3 ComputeSpawnPosition(int index, int playerCount, int tableSide)
    {
        if (playerCount <= 0) return spawnCenter;
        float angle = (index / (float)playerCount) * Mathf.PI * 2f;
        float c = Mathf.Cos(angle);
        float s = Mathf.Sin(angle);
        Vector3 ring = spawnPlane == SpawnPlane.XY
            ? new Vector3(c, s, 0f) * spawnRadius
            : new Vector3(c, 0f, s) * spawnRadius;
        Vector3 sidePush = tableSide == 0 ? -sideOffset : sideOffset;
        return spawnCenter + ring + sidePush;
    }

    private void RebuildListenSet()
    {
        _listenSet = new HashSet<string>();
        if (listenMessageTypes == null) return;
        foreach (string t in listenMessageTypes)
        {
            if (!string.IsNullOrEmpty(t)) _listenSet.Add(t);
        }
    }

    /// <summary>
    /// Lightweight JSON float extraction. We handle the high-frequency input path without
    /// allocating a full deserialized object — the regex-free implementation is both faster
    /// and churns less GC than JsonUtility on 30-60Hz messages.
    /// </summary>
    private static float ExtractFloat(string json, string fieldName)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName)) return 0f;

        string pattern = "\"" + fieldName + "\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return 0f;

        idx = json.IndexOf(':', idx + pattern.Length);
        if (idx < 0) return 0f;
        idx++;

        while (idx < json.Length && json[idx] == ' ') idx++;

        int start = idx;
        while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '.' || json[idx] == '-'))
            idx++;

        if (start == idx) return 0f;

        return float.TryParse(
            json.Substring(start, idx - start),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float result) ? result : 0f;
    }
}
