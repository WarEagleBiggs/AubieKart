using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Random = UnityEngine.Random;

public class KartAgent : Agent
{
    [SerializeField] private Transform target;
    [SerializeField] private Transform spawnPoint;
    private KART kartController;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float previousDistanceToTarget;

    public override void Initialize()
    {
        kartController = GetComponent<KART>();
        kartController.useAgentControls = true; // Enable AI control
        startPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        startRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
    }

    private void Start()
    {
        // Get layer ID of "Agent"
        int agentLayer = LayerMask.NameToLayer("KART");

        // Ignore collisions between all objects in the "Agent" layer
        Physics.IgnoreLayerCollision(agentLayer, agentLayer);
    }


    void FixedUpdate()
    {
        RequestDecision();
    }

    public override void OnEpisodeBegin()
    {
        transform.position = spawnPoint != null ? spawnPoint.position : new Vector3(Random.Range(-10f, 10f), 1f, Random.Range(-10f, 10f));
        transform.rotation = spawnPoint != null ? spawnPoint.rotation : startRotation;

        // ✅ Reset AI inputs
        kartController.agentAccelInput = 0.5f;
        kartController.agentSteerInput = 0f;

        // ✅ Place the target randomly within a range
        float randomX = Random.Range(-30f, 30f);
        float randomZ = Random.Range(-30f, 30f);
        target.position = new Vector3(randomX, target.position.y, randomZ);

        // ✅ Store the initial distance to the target
        previousDistanceToTarget = Vector3.Distance(transform.position, target.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.position);
        sensor.AddObservation(target.position);
        sensor.AddObservation(Vector3.Distance(transform.position, target.position));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float acceleration = Mathf.Clamp(actions.ContinuousActions[1], 0f, 1f); 

        // ✅ Assign AI inputs
        kartController.agentSteerInput = steering;
        kartController.agentAccelInput = acceleration;

        // ✅ Calculate distance to the target
        float currentDistanceToTarget = Vector3.Distance(transform.position, target.position);

        // ✅ Reward for getting closer to the target
        float distanceChange = previousDistanceToTarget - currentDistanceToTarget;
        if (distanceChange > 0)
        {
            AddReward(distanceChange * 0.1f); // Reward proportional to distance improvement
        }
        else
        {
            AddReward(-0.05f); // Small penalty for moving away
        }

        // ✅ Update previous distance
        previousDistanceToTarget = currentDistanceToTarget;

        // ✅ Debugging log
        Debug.Log($"Distance to Target: {currentDistanceToTarget}, Reward: {distanceChange * 0.1f}");
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Mathf.Clamp(Input.GetAxis("Vertical"), 0.3f, 1f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Target"))
        {
            AddReward(5f); // Big reward for reaching the target
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Wall")) 
        {
            AddReward(-1); // Penalty for crashing
            EndEpisode();
        }
    }
}
