using UnityEngine;

/// <summary>
/// Static geometry helpers for the polygon Pong game.
/// All math is 2D (Vector2). No Unity physics engine involved.
/// </summary>
public static class PongPhysics
{
    /// <summary>
    /// Line-segment intersection test (segments A1→A2 and B1→B2).
    /// Returns true if they cross; outputs the intersection point and
    /// the parametric t along A1→A2 (0-1 means on the segment).
    /// </summary>
    public static bool SegmentIntersection(
        Vector2 a1, Vector2 a2,
        Vector2 b1, Vector2 b2,
        out Vector2 hit, out float tA)
    {
        hit = Vector2.zero;
        tA = 0f;

        Vector2 d1 = a2 - a1;
        Vector2 d2 = b2 - b1;

        float denom = d1.x * d2.y - d1.y * d2.x;
        if (Mathf.Abs(denom) < 1e-8f) return false;

        Vector2 ab = b1 - a1;
        tA = (ab.x * d2.y - ab.y * d2.x) / denom;
        float tB = (ab.x * d1.y - ab.y * d1.x) / denom;

        if (tA < 0f || tA > 1f || tB < 0f || tB > 1f) return false;

        hit = a1 + d1 * tA;
        return true;
    }

    /// <summary>
    /// Reflects a velocity vector off a surface with the given inward normal.
    /// </summary>
    public static Vector2 Reflect(Vector2 velocity, Vector2 normal)
    {
        return velocity - 2f * Vector2.Dot(velocity, normal) * normal;
    }

    /// <summary>
    /// Compute the inward-pointing normal of a polygon edge (v1→v2),
    /// given the polygon center for orientation.
    /// </summary>
    public static Vector2 InwardNormal(Vector2 v1, Vector2 v2, Vector2 center)
    {
        Vector2 edge = v2 - v1;
        Vector2 n = new Vector2(-edge.y, edge.x).normalized;
        if (Vector2.Dot(n, center - v1) < 0f) n = -n;
        return n;
    }

    /// <summary>
    /// Project point P onto line segment A→B, returning the parametric t (clamped 0-1).
    /// </summary>
    public static float ProjectOntoSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-10f) return 0.5f;
        return Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
    }

    /// <summary>
    /// Closest point on segment A→B to point P.
    /// </summary>
    public static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        float t = ProjectOntoSegment(p, a, b);
        return a + (b - a) * t;
    }

    /// <summary>
    /// Distance from point P to line segment A→B.
    /// </summary>
    public static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        return (p - ClosestPointOnSegment(p, a, b)).magnitude;
    }
}
