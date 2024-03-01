using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CannonBehaviour : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int bulletPoolSize = 5;
    [SerializeField] private Transform bulletEmitter;
    [SerializeField] private float fireRatio;
    [SerializeField] private float bulletLifetime;
    private float fireTimeStamp;
    private float bulletTimeStamp;

    private Queue<GameObject> bulletPool;
    
    // Start is called before the first frame update
    void Start()
    {
        //bulletPool = new Queue<GameObject>();

        //for (int i = 0; i < bulletPoolSize; i++)
        //{
        //    GameObject bullet = Instantiate(bulletPrefab);
        //    bullet.SetActive(false);
        //    bullet.transform.parent = bulletEmitter;
        //    bulletPool.Enqueue(bullet);
        //}
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        if (Time.time - fireTimeStamp >= fireRatio)
        {
            fireTimeStamp = Time.time;
            Shoot();
        }

    }

    void Shoot()
    {
        //if (bulletPool.Count <= 0)
        //{
        //    bulletPool.Enqueue(bulletPool.Peek());
        //}
        //GameObject bullet = bulletPool.Dequeue();
        //bullet.transform.rotation = bulletEmitter.rotation;
        //bullet.transform.position = bulletEmitter.position;
        //bullet.SetActive(true);

        Instantiate(bulletPrefab, bulletEmitter.position, bulletEmitter.rotation, bulletEmitter);
    }

}
