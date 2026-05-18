using UnityEngine;

/// <summary>
/// The 6 possible tile shapes for the binary Wang-tile alphabet.
/// Each tile has exactly two "endpoint" edges (where the drawn line touches the border)
/// and two "blank" edges. Letters describe which edges have endpoints (N=top, E=right,
/// S=bottom, W=left). Endpoints are always placed at the midpoint of the edge.
/// </summary>
public enum WangTileShape
{
    NS, // top + bottom
    EW, // left + right
    NE, // top + right
    NW, // top + left
    SE, // bottom + right
    SW, // bottom + left
}

/// <summary>
/// Cardinal edges of a tile, ordered N, E, S, W (consistent with array indexing).
/// </summary>
public enum WangEdge
{
    N = 0,
    E = 1,
    S = 2,
    W = 3,
}

/// <summary>
/// Static helpers for the 6-tile Wang alphabet: edge math, adjacency rule, anchor
/// positions in normalized tile space, and config lookup from edge signatures.
///
/// Edge signature convention: a 4-bit mask where bit 0=N, bit 1=E, bit 2=S, bit 3=W.
/// A set bit means "this edge has an endpoint"; a clear bit means "blank".
/// Only signatures with exactly 2 bits set are valid (the 6 shapes above).
/// </summary>
public static class WangTiles
{
    /// <summary>Total number of valid tile shapes in the binary alphabet.</summary>
    public const int ShapeCount = 6;

    private static readonly WangTileShape[] AllShapes =
    {
        WangTileShape.NS, WangTileShape.EW,
        WangTileShape.NE, WangTileShape.NW,
        WangTileShape.SE, WangTileShape.SW,
    };

    /// <summary>True if the given shape has an endpoint on the given edge.</summary>
    public static bool HasEndpoint(WangTileShape shape, WangEdge edge)
    {
        switch (shape)
        {
            case WangTileShape.NS: return edge == WangEdge.N || edge == WangEdge.S;
            case WangTileShape.EW: return edge == WangEdge.E || edge == WangEdge.W;
            case WangTileShape.NE: return edge == WangEdge.N || edge == WangEdge.E;
            case WangTileShape.NW: return edge == WangEdge.N || edge == WangEdge.W;
            case WangTileShape.SE: return edge == WangEdge.S || edge == WangEdge.E;
            case WangTileShape.SW: return edge == WangEdge.S || edge == WangEdge.W;
            default: return false;
        }
    }

    /// <summary>The opposite edge — used for adjacency checks across a shared border.</summary>
    public static WangEdge Opposite(WangEdge edge)
    {
        switch (edge)
        {
            case WangEdge.N: return WangEdge.S;
            case WangEdge.S: return WangEdge.N;
            case WangEdge.E: return WangEdge.W;
            case WangEdge.W: return WangEdge.E;
            default: return edge;
        }
    }

    /// <summary>
    /// Anchor position in normalized tile space (0..1, origin top-left to match canvas
    /// coordinates the phone sends). Returns Vector2(NaN, NaN) if the edge is blank.
    /// </summary>
    public static Vector2 AnchorPosition(WangTileShape shape, WangEdge edge)
    {
        if (!HasEndpoint(shape, edge)) return new Vector2(float.NaN, float.NaN);
        switch (edge)
        {
            case WangEdge.N: return new Vector2(0.5f, 0f);
            case WangEdge.E: return new Vector2(1f, 0.5f);
            case WangEdge.S: return new Vector2(0.5f, 1f);
            case WangEdge.W: return new Vector2(0f, 0.5f);
            default: return new Vector2(float.NaN, float.NaN);
        }
    }

    /// <summary>The two endpoint edges of the given shape, in N,E,S,W order.</summary>
    public static void GetEndpointEdges(WangTileShape shape, out WangEdge a, out WangEdge b)
    {
        switch (shape)
        {
            case WangTileShape.NS: a = WangEdge.N; b = WangEdge.S; return;
            case WangTileShape.EW: a = WangEdge.E; b = WangEdge.W; return;
            case WangTileShape.NE: a = WangEdge.N; b = WangEdge.E; return;
            case WangTileShape.NW: a = WangEdge.N; b = WangEdge.W; return;
            case WangTileShape.SE: a = WangEdge.S; b = WangEdge.E; return;
            case WangTileShape.SW: a = WangEdge.S; b = WangEdge.W; return;
            default: a = WangEdge.N; b = WangEdge.S; return;
        }
    }

    /// <summary>Returns a uniformly-random tile shape.</summary>
    public static WangTileShape RandomShape()
    {
        return AllShapes[Random.Range(0, AllShapes.Length)];
    }

    /// <summary>
    /// Pack the four edges of a shape into a 4-bit signature (bit 0=N, 1=E, 2=S, 3=W).
    /// </summary>
    public static int Signature(WangTileShape shape)
    {
        int sig = 0;
        if (HasEndpoint(shape, WangEdge.N)) sig |= 1 << 0;
        if (HasEndpoint(shape, WangEdge.E)) sig |= 1 << 1;
        if (HasEndpoint(shape, WangEdge.S)) sig |= 1 << 2;
        if (HasEndpoint(shape, WangEdge.W)) sig |= 1 << 3;
        return sig;
    }

    /// <summary>
    /// Grid-step delta for an edge. (N = up = +Y; E = right = +X.)
    /// </summary>
    public static Vector2Int Step(WangEdge edge)
    {
        switch (edge)
        {
            case WangEdge.N: return new Vector2Int(0, 1);
            case WangEdge.E: return new Vector2Int(1, 0);
            case WangEdge.S: return new Vector2Int(0, -1);
            case WangEdge.W: return new Vector2Int(-1, 0);
            default: return Vector2Int.zero;
        }
    }

    /// <summary>String name used in the network protocol (matches enum).</summary>
    public static string Name(WangTileShape shape) => shape.ToString();
}
