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
    [SerializeField] private List<Transform> targetPositions;

    private GameObject targetInstance;
    private Transform assignedTarget;
    private KART kartController;
    private Rigidbody rb;
    private Vector3 previousPosition;
    
    private float stuckTimer = 0f;
    private float stuckThreshold = 3f;

    public override void Initialize()
    {
        kartController = GetComponent<KART>();
        kartController.useAgentControls = true;
        targetInstance = Instantiate(targetPrefab);
        rb = GetComponent<Rigidbody>();
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal"); // Steering
        continuousActions[1] = Input.GetAxis("Vertical");   // Acceleration
        continuousActions[2] = Input.GetKey(KeyCode.Space) ? 1f : 0f; // Optional: braking
    }


    void FixedUpdate()
    {
        RequestDecision();
        CheckIfStuck();
        CheckIfFlipped();
        DrawRayToTarget();
    }

    public override void OnEpisodeBegin()
    {
        Transform spawnLocation = targetPositions[Random.Range(0, targetPositions.Count)];
        transform.position = spawnLocation.position;
        transform.rotation = spawnLocation.rotation;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        //kartController.ForceStopWheels();

        stuckTimer = 0f;

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
        sensor.AddObservation(Vector3.Dot(transform.forward, (targetInstance.transform.position - transform.position).normalized));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float acceleration = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float braking = Mathf.Clamp(actions.ContinuousActions[2], 0f, 1f);

        kartController.agentSteerInput = steering;
        kartController.agentAccelInput = acceleration;
        //kartController.agentBrakeInput = braking;
        
        //reward for getting closesr
        float currentDistance = Vector3.Distance(transform.position, targetInstance.transform.position);
        float previousDistance = Vector3.Distance(previousPosition, targetInstance.transform.position);
        AddReward(previousDistance - currentDistance);

        //small reward/punishment for forwards/backwards
        if (kartController.agentAccelInput > 0) 
        {
            AddReward(0.01f);
        }
        else if (kartController.agentAccelInput < 0) 
        {
            AddReward(-0.02f); 
        }

        
    }

    private void CheckIfStuck()
    {
        float moveDistance = Vector3.Distance(transform.position, previousPosition);
        if (moveDistance < 0.1f)
        {
            stuckTimer += Time.deltaTime;
        }
        else
        {
            stuckTimer = 0f;
            previousPosition = transform.position;
        }

        if (stuckTimer > stuckThreshold)
        {
            AddReward(-1.0f);
            stuckTimer = 0f;
        }
    }

    private void CheckIfFlipped()
    {
        if (Vector3.Dot(transform.up, Vector3.down) > 0.7f)
        {
            EndEpisode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Target") && other.transform.position == assignedTarget.position)
        {
            AddReward(5.0f);
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-1.0f);
        }
    }
    
    private void DrawRayToTarget()
    {
        if (assignedTarget == null) return;
        Debug.DrawLine(transform.position, assignedTarget.position, Color.green);
    }
}