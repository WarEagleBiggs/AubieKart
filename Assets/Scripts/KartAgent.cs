using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(KART))]
public class KartAgent : Agent
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private LayerMask wallMask;

    [Header("Rewards")]
    [SerializeField] private float distanceRewardScale = 0.02f;
    [SerializeField] private float wallHitPenalty = -0.1f;
    [SerializeField] private float targetReward = 1.0f;
    [SerializeField] private float timePenalty = -0.001f;

    [Header("Observation Scaling")]
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float maxAngularSpeed = 10f;
    [SerializeField] private float maxTargetDistance = 100f;

    private Rigidbody rb;
    private KART kart;
    private float previousDistance;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        kart = GetComponent<KART>();
        kart.useAgentControls = true;
    }

    public override void OnEpisodeBegin()
    {
        if (spawnPoint != null)
        {
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        kart.agentSteerInput = 0f;
        kart.agentAccelInput = 0f;

        previousDistance = GetDistanceToTarget();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);

        Vector3 localTarget = Vector3.zero;
        if (target != null)
            localTarget = transform.InverseTransformPoint(target.position);

        sensor.AddObservation(localVelocity.x / maxSpeed);
        sensor.AddObservation(localVelocity.z / maxSpeed);
        sensor.AddObservation(rb.angularVelocity.y / maxAngularSpeed);

        sensor.AddObservation(localTarget.x / maxTargetDistance);
        sensor.AddObservation(localTarget.z / maxTargetDistance);

        sensor.AddObservation(GetDistanceToTarget() / maxTargetDistance);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steer = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float drive = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        kart.agentSteerInput = steer;
        kart.agentAccelInput = drive;

        float currentDistance = GetDistanceToTarget();
        float distanceDelta = previousDistance - currentDistance;

        AddReward(distanceDelta * distanceRewardScale);
        AddReward(timePenalty);

        previousDistance = currentDistance;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> actions = actionsOut.ContinuousActions;
        actions[0] = Input.GetAxis("Horizontal");
        actions[1] = Input.GetAxis("Vertical");
    }

    private float GetDistanceToTarget()
    {
        if (target == null) return 0f;
        return Vector3.Distance(transform.position, target.position);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & wallMask.value) != 0)
        {
            AddReward(wallHitPenalty);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (target != null && other.transform == target)
        {
            AddReward(targetReward);
            EndEpisode();
        }
        else if (other.CompareTag("Target"))
        {
            AddReward(targetReward);
            EndEpisode();
        }
    }
}