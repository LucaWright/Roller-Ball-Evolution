using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformAttachment : MonoBehaviour
{
    private GameObject player;
    private Vector3 oldPosition;
    private Vector3 computedVelocity;
    private float allowance;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        allowance = Mathf.Cos(player.GetComponent<RollerBall>().GetMinSlopeAngle());
        oldPosition = transform.position;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject == player)
        {
            foreach (ContactPoint contact in collision.contacts)
            {
                if (Vector3.Dot(contact.normal, Physics.gravity.normalized) >= allowance) // Get Slope?
                {
                    player.transform.parent = transform;
                    break;
                }
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject == player)
        {
            player.transform.parent = null;
        }
    }

    private void FixedUpdate()
    {
        computedVelocity = (transform.position - oldPosition) / Time.fixedDeltaTime;
        oldPosition = transform.position;
    }
}
