using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletBehaviour : MonoBehaviour
{
    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private float bulletLifetime = 5f;
    private float timeStamp;

    void Start()
    {
        timeStamp = Time.time;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        if (Time.time - timeStamp >= bulletLifetime) Destroy(this.gameObject);
        transform.position += transform.forward * bulletSpeed * Time.fixedDeltaTime;
        transform.Rotate(180f * Time.deltaTime * Vector3.forward);
    }
}
