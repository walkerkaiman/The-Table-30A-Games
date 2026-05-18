using UnityEngine;

/// <summary>
/// Reads <see cref="AcceleratedMover.CurrentVelocity"/> every frame and swaps the target
/// <see cref="SpriteRenderer"/> to the appropriate directional sprite (or animated sequence).
///
/// Usage on ExplorerPrefab:
///   1. Add this component.
///   2. Assign <c>mover</c> (or leave blank — it auto-finds on the same GameObject).
///   3. Assign <c>bodyRenderer</c> (the child SpriteRenderer for the character body).
///   4. Disable <b>Face Move Direction</b> on the <c>AcceleratedMover</c> so the transform
///      stops rotating; this component owns the facing direction instead.
///   5. For each of the four directions, drop in:
///        • <c>idle</c> — sprite shown when standing still facing that way.
///        • <c>walkFrames</c> — array of sprites cycled while moving in that direction.
///          Leave empty if your pack has no walk animation (idle is used while moving).
///
/// The component remembers the last significant direction so the character faces the
/// correct way while standing still.
/// </summary>
public class DirectionalSpriteSwapper : MonoBehaviour
{
    // ── Direction enum ──────────────────────────────────────────────────────────
    public enum Facing { South, North, East, West }

    // ── Per-direction data ──────────────────────────────────────────────────────
    [System.Serializable]
    public class DirectionSet
    {
        [Tooltip("Sprite shown when standing still or when walkFrames is empty.")]
        public Sprite idle;

        [Tooltip("Sprites cycled while moving. Leave empty to reuse idle while moving.")]
        public Sprite[] walkFrames;
    }

    // ── Inspector fields ────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("The SpriteRenderer to drive. Usually the body child of ExplorerPrefab.")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    [Tooltip("Leave blank to auto-find on this GameObject.")]
    [SerializeField] private AcceleratedMover mover;

    [Header("Directional Sprites")]
    [Tooltip("Moving down / toward the camera (default idle pose).")]
    public DirectionSet south;

    [Tooltip("Moving up / away from the camera.")]
    public DirectionSet north;

    [Tooltip("Moving right.")]
    public DirectionSet east;

    [Tooltip("Moving left.")]
    public DirectionSet west;

    [Header("Settings")]
    [Tooltip("Speed (world units/sec) below which the character is considered idle.")]
    [SerializeField] private float idleThreshold = 0.15f;

    [Tooltip("Frames per second when cycling walkFrames.")]
    [SerializeField] private float walkFps = 8f;

    // ── Private state ───────────────────────────────────────────────────────────
    private Facing _facing = Facing.South;
    private float  _frameTimer;
    private int    _frameIndex;

    // ── Unity lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        if (mover == null)
            mover = GetComponent<AcceleratedMover>();
    }

    // LateUpdate runs after FixedUpdate physics ticks so CurrentVelocity is settled.
    private void LateUpdate()
    {
        if (mover == null || bodyRenderer == null) return;

        Vector3 vel = mover.CurrentVelocity;
        bool isMoving = vel.sqrMagnitude > idleThreshold * idleThreshold;

        // Update facing only while actually moving so idle keeps the last direction.
        if (isMoving)
            _facing = VelocityToFacing(vel);

        DirectionSet set = FacingToSet(_facing);
        Sprite next = PickSprite(set, isMoving);

        if (bodyRenderer.sprite != next)
            bodyRenderer.sprite = next;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>Determines cardinal direction from a velocity vector.</summary>
    private static Facing VelocityToFacing(Vector3 vel)
    {
        // Compare absolute components to find the dominant axis, then sign for direction.
        if (Mathf.Abs(vel.x) >= Mathf.Abs(vel.y))
            return vel.x >= 0f ? Facing.East : Facing.West;
        else
            return vel.y >= 0f ? Facing.North : Facing.South;
    }

    private DirectionSet FacingToSet(Facing f) => f switch
    {
        Facing.North => north,
        Facing.East  => east,
        Facing.West  => west,
        _            => south,
    };

    /// <summary>Returns the correct sprite, advancing the walk animation if needed.</summary>
    private Sprite PickSprite(DirectionSet set, bool isMoving)
    {
        if (set == null) return null;

        // No walk frames, or not moving → idle sprite.
        if (!isMoving || set.walkFrames == null || set.walkFrames.Length == 0)
        {
            _frameTimer = 0f;
            _frameIndex = 0;
            return set.idle;
        }

        // Advance the frame timer.
        _frameTimer += Time.deltaTime;
        float frameDuration = walkFps > 0f ? 1f / walkFps : 0.125f;

        if (_frameTimer >= frameDuration)
        {
            _frameTimer -= frameDuration;
            _frameIndex = (_frameIndex + 1) % set.walkFrames.Length;
        }

        Sprite frame = set.walkFrames[_frameIndex];
        return frame != null ? frame : set.idle;
    }
}
