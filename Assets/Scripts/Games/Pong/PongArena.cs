using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates and renders the polygon arena for N-player Pong.
/// Creates a regular polygon, draws walls and paddles using LineRenderers,
/// and provides geometry queries for PongBall collision.
/// </summary>
public class PongArena : MonoBehaviour
{
    [Header("Arena")]
    [SerializeField] private float radius = 4f;
    [SerializeField] private float paddleFraction = 0.35f;
    [SerializeField] private float wallWidth = 0.08f;
    [SerializeField] private float paddleWidth = 0.15f;

    [Header("Colors")]
    [SerializeField] private Color wallColor = new Color(0.3f, 0.35f, 0.5f);
    [SerializeField] private Color eliminatedWallColor = new Color(0.5f, 0.2f, 0.2f);

    private static readonly Color[] PlayerColors =
    {
        new Color(0.39f, 1f, 0.85f),    // cyan
        new Color(1f, 0.72f, 0.29f),    // orange
        new Color(0.72f, 0.45f, 1f),    // purple
        new Color(1f, 0.42f, 0.42f),    // red
        new Color(0.42f, 1f, 0.42f),    // green
        new Color(1f, 0.84f, 0.42f),    // yellow
        new Color(0.42f, 0.73f, 1f),    // blue
        new Color(1f, 0.42f, 0.84f),    // pink
    };

    public int NumSides { get; private set; }
    public Vector2[] Vertices { get; private set; }

    private struct SideInfo
    {
        public int ownerIndex;         // -1 = wall
        public bool eliminated;
        public LineRenderer wallLR;
        public LineRenderer paddleLR;
        public TextMesh nameLabel;
    }

    private SideInfo[] _sides;
    private readonly Dictionary<int, float> _paddlePositions = new Dictionary<int, float>();
    private readonly Dictionary<int, string> _sideToPlayerId = new Dictionary<int, string>();

    // ── Public API ───────────────────────────────

    public void Build(string[] playerIds)
    {
        int playerCount = playerIds.Length;
        NumSides = playerCount <= 2 ? 4 : playerCount;

        GenerateVertices();
        _sides = new SideInfo[NumSides];

        AssignPlayers(playerIds);

        for (int i = 0; i < NumSides; i++)
        {
            bool isPlayerSide = _sides[i].ownerIndex >= 0;
            _sides[i].wallLR = CreateLineRenderer($"Wall_{i}", wallWidth, wallColor);
            UpdateWallLine(i);

            if (isPlayerSide)
            {
                int pIdx = _sides[i].ownerIndex;
                Color c = PlayerColors[pIdx % PlayerColors.Length];
                _sides[i].paddleLR = CreateLineRenderer($"Paddle_{i}", paddleWidth, c);
                _paddlePositions[i] = 0.5f;
                UpdatePaddleLine(i);

                _sides[i].nameLabel = CreateNameLabel(i, playerIds[pIdx], c);
            }
        }
    }

    public void SetPaddlePosition(int sideIndex, float normalizedPos)
    {
        _paddlePositions[sideIndex] = Mathf.Clamp01(normalizedPos);
        UpdatePaddleLine(sideIndex);
    }

    public void EliminateSide(int sideIndex)
    {
        if (sideIndex < 0 || sideIndex >= NumSides) return;
        _sides[sideIndex].eliminated = true;
        if (_sides[sideIndex].paddleLR != null)
            _sides[sideIndex].paddleLR.gameObject.SetActive(false);
        if (_sides[sideIndex].wallLR != null)
        {
            _sides[sideIndex].wallLR.startColor = eliminatedWallColor;
            _sides[sideIndex].wallLR.endColor = eliminatedWallColor;
        }
        if (_sides[sideIndex].nameLabel != null)
            _sides[sideIndex].nameLabel.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
    }

    public bool IsSideEliminated(int sideIndex) =>
        sideIndex >= 0 && sideIndex < NumSides && _sides[sideIndex].eliminated;

    public bool IsPlayerSide(int sideIndex) =>
        sideIndex >= 0 && sideIndex < NumSides && _sides[sideIndex].ownerIndex >= 0;

    public int GetPlayerIndexForSide(int sideIndex) =>
        sideIndex >= 0 && sideIndex < NumSides ? _sides[sideIndex].ownerIndex : -1;

    public string GetPlayerIdForSide(int sideIndex) =>
        _sideToPlayerId.TryGetValue(sideIndex, out string id) ? id : null;

    public int GetSideForPlayerId(string playerId)
    {
        foreach (var kvp in _sideToPlayerId)
            if (kvp.Value == playerId) return kvp.Key;
        return -1;
    }

    /// <summary>
    /// Returns the paddle segment endpoints for a side, accounting for current position.
    /// </summary>
    public void GetPaddleSegment(int sideIndex, out Vector2 p1, out Vector2 p2)
    {
        Vector2 v1 = Vertices[sideIndex];
        Vector2 v2 = Vertices[(sideIndex + 1) % NumSides];
        float pos = _paddlePositions.ContainsKey(sideIndex) ? _paddlePositions[sideIndex] : 0.5f;
        float halfPaddle = paddleFraction * 0.5f;
        float center = Mathf.Lerp(halfPaddle, 1f - halfPaddle, pos);
        p1 = Vector2.Lerp(v1, v2, center - halfPaddle);
        p2 = Vector2.Lerp(v1, v2, center + halfPaddle);
    }

    public void GetSideEndpoints(int sideIndex, out Vector2 v1, out Vector2 v2)
    {
        v1 = Vertices[sideIndex];
        v2 = Vertices[(sideIndex + 1) % NumSides];
    }

    public Color GetPlayerColor(int playerIndex) =>
        PlayerColors[playerIndex % PlayerColors.Length];

    // ── Geometry ─────────────────────────────────

    private void GenerateVertices()
    {
        Vertices = new Vector2[NumSides];
        for (int i = 0; i < NumSides; i++)
        {
            float angle = 2f * Mathf.PI * i / NumSides - Mathf.PI / 2f;
            Vertices[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }

    private void AssignPlayers(string[] playerIds)
    {
        _sideToPlayerId.Clear();

        if (playerIds.Length == 2 && NumSides == 4)
        {
            _sides[0].ownerIndex = 0; _sideToPlayerId[0] = playerIds[0];
            _sides[1].ownerIndex = -1;
            _sides[2].ownerIndex = 1; _sideToPlayerId[2] = playerIds[1];
            _sides[3].ownerIndex = -1;
        }
        else
        {
            for (int i = 0; i < NumSides; i++)
            {
                if (i < playerIds.Length)
                {
                    _sides[i].ownerIndex = i;
                    _sideToPlayerId[i] = playerIds[i];
                }
                else
                {
                    _sides[i].ownerIndex = -1;
                }
            }
        }
    }

    // ── Rendering ────────────────────────────────

    private LineRenderer CreateLineRenderer(string name, float width, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.positionCount = 2;
        lr.sortingOrder = 1;
        return lr;
    }

    private void UpdateWallLine(int sideIndex)
    {
        var lr = _sides[sideIndex].wallLR;
        if (lr == null) return;
        Vector2 v1 = Vertices[sideIndex];
        Vector2 v2 = Vertices[(sideIndex + 1) % NumSides];
        lr.SetPosition(0, new Vector3(v1.x, v1.y, 0));
        lr.SetPosition(1, new Vector3(v2.x, v2.y, 0));
    }

    private void UpdatePaddleLine(int sideIndex)
    {
        var lr = _sides[sideIndex].paddleLR;
        if (lr == null) return;
        GetPaddleSegment(sideIndex, out Vector2 p1, out Vector2 p2);
        lr.SetPosition(0, new Vector3(p1.x, p1.y, 0));
        lr.SetPosition(1, new Vector3(p2.x, p2.y, 0));
    }

    private TextMesh CreateNameLabel(int sideIndex, string playerId, Color color)
    {
        Vector2 v1 = Vertices[sideIndex];
        Vector2 v2 = Vertices[(sideIndex + 1) % NumSides];
        Vector2 midpoint = (v1 + v2) * 0.5f;
        Vector2 outward = (midpoint - Vector2.zero).normalized;
        Vector2 labelPos = midpoint + outward * 0.6f;

        string playerName = PlayerManager.Instance.GetPlayerName(playerId);

        var go = new GameObject($"Label_{sideIndex}");
        go.transform.SetParent(transform);
        go.transform.position = new Vector3(labelPos.x, labelPos.y, 0);

        var tm = go.AddComponent<TextMesh>();
        tm.text = playerName;
        tm.fontSize = 24;
        tm.characterSize = 0.15f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        return tm;
    }

    public void UpdateNameLabel(int sideIndex, string text)
    {
        if (sideIndex >= 0 && sideIndex < NumSides && _sides[sideIndex].nameLabel != null)
            _sides[sideIndex].nameLabel.text = text;
    }
}
