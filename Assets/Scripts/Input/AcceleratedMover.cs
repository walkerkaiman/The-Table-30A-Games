using UnityEngine;

/// <summary>
/// Reusable physics-friendly character mover in the style of Mario 64: a 2D input vector (stick)
/// drives target velocity capped at <see cref="maxSpeed"/>; actual velocity is lerped toward the
/// target using separate acceleration and deceleration constants so letting go of the stick slides
/// the character to a stop rather than snapping.
///
/// Input side: expects a component implementing <see cref="IJoystickInputSource"/> on the same
/// GameObject (or injected via <see cref="SetSource"/>). The mover does not care where the stick
/// vector came from — phone WebSocket, keyboard, AI script. This is what keeps it reusable across
/// games.
///
/// Output side: supports three backends, chosen automatically by what components are present.
///   • Rigidbody2D — top-down 2D (the default for this project's Sheep Herder).
///   • Rigidbody   — 3D. Works in planar mode (inputPlane=XZ) or vertical (XY).
///   • Transform   — fallback for kinematic / trigger-only setups.
///
/// Attach this to the same GameObject as the rigidbody (if any) and configure the Inspector.
/// </summary>
public class AcceleratedMover : MonoBehaviour
{
    public enum InputPlane
    {
        /// <summary> Stick X→world X, stick Y→world Y (top-down 2D or side-view). </summary>
        XY,
        /// <summary> Stick X→world X, stick Y→world Z (top-down 3D). </summary>
        XZ,
    }

    [Header("Movement")]
    [Tooltip("Maximum speed the character reaches while holding the stick fully.")]
    [SerializeField] private float maxSpeed = 6f;

    [Tooltip("Units/sec² added toward the target velocity while input is non-zero. Higher = snappier.")]
    [SerializeField] private float acceleration = 30f;

    [Tooltip("Units/sec² bled from current velocity when input is zero. Higher = stops faster.")]
    [SerializeField] private float deceleration = 20f;

    [Header("Mapping")]
    [Tooltip("Which world plane the stick maps onto.")]
    [SerializeField] private InputPlane inputPlane = InputPlane.XY;

    [Tooltip("Face-forward rotation toward the current velocity. Only applies if velocity > 0.1.")]
    [SerializeField] private bool faceMoveDirection = true;

    [Tooltip("How fast the transform rotates (deg/sec) when faceMoveDirection is on.")]
    [SerializeField] private float turnSpeed = 720f;

    [Tooltip("Extra degrees added to the facing angle in 2D (XY) mode. Default -90 assumes the " +
             "sprite's 'up' axis is its forward direction (standard top-down convention). Use 0 " +
             "for sprites drawn facing right.")]
    [SerializeField] private float facingAngleOffset2D = -90f;

    private IJoystickInputSource _source;
    private Rigidbody _rb3d;
    private Rigidbody2D _rb2d;

    public Vector3 CurrentVelocity { get; private set; }
    public float MaxSpeed { get => maxSpeed; set => maxSpeed = Mathf.Max(0f, value); }

    // ── Unity lifecycle ──────────────────────────

    private void Awake()
    {
        _rb3d = GetComponent<Rigidbody>();
        _rb2d = GetComponent<Rigidbody2D>();
        if (_source == null) _source = GetComponent<IJoystickInputSource>();
    }

    /// <summary>Override auto-wiring (e.g. to drive a shepherd from a remote player node).</summary>
    public void SetSource(IJoystickInputSource source) => _source = source;

    private void FixedUpdate()
    {
        if (_source == null) return;

        Vector2 stick = _source.Stick;
        Vector3 targetVel = StickToWorld(stick) * maxSpeed;

        // Separate accel/decel feels closer to Mario than a single lerp rate: you ramp up smoothly
        // from stationary and coast to a stop on release, rather than a symmetric spring curve.
        float rate = stick.sqrMagnitude > 0.0001f ? acceleration : deceleration;
        CurrentVelocity = Vector3.MoveTowards(CurrentVelocity, targetVel, rate * Time.fixedDeltaTime);

        ApplyVelocity(CurrentVelocity);

        if (faceMoveDirection && CurrentVelocity.sqrMagnitude > 0.01f)
        {
            ApplyFacing(CurrentVelocity);
        }
    }

    // ── Mapping & write-back ─────────────────────

    private Vector3 StickToWorld(Vector2 stick)
    {
        return inputPlane == InputPlane.XZ
            ? new Vector3(stick.x, 0f, stick.y)
            : new Vector3(stick.x, stick.y, 0f);
    }

    private void ApplyVelocity(Vector3 vel)
    {
        if (_rb2d != null)
        {
            _rb2d.linearVelocity = new Vector2(vel.x, vel.y);
        }
        else if (_rb3d != null)
        {
            var v = _rb3d.linearVelocity;
            if (inputPlane == InputPlane.XZ) { v.x = vel.x; v.z = vel.z; }
            else { v.x = vel.x; v.y = vel.y; }
            _rb3d.linearVelocity = v;
        }
        else
        {
            transform.position += vel * Time.fixedDeltaTime;
        }
    }

    private void ApplyFacing(Vector3 vel)
    {
        if (inputPlane == InputPlane.XZ)
        {
            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            if (flat.sqrMagnitude < 0.0001f) return;
            var target = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.fixedDeltaTime);
        }
        else
        {
            float angle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg + facingAngleOffset2D;
            var target = Quaternion.Euler(0f, 0f, angle);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.fixedDeltaTime);
        }
    }
}

/// <summary>
/// Minimal surface the mover needs from any input provider. Kept separate from
/// <see cref="PlayerJoystickNode"/> so keyboard, AI, or replay sources can be dropped in.
/// </summary>
public interface IJoystickInputSource
{
    Vector2 Stick { get; }
}
