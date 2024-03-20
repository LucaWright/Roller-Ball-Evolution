using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CannonBehaviour : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float fireRatio = 1f;
    [SerializeField] private float bulletLifetime = 1f;
    private List<GameObject> bulletPool;
    private int bulletIndex;
    private float fireTimeStamp;

    void Start()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning(
                "Warning: no bullet prefab. Emitter disabled.");
            this.enabled = false;
            return;
        }
        
        bulletPool = new List<GameObject>();
        var bulletCount = Mathf.CeilToInt(bulletLifetime / fireRatio) + 1;

        for (int i = 0; i < bulletCount ; i++)
        {
            GameObject bullet = Instantiate(bulletPrefab, this.transform);
            bullet.transform.position = this.transform.position;
            bullet.transform.rotation = this.transform.rotation;
            bulletPool.Add(bullet);
            bullet.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        var timeEnlapsed = Time.time - fireTimeStamp;
        if (timeEnlapsed >= fireRatio)
        {
            fireTimeStamp = Time.time - (timeEnlapsed - fireRatio);
            Shoot();
        }
    }

    void Shoot()
    {
        var bullet = bulletPool[bulletIndex % bulletPool.Count];
        StartCoroutine(BulletCountdown(bullet));
        bulletIndex++;
    }

    IEnumerator BulletCountdown(GameObject bullet)
    {
        yield return new WaitForFixedUpdate();

        bullet.SetActive(true);
        var timeStamp = Time.time;

        while(Time.time - timeStamp < bulletLifetime)
        {
            yield return new WaitForFixedUpdate();
        }

        bullet.transform.position = this.transform.position;
        bullet.transform.rotation = this.transform.rotation;
        bullet.SetActive(false);
    }
}
