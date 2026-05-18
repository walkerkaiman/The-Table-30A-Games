using System;

/// <summary>
/// One brush stroke captured from a player's phone canvas. Same shape as
/// <see cref="TelephoneStroke"/> but with optional per-point timestamps so
/// the renderer can vary brush thickness with drawing speed.
/// </summary>
[Serializable]
public class WangTileStroke
{
    /// <summary>Flat array of segments: [x0, y0, x1, y1, ...] in normalized 0..1 space.</summary>
    public float[] points;

    /// <summary>HTML color string (e.g. "#ff8800"). Defaults to white when null/empty.</summary>
    public string color;

    /// <summary>
    /// Optional per-point timestamps in milliseconds from the phone clock, parallel to
    /// <see cref="points"/> (one timestamp per (x,y) pair → half the length of points).
    /// When present, the renderer derives per-segment thickness from speed.
    /// May be null or empty — renderer falls back to constant thickness.
    /// </summary>
    public float[] timestamps;
}

/// <summary>
/// A completed tile drawing ready to be placed in the mosaic.
/// Author identity (id + color) is preserved so each tile's brush honors player color.
/// </summary>
public class WangTileDrawing
{
    public WangTileShape shape;
    public WangTileStroke[] strokes;
    public string playerId;
    public string playerName;
    public string color;        // Player's chosen hex color (e.g. "#64ffda")
    public long submittedAtMs;  // Server timestamp — used for inventory FIFO eviction
}
