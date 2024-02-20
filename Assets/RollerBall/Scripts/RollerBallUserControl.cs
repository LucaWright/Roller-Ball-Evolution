using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class RollerBallUserControl : MonoBehaviour
{
    private RollerBall ball;
    
    private Transform cameraTransform; // A reference to the main camera in the scenes transform
    private Vector3 camForward, camRight; // The current forward direction of the camera
    private Vector3 moveInputDirection, lookInputDirection;
    public bool jumpButton { get; private set; }
    public bool dashButton { get; private set; }

    public Vector3 movementDirection { get; private set; }
    public Vector3 lookDirection { get; private set; }



    private void Awake()
    {
        ball = GetComponentInParent<RollerBall>();
        // get the transform of the main camera
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogWarning(
                "Warning: no main camera found. Ball needs a Camera tagged \"MainCamera\", for camera-relative controls.");
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInputDirection = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInputDirection = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        switch (context.phase)
        {
            case InputActionPhase.Disabled:
                break;
            case InputActionPhase.Waiting:
                break;
            case InputActionPhase.Started:
                jumpButton = true;
                ball.Jump();
                break;
            case InputActionPhase.Performed:
                break;
            case InputActionPhase.Canceled:
                jumpButton = false;
                break;
            default:
                break;
        }
    }


    private void Update()
    {
        // calculate camera relative direction to move:
        camForward = Vector3.Scale(cameraTransform.forward, new Vector3(1, 0, 1)).normalized;
        camRight = cameraTransform.right;
        movementDirection = (moveInputDirection.y * camForward + moveInputDirection.x * camRight).normalized;
        //Debug.DrawRay(transform.position, moveInputDirection, Color.grey);
    }

    private void FixedUpdate()
    {
        ball.Move(movementDirection);

        switch (ball.state)
        {
            case PlayerState.Grounded:
                break;
            case PlayerState.Airborne:
                break;
            default:
                break;
        }
    }

    //void SwitchState(PlayerState newState)
    //{
    //    OnExitState(ball.state);
    //    ball.state = newState;
    //    OnEnterState(newState);
    //}

    //void OnEnterState(PlayerState state)
    //{
    //    switch (state)
    //    {
    //        case PlayerState.Grounded:
    //            //ChangeMovementData(groundMovement);
    //            //EVALUATE!
    //            break;
    //        case PlayerState.Airborne:
    //            SetMovementData(airMovement);
    //            break;
    //        default:
    //            break;
    //    }
    //}

    //void OnExitState(PlayerState state)
    //{
    //    switch (state)
    //    {
    //        case PlayerState.Grounded:
    //            break;
    //        case PlayerState.Airborne:
    //            break;
    //        default:
    //            break;
    //    }
    //}
}
