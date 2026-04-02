using UnityEngine;

/// <summary>
/// Manages ball movement and collision inside the polygon arena.
/// All physics is custom (no Rigidbody/Collider).
/// PongManager calls Tick() every FixedUpdate and reads back collision results.
/// </summary>
public class PongBall : MonoBehaviour
{
    [Header("Ball")]
    [SerializeField] private float startSpeed = 5f;
    [SerializeField] private float speedIncrement = 0.3f;
    [SerializeField] private float maxSpeed = 15f;
    [SerializeField] private float ballRadius = 0.15f;

    public Vector2 Position { get; private set; }
    public Vector2 Velocity { get; private set; }
    public float CurrentSpeed { get; private set; }

    private PongArena _arena;
    private GameObject _visual;

    // Last collision result (read by PongManager after Tick)
    public bool HitOccurred { get; private set; }
    public int HitSideIndex { get; private set; }
    public bool HitPaddle { get; private set; }

    public void Init(PongArena arena)
    {
        _arena = arena;
        CurrentSpeed = startSpeed;
        CreateVisual();
    }

    public void ResetBall()
    {
        Position = Vector2.zero;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * CurrentSpeed;
        UpdateVisual();
    }

    public void IncreaseSpeed()
    {
        CurrentSpeed = Mathf.Min(CurrentSpeed + speedIncrement, maxSpeed);
        Velocity = Velocity.normalized * CurrentSpeed;
    }

    /// <summary>
    /// Advance the ball by one physics step. Returns collision info via properties.
    /// </summary>
    public void Tick(float dt)
    {
        HitOccurred = false;
        HitSideIndex = -1;
        HitPaddle = false;

        Vector2 newPos = Position + Velocity * dt;

        float closestT = float.MaxValue;
        int closestSide = -1;
        Vector2 closestHit = Vector2.zero;

        for (int i = 0; i < _arena.NumSides; i++)
        {
            _arena.GetSideEndpoints(i, out Vector2 v1, out Vector2 v2);

            if (PongPhysics.SegmentIntersection(Position, newPos, v1, v2, out Vector2 hit, out float t))
            {
                if (t < closestT)
                {
                    closestT = t;
                    closestSide = i;
                    closestHit = hit;
                }
            }
        }

        if (closestSide < 0)
        {
            // Also check if the ball is very close to any wall (within radius)
            for (int i = 0; i < _arena.NumSides; i++)
            {
                _arena.GetSideEndpoints(i, out Vector2 v1, out Vector2 v2);
                float dist = PongPhysics.PointSegmentDistance(newPos, v1, v2);
                if (dist < ballRadius)
                {
                    closestSide = i;
                    closestHit = PongPhysics.ClosestPointOnSegment(newPos, v1, v2);
                    closestT = 0f;
                    break;
                }
            }
        }

        if (closestSide >= 0)
        {
            _arena.GetSideEndpoints(closestSide, out Vector2 sv1, out Vector2 sv2);
            Vector2 normal = PongPhysics.InwardNormal(sv1, sv2, Vector2.zero);

            bool isActivePlayerSide = _arena.IsPlayerSide(closestSide) && !_arena.IsSideEliminated(closestSide);

            if (isActivePlayerSide)
            {
                _arena.GetPaddleSegment(closestSide, out Vector2 pp1, out Vector2 pp2);
                float projT = PongPhysics.ProjectOntoSegment(closestHit, pp1, pp2);
                float distToPaddle = PongPhysics.PointSegmentDistance(closestHit, pp1, pp2);

                float paddleLen = (pp2 - pp1).magnitude;
                bool onPaddle = distToPaddle < (paddleLen * 0.1f + ballRadius);

                float hitProj = PongPhysics.ProjectOntoSegment(closestHit, sv1, sv2);
                float paddleStart = PongPhysics.ProjectOntoSegment(pp1, sv1, sv2);
                float paddleEnd = PongPhysics.ProjectOntoSegment(pp2, sv1, sv2);

                if (hitProj >= paddleStart - 0.02f && hitProj <= paddleEnd + 0.02f)
                    onPaddle = true;

                if (onPaddle)
                {
                    float offset = projT - 0.5f;
                    Vector2 edge = (sv2 - sv1).normalized;
                    Vector2 reflected = PongPhysics.Reflect(Velocity, normal);
                    reflected += edge * offset * CurrentSpeed * 0.5f;
                    Velocity = reflected.normalized * CurrentSpeed;

                    HitOccurred = true;
                    HitSideIndex = closestSide;
                    HitPaddle = true;
                }
                else
                {
                    // Ball passed through the goal zone
                    HitOccurred = true;
                    HitSideIndex = closestSide;
                    HitPaddle = false;

                    Velocity = PongPhysics.Reflect(Velocity, normal).normalized * CurrentSpeed;
                }
            }
            else
            {
                Velocity = PongPhysics.Reflect(Velocity, normal).normalized * CurrentSpeed;
                HitOccurred = true;
                HitSideIndex = closestSide;
                HitPaddle = true;
            }

            Position = closestHit + normal * (ballRadius + 0.01f);
        }
        else
        {
            Position = newPos;
        }

        UpdateVisual();
    }

    // ── Visual ───────────────────────────────────

    private void CreateVisual()
    {
        _visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _visual.name = "PongBall_Visual";
        _visual.transform.SetParent(transform);
        _visual.transform.localScale = Vector3.one * ballRadius * 2f;
        Object.Destroy(_visual.GetComponent<SphereCollider>());

        var renderer = _visual.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = Color.white;
        renderer.sortingOrder = 5;
    }

    private void UpdateVisual()
    {
        if (_visual != null)
            _visual.transform.position = new Vector3(Position.x, Position.y, 0);
    }
}
