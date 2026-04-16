using UnityEngine;

public class Platform : MonoBehaviour
{
    [Header("Movement Range")]
    public float minY = 0f;
    public float maxY = 3f;

    [Header("Speed")]
    public float moveSpeed = 2f;

    [Header("Random Pause")]
    public float waitTimeMin = 0.2f;
    public float waitTimeMax = 1.0f;

    private float targetY;
    private float waitTimer;

    void Start()
    {
        PickNewTarget();
    }

    void Update()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        Vector3 pos = transform.position;
        float newY = Mathf.MoveTowards(pos.y, targetY, moveSpeed * Time.deltaTime);
        transform.position = new Vector3(pos.x, newY, pos.z);

        if (Mathf.Abs(newY - targetY) < 0.05f)
        {
            PickNewTarget();
        }
    }

    void PickNewTarget()
    {
        targetY = Random.Range(minY, maxY);
        waitTimer = Random.Range(waitTimeMin, waitTimeMax);
    }
}