using System;
using UnityEngine;

/// <summary>
/// Abstract MonoBehaviour base for all puzzle site implementations.
/// Handles self-registration with <see cref="TreasureHunterManager"/> on enable/disable,
/// and exposes the canonical <see cref="OnSolved"/> event and <see cref="IsSolved"/> flag.
///
/// Subclasses override <see cref="OnInitialize"/> to apply layout-derived data and
/// call <see cref="CompletePuzzle"/> when their solve condition is met.
/// </summary>
public abstract class PuzzleSiteBase : MonoBehaviour, IPuzzleSite
{
    public event Action<IPuzzleSite> OnSolved;
    public bool IsSolved { get; private set; }

    [Header("Puzzle Base")]
    [Tooltip("Optional VFX/sound trigger activated when the puzzle is solved.")]
    [SerializeField] protected GameObject solvedVfx;

    protected virtual void OnEnable()
    {
        TreasureHunterManager.Instance?.RegisterPuzzleSite(this);
    }

    protected virtual void OnDisable()
    {
        // Note: manager holds the list; unregister is handled by the manager on session end.
    }

    // ── IPuzzleSite ───────────────────────────────

    public void Initialize(DungeonContext ctx)
    {
        IsSolved = false;
        OnInitialize(ctx);
    }

    // ── Abstract / virtual API ────────────────────

    /// <summary>Called by Initialize. Apply layout-derived config here.</summary>
    protected abstract void OnInitialize(DungeonContext ctx);

    // ── Protected helpers ────────────────────────

    /// <summary>Call this from a subclass when the solve condition is met.</summary>
    protected void CompletePuzzle()
    {
        if (IsSolved) return;
        IsSolved = true;

        if (solvedVfx != null) solvedVfx.SetActive(true);

        OnSolved?.Invoke(this);
        GameLog.Game($"Puzzle solved: {gameObject.name}");
    }
}
