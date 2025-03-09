using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Random = UnityEngine.Random;

public class KartAgent : Agent
{
    [SerializeField] private Transform target;  // The goal the agent must reach
    private KART kartController;  // Reference to the KART script
    private Rigidbody rb;  // Rigidbody for physics-based observations
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float previousDistanceToTarget;

    public override void Initialize()
    {
        kartController = GetComponent<KART>();
        rb = GetComponent<Rigidbody>();

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    public override void OnEpisodeBegin()
    {
        // Reset agent's position and rotation
        transform.position = startPosition;
        transform.rotation = startRotation;
        rb.velocity = Vector3.zero;  // Stop movement
        rb.angularVelocity = Vector3.zero;

        // Stop any movement input
        kartController.verticalInput = 0f;
        kartController.horizontalInput = 0f;
        kartController.isBreaking = false;

        // Randomize the target position within a valid area
        target.position = new Vector3(Random.Range(-10f, 10f), target.position.y, Random.Range(-10f, 10f));

        // Store initial distance to target
        previousDistanceToTarget = Vector3.Distance(transform.position, target.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Add agent's position
        sensor.AddObservation(transform.position);

        // Add target position
        sensor.AddObservation(target.position);

        // Add velocity and angular velocity
        sensor.AddObservation(rb.velocity);
        sensor.AddObservation(rb.angularVelocity);

        // Distance to target
        sensor.AddObservation(Vector3.Distance(transform.position, target.position));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Get continuous action inputs (steering & acceleration)
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);  // Steering (-1 to 1)
        float acceleration = Mathf.Clamp(actions.ContinuousActions[1], 0f, 1f); // Acceleration (0 to 1)
        bool isBreaking = actions.ContinuousActions[2] > 0.5f;  // Braking (Boolean)

        // Apply scaled inputs to the KART controller
        kartController.horizontalInput = steering * 1.5f;  // Increase sensitivity
        kartController.verticalInput = acceleration * 2f;  // Boost acceleration
        kartController.isBreaking = isBreaking;

        // Reward progress towards target
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        float progressReward = (previousDistanceToTarget - distanceToTarget) * 0.01f;
        AddReward(Mathf.Clamp(progressReward, -0.05f, 0.05f));

        // Small time penalty to prevent idle behavior
        AddReward(-0.0005f);

        // Update distance for next step
        previousDistanceToTarget = distanceToTarget;
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
        
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Obstacle") || other.gameObject.CompareTag("Wall"))
        {
            AddReward(-1f); // Penalize hitting an obstacle
            EndEpisode();
        }
    }
}
