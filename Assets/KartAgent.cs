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
        // Ignore collisions between agents
        int agentLayer = LayerMask.NameToLayer("KART");
        Physics.IgnoreLayerCollision(agentLayer, agentLayer);

        //Time.timeScale = 1f;
    }

    void FixedUpdate()
    {
        RequestDecision();
    }

    public override void OnEpisodeBegin()
    {
        // Reset the agent position
        transform.position = spawnPoint != null ? spawnPoint.position : new Vector3(Random.Range(-10f, 10f), 1f, Random.Range(-10f, 10f));
        transform.rotation = spawnPoint != null ? spawnPoint.rotation : startRotation;

        // ✅ Ensure AI starts with some acceleration so it doesn't get stuck
        kartController.agentAccelInput = 0.5f; // Start moving forward slightly
        kartController.agentSteerInput = 0f; // Keep it straight

        // ✅ Place the target randomly
        float randomX = Random.Range(-30f, 30f);
        float randomZ = Random.Range(-30f, 30f);
        target.position = new Vector3(randomX, target.position.y, randomZ);

        // ✅ Store the initial distance to the target
        previousDistanceToTarget = Vector3.Distance(transform.position, target.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Agent Position
        sensor.AddObservation(transform.position);
        // Target Position
        sensor.AddObservation(target.position);
        // Distance to Target
        sensor.AddObservation(Vector3.Distance(transform.position, target.position));

        // ✅ Ray Perception: Detect walls
        float[] rayAngles = { -60f, -30f, -15f, 0f, 15f, 30f, 60f };
        foreach (float angle in rayAngles)
        {
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, 10f))
            {
                if (hit.collider.CompareTag("Wall"))
                {
                    sensor.AddObservation(1f); // Detected a wall
                    sensor.AddObservation(hit.distance / 10f); // Normalize distance (0 to 1)
                }
                else
                {
                    sensor.AddObservation(0f); // No wall detected
                    sensor.AddObservation(1f); // Max distance (no obstacle)
                }
            }
            else
            {
                sensor.AddObservation(0f); // No wall detected
                sensor.AddObservation(1f); // Max distance
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float acceleration = Mathf.Clamp(actions.ContinuousActions[1], 0f, 1f); // Allow full stop

        // ✅ Assign AI inputs
        kartController.agentSteerInput = steering;
        kartController.agentAccelInput = acceleration;

        // ✅ Calculate distance to the target
        float currentDistanceToTarget = Vector3.Distance(transform.position, target.position);

        // ✅ Reward for getting closer to the target
        float distanceChange = previousDistanceToTarget - currentDistanceToTarget;
        if (distanceChange > 0)
        {
            AddReward(distanceChange * 0.1f); // Reward for improvement
        }
        else
        {
            AddReward(-0.05f); // Small penalty for moving away
        }

        // ✅ Penalize AI for getting close to walls
        float[] rayAngles = { -60f, -30f, -15f, 0f, 15f, 30f, 60f };
        foreach (float angle in rayAngles)
        {
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, 3f)) // Reduce distance for penalty
            {
                if (hit.collider.CompareTag("Wall"))
                {
                    AddReward(-0.2f); // Penalize for being too close to a wall
                    //Debug.Log("AI Too Close to Wall! Penalty Applied.");
                }
            }
        }

        // ✅ Update previous distance
        previousDistanceToTarget = currentDistanceToTarget;

        // ✅ Debugging log
        //Debug.Log($"AI Steering: {steering}, Acceleration: {acceleration}, Distance to Target: {currentDistanceToTarget}, Reward: {distanceChange * 0.1f}");
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Mathf.Clamp(Input.GetAxis("Vertical"), 0f, 1f); // Allow full stop
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
