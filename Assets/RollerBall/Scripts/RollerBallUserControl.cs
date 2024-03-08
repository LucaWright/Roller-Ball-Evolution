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

    private Vector3 desiredMovementDirection;
    private Vector3 lookDirection;

    private Vector3 previousMovementDirection;

    public enum GameMode { _3D , _2D }
    [SerializeField] private GameMode gameMode;


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
                StartCoroutine(ball.TryJump());
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
                        ball.EvaluateJumpCutOff();
                        break;
                    default:
                        break;
                }
                break;
            default:
                break;
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        switch (context.phase)
        {
            case InputActionPhase.Disabled:
                break;
            case InputActionPhase.Waiting:
                break;
            case InputActionPhase.Started:
                switch (gameMode)
                {
                    case GameMode._3D:
                        StartCoroutine(ball.TryDash(desiredMovementDirection, camForward));
                        break;
                    case GameMode._2D:
                        StartCoroutine(ball.TryDash(desiredMovementDirection, previousMovementDirection)); // Dovrebbe prendere l'ultima direction
                        break;
                }
                break;
            case InputActionPhase.Performed:
                break;
            case InputActionPhase.Canceled:
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
        switch (gameMode)
        {
            case GameMode._3D:
                desiredMovementDirection = (moveInputDirection.y * camForward + moveInputDirection.x * camRight).normalized;
                break;
            case GameMode._2D:
                desiredMovementDirection = (moveInputDirection.x * camRight).normalized;
                if (desiredMovementDirection != Vector3.zero) previousMovementDirection = desiredMovementDirection.normalized;
                break;
        }
        ball.DrawGroundNormal();

        //Debug.DrawRay(transform.position, moveInputDirection, Color.grey);
    }

    private void FixedUpdate()
    {
        
        switch (ball.state)
        {
            case PlayerState.Grounded:
                OnFixedUpdateGrounded();
                /* E se facessi un "internal collision check?"
                 * Sparo uno spherecast, anticipo la collisione, aggiungo forze.
                 * Va testato.
                 * Questo refactoring si sta rilevando un casino totale
                 */
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
        ball.GroundedMove(desiredMovementDirection);
    }

    void OnFixedUpdareAirborne()
    {
        ball.AirborneMove(desiredMovementDirection);
    }

    public void SwitchGameMode()
    {
        switch (gameMode)
        {
            case GameMode._3D:
                gameMode = GameMode._2D;
                break;
            case GameMode._2D:
                gameMode = GameMode._3D;
                break;
            default:
                break;
        }
    }    
}
