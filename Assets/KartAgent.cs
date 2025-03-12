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
    [SerializeField] private Transform spawnPoint;
    private GameObject targetInstance;
    private KART kartController;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float previousDistanceToTarget;

    public override void Initialize()
    {
        kartController = GetComponent<KART>();
        kartController.useAgentControls = true;
        startPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        startRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        targetInstance = Instantiate(targetPrefab);
    }

    private void Start()
    {
        int agentLayer = LayerMask.NameToLayer("KART");
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

        kartController.agentAccelInput = 0.5f;
        kartController.agentSteerInput = 0f;
        kartController.agentBrakeInput = 0f;

        float randomX = Random.Range(-30f, 30f);
        float randomZ = Random.Range(-30f, 30f);
        targetInstance.transform.position = new Vector3(randomX, targetInstance.transform.position.y, randomZ);

        previousDistanceToTarget = Vector3.Distance(transform.position, targetInstance.transform.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.position);
        sensor.AddObservation(targetInstance.transform.position);
        sensor.AddObservation(Vector3.Distance(transform.position, targetInstance.transform.position));

        Vector3 directionToTarget = (targetInstance.transform.position - transform.position).normalized;
        float angleToTarget = Vector3.Dot(transform.forward, directionToTarget);
        sensor.AddObservation(angleToTarget);

        // ✅ Detect walls using ray perception
        float[] rayAngles = { -60f, -30f, -15f, 0f, 15f, 30f, 60f };
        foreach (float angle in rayAngles)
        {
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, 10f))
            {
                if (hit.collider.CompareTag("Wall"))
                {
                    sensor.AddObservation(1f); // Wall detected
                    sensor.AddObservation(hit.distance / 10f); // Normalize distance
                }
                else
                {
                    sensor.AddObservation(0f); // No wall detected
                    sensor.AddObservation(1f); // Max distance
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
        if (actions.ContinuousActions.Length < 3)
        {
            Debug.LogError($"Not enough actions received! Expected 3, got {actions.ContinuousActions.Length}");
            return;
        }

        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float acceleration = Mathf.Clamp(actions.ContinuousActions[1], 0f, 1f);
        float braking = Mathf.Clamp(actions.ContinuousActions[2], 0f, 1f);

        kartController.agentSteerInput = steering;
        kartController.agentAccelInput = acceleration;
        kartController.agentBrakeInput = braking;

        float currentDistanceToTarget = Vector3.Distance(transform.position, targetInstance.transform.position);
        Vector3 directionToTarget = (targetInstance.transform.position - transform.position).normalized;
        float angleToTarget = Vector3.Dot(transform.forward, directionToTarget);

        float distanceChange = previousDistanceToTarget - currentDistanceToTarget;
        AddReward(distanceChange * 2.0f);

        if (braking > 0.5f) AddReward(-0.2f);

        // ✅ Penalize AI for getting too close to walls
        float[] rayAngles = { -60f, -30f, -15f, 0f, 15f, 30f, 60f };
        foreach (float angle in rayAngles)
        {
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, 2.5f)) // Detect walls within 2.5m
            {
                if (hit.collider.CompareTag("Wall"))
                {
                    AddReward(-0.5f); // ✅ Penalize for being too close to walls
                    Debug.Log("AI Too Close to Wall! Penalty Applied.");
                }
            }
        }

        previousDistanceToTarget = currentDistanceToTarget;
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Wall"))
        {
            AddReward(-2f); // ✅ Strong penalty for hitting walls
            Debug.Log("AI Crashed into Wall! Major Penalty.");
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Mathf.Clamp(Input.GetAxis("Vertical"), 0f, 1f);
        continuousActions[2] = Input.GetKey(KeyCode.Space) ? 1f : 0f; // ✅ Player can brake too
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == targetInstance)
        {
            AddReward(10f); // ✅ Big reward for reaching the target
            EndEpisode();
        }
    }
}
