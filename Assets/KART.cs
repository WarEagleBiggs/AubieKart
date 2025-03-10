using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class KART : MonoBehaviour
{
    public GameObject Ball;

    public float horizontalInput, verticalInput;
    public float currentSteerAngle, currentbreakForce;
    public bool isBreaking;

    public float rearWheelDrive = 0.25f;
    public float frontWheelDrive = 0.75f;
    
    // Touch Support
    public Button gasButton, breakButton;
    public float touchSensitivity = 0.2f;
    private int? steerTouchIndex, gasTouchIndex, breakTouchIndex;
    private float steerStartPosition;
    private RectTransform gasShape;
    private RectTransform breakShape;
    private Vector2 referenceScreenSize = new Vector2(800, 600);
    private Vector2 pixelMultiple;

    public bool isPlayer;
    
    // Settings
    [SerializeField] private float motorForce, breakForce, maxSteerAngle;

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
            breakShape = breakButton.GetComponent<RectTransform>();
            pixelMultiple = new Vector2(Screen.width / referenceScreenSize.x, Screen.height / referenceScreenSize.y);
        }
        
    }
    
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

    private void TouchDebug()
    {
        if (steerTouchIndex.HasValue)
        {
            Debug.Log("Steering", gasButton);
            Vector2 touchPos = Input.GetTouch(steerTouchIndex.Value).position;
            Debug.DrawLine(Input.GetTouch(steerTouchIndex.Value).rawPosition ,touchPos,Color.red);
        }
        if (gasTouchIndex.HasValue){ Debug.Log("Gas", gasButton); }
        if (breakTouchIndex.HasValue){ Debug.Log("Break", breakButton); }
    }

    private void GetInput() {
        // Bypass keyboard input if touchscreen in use
        if (Input.touchCount > 0)
        {
            GetTouchInput();
            return;
        }
        steerTouchIndex = null;
        gasTouchIndex = null;
        breakTouchIndex = null;
        
        // Steering Input
        horizontalInput = Input.GetAxis("Horizontal");

        // Acceleration Input
        verticalInput = Input.GetAxis("Vertical");

        // Breaking Input
        isBreaking = Input.GetKey(KeyCode.Space);
    }

    private void GetTouchInput()
    {
        if (gasTouchIndex.HasValue)
        {
            Touch t = Input.GetTouch(gasTouchIndex.Value);
            if (t.phase is TouchPhase.Ended or TouchPhase.Canceled)
            {
                gasTouchIndex = null;
                verticalInput = 0f;
            }
        }
        if (breakTouchIndex.HasValue)
        {
            Touch t = Input.GetTouch(breakTouchIndex.Value);
            if (t.phase is TouchPhase.Ended or TouchPhase.Canceled)
            {
                breakTouchIndex = null;
                isBreaking = false;
            }
        }
        if (steerTouchIndex.HasValue)
        {
            Touch t = Input.GetTouch(steerTouchIndex.Value);
            if (t.phase == TouchPhase.Moved)
            {
                float change = t.position.x - steerStartPosition;
                horizontalInput = Mathf.Clamp(change * touchSensitivity, -1f, 1f);
            }
            else if (t.phase is TouchPhase.Ended or TouchPhase.Canceled)
            {
                steerTouchIndex = null;
                horizontalInput = 0f;
            }
        }
        for(int i = 0; i < Input.touchCount; i++)
        {
            if(i.Equals(gasTouchIndex) || i.Equals(breakTouchIndex) || i.Equals(steerTouchIndex)){ continue; }
            Touch t = Input.GetTouch(i);
            if (!gasTouchIndex.HasValue && touchOnButton(i, gasShape))// && t.phase == TouchPhase.Began)
            {
                gasTouchIndex = i;
                verticalInput = 1;
                Debug.Log("Gas Pressed", gasButton);
                continue;
            }
            else if (!breakTouchIndex.HasValue && touchOnButton(i, breakShape))// && t.phase == TouchPhase.Began)
            {
                breakTouchIndex = i;
                isBreaking = true;
                Debug.Log("Break Pressed", breakButton);
                continue;
            }
            else if (!steerTouchIndex.HasValue)
            {
                steerStartPosition = t.position.x;
                steerTouchIndex = i;
                Debug.Log("Began Steering", gasButton);
                continue;
            }
        }
    }

    private bool touchOnButton(int index, RectTransform rt)
    {
        Touch touch = Input.GetTouch(index);
        Vector2 pos = touch.position;
        Vector2 rtPos = rt.position; 
        float width = rt.rect.width * pixelMultiple.x;
        float height = rt.rect.height * pixelMultiple.y;
        //Debug.Log("x values[" + rtPos.x + "__" + pos.x + "__" + (rtPos.x + width) + "]", gasButton);
        //Debug.Log("y values[" + rtPos.y + "__" + pos.y + "__" + (rtPos.y + height) + "]", gasButton);
        if (pos.x <= rtPos.x + width && pos.x >= rtPos.x && pos.y <= rtPos.y + height &&
            pos.y >= rtPos.y){
            return true;
        }
        return false;
        
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