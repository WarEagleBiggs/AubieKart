using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KART : MonoBehaviour
{
    public GameObject Ball;

    private float horizontalInput, verticalInput;
    private float currentSteerAngle, currentbreakForce;
    private bool isBreaking;

    public float rearWheelDrive = 0.2f;
    public float frontWheelDrive = 1.0f;
    

    // Settings
    [SerializeField] private float motorForce, breakForce, maxSteerAngle;

    // Wheel Colliders
    [SerializeField] private WheelCollider frontLeftWheelCollider, frontRightWheelCollider;
    [SerializeField] private WheelCollider rearLeftWheelCollider, rearRightWheelCollider;

    // Wheels
    [SerializeField] private Transform frontLeftWheelTransform, frontRightWheelTransform;
    [SerializeField] private Transform rearLeftWheelTransform, rearRightWheelTransform;

    private void Update()
    {
        //check for shoot
        if (Input.GetKeyDown(KeyCode.E))
        {
            //shoots ball
            GameObject test = Instantiate(Ball, Ball.transform);
            test.transform.parent = this.transform;
            Rigidbody rb = test.GetComponent<Rigidbody>();
           
            test.SetActive(true);
            rb.isKinematic = false;
            
            rb.AddForce(transform.forward * 1000, ForceMode.Impulse);
            
        }
    }

    private void FixedUpdate() {
        GetInput();
        HandleMotor();
        HandleSteering();
        UpdateWheels();
    }

    private void GetInput() {
        
        
        // Steering Input
        horizontalInput = Input.GetAxis("Horizontal");

        // Acceleration Input
        verticalInput = Input.GetAxis("Vertical");

        // Breaking Input
        isBreaking = Input.GetKey(KeyCode.Space);
    }

    private void HandleMotor() {
            rearLeftWheelCollider.motorTorque = verticalInput * motorForce * rearWheelDrive;
            rearRightWheelCollider.motorTorque = verticalInput * motorForce * rearWheelDrive;

            frontLeftWheelCollider.motorTorque = verticalInput * motorForce * frontWheelDrive;
            frontRightWheelCollider.motorTorque = verticalInput * motorForce * frontWheelDrive;

        currentbreakForce = isBreaking ? breakForce : 0f;
        ApplyBreaking();
    }

    private void ApplyBreaking() {
        frontRightWheelCollider.brakeTorque = currentbreakForce;
        frontLeftWheelCollider.brakeTorque = currentbreakForce;
        rearLeftWheelCollider.brakeTorque = currentbreakForce;
        rearRightWheelCollider.brakeTorque = currentbreakForce;
    }

    private void HandleSteering() {
        currentSteerAngle = maxSteerAngle * horizontalInput;
        frontLeftWheelCollider.steerAngle = currentSteerAngle;
        frontRightWheelCollider.steerAngle = currentSteerAngle;
    }

    private void UpdateWheels() {
        UpdateSingleWheel(frontLeftWheelCollider, frontLeftWheelTransform);
        UpdateSingleWheel(frontRightWheelCollider, frontRightWheelTransform);
        UpdateSingleWheel(rearRightWheelCollider, rearRightWheelTransform);
        UpdateSingleWheel(rearLeftWheelCollider, rearLeftWheelTransform);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform) {
        Vector3 pos;
        Quaternion rot; 
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelTransform.rotation = rot;
        wheelTransform.position = pos;
    }
}