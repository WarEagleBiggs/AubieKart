using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Random = UnityEngine.Random;

public class KartAgent : Agent
{
    [SerializeField] private GameObject targetPrefab;
    [SerializeField] private List<Transform> targetPositions; // Assign in Unity Inspector

    private GameObject targetInstance;
    private Transform assignedTarget;
    private KART kartController;
    private Vector3 previousPosition;
    
    private Rigidbody rb;
    
    private float stuckTimer = 0f;
    private float stuckThreshold = 3f; // Time before considering stuck
    private bool isReversing = false;

    private float idleTimer = 0f;
    private float idleThreshold = 2f; // Time before punishment for staying still

    private float reverseTime = 0.5f; // Time to reverse before turning
    private float reverseStartTime = 0f;

    private float backwardTimer = 0f; // Tracks how long the agent drives backward
    private float backwardPenaltyThreshold = 2.0f; // Time before punishing for reversing too long

    public override void Initialize()
    {
        kartController = GetComponent<KART>();
        kartController.useAgentControls = true;
        targetInstance = Instantiate(targetPrefab);
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;

        // Ignore collisions between Karts
        int kartLayer = LayerMask.NameToLayer("KART");
        Physics.IgnoreLayerCollision(kartLayer, kartLayer, true);
    }

    void FixedUpdate()
    {
        RequestDecision();
        CheckIfFlipped();
        CheckIfStuck();
        CheckIfIdle();
        CheckIfDrivingBackwards();
        DrawRayToTarget(); // Call the new function to visualize the ray
    }


    public override void OnEpisodeBegin()
    {
        if (targetPositions == null || targetPositions.Count == 0)
        {
            Debug.LogError("No target positions assigned! Please add them in the Inspector.");
            return;
        }

        // Pick a random spawn position for the agent
        Transform spawnLocation = targetPositions[Random.Range(0, targetPositions.Count)];
        transform.position = spawnLocation.position;
        transform.rotation = spawnLocation.rotation;

        // Reset physics to avoid falling
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        kartController.ForceStopWheels();

        stuckTimer = 0f;
        idleTimer = 0f;
        backwardTimer = 0f;
        isReversing = false;

        // Assign a unique target for this agent
        do
        {
            assignedTarget = targetPositions[Random.Range(0, targetPositions.Count)];
        } while (Vector3.Distance(transform.position, assignedTarget.position) < 5f);

        targetInstance.transform.position = assignedTarget.position;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.position);
        sensor.AddObservation(targetInstance.transform.position);
        sensor.AddObservation(Vector3.Distance(transform.position, targetInstance.transform.position));

        Vector3 directionToTarget = (targetInstance.transform.position - transform.position).normalized;
        float angleToTarget = Vector3.Dot(transform.forward, directionToTarget);
        sensor.AddObservation(angleToTarget);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (actions.ContinuousActions.Length < 3)
        {
            return;
        }

        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float acceleration = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float braking = Mathf.Clamp(actions.ContinuousActions[2], 0f, 1f);

        // Track if the agent is moving backward for too long
        if (acceleration < 0)
        {
            backwardTimer += Time.deltaTime;
        }
        else
        {
            backwardTimer = 0f; // Reset when moving forward
        }

        if (isReversing)
        {
            if (Time.time - reverseStartTime > reverseTime)
            {
                // Stop reversing and turn
                isReversing = false;
                kartController.agentAccelInput = 1.0f;
                kartController.agentBrakeInput = 0f;
                kartController.agentSteerInput = Random.Range(-1f, 1f);
            }
            return;
        }

        kartController.agentSteerInput = Mathf.Lerp(kartController.agentSteerInput, steering, Time.deltaTime * 3f);
        kartController.agentAccelInput = acceleration;
        kartController.agentBrakeInput = braking;
    }

    private void CheckIfFlipped()
    {
        float upDot = Vector3.Dot(transform.up, Vector3.down);
        float sideDot = Mathf.Abs(Vector3.Dot(transform.right, Vector3.up));

        if (upDot > 0.7f || sideDot > 0.8f) 
        {
            AddReward(-1.0f); // Penalize flipping
            EndEpisode();
        }
    }
    
    private void DrawRayToTarget()
    {
        if (assignedTarget == null) return;

        Vector3 direction = (assignedTarget.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, assignedTarget.position);

        // Draw a visible debug ray
        Debug.DrawLine(transform.position, assignedTarget.position, Color.green);

    }


    private void CheckIfStuck()
    {
        if (Vector3.Distance(transform.position, previousPosition) < 0.3f)
        {
            stuckTimer += Time.deltaTime;
        }
        else
        {
            stuckTimer = 0f;
            previousPosition = transform.position;
        }

        if (stuckTimer > stuckThreshold && !isReversing) 
        {
            // Reverse just enough to turn
            isReversing = true;
            reverseStartTime = Time.time;
            kartController.agentAccelInput = -1.0f; 
            kartController.agentSteerInput = Random.Range(-1f, 1f); 
        }
    }

    private void CheckIfIdle()
    {
        if (Vector3.Distance(transform.position, previousPosition) < 0.1f)
        {
            idleTimer += Time.deltaTime;
        }
        else
        {
            idleTimer = 0f;
        }

        if (idleTimer > idleThreshold)
        {
            AddReward(-1f); // Punish staying still
        }
    }

    private void CheckIfDrivingBackwards()
    {
        if (backwardTimer > backwardPenaltyThreshold)
        {
            AddReward(-1f); // Punish driving backwards too long
            backwardTimer = 0f; // Reset timer to prevent continuous penalties
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ensure the agent only wins if it reaches its OWN target
        if (other.CompareTag("Target") && other.transform.position == assignedTarget.position)
        {
            AddReward(5.0f); // Reward for reaching the assigned target
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-2.0f); // Penalize hitting walls
        }
    }
}
