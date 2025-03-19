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
    private Vector3 previousPosition;

    private float upsideDownTimer = 0f;
    private float stuckTimer = 0f;
    private float lockTimer = 0f;
    
    private float upsideDownThreshold = 3f;
    private float stuckThreshold = 3f; 
    private float lockThreshold = 5.0f;
    private float reverseTime = 5.0f;
    private bool isLocked = false;
    private bool wasStuck = false;
    private bool isReversing = false;
    
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
        
        Time.timeScale = 1;
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

        kartController.agentAccelInput = 0f;
        kartController.agentSteerInput = 0f;
        kartController.agentBrakeInput = 1.0f;
        kartController.ForceStopWheels();

        upsideDownTimer = 0f;
        stuckTimer = 0f;
        lockTimer = 0f;
        previousPosition = transform.position;
        isLocked = false;
        wasStuck = false;
        isReversing = false;

        do
        {
            Transform selectedTarget = targetPositions[Random.Range(0, targetPositions.Count)];
            targetInstance.transform.position = selectedTarget.position;
        } while (Vector3.Distance(transform.position, targetInstance.transform.position) < 5f);

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
        float acceleration = Mathf.Clamp(actions.ContinuousActions[1], -4f, 1f);
        float braking = Mathf.Clamp(actions.ContinuousActions[2], 0f, 1f);

        if (stuckTimer > stuckThreshold || IsAgainstWall())
        {
            Debug.Log("Agent is stuck. APPLYING BRAKES...");
            kartController.agentBrakeInput = 1.0f; // Use player braking logic
            kartController.agentAccelInput = 0f;
            isLocked = true;
            lockTimer = 0f;
            return;
        }

        if (isLocked)
        {
            lockTimer += Time.deltaTime;
            if (lockTimer > 1.5f) // Pause before reversing
            {
                Debug.Log("Brakes applied. Switching to REVERSE...");
                isLocked = false;
                isReversing = true;
                kartController.agentBrakeInput = 0f; // Release brakes
                kartController.agentAccelInput = -4.0f; // Strong reverse force
                kartController.agentSteerInput = Mathf.Sign(Random.Range(-1f, 1f)) * 1.2f;
                lockTimer = 0f;
            }
            return;
        }

        if (isReversing)
        {
            lockTimer += Time.deltaTime;
            if (lockTimer > reverseTime)
            {
                Debug.Log("Reversing complete. Switching to FORWARD drive...");
                isReversing = false;
                kartController.agentAccelInput = 1.0f;
                kartController.agentBrakeInput = 0f;
                kartController.agentSteerInput = Mathf.Sign(Random.Range(-1f, 1f)) * 0.8f;
            }
            return;
        }

        kartController.agentSteerInput = Mathf.Lerp(kartController.agentSteerInput, steering, Time.deltaTime * 5f);
        kartController.agentAccelInput = acceleration;
        kartController.agentBrakeInput = braking;

        float currentDistanceToTarget = Vector3.Distance(transform.position, targetInstance.transform.position);
        float distanceChange = previousDistanceToTarget - currentDistanceToTarget;
        AddReward(distanceChange * 3.0f);
        previousDistanceToTarget = currentDistanceToTarget;
    }

    private bool IsAgainstWall()
    {
        RaycastHit hit;
        bool isWall = Physics.Raycast(transform.position, transform.forward, out hit, 1.5f) && hit.collider.CompareTag("Wall");
        return isWall;
    }

    private void CheckIfFlipped()
    {
        float upDot = Vector3.Dot(transform.up, Vector3.down);

        if (upDot > 0.7f) 
        {
            upsideDownTimer += Time.deltaTime;
            if (upsideDownTimer > upsideDownThreshold)
            {
                AddReward(-3f);
                EndEpisode();
            }
        }
        else
        {
            upsideDownTimer = 0f;
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
            previousPosition = transform.position;
        }
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