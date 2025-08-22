using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Rigidbody))]
public class KartAgent : Agent
{
    [Header("Targets")]
    [SerializeField] private GameObject targetPrefab;
    [SerializeField] private List<Transform> targetPositions; // set in Inspector

    [Header("Proximity Sensing (for rewards only)")]
    [Tooltip("Extra simple rays used ONLY for reward shaping; the real observations come from the RayPerceptionSensor3D child.")]
    [SerializeField] private float proximityRayLength = 10f;
    [SerializeField] private LayerMask wallMask;            // set to Walls/Track as needed
    private readonly float[] frontRayAngles = new float[] { -25f, 0f, 25f };

    [Header("Rewards")]
    [SerializeField] private float progressScale = 1.2f;    // reward for getting closer to target
    [SerializeField] private float alignScale = 0.01f;      // light reward for facing target
    [SerializeField] private float timePenalty = -0.0005f;  // tiny step penalty
    [SerializeField] private float wallTouchPenalty = -0.05f;
    [SerializeField] private float stuckEndAfter = 3.0f;    // seconds at low speed before ending
    [SerializeField] private float lowSpeed = 0.5f;         // m/s considered "stuck"

    private GameObject targetInstance;
    private Transform assignedTarget;
    private KART kart;
    private Rigidbody rb;

    // progress bookkeeping
    private float prevDistToTarget;
    private float stuckTimer;

    // cached per step
    private Vector3 toTargetWorld, toTargetDir;

    public override void Initialize()
    {
        kart = GetComponent<KART>();
        kart.useAgentControls = true;   // we drive it
        rb = GetComponent<Rigidbody>();

        targetInstance = Instantiate(targetPrefab);
        targetInstance.tag = "Target";
    }

    public override void OnEpisodeBegin()
    {
        // spawn kart
        Transform spawn = targetPositions[Random.Range(0, targetPositions.Count)];
        transform.SetPositionAndRotation(spawn.position, spawn.rotation);

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        stuckTimer = 0f;

        // pick a non-trivial target
        do
        {
            assignedTarget = targetPositions[Random.Range(0, targetPositions.Count)];
        } while (Vector3.Distance(transform.position, assignedTarget.position) < 5f);

        targetInstance.transform.position = assignedTarget.position;

        prevDistToTarget = Vector3.Distance(transform.position, targetInstance.transform.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // --- Ego-centric target features ---
        toTargetWorld = assignedTarget.position - transform.position;
        toTargetDir = toTargetWorld.sqrMagnitude > 1e-6f ? toTargetWorld.normalized : transform.forward;
        Vector3 toTargetLocal = transform.InverseTransformDirection(toTargetWorld);

        float dist = toTargetWorld.magnitude;
        float headingDeltaRad = Mathf.Deg2Rad *
            Vector3.SignedAngle(transform.forward, toTargetDir, Vector3.up);

        // --- Ego-centric velocities ---
        Vector3 velLocal = transform.InverseTransformDirection(rb.velocity);

        // --- Lightweight, scaled observations ---
        sensor.AddObservation(toTargetLocal.x / 25f);   // lateral offset
        sensor.AddObservation(toTargetLocal.z / 25f);   // forward offset
        sensor.AddObservation(dist / 50f);              // distance
        sensor.AddObservation(Mathf.Sin(headingDeltaRad));
        sensor.AddObservation(Mathf.Cos(headingDeltaRad));
        sensor.AddObservation(velLocal.x / 20f);        // lateral speed
        sensor.AddObservation(velLocal.z / 40f);        // forward speed
        sensor.AddObservation(rb.angularVelocity.y / 10f);

        // NOTE: Your RayPerceptionSensor3D (child) automatically appends its ray observations.
        // Keep "Use Child Sensors" enabled in Behavior Parameters.
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Actions: [0] steer ∈ [-1,1], [1] drive ∈ [-1,1] (negative = reverse)
        float steer = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float drive = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        kart.agentSteerInput = steer;
        kart.agentAccelInput = drive;

        // ---------- Rewards ----------
        float dist = Vector3.Distance(transform.position, targetInstance.transform.position);

        // Reward genuine progress toward the target
        float progress = Mathf.Max(0f, prevDistToTarget - dist);
        prevDistToTarget = dist;

        // If a wall blocks line of sight, dampen progress reward (prevents flooring into walls)
        bool los =
            !Physics.Raycast(transform.position + Vector3.up * 0.2f, toTargetDir, dist, wallMask);
        float losFactor = los ? 1f : 0.3f;

        AddReward(progress * progressScale * losFactor);

        // Gentle shaping for pointing toward target (encourages quick align/backup/turn)
        float align = Vector3.Dot(transform.forward, toTargetDir);
        AddReward(Mathf.Clamp(align, -1f, 1f) * alignScale);

        // Tiny time pressure
        AddReward(timePenalty);

        // Proximity penalty from short forward fan → teaches to slow/turn/reverse when too close
        float worstFront = FrontProximity(); // 0 (touching) … 1 (clear)
        // Penalize being very close; no penalty if > 60% clear
        float proxPenalty = Mathf.Clamp01(0.6f - worstFront) * 0.01f;
        AddReward(-proxPenalty);

        // Stuck detection (learn to reverse & get out)
        float speed = rb.velocity.magnitude;
        if (speed < lowSpeed) stuckTimer += Time.fixedDeltaTime; else stuckTimer = 0f;
        if (stuckTimer > stuckEndAfter)
        {
            AddReward(-0.2f);
            EndEpisode();
        }

        // Close enough safeguard (in case trigger misses)
        if (dist < 1.0f)
        {
            AddReward(2.0f);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        // Forward = positive, Reverse = negative
        ca[1] = Mathf.Clamp(Input.GetAxis("Vertical"), -1f, 1f);
    }

    private float FrontProximity()
    {
        // Cast a few short rays in front. Return the *closest* normalized hit distance (0..1).
        float best = 1f;
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        foreach (var ang in frontRayAngles)
        {
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * transform.forward;
            if (Physics.Raycast(origin, dir, out var hit, proximityRayLength, wallMask))
            {
                float norm = Mathf.Clamp01(hit.distance / proximityRayLength);
                if (norm < best) best = norm;
            }
        }
        return best;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Target") && assignedTarget != null &&
            other.transform.position == assignedTarget.position)
        {
            AddReward(3.0f);
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Wall"))
        {
            // Do NOT end the episode here; allow the policy to learn reversing/escape.
            AddReward(wallTouchPenalty);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Debug the front proximity rays
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        foreach (var ang in frontRayAngles)
        {
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * transform.forward;
            Gizmos.DrawRay(origin, dir * proximityRayLength);
        }
    }
}
