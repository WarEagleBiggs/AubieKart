using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.AI;

public class KartAgent : Agent
{
    [SerializeField] private Transform target;  // The goal the agent must reach
    private KART kartController;  // Reference to the KART script
    private Vector3 startPosition;
    private Quaternion startRotation;

    public override void Initialize()
    {
        kartController = GetComponent<KART>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    public override void OnEpisodeBegin()
    {
        // Reset the agent's position and rotation
        transform.position = startPosition;
        transform.rotation = startRotation;

        // Stop any movement
        kartController.verticalInput = 0f;
        kartController.horizontalInput = 0f;

        // Randomize the target position in a valid area
        target.position = new Vector3(Random.Range(-10f, 10f), target.position.y, Random.Range(-10f, 10f));
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Add agent's own position
        sensor.AddObservation(transform.position);

        // Add target position
        sensor.AddObservation(target.position);

        // Add velocity of the kart
        sensor.AddObservation(GetComponent<Rigidbody>().velocity);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Get continuous actions (steering and acceleration)
        float steering = actions.ContinuousActions[0];  // Steering input (-1 to 1)
        float acceleration = actions.ContinuousActions[1]; // Acceleration input (0 to 1)
        bool isBreaking = actions.ContinuousActions[2] > 0.5f; // Braking (boolean threshold)

        // Apply to KART controller
        kartController.horizontalInput = steering;
        kartController.verticalInput = acceleration;
        kartController.isBreaking = isBreaking;

        // Reward progress towards the target
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        float progressReward = -distanceToTarget * 0.01f; // Small negative reward for distance
        AddReward(progressReward);

        // Small time penalty to encourage efficiency
        AddReward(-0.0005f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");  // Steering
        continuousActions[1] = Input.GetAxis("Vertical");    // Acceleration
        continuousActions[2] = Input.GetKey(KeyCode.Space) ? 1f : 0f;  // Braking
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Target"))
        {
            AddReward(5f); // Large reward for reaching the target
            EndEpisode();
        }
        else if (other.gameObject.CompareTag("Obstacle") || other.gameObject.CompareTag("Wall"))
        {
            AddReward(-1f); // Penalize hitting an obstacle
            EndEpisode();
        }
    }
}
