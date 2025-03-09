using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

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

    void FixedUpdate()
    {
        Time.timeScale = 1.0f;  // Prevent ML-Agents from changing simulation speed
    }

    public override void OnEpisodeBegin()
    {
        // Reset agent's position and rotation
        transform.position = startPosition;
        transform.rotation = startRotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Stop any movement input
        kartController.verticalInput = 0f;
        kartController.horizontalInput = 0f;
        kartController.isBreaking = false;

        // ✅ Randomize the target position within a 30x30 area
        float randomX = Random.Range(-30f, 30f);  // Centered at (0,0) with range -15 to 15
        float randomZ = Random.Range(-30f, 30f);
        target.position = new Vector3(randomX, target.position.y, randomZ);

        // Store initial distance to target
        previousDistanceToTarget = Vector3.Distance(transform.position, target.position);

        Debug.Log($"🎯 New Target Position: {target.position}");
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
        float distance = Vector3.Distance(transform.position, target.position);
        sensor.AddObservation(distance);

        // Debug log to ensure observations are valid
        Debug.Log($"Observations - Position: {transform.position}, Target: {target.position}, Velocity: {rb.velocity}");
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);  
        float acceleration = Mathf.Clamp(actions.ContinuousActions[1], 0f, 1f);  
        bool isBreaking = actions.ContinuousActions[2] > 0.5f;  

        Debug.Log($"🎮 AI Inputs - Steering: {steering}, Acceleration: {acceleration}, Braking: {isBreaking}");

        kartController.horizontalInput = steering * 1.5f;  
        kartController.verticalInput = acceleration * 2f;  
        kartController.isBreaking = isBreaking;

        // ✅ REWARD FOR MOVING
        if (acceleration > 0.1f) AddReward(0.005f);  // Encourage acceleration

        // ✅ REWARD FOR MOVING TOWARD THE TARGET
        float currentDistance = Vector3.Distance(transform.position, target.position);
        float progressReward = (previousDistanceToTarget - currentDistance) * 0.01f;
        AddReward(Mathf.Clamp(progressReward, -0.05f, 0.05f));

        // ✅ Small penalty per step to prevent idling
        AddReward(-0.0005f);

        previousDistanceToTarget = currentDistance;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal") * 1.5f;  // Steering
        continuousActions[1] = Input.GetAxis("Vertical") * 2f;    // Acceleration
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
