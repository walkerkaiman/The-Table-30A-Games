using UnityEngine;

/// <summary>
/// Rasterizes a single <see cref="WangTileDrawing"/> to a square <see cref="Texture2D"/>
/// with transparent background so adjacent tiles butt seamlessly. Brush thickness
/// varies with drawing speed when per-point timestamps are available
/// (slow stroke = thick, fast stroke = thin).
///
/// Coordinate convention: phone sends (x, y) with Y growing DOWN (canvas convention).
/// Texture pixel space uses Y-up, so we flip with (1 - y) when sampling. This matches
/// how <see cref="TelephoneTableDisplay"/> already handles its strokes.
///
/// Stateless static class — caller owns the Texture2D and decides when to Apply().
/// </summary>
public static class WangTileRenderer
{
    /// <summary>Maximum stroke thickness in texture pixels (slow strokes).</summary>
    public const int MaxThicknessPx = 24;
    /// <summary>Minimum stroke thickness in texture pixels (fast strokes).</summary>
    public const int MinThicknessPx = 4;
    /// <summary>
    /// Speed (in normalized-units-per-second) at which thickness reaches the minimum.
    /// Lower = strokes thin out sooner. A "leisurely" stroke crosses ~0.5 units / sec.
    /// </summary>
    public const float MaxSpeedNormPerSec = 1.5f;

    private static Color32[] _clearBuffer;
    private static int _clearBufferSize;

    /// <summary>
    /// Allocate (or reuse) a Texture2D suitable for tile rendering.
    /// Caller owns disposal.
    /// </summary>
    public static Texture2D AllocateTexture(int sizePx)
    {
        var tex = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        ClearTexture(tex);
        return tex;
    }

    /// <summary>Reset all pixels to transparent. Does not call Apply().</summary>
    public static void ClearTexture(Texture2D tex)
    {
        int sz = tex.width;
        int n = sz * tex.height;
        if (_clearBuffer == null || _clearBufferSize != n)
        {
            _clearBuffer = new Color32[n];
            _clearBufferSize = n;
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < n; i++) _clearBuffer[i] = clear;
        }
        tex.SetPixels32(_clearBuffer);
    }

    /// <summary>
    /// Render the drawing's strokes onto the texture. Caller is responsible for
    /// clearing the texture first (or not, to composite). Does not call Apply().
    /// </summary>
    public static void Render(Texture2D tex, WangTileDrawing drawing, Color fallbackColor)
    {
        if (drawing == null || drawing.strokes == null) return;
        Color baseColor = ParseColor(drawing.color, fallbackColor);
        foreach (var stroke in drawing.strokes)
        {
            if (stroke == null || stroke.points == null || stroke.points.Length < 4) continue;
            Color col = string.IsNullOrEmpty(stroke.color) ? baseColor : ParseColor(stroke.color, baseColor);
            RenderStroke(tex, stroke, col);
        }
    }

    private static void RenderStroke(Texture2D tex, WangTileStroke stroke, Color col)
    {
        int size = tex.width;
        float[] pts = stroke.points;
        float[] ts = stroke.timestamps;
        bool hasTs = ts != null && ts.Length >= 2 && (ts.Length * 2 == pts.Length);

        for (int i = 0; i + 3 < pts.Length; i += 4)
        {
            float ax = pts[i], ay = pts[i + 1];
            float bx = pts[i + 2], by = pts[i + 3];

            int thickness;
            if (hasTs)
            {
                int tsIdxA = i / 2;
                int tsIdxB = tsIdxA + 1;
                float dt = (ts[tsIdxB] - ts[tsIdxA]) * 0.001f; // ms → s
                float dx = bx - ax;
                float dy = by - ay;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float speed = (dt > 0.0001f) ? dist / dt : 0f;
                float t = Mathf.Clamp01(speed / MaxSpeedNormPerSec);
                thickness = Mathf.RoundToInt(Mathf.Lerp(MaxThicknessPx, MinThicknessPx, t));
            }
            else
            {
                thickness = (MaxThicknessPx + MinThicknessPx) / 2;
            }

            int x0 = Mathf.Clamp(Mathf.RoundToInt(ax * (size - 1)), 0, size - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt((1f - ay) * (size - 1)), 0, size - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(bx * (size - 1)), 0, size - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt((1f - by) * (size - 1)), 0, size - 1);
            DrawThickLine(tex, x0, y0, x1, y1, col, thickness);
        }
    }

    private static void DrawThickLine(Texture2D tex, int x0, int y0, int x1, int y1, Color col, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int half = thickness / 2;
        int rSq = half * half;
        int size = tex.width;

        while (true)
        {
            // Filled disc at each step → continuous variable-thickness stroke
            for (int oy = -half; oy <= half; oy++)
            {
                for (int ox = -half; ox <= half; ox++)
                {
                    if (ox * ox + oy * oy > rSq) continue;
                    int px = x0 + ox, py = y0 + oy;
                    if (px < 0 || px >= size || py < 0 || py >= size) continue;
                    tex.SetPixel(px, py, col);
                }
            }

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    private static Color ParseColor(string html, Color fallback)
    {
        if (string.IsNullOrEmpty(html)) return fallback;
        return ColorUtility.TryParseHtmlString(html, out Color c) ? c : fallback;
    }
}
