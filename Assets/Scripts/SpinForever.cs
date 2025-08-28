using UnityEngine;

public class SpinForever : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Vector3 rotationAxis = Vector3.up; // Default is Y-axis
    public float rotationSpeed = 90f; // Degrees per second

    public bool isArrow;
    public GameObject Car;

    void Update()
    {
        if (isArrow)
        {
            transform.LookAt(Car.transform);
        }
        else
        {
            transform.Rotate(rotationAxis.normalized * rotationSpeed * Time.deltaTime);
        }
    }
}
