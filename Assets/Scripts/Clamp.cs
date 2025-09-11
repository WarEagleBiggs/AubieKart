using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class Clamp : MonoBehaviour
{
    public GameObject refernce;
    
    public void doClamp()
    {
        RaycastHit hit;
        if (Physics.Raycast(refernce.transform.position, transform.TransformDirection(Vector3.down), out hit))
        {
            Debug.DrawRay(refernce.transform.position, transform.TransformDirection(Vector3.down) * hit.distance, Color.yellow);
            refernce.transform.position = hit.point;
            transform.position = new Vector3(transform.position.x,
                hit.point.y,
                transform.position.z);
        }
    }
}