using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CustomBallControl : MonoBehaviour
{
    private CustomBall ball; // Reference to the ball controller.

    private Vector3 movementInputDir, lookingInputDir;
    private Vector3 movementVector;
    // the world-relative desired move direction, calculated from the camForward and user input.

    public Transform orientationObject; // A reference to the main camera in the scenes transform
    private Vector3 camForward, camRight; // The current forward direction of the camera
    private bool jump; // whether the jump button is currently pressed
    private float jumpTimer;


    private void Awake()
    {
        // Set up the reference.
        ball = GetComponentInParent<CustomBall>();

        // get the transform of the main camera
        if (Camera.main != null)
        {
            orientationObject = Camera.main.transform;
        }
        else
        {
            Debug.LogWarning(
                "Warning: no main camera found. Ball needs a Camera tagged \"MainCamera\", for camera-relative controls.");
            // we use world-relative controls in this case, which may not be what the user wants, but hey, we warned them!
        }

    }

    public void OnMove(InputAction.CallbackContext context)
    {
        movementInputDir = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookingInputDir = context.ReadValue<Vector2>();
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
                jump = true;
                //ball.Jump();
                break;
            case InputActionPhase.Performed:
                //ball.Jump();
                break;
            case InputActionPhase.Canceled:
                jump = false;
                break;
            default:
                break;
        }
    }


    private void Update()
    {
        // Get the axis and jump input.

        /* ORIGINALS ================================================
        //float h = CrossPlatformInputManager.GetAxis("Horizontal");
        //float v = CrossPlatformInputManager.GetAxis("Vertical");
        //jump = CrossPlatformInputManager.GetButton("Jump");
        ===========================================================*/
        //float h = Input.GetAxis("Horizontal");
        //float v = Input.GetAxis("Vertical");
        //jump = Input.GetButton("Jump");

        // calculate move direction
        if (orientationObject != null)
        {
            // calculate camera relative direction to move:
            camForward = Vector3.Scale(orientationObject.forward, new Vector3(1, 0, 1)).normalized;
            camRight = orientationObject.right;
            // Il vettore scale deve essere ricavato dalla normale!
            // Sono i due vettori sul piano!

            //movementVector = (v * camForward + h * orientationObject.right).normalized;
            movementVector = (movementInputDir.y * camForward + movementInputDir.x * camRight).normalized;
        }
        else
        {
            // we use world-relative directions in the case of no main camera
            //movementVector = (v * Vector3.forward + h * Vector3.right).normalized;
            //movementVector = (movementInputDir.y * Vector3.forward + movementInputDir.x * Vector3.right).normalized;
        }
    }


    private void FixedUpdate()
    {
        // Call the Move function of the ball controller
        ball.Move(movementVector);
        //jump = false;
    }
}
