using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns one child GameObject per player and keeps it updated with real-time
/// positional input from phone WebSocket messages.
///
/// Place this component in any game scene that needs near-realtime phone input.
/// Call Initialize(playerIds) from your IGameSession.OnSessionStart, then read
/// from GetRawInput / GetNode / child transforms in your game loop.
///
/// Table-side positioning is handled here (not in each game) via sideOffset.
/// This Side (0) nodes are offset by -sideOffset, That Side (1) by +sideOffset,
/// so prefabs automatically appear on the correct physical half of the table.
///
/// Subscribes directly to GameEvents.GameMessageReceived so input arrives with
/// minimal latency, independent of the IGameSession message routing.
/// </summary>
public class PlayerInputRelay : MonoBehaviour
{
    [Header("Node Spawning")]
    [Tooltip("Optional prefab instantiated per player. If null, an empty GameObject is created. " +
             "A PlayerInputNode component is added automatically if the prefab lacks one.")]
    [SerializeField] private GameObject playerNodePrefab;

    [Header("Message Filtering")]
    [Tooltip("Which incoming message types this relay should intercept.")]
    [SerializeField] private string[] listenMessageTypes = { "paddle_move", "player_input" };

    [Tooltip("JSON field name to extract as the primary (1D) input value. " +
             "Falls back to 'x' if the named field is not found.")]
    [SerializeField] private string inputFieldName = "position";

    [Header("Transform Mapping")]
    [Tooltip("When true, each node's transform is moved to reflect its input value.")]
    [SerializeField] private bool applyToTransform = true;

    [Tooltip("World axis the 0-1 input maps onto.")]
    [SerializeField] private Vector3 positionAxis = Vector3.right;

    [Tooltip("Total world-space extent along positionAxis (centered on positionOffset). " +
             "Input 0 maps to -range/2, input 1 maps to +range/2.")]
    [SerializeField] private float positionRange = 8f;

    [Tooltip("World-space origin for the position mapping.")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;

    [Tooltip("Lerp speed for transform smoothing. 0 = instant snap.")]
    [SerializeField] private float smoothSpeed = 20f;

    [Header("Table Side Positioning")]
    [Tooltip("Offset from center that separates the two table sides. " +
             "'This Side' (0) nodes are placed at -sideOffset, 'That Side' (1) at +sideOffset. " +
             "For a table split along Y, use e.g. (0, 3, 0).")]
    [SerializeField] private Vector3 sideOffset = new Vector3(0f, 3f, 0f);

    private readonly Dictionary<string, PlayerInputNode> _nodes = new Dictionary<string, PlayerInputNode>();
    private HashSet<string> _listenSet;
    private bool _initialized;

    public IReadOnlyDictionary<string, PlayerInputNode> AllNodes => _nodes;
    public int NodeCount => _nodes.Count;

    // ── Lifecycle ─────────────────────────────────

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

    private void Update()
    {
        if (!_initialized || !applyToTransform) return;

        foreach (var node in _nodes.Values)
        {
            Vector3 target = ComputeWorldPosition(node.RawInput, node.TableSide);

            if (smoothSpeed <= 0f)
            {
                node.transform.localPosition = target;
            }
            else
            {
                node.transform.localPosition = Vector3.Lerp(
                    node.transform.localPosition,
                    target,
                    Time.deltaTime * smoothSpeed
                );
            }
        }
    }

    // ── Public API ────────────────────────────────

    /// <summary>
    /// Spawn one child node per player. Call from IGameSession.OnSessionStart.
    /// Safe to call multiple times — tears down existing nodes first.
    /// </summary>
    public void Initialize(string[] playerIds)
    {
        if (_initialized) Teardown();

        for (int i = 0; i < playerIds.Length; i++)
        {
            string id = playerIds[i];
            string playerName = PlayerManager.Instance.GetPlayerName(id);

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

            var node = go.GetComponent<PlayerInputNode>();
            if (node == null)
                node = go.AddComponent<PlayerInputNode>();

            int tableSide = PlayerManager.Instance.GetTableSide(id);
            node.Init(id, playerName, i, tableSide);
            go.transform.localPosition = ComputeWorldPosition(0.5f, tableSide);

            _nodes[id] = node;
        }

        _initialized = true;
        GameLog.Game($"PlayerInputRelay initialized — {playerIds.Length} node(s) spawned");
    }

    /// <summary> Destroy all child nodes and reset state. </summary>
    public void Teardown()
    {
        foreach (var node in _nodes.Values)
        {
            if (node != null && node.gameObject != null)
                Destroy(node.gameObject);
        }

        _nodes.Clear();
        _initialized = false;
    }

    public PlayerInputNode GetNode(string playerId)
    {
        _nodes.TryGetValue(playerId, out var node);
        return node;
    }

    public float GetRawInput(string playerId)
    {
        return _nodes.TryGetValue(playerId, out var node) ? node.RawInput : 0.5f;
    }

    public Vector2 GetRawInput2D(string playerId)
    {
        return _nodes.TryGetValue(playerId, out var node) ? node.RawInput2D : new Vector2(0.5f, 0.5f);
    }

    public Transform GetPlayerTransform(string playerId)
    {
        return _nodes.TryGetValue(playerId, out var node) ? node.transform : null;
    }

    // ── Message Handling ──────────────────────────

    private void OnGameMessage(string playerId, string messageType, string json)
    {
        if (!_initialized) return;
        if (!_listenSet.Contains(messageType)) return;
        if (!_nodes.TryGetValue(playerId, out var node)) return;

        float primary = ExtractFloat(json, inputFieldName);
        float secondary = ExtractFloat(json, "y");

        node.RawInput = Mathf.Clamp01(primary);
        node.RawInput2D = new Vector2(Mathf.Clamp01(primary), Mathf.Clamp01(secondary));
        node.LastUpdateTime = Time.time;
    }

    // ── Helpers ───────────────────────────────────

    private void RebuildListenSet()
    {
        _listenSet = new HashSet<string>();
        if (listenMessageTypes != null)
        {
            foreach (string t in listenMessageTypes)
            {
                if (!string.IsNullOrEmpty(t))
                    _listenSet.Add(t);
            }
        }
    }

    /// <summary>
    /// Maps a 0-1 input value to a world position along positionAxis,
    /// offset to the correct table half based on tableSide.
    /// </summary>
    private Vector3 ComputeWorldPosition(float normalizedInput, int tableSide)
    {
        float t = normalizedInput - 0.5f; // -0.5 .. +0.5
        Vector3 basePos = positionOffset + positionAxis.normalized * (t * positionRange);
        return basePos + (tableSide == 0 ? -sideOffset : sideOffset);
    }

    /// <summary>
    /// Lightweight JSON field extraction. Avoids allocating a full deserialized object
    /// for every high-frequency input message. Returns 0.5 if the field is not found.
    /// </summary>
    private static float ExtractFloat(string json, string fieldName)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
            return 0.5f;

        string pattern = "\"" + fieldName + "\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0)
        {
            if (fieldName == "position")
                return ExtractFloat(json, "x");
            return 0.5f;
        }

        idx = json.IndexOf(':', idx + pattern.Length);
        if (idx < 0) return 0.5f;
        idx++;

        while (idx < json.Length && json[idx] == ' ') idx++;

        int start = idx;
        while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '.' || json[idx] == '-'))
            idx++;

        if (start == idx) return 0.5f;

        if (float.TryParse(json.Substring(start, idx - start),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float result))
        {
            return result;
        }

        return 0.5f;
    }
}
