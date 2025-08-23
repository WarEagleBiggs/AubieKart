using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class KartAgent : Agent
{
    // ---------- References ----------
    [Header("Map / Spawning")]
    [SerializeField] private Transform mapCenter;      
    [SerializeField] private float minSpawnRadius = 5f;
    [SerializeField] private float maxSpawnRadius = 45f;
    [SerializeField] private LayerMask groundMask;     
    [SerializeField] private LayerMask wallMask;       
    [SerializeField] private float groundCheckHeight = 60f;
    [SerializeField] private float spawnYOffset = 0.25f;
    [SerializeField] private float clearanceCheckRadius = 0.6f;

    [Header("Exploration Grid (episode-local)")]
    [SerializeField] private Vector2 gridOrigin = Vector2.zero; 
    [SerializeField] private float cellSize = 5f;
    [SerializeField] private int gridWidth = 80;
    [SerializeField] private int gridHeight = 80;

    [Header("Ray Sensing (for obs + shaping)")]
    [SerializeField] private float rayLength = 12f;
    private readonly float[] rayAngles = new float[] { -60f, -30f, -15f, 0f, 15f, 30f, 60f };

    [Header("Rewards")]
    [SerializeField] private float coverageReward = 0.5f;        // new cell bonus
    [SerializeField] private float revisitReward = 0.01f;        // tiny for revisits
    [SerializeField] private float flowScale = 0.02f;            // speed * forwardness * clearance
    [SerializeField] private float proximityPenaltyScale = 0.01f;// penalize hugging walls
    [SerializeField] private float spinPenaltyScale = 0.0015f;   // penalize yaw spin
    [SerializeField] private float timePenalty = -0.0005f;       // tiny step cost
    [SerializeField] private float wallHitPenalty = -0.1f;       // collision cost (no episode end)

    [Header("Episode Enders")]
    [SerializeField] private float stuckSpeed = 0.6f;            // m/s considered "stuck"
    [SerializeField] private float stuckTime = 3f;               // seconds below stuckSpeed → end

    private KART kart;
    private Rigidbody rb;
    private HashSet<int> visited;
    private float stuckTimer = 0f;

    public override void Initialize()
    {
        kart = GetComponent<KART>();
        kart.useAgentControls = true; 
        rb = GetComponent<Rigidbody>();
        visited = new HashSet<int>();
    }

    public override void OnEpisodeBegin()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        stuckTimer = 0f;

        // ----------  spawn around mapCenter ----------
        const int maxTries = 20;
        bool spawned = false;
        for (int i = 0; i < maxTries && !spawned; i++)
        {
            float a = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float r = Random.Range(minSpawnRadius, maxSpawnRadius);
            Vector3 flat = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);

            Vector3 center = mapCenter != null ? mapCenter.position : transform.position;
            Vector3 castStart = center + flat + Vector3.up * groundCheckHeight;

            if (Physics.SphereCast(castStart, 0.25f, Vector3.down, out var hit, groundCheckHeight * 2f, groundMask))
            {
                Vector3 candidate = hit.point + Vector3.up * spawnYOffset;

                // reject spots too close to walls/obstacles
                bool nearWall = Physics.CheckSphere(candidate, clearanceCheckRadius, wallMask);
                if (!nearWall)
                {
                    Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    transform.SetPositionAndRotation(candidate, rot);
                    Physics.SyncTransforms();
                    spawned = true;
                }
            }
        }

        // Fallback: center spot
        if (!spawned)
        {
            Vector3 center = mapCenter != null ? mapCenter.position : transform.position;
            Vector3 start = center + Vector3.up * groundCheckHeight;
            Vector3 pos = center + Vector3.up * 0.5f;
            if (Physics.Raycast(start, Vector3.down, out var hit2, groundCheckHeight * 2f, groundMask))
                pos = hit2.point + Vector3.up * spawnYOffset;
            transform.SetPositionAndRotation(pos, Quaternion.identity);
            Physics.SyncTransforms();
        }

        // reset exploration memory
        visited.Clear();
        MarkVisitedCell(); 
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Ego-centric motion
        Vector3 velLocal = transform.InverseTransformDirection(rb.velocity);
        sensor.AddObservation(velLocal.x / 20f);      // lateral speed
        sensor.AddObservation(velLocal.z / 40f);      // forward speed
        sensor.AddObservation(rb.angularVelocity.y / 10f);

        // Rays (0..1 where 1 = clear)
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        Vector3 fwd = transform.forward;
        foreach (float ang in rayAngles)
        {
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * fwd;
            float norm = 1f;
            if (Physics.Raycast(origin, dir, out var hit, rayLength, wallMask))
                norm = Mathf.Clamp01(hit.distance / rayLength);
            sensor.AddObservation(norm);
        }

        // Bias (helps learning stability a bit)
        sensor.AddObservation(1f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Continuous actions: steer [-1,1], drive [-1,1] (negative = reverse)
        float steer = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float drive = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        kart.agentSteerInput = steer;
        kart.agentAccelInput = drive;

        // ---------- Rewards ----------
        // (1) Coverage / exploration
        bool isNewCell = MarkVisitedCell();
        AddReward(isNewCell ? coverageReward : revisitReward);

        // (2) Flow through open space: speed * forwardness * front clearance
        float speed = rb.velocity.magnitude;
        float forwardness = rb.velocity.sqrMagnitude > 1e-6f
            ? Mathf.Max(0f, Vector3.Dot(transform.forward, rb.velocity.normalized))
            : 0f;
        float frontClear = FrontClearance();
        AddReward(speed * forwardness * frontClear * flowScale);

        // (3) Proximity penalty (don’t skim walls)
        float closeness = 1f - frontClear; // 0 clear … 1 touching
        AddReward(-closeness * proximityPenaltyScale);

        // (4) Anti-wiggle
        AddReward(-Mathf.Abs(rb.angularVelocity.y) * spinPenaltyScale);

        // (5) Tiny step cost
        AddReward(timePenalty);

        // Stuck / flipped enders (so it learns to reverse & recover)
        if (speed < stuckSpeed) stuckTimer += Time.fixedDeltaTime; else stuckTimer = 0f;
        if (stuckTimer > stuckTime)
        {
            AddReward(-0.2f);
            EndEpisode();
        }
        if (Vector3.Dot(transform.up, Vector3.down) > 0.7f)
        {
            AddReward(-0.2f);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Mathf.Clamp(Input.GetAxis("Vertical"), -1f, 1f); // reverse allowed
    }

    private float FrontClearance()
    {
        // average clearance using front-biased subset of rays
        float sum = 0f; int n = 0;
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        foreach (float ang in new float[] { -30f, -15f, 0f, 15f, 30f })
        {
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * transform.forward;
            float norm = 1f;
            if (Physics.Raycast(origin, dir, out var hit, rayLength, wallMask))
                norm = Mathf.Clamp01(hit.distance / rayLength);
            sum += norm; n++;
        }
        return (n > 0) ? sum / n : 1f;
    }

    private bool MarkVisitedCell()
    {
        Vector3 pos = transform.position;
        int cx = Mathf.FloorToInt((pos.x - gridOrigin.x) / cellSize);
        int cz = Mathf.FloorToInt((pos.z - gridOrigin.y) / cellSize);
        if (cx < 0 || cz < 0 || cx >= gridWidth || cz >= gridHeight) return false;
        int key = cz * gridWidth + cx;
        if (visited.Contains(key)) return false;
        visited.Add(key);
        return true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Collision with walls/obstacles → small penalty, do NOT end episode
        if (((1 << collision.collider.gameObject.layer) & wallMask) != 0)
            AddReward(wallHitPenalty);
    }

    private void OnDrawGizmosSelected()
    {
        // visualize spawn clearance
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, clearanceCheckRadius);
    }
}
