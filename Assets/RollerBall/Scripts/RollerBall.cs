using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum PlayerState { Grounded, Airborne }

public class RollerBall : MonoBehaviour
{
    [Header ("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private GameObject model;

    private MovementData activeMovementData;
    private float acceleration, maxSpeed, deceleration, turningFactor;

    public PlayerState state { get; private set; }
    private float sphereRadius;
    private Nullable<Vector3> groundNormal;

    [Header("Movement")]
    public MovementData groundMovement;
    public MovementData airMovement;
    [Header("Slopes")]
    [Tooltip("The minimum slope angle the player can climb."),
    SerializeField, Range(0f, 89.9f)] private float minSlopeAngle = 0f;
    [Tooltip("The maximum slope angle the player can climb."),
    SerializeField, Range(0f, 89.9f)] private float maxSlopeAngle = 45f;

    [Tooltip("Automatically handles the forces acting on the player during slopes."),
    SerializeField] private bool autoSlope = true;
    private float autoSlopeModifier;
    [Space]
    public SlopeHandler[] manualSlopes;
    [System.Serializable] public struct SlopeHandler
    {
        [Range(0f, 89.9f)] public float lowerAngle;
        [Range(0f, 89.9f)] public float higherAngle;
        public MovementData slopeMovementData;
    }

    [Header("Jumps")]
    private bool shouldDampenJump; // VEDI DI RENDERE PRIVATE
    private int jumpCount;
    // CREARE LO STRUCT!
    [System.Serializable]
    public struct JumpHandler
    {
        [Min (0.1f)]        public float jumpHeight;
        [Tooltip("The factor by which gravity is dampened during the upward phase of the jump."),
        Range (0.1f, 1f)]   public float gravityDampOnRise;
        [Tooltip("The factor by which the player's upward velocity is cut off when the jump button is released."),
        Min (1f)]           public float jumpCutOffFactor;        
    }
    public JumpHandler[] jumpSettings = new JumpHandler[]
    {
        new JumpHandler {   jumpHeight = 2.5f,
                            gravityDampOnRise = 1f,
                            jumpCutOffFactor = 3f
                        }
    };
    public float fallSpeedCap = 12f;

    [Header("Dash")]
    [SerializeField, Min(0.1f)] private float dashSpeed;
    [SerializeField, Min(0.1f)] private float dashDuration;
    [Tooltip("On dash start, freeze the player for this number of frames"),
    SerializeField, Min(0)] private int dashFreezeFrames;
    //Camera Shake

    private int dashCount;
    private float dashTimeStamp;
    [Tooltip("The maximum number of dashes the player can chain together."),
    SerializeField] private int maxDashChain;
    [Tooltip("The cooldown between chained dashes."),
    SerializeField] private float chainedDashCooldown;
    [Tooltip("The cooldown between a completed dash chain and a new one."),
    SerializeField] private float dashCooldown;

    [Header("Debug")]
    public TextMeshProUGUI debugPanel;
    bool turningMod;

    void OnValidate()
    {
        minSlopeAngle = Mathf.Min(minSlopeAngle, maxSlopeAngle);

        for (int i = 0; i < manualSlopes.Length; i++)
        {
            var slope = manualSlopes[i];
            var lowerAngle = slope.lowerAngle;
            var higherAngle = slope.higherAngle;            
            if (i == 0)
            {
                higherAngle = Mathf.Min(higherAngle, maxSlopeAngle);
                lowerAngle = Mathf.Min(lowerAngle, higherAngle);
                lowerAngle = Mathf.Max(minSlopeAngle, lowerAngle);
            }
            else
            {
                higherAngle = Mathf.Min(higherAngle, maxSlopeAngle);
                var previousSlope = manualSlopes[Mathf.Max(0, i - 1)];
                lowerAngle = Mathf.Max(previousSlope.higherAngle, lowerAngle);
                higherAngle = Mathf.Max(lowerAngle, higherAngle);
            }

            manualSlopes[i].lowerAngle = lowerAngle;
            manualSlopes[i].higherAngle = higherAngle;
        }
    }


    private void Start()
    {
        rb.drag = rb.angularDrag = 0f;
        sphereRadius = GetComponent<SphereCollider>().radius;
        groundNormal = Vector3.up;
        SetMovementData(groundMovement);
        dashTimeStamp = -dashCooldown;
    }

    private void Update()
    {
        debugPanel.text =   state + 
                            "<br> " + activeMovementData + 
                            "<br>Speed: " + /*Vector3.ProjectOnPlane(*/rb.velocity/*, GetMovementPlaneNormal())*/.magnitude +
                            "<br>Turning? " + turningMod;
    }

    #region STATE MACHINE
    public void SwitchState(PlayerState newState)
    {
        OnExitState(state);
        state = newState;
        OnEnterState(newState);
    }

    void OnEnterState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Grounded:
                EvaluateGroundedMovementData((Vector3)groundNormal);
                dashCount = 0;
                break;
            case PlayerState.Airborne:
                SetMovementData(airMovement);
                shouldDampenJump = false;
                break;
            default:
                break;
        }
    }

    void OnExitState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Grounded:
                break;
            case PlayerState.Airborne:
                shouldDampenJump = false;
                jumpCount = 0;
                break;
            default:
                break;
        }
    }
    #endregion
    #region COLLISIONS

    private void OnCollisionExit(Collision collision)
    {
        if (collision.contactCount == 0)
        {
            SwitchState(PlayerState.Airborne);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        switch (state)
        {
            case PlayerState.Grounded:
                if (collision.contactCount > 1)
                {
                    groundNormal = GetGroundNormal(collision.contacts);
                    EvaluateGroundedMovementData((Vector3)groundNormal);
                }             
                break;
            case PlayerState.Airborne:
                // Ci pensa OnCollisionStay
                break;
            default:
                break;
        }
    }

    void OnCollisionStay(Collision collision)
    {
        switch (state)
        {
            case PlayerState.Grounded:
                // Ci pensa OnCollisionEnter
                break;
            case PlayerState.Airborne:
                groundNormal = TryGetGroundNormal(collision.contacts);
                if (groundNormal != null) SwitchState(PlayerState.Grounded);
                break;
            default:
                break;
        }
    }

    Vector3 GetGroundNormal(ContactPoint[] contacts)
    {
        float minAngle = maxSlopeAngle;
        Vector3 groundNormal = Vector3.up;

        foreach (var contact in contacts)
        {
            var negativeNormal = -contact.normal;
            float angle = Vector3.Angle(Physics.gravity, negativeNormal);

            if (angle < minAngle)
            {
                minAngle = angle;
                groundNormal = contact.normal;
            }
        }
        return groundNormal;
    }

    Vector3? TryGetGroundNormal(ContactPoint[] contacts)
    {
        float minAngle = maxSlopeAngle;
        Vector3? groundNormal = null;

        foreach (var contact in contacts)
        {
            var negativeNormal = -contact.normal;
            float angle = Vector3.Angle(Physics.gravity, negativeNormal);

            if (angle < minAngle)
            {
                minAngle = angle;
                groundNormal = contact.normal;
            }
        }
        return groundNormal;
    }
    #endregion
    #region GROUNDED & AIRBORN MOVEMENT
    public void GroundedMove(Vector3 desiredDirection)
    {
        var movementPlaneNormal = (Vector3)groundNormal;

        var normalPlaneVelocity = Vector3.ProjectOnPlane(rb.velocity, movementPlaneNormal);
        var normalAlignedVelocity = Vector3.Project(rb.velocity, movementPlaneNormal);

        if (autoSlope)
        {
            autoSlopeModifier = SetAutoSlopeModifier(movementPlaneNormal);
        }
        else
        {
            autoSlopeModifier = 1f;
            EvaluateGroundedMovementData(movementPlaneNormal);
        }

        // Set sulla base della Movement Data attiva
        var maxSpeed = activeMovementData.maxSpeed;
        var acceleration = activeMovementData.acceleration;
        var deceleration = activeMovementData.deceleration;
        var turningFactor = activeMovementData.turningFactor;

        if (desiredDirection * autoSlopeModifier != Vector3.zero) // Slope modifier serve a rendere 0 questa funzione nel caso in cui fossimo oltre maxSlope
        {
            // Annullamento di qualsiasi forza\accelerazione che possa compromettere il movimento sul piano inclinato.
            var gravityProjectionOnNormalPlane = Vector3.ProjectOnPlane(Physics.gravity, movementPlaneNormal);
            rb.AddForce(-gravityProjectionOnNormalPlane, ForceMode.Acceleration);

            // Calcolo del modificatore di virata
            float turningModifier = Vector3.Dot(desiredDirection, normalPlaneVelocity) >= 0f ? 1f : turningFactor;
            /*DEBUG*/ turningMod = turningModifier > 1f ? true : false;

            // Applico i modificatori
            maxSpeed *= autoSlopeModifier;
            acceleration *= autoSlopeModifier * turningModifier;            

            if (normalPlaneVelocity.magnitude <= maxSpeed) // Se deve raggiungere Max Speed, accelera
            {
                normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, desiredDirection * maxSpeed, acceleration * Time.fixedDeltaTime);
            }
            else // Se ha superato Max Speed, decellera. Ma questa non è meglio che avvenga solo sugli slope?
            {
                normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, desiredDirection * maxSpeed, deceleration * Time.fixedDeltaTime);
            }
        }
        else
        {
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }

        //CapToTermninalFallVelocity(ref normalAlignedVelocity);

        ModelRotation(normalPlaneVelocity);
        
        rb.velocity = normalPlaneVelocity + normalAlignedVelocity;
        Debug.DrawRay(transform.position, rb.velocity, Color.blue);
    }

    public void AirborneMove(Vector3 desiredDirection)
    {
        var movementNormal = -Physics.gravity.normalized;

        var normalPlaneVelocity = Vector3.ProjectOnPlane(rb.velocity, movementNormal);
        var normalAlignedVelocity = Vector3.Project(rb.velocity, movementNormal);

        if (jumpCount > 0 && Vector3.Dot(normalAlignedVelocity, movementNormal) > 0)
        {
            var ascendingGravityMultipier = jumpSettings[jumpCount - 1].gravityDampOnRise;
            rb.AddForce(-Physics.gravity * (1f - ascendingGravityMultipier), ForceMode.Acceleration);
        }

        // Set sulla base della Movement Data attiva
        var maxSpeed = activeMovementData.maxSpeed;
        var acceleration = activeMovementData.acceleration;
        var deceleration = activeMovementData.deceleration;
        var turningFactor = activeMovementData.turningFactor;

        if (desiredDirection != Vector3.zero) // Slope modifier serve a rendere 0 questa funzione nel caso in cui fossimo oltre maxSlope
        {

            // Calcolo del modificatore di virata
            float turningModifier = Vector3.Dot(desiredDirection, normalPlaneVelocity) >= 0f ? 1f : turningFactor;
            /*DEBUG*/ turningMod = turningModifier > 1f ? true : false;

            acceleration *= turningModifier;

            if (normalPlaneVelocity.magnitude <= maxSpeed) // Se deve raggiungere Max Speed, accelera
            {
                normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, desiredDirection * maxSpeed, acceleration * Time.fixedDeltaTime);
            }
            else // Se ha superato Max Speed, decellera. Ma questa non è meglio che avvenga solo sugli slope?
            {
                normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, desiredDirection * maxSpeed, deceleration * Time.fixedDeltaTime);
            }
        }
        else
        {
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }

        CapToTermninalFallVelocity(ref normalAlignedVelocity);

        ModelRotation(normalPlaneVelocity);

        rb.velocity = normalPlaneVelocity + normalAlignedVelocity;

        if (shouldDampenJump) EvaluateJumpCutOff(normalAlignedVelocity);
    }

    void SetMovementData(MovementData newMovementData)
    {
        activeMovementData = newMovementData;

        //acceleration = currentMovementData.acceleration;
        //maxSpeed = currentMovementData.maxSpeed;
        //deceleration = currentMovementData.deceleration;
        //turningFactor = currentMovementData.turningFactor;
    }
    #endregion
    #region EVALUATE SLOPE
    void EvaluateGroundedMovementData(Vector3 normal)
    {
        var angle = Vector3.Angle(Physics.gravity, -normal);

        if (angle < minSlopeAngle)
        {
            SetMovementData(groundMovement);
        }
        else
        {
            for (int i = 0; i < manualSlopes.Length; i++)
            {
                var slope = manualSlopes[i];

                if (angle < slope.higherAngle && angle >= slope.lowerAngle)
                {
                    SetMovementData(slope.slopeMovementData);
                }
            }
        }      
    }

    float SetAutoSlopeModifier(Vector3 normal)
    {
        var angle = Vector3.Angle(Physics.gravity, -normal);
        var value = Mathf.Clamp01((angle - minSlopeAngle) / (maxSlopeAngle - minSlopeAngle));
        return 1f - value;
    }

    float SetAutoSlopeModifier(float angle)
    {
        var value = Mathf.Clamp01((angle - minSlopeAngle) / (maxSlopeAngle - minSlopeAngle));
        return 1f - value;
    }
    #endregion
    #region JUMP & FALL

    public void EvaluateJump()
    {
        jumpCount++;

        if (jumpCount <= jumpSettings.Length)
        {
            shouldDampenJump = false;
            var gravity = Physics.gravity;
            var jumpHeight = jumpSettings[jumpCount - 1].jumpHeight;
            var gravityDampOnRise = jumpSettings[jumpCount - 1].gravityDampOnRise;

            var jumpNormal = groundNormal != null ? (Vector3)groundNormal : -gravity.normalized;

            var jumpVector = Vector3.Project(gravity, jumpNormal) * gravityDampOnRise;
            var jumpVelocity = Mathf.Sqrt(2 *  jumpHeight * jumpVector.magnitude);

            var velocity = rb.velocity;
            var angle = Vector3.Angle(gravity, -jumpNormal);
            var normalPlaneVelocity = angle >= minSlopeAngle ? Vector3.ProjectOnPlane(velocity, jumpNormal) : Vector3.zero;

            rb.velocity += jumpVelocity * jumpNormal - normalPlaneVelocity;
        }        
    }

    public void ApplyJumpCutOff()
    {
        shouldDampenJump = true;
    }

    public void EvaluateJumpCutOff(Vector3 normalAlignedVelocity)
    {
        var gravityVector = Physics.gravity;
        var upwardVelocity = Vector3.Dot(rb.velocity, gravityVector);
        if (upwardVelocity < 0f)
        {
            var cutoffCount = Math.Clamp(jumpCount, 1, jumpSettings.Length);
            var jumpCutOffFactor = jumpSettings[cutoffCount - 1].jumpCutOffFactor;
            var cutOffAcceleration = gravityVector * jumpCutOffFactor - gravityVector;

            cutOffAcceleration = Vector3.ClampMagnitude(cutOffAcceleration, normalAlignedVelocity.magnitude / Time.fixedDeltaTime);
            rb.AddForce(cutOffAcceleration, ForceMode.Acceleration);
        }
        else
        {
            shouldDampenJump = false;
        }
    }

    void CapToTermninalFallVelocity(ref Vector3 velocity)
    {
        float gravityAlignedSpeed = Vector3.Dot(velocity, Physics.gravity.normalized);

        // Se la velocità supera il limite
        if (gravityAlignedSpeed > fallSpeedCap)
        {
            // Calcola la velocità eccessiva
            float excessSpeed = gravityAlignedSpeed - fallSpeedCap;

            // Rimuovi la velocità eccessiva dalla velocità del Rigidbody
            velocity -= Physics.gravity.normalized * excessSpeed;
        }
    }

    #endregion
    #region DASH
    public IEnumerator DashCoroutine(Vector3 movementDirection)
    {
        var cooldown = dashCount < maxDashChain ? chainedDashCooldown : dashCooldown;

        if (Time.time - dashTimeStamp < dashDuration + dashFreezeFrames * Time.fixedDeltaTime + cooldown) yield break;

        dashCount %= maxDashChain;
        dashCount++;

        dashTimeStamp = Time.time;
        
        rb.useGravity = false;

        var activeDashFrames = Mathf.RoundToInt(dashDuration / Time.fixedDeltaTime);

        for (int i = 0; i < dashFreezeFrames; i++)
        {
            rb.velocity = Vector3.zero;
            yield return new WaitForFixedUpdate();
        }

        for (int i = 0; i < activeDashFrames ; i++)
        {
            rb.velocity = movementDirection * dashSpeed;
            yield return new WaitForFixedUpdate();
        }

        rb.useGravity = true;
    }
    #endregion

    public void ModelRotation(Vector3 velocity)
    {
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, velocity);

        float angularVelocity = velocity.magnitude / sphereRadius * Mathf.Rad2Deg;
        float angle = angularVelocity * Time.fixedDeltaTime;

        model.transform.Rotate(rotationAxis, angle, Space.World);
    }
}

