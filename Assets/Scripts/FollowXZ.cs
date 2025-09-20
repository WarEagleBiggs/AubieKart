using UnityEngine;

public class FollowXZ : MonoBehaviour
{
    public Transform target;

    void Update()
    {
        if (target != null)
        {
            transform.position = new Vector3(
                target.position.x,
                100, 
                target.position.z
            );
        }
    }
}