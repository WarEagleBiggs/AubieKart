using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

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
        startPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        startRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
    }

    private void Start()
    {
        Time.timeScale = 1f;
    }

    void FixedUpdate()
    {
        RequestDecision();
    }

    public override void OnEpisodeBegin()
    {
        transform.position = spawnPoint != null ? spawnPoint.position : new Vector3(Random.Range(-10f, 10f), 1f, Random.Range(-10f, 10f));
        transform.rotation = spawnPoint != null ? spawnPoint.rotation : startRotation;
        kartController.verticalInput = 0.5f;
        kartController.horizontalInput = 0f;

        float randomX = Random.Range(-15f, 15f);
        float randomZ = Random.Range(-15f, 15f);
        target.position = new Vector3(randomX, target.position.y, randomZ);

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
        float acceleration = Mathf.Clamp(actions.ContinuousActions[1], 0.3f, 1f); 

        kartController.horizontalInput = steering;
        kartController.verticalInput = acceleration;

        Debug.Log($"🎮 AI Inputs - Steering: {kartController.horizontalInput}, Acceleration: {kartController.verticalInput}");

        float currentDistance = Vector3.Distance(transform.position, target.position);
        float progressReward = (previousDistanceToTarget - currentDistance) * 0.01f;
        AddReward(Mathf.Clamp(progressReward, -0.05f, 0.05f));

        // Small penalty per step to prevent idling
        AddReward(-0.05f);

        previousDistanceToTarget = currentDistance;
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
            AddReward(50f);
            EndEpisode();
        }
    }
}
