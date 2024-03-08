using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleCharacter : MonoBehaviour
{
    public float speed = 3.0F;
    public float rotateSpeed = 3.0F;

    private CharacterController controller;
    private Vector3 moveDirection = Vector3.zero;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Muovi il personaggio con WASD
        moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        moveDirection = transform.TransformDirection(moveDirection);
        moveDirection *= speed;

        // Ruota la visuale a destra/sinistra con il mouse
        transform.Rotate(0, Input.GetAxis("Mouse X") * rotateSpeed, 0);

        // Applica il movimento al character controller
        controller.Move(moveDirection * Time.deltaTime);
    }
}
