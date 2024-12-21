using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KART : MonoBehaviour
{

    public Rigidbody rb;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            //move forward
            rb.AddForce(transform.forward * 10);
            
            if (Input.GetKey(KeyCode.D))
            {
                //turn and forward
                transform.Rotate(0,1,0);
            } else if (Input.GetKey(KeyCode.A))
            {
                //turn and forward
                transform.Rotate(0,-1,0);
            }
            
        } else if (Input.GetKey(KeyCode.S))
        {
            //move back
            rb.AddForce(transform.forward * -10);
        }
        
        
    }
}
