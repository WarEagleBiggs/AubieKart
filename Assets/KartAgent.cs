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
    [SerializeField] private List<Transform> targetPositions;

    private GameObject targetInstance;
    private KART kartController;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float previousDistanceToTarget;

    private float upsideDownTimer = 0f;
    private float sidewaysTimer = 0f;
    private float stuckTimer = 0f;
    private float reverseTimer = 0f;

    private float upsideDownThreshold = 3f;
    private float sidewaysThreshold = 3f;
    private float stuckThreshold = 3f; 
    private float reverseThreshold = 2f; 

    private Vector3 previousPosition; 

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
        CheckIfFlipped();
        CheckIfStuck();
    }

    public override void OnEpisodeBegin()
    {
        transform.position = spawnPoint != null ? spawnPoint.position : new Vector3(Random.Range(-10f, 10f), 1f, Random.Range(-10f, 10f));
        transform.rotation = spawnPoint != null ? spawnPoint.rotation : startRotation;

        kartController.agentAccelInput = 0.3f;
        kartController.agentSteerInput = 0f;
        kartController.agentBrakeInput = 0f;
        upsideDownTimer = 0f;
        sidewaysTimer = 0f;
        stuckTimer = 0f;
        reverseTimer = 0f;

        previousPosition = transform.position; 

        if (targetPositions.Count > 0)
        {
            Transform selectedTarget = targetPositions[Random.Range(0, targetPositions.Count)];
            targetInstance.transform.position = selectedTarget.position;
        }
        else
        {
            Debug.LogError("Target positions list is empty! Please add target positions.");
        }

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

        AddReward(distanceChange * 3.0f);
        AddReward(angleToTarget * 0.5f);

        if (acceleration > 0.7f && angleToTarget < 0.3f)
        {
            AddReward(-0.3f);
        }

        if (braking > 0.5f)
        {
            AddReward(0.2f);
        }

        previousDistanceToTarget = currentDistanceToTarget;
    }

    private void CheckIfFlipped()
    {
        float upDot = Vector3.Dot(transform.up, Vector3.down);
        float sideDot = Mathf.Abs(Vector3.Dot(transform.right, Vector3.up));

        if (upDot > 0.7f) 
        {
            upsideDownTimer += Time.deltaTime;
            if (upsideDownTimer > upsideDownThreshold)
            {
                Debug.Log("AI Stuck Upside Down! Resetting...");
                AddReward(-3f);
                EndEpisode();
            }
        }
        else
        {
            upsideDownTimer = 0f;
        }

        if (sideDot > 0.8f) 
        {
            sidewaysTimer += Time.deltaTime;
            if (sidewaysTimer > sidewaysThreshold)
            {
                Debug.Log("AI Stuck On Side! Resetting...");
                AddReward(-3f);
                EndEpisode();
            }
        }
        else
        {
            sidewaysTimer = 0f;
        }
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
            reverseTimer = 0f; 
            previousPosition = transform.position; 
        }

        if (stuckTimer > stuckThreshold)
        {
            Debug.Log("AI Stuck! Attempting Reverse...");
            kartController.agentAccelInput = -0.5f; 
            kartController.agentBrakeInput = 0f; 
            reverseTimer += Time.deltaTime;

            if (reverseTimer > reverseThreshold) 
            {
                Debug.Log("AI Still Stuck! Resetting...");
                AddReward(-3f);
                EndEpisode();
            }
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Wall"))
        {
            AddReward(-3f);
            Debug.Log("AI Crashed into Wall! Major Penalty.");
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Mathf.Clamp(Input.GetAxis("Vertical"), 0f, 1f);
        continuousActions[2] = Input.GetKey(KeyCode.Space) ? 1f : 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == targetInstance)
        {
            AddReward(10f);
            EndEpisode();
        }
    }
}
