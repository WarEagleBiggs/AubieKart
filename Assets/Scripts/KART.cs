using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using Random = UnityEngine.Random;

public class KART : MonoBehaviour
{

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
    
    //ai test
    public bool useAgentControls = false; // Flag to determine if AI is controlling the kart
    public float agentSteerInput = 0f; // AI Steering input
    public float agentAccelInput = 0f; // AI Acceleration input
    
    // Settings
    [SerializeField] private float motorForce, breakForce, maxSteerAngle;

    // Wheel Colliders
    [SerializeField] private WheelCollider frontLeftWheelCollider, frontRightWheelCollider;
    [SerializeField] private WheelCollider rearLeftWheelCollider, rearRightWheelCollider;

    // Wheels
    [SerializeField] private Transform frontLeftWheelTransform, frontRightWheelTransform;
    [SerializeField] private Transform rearLeftWheelTransform, rearRightWheelTransform;
    
    
    // --- Auto Unflip / Stuck Recovery ---
    [Header("Auto Recovery")]
    [SerializeField] private bool autoUnflip = true;
    [SerializeField] private float unflipDelay = 1.0f;        // seconds tipped before recovering
    [SerializeField] private float flipDotThreshold = 0.35f;   // <= this means "tipped" (dot(up,WorldUp))
    [SerializeField] private float minSpeedForRecovery = 0.8f; // only recover if mostly stopped
    [SerializeField] private float groundRayHeight = 5f;       // raycast height above kart for fallback
    [SerializeField] private float groundRayDistance = 20f;    // how far to look for the floor
    [SerializeField] private float uprightOffsetY = 0.25f;     // small lift off the ground

    [SerializeField] private DebugGame debugGame;

    private float tippedTimer = 0f;
    private Rigidbody rb;
    
    
    //item manager
    public bool hasItem;

    public bool canUseItem;

    public GameObject itemImage_Box;
    public GameObject itemImage_Banana;
    public GameObject itemImage_Tire;

    public GameObject item_Banana;
    public GameObject item_Tire;
    



    private void Start()
    {
        if (isPlayer)
        {
            gasShape = gasButton.GetComponent<RectTransform>();
            breakShape = breakButton.GetComponent<RectTransform>();
            pixelMultiple = new Vector2(Screen.width / referenceScreenSize.x, Screen.height / referenceScreenSize.y);
        }
        rb = GetComponent<Rigidbody>();
    }

    public GameObject PowerBoxParent;
    private void Update()
    {
        //check for shoot
        if (Input.GetKeyDown(KeyCode.E) && canUseItem && isPlayer && hasItem)
        {

            canUseItem = false;
            
            if (itemImage_Tire.activeSelf)
            {
                //shoots tire
                GameObject test = Instantiate(item_Tire, item_Tire.transform);
                test.transform.parent = this.transform;
                Rigidbody rb = test.GetComponent<Rigidbody>();
                test.SetActive(true);
                rb.isKinematic = false;
                rb.AddForce(transform.forward * 1000, ForceMode.Impulse);
            } else if (itemImage_Banana.activeSelf)
            {

                //drops banana
                GameObject bana = Instantiate(item_Banana, item_Banana.transform);
                bana.transform.parent = PowerBoxParent.transform.parent;
                bana.SetActive(true);

                Clamp clampScript = bana.GetComponent<Clamp>();
                clampScript.doClamp();


            }
            
            itemImage_Box.SetActive(false);
            itemImage_Banana.SetActive(false);
            itemImage_Tire.SetActive(false);


        }
    }

    private void FixedUpdate() {
        GetInput();
        HandleMotor();
        HandleSteering();
        UpdateWheels();
        
        CheckFlipAndRecover();
    }
    
    private void CheckFlipAndRecover()
    {
        if (!autoUnflip || rb == null) return;

        // Dot of our up vs world up: 1 = upright, 0 = sideways, -1 = fully upside-down
        float upDot = Vector3.Dot(transform.up, Vector3.up);
        bool tipped = upDot <= flipDotThreshold; // sideways or worse
        bool slow = rb.velocity.magnitude <= minSpeedForRecovery;

        if (tipped && slow)
        {
            tippedTimer += Time.fixedDeltaTime;
            if (tippedTimer >= unflipDelay)
            {
                DoAutoRecover();
                tippedTimer = 0f;
            }
        }
        else
        {
            tippedTimer = 0f;
        }
    }

    private void DoAutoRecover()
    {
        // 1) If you’ve linked DebugGame, use its exact Stuck() behavior (teleport to REF)
        if (debugGame != null)
        {
            debugGame.Stuck();
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        // 2) Otherwise, self-recover: find ground under/nearby, upright the kart, zero velocity
        Vector3 start = transform.position + Vector3.up * groundRayHeight;
        Vector3 targetPos = transform.position + Vector3.up * 1.0f; // fallback position

        if (Physics.Raycast(start, Vector3.down, out var hit, groundRayDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            targetPos = hit.point + Vector3.up * uprightOffsetY;
        }

        // Keep current yaw (heading) but align to world up
        Vector3 fwdFlat = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (fwdFlat.sqrMagnitude < 1e-4f) fwdFlat = transform.right; // fallback if degenerate

        Quaternion uprightRot = Quaternion.LookRotation(fwdFlat, Vector3.up);

        // Apply transform & clear motion
        transform.SetPositionAndRotation(targetPos, uprightRot);
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
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
        if (useAgentControls) 
        {
            // Use AI inputs instead of player inputs
            horizontalInput = agentSteerInput;
            verticalInput = agentAccelInput;
            isBreaking = false; // You can modify this if braking is necessary
            return;
        }

        // Normal player inputs
        if (Input.touchCount > 0) {
            GetTouchInput();
            return;
        }
    
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");
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

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "BOX")
        {
            MysteryBox Mys = other.GetComponent<MysteryBox>();
            Mys.StartCoroutine(Mys.ShrinkAndRespawn());
            ItemPickup();
        } else if (other.CompareTag("Banana"))
        {
            
            rb.AddTorque(Vector3.up * 300, ForceMode.Impulse);
        }
    }

    public void ItemPickup()
    {
        hasItem = true;
        StartCoroutine(ItemCycleAnimation());

    }

    public IEnumerator ItemCycleAnimation()
    {
        itemImage_Box.SetActive(true);
        yield return new WaitForSeconds(2);
        
        //select item
        int rand = Random.Range(0,2);
        if (rand == 0)
        {
            //banana
            itemImage_Box.SetActive(false);
            itemImage_Banana.SetActive(true);
            itemImage_Tire.SetActive(false);
        } else if (rand == 1)
        {
            //tire
            itemImage_Box.SetActive(false);
            itemImage_Banana.SetActive(false);
            itemImage_Tire.SetActive(true);
        }
        
        canUseItem = true;
    }
}