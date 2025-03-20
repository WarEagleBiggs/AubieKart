using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class KART : MonoBehaviour
{
    public GameObject Ball;

    public float horizontalInput, verticalInput;
    public float currentSteerAngle, currentBrakeForce;
    public bool isBraking;

    public float rearWheelDrive = 0.25f;
    public float frontWheelDrive = 0.75f;

    // Touch Support
    public Button gasButton, brakeButton;
    public float touchSensitivity = 0.2f;
    private int? steerTouchIndex, gasTouchIndex, brakeTouchIndex;
    private float steerStartPosition;
    private RectTransform gasShape;
    private RectTransform brakeShape;
    private Vector2 referenceScreenSize = new Vector2(800, 600);
    private Vector2 pixelMultiple;

    public bool isPlayer;

    // AI control inputs
    public bool useAgentControls = false;
    public float agentSteerInput = 0f;
    public float agentAccelInput = 0f;
    public float agentBrakeInput = 0f;

    // Settings
    [SerializeField] private float motorForce, brakeForce, maxSteerAngle;

    // Wheel Colliders
    [SerializeField] private WheelCollider frontLeftWheelCollider, frontRightWheelCollider;
    [SerializeField] private WheelCollider rearLeftWheelCollider, rearRightWheelCollider;

    // Wheels
    [SerializeField] private Transform frontLeftWheelTransform, frontRightWheelTransform;
    [SerializeField] private Transform rearLeftWheelTransform, rearRightWheelTransform;

    private void Start()
    {
        if (isPlayer)
        {
            gasShape = gasButton.GetComponent<RectTransform>();
            brakeShape = brakeButton.GetComponent<RectTransform>();
            pixelMultiple = new Vector2(Screen.width / referenceScreenSize.x, Screen.height / referenceScreenSize.y);
        }
    }

    private void Update()
    {
        // Check for shooting (player only)
        if (Input.GetKeyDown(KeyCode.E))
        {
            GameObject test = Instantiate(Ball, Ball.transform);
            test.transform.parent = this.transform;
            Rigidbody rb = test.GetComponent<Rigidbody>();

            test.SetActive(true);
            rb.isKinematic = false;
            rb.AddForce(transform.forward * 1000, ForceMode.Impulse);
        }
    }

    private void FixedUpdate()
    {
        GetInput();
        HandleMotor();
        HandleSteering();
        UpdateWheels();
    }

    private void GetInput()
    {
        if (useAgentControls)
        {
            // Use AI inputs
            horizontalInput = agentSteerInput;
            verticalInput = agentAccelInput;
            isBraking = agentBrakeInput > 0.1f; // AI can apply brakes
            return;
        }

        // Player controls
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");
        isBraking = Input.GetKey(KeyCode.Space);
    }
    

    private void HandleMotor()
    {
        if (verticalInput > 0) 
        {
            // Apply forward motor force
            rearLeftWheelCollider.motorTorque = verticalInput * motorForce * rearWheelDrive;
            rearRightWheelCollider.motorTorque = verticalInput * motorForce * rearWheelDrive;
            frontLeftWheelCollider.motorTorque = verticalInput * motorForce * frontWheelDrive;
            frontRightWheelCollider.motorTorque = verticalInput * motorForce * frontWheelDrive;

            currentBrakeForce = 0f;
        } 
        else if (verticalInput < 0) 
        {
            // Apply reverse force equally
            rearLeftWheelCollider.motorTorque = verticalInput * motorForce * rearWheelDrive;
            rearRightWheelCollider.motorTorque = verticalInput * motorForce * rearWheelDrive;
            frontLeftWheelCollider.motorTorque = verticalInput * motorForce * frontWheelDrive;
            frontRightWheelCollider.motorTorque = verticalInput * motorForce * frontWheelDrive;

            currentBrakeForce = 0f; // Disable braking when reversing
        }
        else if (isBraking) 
        {
            // Apply full braking
            currentBrakeForce = brakeForce;
        } 
        else 
        {
            // Natural coasting
            currentBrakeForce = brakeForce * 0.2f;
        }

        ApplyBraking();
    }


    private void ApplyBraking()
    {
        frontRightWheelCollider.brakeTorque = currentBrakeForce;
        frontLeftWheelCollider.brakeTorque = currentBrakeForce;
        rearLeftWheelCollider.brakeTorque = currentBrakeForce;
        rearRightWheelCollider.brakeTorque = currentBrakeForce;
    }
    
    public void ForceStopWheels()
    {
        // Completely stop any forward/reverse movement
        frontLeftWheelCollider.motorTorque = 0f;
        frontRightWheelCollider.motorTorque = 0f;
        rearLeftWheelCollider.motorTorque = 0f;
        rearRightWheelCollider.motorTorque = 0f;

        // Apply maximum brake force
        frontLeftWheelCollider.brakeTorque = brakeForce * 10f;
        frontRightWheelCollider.brakeTorque = brakeForce * 10f;
        rearLeftWheelCollider.brakeTorque = brakeForce * 10f;
        rearRightWheelCollider.brakeTorque = brakeForce * 10f;
    
        //Debug.Log("ForceStopWheels: Wheels are fully locked.");
    }


    private void HandleSteering()
    {
        currentSteerAngle = maxSteerAngle * horizontalInput;
        frontLeftWheelCollider.steerAngle = currentSteerAngle;
        frontRightWheelCollider.steerAngle = currentSteerAngle;
    }

    private void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftWheelCollider, frontLeftWheelTransform);
        UpdateSingleWheel(frontRightWheelCollider, frontRightWheelTransform);
        UpdateSingleWheel(rearRightWheelCollider, rearRightWheelTransform);
        UpdateSingleWheel(rearLeftWheelCollider, rearLeftWheelTransform);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
    {
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelTransform.rotation = rot;
        wheelTransform.position = pos;
    }
}
