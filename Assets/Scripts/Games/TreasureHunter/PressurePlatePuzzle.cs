using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cooperative puzzle: N players must stand on the matching rune plates simultaneously.
///
/// Inspector setup (on the PressurePlatePuzzlePrefab):
///   • Set <see cref="requiredPlates"/> to the number of plates that must be pressed at once.
///   • Optionally assign <see cref="plateColorOrder"/> for themed clue generation.
///   • Add <c>PressurePlate</c> child GameObjects; they register themselves in <see cref="Awake"/>.
///     DungeonPainter positions them; for a hand-placed prefab arrange them in the editor.
///   • Set <see cref="solveHoldSeconds"/> > 0 to require players to hold for that long.
///
/// Puzzle is solved when <see cref="requiredPlates"/> plates are pressed simultaneously
/// (and held for <see cref="solveHoldSeconds"/> if configured).
/// </summary>
public class PressurePlatePuzzle : PuzzleSiteBase
{
    [Header("Puzzle Settings")]
    [Tooltip("How many plates must be simultaneously pressed to solve the puzzle.")]
    public int requiredPlates = 2;

    [Tooltip("Optional ordered color sequence (used by the clue generator to build player hints).")]
    public Color[] plateColorOrder;

    [Tooltip("Seconds all required plates must remain pressed to solve. 0 = instant solve.")]
    public float solveHoldSeconds = 0f;

    [Header("State Feedback")]
    [Tooltip("Optional SpriteRenderer that glows / changes when all plates are pressed but " +
             "not yet solved (e.g., a door glowing before it opens).")]
    [SerializeField] private SpriteRenderer allPressedIndicator;

    private readonly List<PressurePlate> _plates = new List<PressurePlate>();
    private float _holdProgress;
    private bool _allPressed;

    // ── Lifecycle ────────────────────────────────

    private void Awake()
    {
        GetComponentsInChildren<PressurePlate>(_plates);
        foreach (var plate in _plates) plate.SetPuzzle(this);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
    }

    private void Update()
    {
        if (IsSolved || !_allPressed || solveHoldSeconds <= 0f) return;

        _holdProgress += Time.deltaTime / solveHoldSeconds;
        if (_holdProgress >= 1f) CompletePuzzle();
    }

    // ── IPuzzleSite ───────────────────────────────

    protected override void OnInitialize(DungeonContext ctx)
    {
        _holdProgress = 0f;
        _allPressed = false;
    }

    // ── Called by PressurePlate ───────────────────

    public void OnPlateStateChanged()
    {
        if (IsSolved) return;

        int pressedCount = 0;
        foreach (var plate in _plates)
            if (plate != null && plate.IsPressed) pressedCount++;

        bool allNowPressed = pressedCount >= requiredPlates;

        if (allNowPressed && !_allPressed)
        {
            _allPressed = true;
            _holdProgress = 0f;
            if (allPressedIndicator != null) allPressedIndicator.enabled = true;

            // Instant solve if hold not required.
            if (solveHoldSeconds <= 0f) CompletePuzzle();
        }
        else if (!allNowPressed && _allPressed)
        {
            _allPressed = false;
            _holdProgress = 0f;
            if (allPressedIndicator != null) allPressedIndicator.enabled = false;
        }
    }

#if UNITY_EDITOR
    protected new void OnValidate()
    {
        if (requiredPlates < 1) requiredPlates = 1;
        if (solveHoldSeconds < 0f) solveHoldSeconds = 0f;
    }
#endif
}
