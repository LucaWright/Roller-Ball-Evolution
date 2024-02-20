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
    private bool jumpButton;
    private bool dashButton;

    private Vector3 movementDirection;
    private Vector3 lookDirection;


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
                ball.ApplyJump();
                switch (ball.state)
                {
                    case PlayerState.Grounded:
                        //
                        break;
                    case PlayerState.Airborne:
                        //
                        break;
                    default:
                        
                        break;
                }
                
                break;
            case InputActionPhase.Performed:
                break;
            case InputActionPhase.Canceled:
                switch (ball.state)
                {
                    case PlayerState.Grounded:
                        //  
                        break;
                    case PlayerState.Airborne:
                        ball.shouldDampenJump = true;
                        break;
                    default:
                        break;
                }
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
        switch (ball.state)
        {
            case PlayerState.Grounded:
                OnFixedUpdateGrounded();
                break;
            case PlayerState.Airborne:
                OnFixedUpdareAirborne();
                break;
            default:
                break;
        }
    }

    void OnFixedUpdateGrounded()
    {
        ball.GroundedMove(movementDirection);
    }

    void OnFixedUpdareAirborne()
    {
        ball.AirborneMove(movementDirection);
        if(ball.shouldDampenJump) ball.ApplyJumpCutoff();
    }

    
}
