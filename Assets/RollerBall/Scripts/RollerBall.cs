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
    //[SerializeField] private RollerBallUserControl controller;

    private float acceleration, maxSpeed, deceleration, turningFactor;
    private MovementData activeMovementData;
    [Header("Movement")]
    public MovementData groundMovement;
    public MovementData airMovement;
    [Header("Slopes")]
    [Range (0f, 90f)]
    public float minSlopeAngle = 0f;
    [Range (0f, 90f)]
    public float maxSlopeAngle = 45f;
    
    public bool autoSlope = true;
    [Space]
    public SlopeHandler[] manualSlopes;
    [System.Serializable] public struct SlopeHandler
    {
        [Range(0f, 90f)]
        public float lowerAngle;
        [Range(0f, 90f)]
        public float higherAngle;
        public MovementData slopeMovementData;
    }

    [Header("Jumps")]
    //Fare array per Numero di salti?
    private bool canJump;
    public float jumpHeight = 2f;
    public float jumpVelocity = 2f;
    public float jumpCutoff = 1f;
    public float fallSpeedCap = 12f;
    //[Header("Dash")]
    //[Header("Juice")]

    public PlayerState state { get; private set; }
    private float sphereRadius;

    Vector3 pos;
    public GameObject model;

    public TextMeshProUGUI debugPanel;
    /* ET AL.
     * PER JUMP
     * > ascending multiplier
     * > jump cutoff (intensità o time?)
     * > doubleJump?
     * Dash?
     * 
     * Juice?
     */

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
        //rb = GetComponent<Rigidbody>();
        //controller = GetComponentInChildren<RollerBallUserControl>();
        sphereRadius = GetComponent<SphereCollider>().radius;
        SetMovementData(groundMovement);        
    }

    private void Update()
    {
        debugPanel.text =   activeMovementData + 
                            "<br>Speed: " + rb.velocity.magnitude;
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.contactCount == 0)
        {
            //SwitchState(PlayerState.Airborne);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Se la normale del contatto combacia è tra l'angolo della gravità e gli angoli di slope,
        // allora...
        foreach (var contact in collision.contacts)
        {
            if (Vector3.Angle(Physics.gravity, -contact.normal) < maxSlopeAngle)
            {
                //SwitchState(PlayerState.Grounded);
                //EvaluateSlope!
                SlopeModifier(contact.normal);
                break;
            }
        }
    }

    void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            // Aggiungi la normale della collisione alla lista
            //collisionNormals.Add(contact.normal);
        }
        //SwitchState(PlayerState.Grounded);
    }

    public void Move(Vector3 desiredDirection)
    {
        var maxSpeed = activeMovementData.maxSpeed;
        var acceleration = activeMovementData.acceleration;
        var deceleration = activeMovementData.deceleration;
        var turningFactor = activeMovementData.turningFactor;

        var movementNormal = GetMovementPlaneNormal(); // Ritorna un vettore unitario opposto a gravità, se Airborne.

        var normalPlaneVelocity = Vector3.ProjectOnPlane(rb.velocity, movementNormal);
        var normalAlignedVelocity = Vector3.Project(rb.velocity, movementNormal);
        var slopeModifier = SlopeModifier(movementNormal);

        if (desiredDirection * slopeModifier != Vector3.zero)
        {
            var gravityProjection = Vector3.ProjectOnPlane(Physics.gravity, movementNormal);
            rb.AddForce(-gravityProjection, ForceMode.Acceleration);            

            float turningModifier = Vector3.Dot(desiredDirection, normalPlaneVelocity) >= 0f ? 1f : turningFactor;

            /* Ennesimo problema con gli slope!
             * La velocità deve aumentare di acceleration se è < di max speed, ma diminuire di deceleration se è > di maxSpeed!
             */

            if (normalPlaneVelocity.magnitude < maxSpeed * slopeModifier)
            {
                normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, desiredDirection * maxSpeed * slopeModifier, acceleration * turningModifier * slopeModifier * Time.fixedDeltaTime);
            }
            else
            {
                normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, desiredDirection * maxSpeed * slopeModifier, deceleration * Time.fixedDeltaTime);
            }
        }
        else
        {
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }

        // Calcola la componente della velocità lungo la direzione della gravità
        float gravityAlignedSpeed = Vector3.Dot(normalAlignedVelocity, Physics.gravity.normalized);

        // Se la velocità supera il limite
        if (gravityAlignedSpeed > fallSpeedCap)
        {
            // Calcola la velocità eccessiva
            float excessSpeed = gravityAlignedSpeed - fallSpeedCap;

            // Rimuovi la velocità eccessiva dalla velocità del Rigidbody
            normalAlignedVelocity -= Physics.gravity.normalized * excessSpeed;
        }

        ModelRotation(normalPlaneVelocity);
        
        rb.velocity = normalPlaneVelocity + normalAlignedVelocity;
        Debug.DrawRay(transform.position, rb.velocity, Color.blue);
    }

    float SlopeModifier(Vector3 normal)
    {
        if (state == PlayerState.Airborne) return 1;
        
        var angle = Vector3.Angle(Physics.gravity, -normal);
        
        if (autoSlope)
        {
            var interpolationFactor = Mathf.Clamp01((angle - minSlopeAngle) / (maxSlopeAngle - minSlopeAngle));
            return (1f - interpolationFactor);
        }
        else
        {
            if (angle < minSlopeAngle)
            {
                SetMovementData(groundMovement);
                return 1;
            }
            
            for (int i = 0; i < manualSlopes.Length; i++)
            {
                var slope = manualSlopes[i];

                if (angle < slope.higherAngle && angle >= slope.lowerAngle)
                {
                    SetMovementData(slope.slopeMovementData);
                    return 1;
                }
            }
            return 0;
        }
    }

    Vector3 GetMovementPlaneNormal()
    {
        RaycastHit hitInfo;
        if (Physics.Raycast(transform.position, Physics.gravity, out hitInfo, sphereRadius * 2f))
        {
            return hitInfo.normal;
        }
        return -Physics.gravity.normalized;
    }

    public void Jump()
    {
        // ... add force in upwards.
        //rb.AddForce(Vector3.up * m_JumpPower, ForceMode.Impulse);

        //rb.AddForce(Vector3.up * m_JumpPower, ForceMode.Acceleration);

        //SwitchState(PlayerState.Airborne);

        Debug.Log("Ma quante cristo di volte vieni chiamato?");

        var velocity = Mathf.Sqrt(2 * jumpHeight * Physics.gravity.magnitude); // proiezione della gravity sulla normale? questo renderebbe impossibili i wall jump

        rb.velocity += velocity * -Physics.gravity.normalized; //farlo lungo la normale?
    }

    public void JumpCutoff()
    {
        if (Vector3.Dot(rb.velocity, Physics.gravity) < 0f)
        {
            rb.AddForce(Physics.gravity * jumpCutoff * rb.mass, ForceMode.Force);
        }
    }

    public void ModelRotation(Vector3 velocity)
    {
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, velocity);

        float angularVelocity = velocity.magnitude / sphereRadius * Mathf.Rad2Deg;
        float angle = angularVelocity * Time.fixedDeltaTime;

        model.transform.Rotate(rotationAxis, angle, Space.World);
    }

    void SetMovementData(MovementData newMovementData)
    {
        activeMovementData = newMovementData;

        //acceleration = currentMovementData.acceleration;
        //maxSpeed = currentMovementData.maxSpeed;
        //deceleration = currentMovementData.deceleration;
        //turningFactor = currentMovementData.turningFactor;
    }   
}

