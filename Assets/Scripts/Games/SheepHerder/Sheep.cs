using UnityEngine;

/// <summary>
/// A single sheep. Runs a lightweight GameObject-based flocking behaviour (Reynolds boids:
/// separation + alignment + cohesion) with an additional "flee from shepherd" force layered on
/// top. Lives in a <see cref="Rigidbody2D"/> and exposes velocity clamp + debug gizmos.
///
/// Why GameObject-based instead of the SteeringAI ECS pipeline? The party-game sheep count is
/// small (~10-30) and each sheep needs cheap bi-directional talk with GameObject shepherds +
/// trigger zones. Building a burst-job ECS sheep with bridging components is overkill here. The
/// SteeringAI package is still installed (see Tools → Steering AI → Run Full Setup) and ready
/// for any future game that needs thousands of agents.
///
/// Top-down 2D: everything happens on the XY plane. Z is ignored entirely.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Sheep : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxSpeed = 4f;
    [SerializeField] private float maxForce = 18f;

    [Header("Flocking")]
    [SerializeField] private float neighborRadius = 3f;
    [SerializeField] private float separationRadius = 1f;
    [SerializeField] private float separationWeight = 2.5f;
    [SerializeField] private float alignmentWeight = 1f;
    [SerializeField] private float cohesionWeight = 1f;

    [Header("Shepherd Fear")]
    [SerializeField] private float shepherdFearRadius = 4f;
    [SerializeField] private float shepherdFearWeight = 6f;

    [Header("Wander")]
    [Tooltip("Random idle motion so sheep don't perfectly stand still when no shepherd is near.")]
    [SerializeField] private float wanderWeight = 0.4f;
    [SerializeField] private float wanderChangeRate = 0.5f;

    [Header("Containment")]
    [Tooltip("Central region radius the sheep prefer to stay within — soft constraint.")]
    [SerializeField] private float homeRadius = 12f;
    [SerializeField] private float homeWeight = 1.2f;

    [Header("Facing")]
    [Tooltip("Rotate the sheep so its local-up (or local-right, depending on your sprite) faces " +
             "the direction of motion. Disable for sprite characters that shouldn't rotate.")]
    [SerializeField] private bool faceMoveDirection = false;

    private Rigidbody2D _rb;
    private Vector2 _wanderTarget;
    private float _nextWanderTime;

    public bool IsScored { get; private set; }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = !faceMoveDirection;
        _wanderTarget = Random.insideUnitCircle;
    }

    private void FixedUpdate()
    {
        if (IsScored) return;

        var flock = SheepRegistry.Instance;
        if (flock == null) return;

        Vector2 accel = Vector2.zero;

        ComputeFlockingForces(flock, out var separation, out var alignment, out var cohesion);
        accel += separation * separationWeight;
        accel += alignment * alignmentWeight;
        accel += cohesion * cohesionWeight;

        accel += ComputeShepherdFear(flock) * shepherdFearWeight;
        accel += ComputeWander() * wanderWeight;
        accel += ComputeHomePull() * homeWeight;

        // Clamp so no single force dominates; Reynolds calls this steering force clipping.
        accel = Vector2.ClampMagnitude(accel, maxForce);

        var vel = _rb.linearVelocity + accel * Time.fixedDeltaTime;
        vel = Vector2.ClampMagnitude(vel, maxSpeed);
        _rb.linearVelocity = vel;

        if (faceMoveDirection && vel.sqrMagnitude > 0.05f)
        {
            // Local-up points along velocity — standard 2D top-down sprite orientation.
            float angle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg - 90f;
            _rb.rotation = Mathf.LerpAngle(_rb.rotation, angle, 10f * Time.fixedDeltaTime);
        }
    }

    // ── Behaviour helpers ───────────────────────

    private void ComputeFlockingForces(SheepRegistry flock, out Vector2 separation, out Vector2 alignment, out Vector2 cohesion)
    {
        separation = Vector2.zero;
        alignment = Vector2.zero;
        cohesion = Vector2.zero;

        int neighbors = 0;
        int separators = 0;
        Vector2 center = Vector2.zero;
        Vector2 avgVel = Vector2.zero;

        Vector2 myPos = transform.position;

        foreach (var other in flock.Sheep)
        {
            if (other == null || other == this || other.IsScored) continue;

            Vector2 otherPos = other.transform.position;
            Vector2 toOther = otherPos - myPos;
            float dist = toOther.magnitude;
            if (dist > neighborRadius) continue;

            neighbors++;
            center += otherPos;
            avgVel += other._rb.linearVelocity;

            if (dist < separationRadius && dist > 0.001f)
            {
                // Strength scales inversely with distance so tight crowds push apart harder.
                separation -= toOther / (dist * dist);
                separators++;
            }
        }

        if (neighbors > 0)
        {
            center /= neighbors;
            avgVel /= neighbors;
            cohesion = SteerToward(center - myPos);
            alignment = SteerToward(avgVel);
        }
        if (separators > 0) separation = SteerToward(separation);
    }

    private Vector2 ComputeShepherdFear(SheepRegistry flock)
    {
        Vector2 flee = Vector2.zero;
        Vector2 myPos = transform.position;
        foreach (var s in flock.Shepherds)
        {
            if (s == null) continue;
            Vector2 away = myPos - (Vector2)s.position;
            float d = away.magnitude;
            if (d > shepherdFearRadius || d < 0.001f) continue;

            // Quadratic falloff — a shepherd at the edge of the radius barely nudges a sheep,
            // but a shepherd right on top sends it bolting.
            float strength = 1f - (d / shepherdFearRadius);
            flee += (away / d) * strength * strength;
        }
        return flee.sqrMagnitude > 0.0001f ? SteerToward(flee) : Vector2.zero;
    }

    private Vector2 ComputeWander()
    {
        if (Time.time >= _nextWanderTime)
        {
            _wanderTarget = Random.insideUnitCircle;
            _nextWanderTime = Time.time + 1f / Mathf.Max(0.05f, wanderChangeRate);
        }
        return _wanderTarget;
    }

    private Vector2 ComputeHomePull()
    {
        Vector2 pos = transform.position;
        float d = pos.magnitude;
        if (d < homeRadius) return Vector2.zero;

        // Soft radial pull back toward origin — stronger the further outside the ring.
        Vector2 toHome = -pos.normalized;
        float overshoot = (d - homeRadius) / homeRadius;
        return toHome * overshoot;
    }

    /// <summary>
    /// Standard Reynolds "seek" — convert desired direction into a steering acceleration given
    /// current velocity and the sheep's max speed.
    /// </summary>
    private Vector2 SteerToward(Vector2 desired)
    {
        if (desired.sqrMagnitude < 0.0001f) return Vector2.zero;
        Vector2 desiredVel = desired.normalized * maxSpeed;
        return desiredVel - _rb.linearVelocity;
    }

    // ── Public state ────────────────────────────

    public void MarkScored()
    {
        IsScored = true;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        gameObject.SetActive(false);
    }

    public void ResetToPosition(Vector3 pos)
    {
        IsScored = false;
        transform.position = pos;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        gameObject.SetActive(true);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, neighborRadius);
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, shepherdFearRadius);
    }
#endif
}
