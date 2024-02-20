using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CustomBall : MonoBehaviour
{
    public Rigidbody rb;
    public GameObject model;

    public float accelerationTime = 1f, maxSpeed = 6f, decelerationTime = 5f;
    float acceleration, deceleration;
    Vector3 velocity;

    
    // Start is called before the first frame update
    void Start()
    {
        acceleration = maxSpeed / accelerationTime;
        deceleration = maxSpeed / decelerationTime;
    }

    // Update is called once per frame
    void Update()
    {
        acceleration = maxSpeed / accelerationTime;
        deceleration = maxSpeed / decelerationTime;
    }

    public void Move(Vector3 desiredDirection)
    {
        //Debug.Break();
        var velocity = rb.velocity;

        if (desiredDirection != Vector3.zero)
        {
            velocity = Vector3.MoveTowards(velocity, desiredDirection * maxSpeed, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }

        //transform.position += velocity * Time.fixedDeltaTime;
        rb.velocity = velocity;

    }
}
