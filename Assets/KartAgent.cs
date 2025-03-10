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
    private Rigidbody rb;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float previousDistanceToTarget;

    public override void Initialize()
    {
        kartController = GetComponent<KART>();
        rb = GetComponent<Rigidbody>();

        startPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        startRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        Debug.Log("🚀 KartAgent Initialized");
    }

    private void Start()
    {
        Time.timeScale = 1f;
    }

    void FixedUpdate()
    {
        if (StepCount % 5 == 0)
        {
            RequestDecision();
            Debug.Log("🔥 AI requested new decision");
        }

        Debug.Log($"🚗 Applied Movement - Steering: {kartController.horizontalInput}, Acceleration: {kartController.verticalInput}");
    }

    public override void OnEpisodeBegin()
    {
        transform.position = spawnPoint != null ? spawnPoint.position : new Vector3(Random.Range(-10f, 10f), 1f, Random.Range(-10f, 10f));
        transform.rotation = spawnPoint != null ? spawnPoint.rotation : startRotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position += new Vector3(0, 0.5f, 0);

        kartController.verticalInput = 1f;
        kartController.horizontalInput = 0f;
        kartController.isBreaking = false;

        float randomX = Random.Range(-15f, 15f);
        float randomZ = Random.Range(-15f, 15f);
        target.position = new Vector3(randomX, target.position.y, randomZ);

        previousDistanceToTarget = Vector3.Distance(transform.position, target.position);

        Debug.Log($"🎯 New Target Position: {target.position}");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.position);
        sensor.AddObservation(target.position);
        sensor.AddObservation(rb.velocity);
        sensor.AddObservation(rb.angularVelocity);
        sensor.AddObservation(Vector3.Distance(transform.position, target.position));

        Debug.Log($"📡 Observations - Position: {transform.position}, Target: {target.position}, Velocity: {rb.velocity}");
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float acceleration = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        bool isBreaking = actions.ContinuousActions[2] > 0.5f;

        if (Mathf.Abs(acceleration) < 0.5f)
        {
            acceleration = acceleration >= 0 ? 1f : -1f;
        }

        if (Mathf.Abs(steering) < 0.3f)
        {
            steering = steering >= 0 ? 0.7f : -0.7f;
        }

        kartController.horizontalInput = steering * 200f;
        kartController.verticalInput = acceleration * 200f;
        kartController.isBreaking = isBreaking;

        Debug.Log($"🎮 AI Inputs - Steering: {steering}, Acceleration: {acceleration}, Braking: {isBreaking}");

        if (Mathf.Abs(acceleration) > 0.1f)
        {
            AddReward(1f);
        }

        float currentDistance = Vector3.Distance(transform.position, target.position);
        float progressReward = (previousDistanceToTarget - currentDistance) * 1f;
        AddReward(Mathf.Clamp(progressReward, -2f, 2f));

        AddReward(-0.01f);

        previousDistanceToTarget = currentDistance;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal") * 200f;
        continuousActions[1] = Input.GetAxis("Vertical") * 200f;
        continuousActions[2] = Input.GetKey(KeyCode.Space) ? 1f : 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Target"))
        {
            AddReward(50f);
            Debug.Log("🏆 AI reached the target!");
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-10f);
            Debug.Log("🚧 AI hit a wall - Reversing");
            kartController.verticalInput = -1f;
            kartController.horizontalInput = Random.Range(-1f, 1f) * 200f;
            RequestDecision();
        }
    }
}
