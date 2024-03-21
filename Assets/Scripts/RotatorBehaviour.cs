using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotatorBehaviour : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    private void FixedUpdate()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime * rotationAxis);
    }
}
