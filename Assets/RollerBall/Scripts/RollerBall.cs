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

    private float acceleration, maxSpeed, deceleration, turningFactor;
    public MovementData activeMovementData { get; private set; }
    [Header("Movement")]
    public MovementData groundMovement;
    public MovementData airMovement;
    [Header("Slopes")]
    [Range (0f, 89.9f)] public float minSlopeAngle = 0f;
    [Range (0f, 89.9f)] public float maxSlopeAngle = 45f;
    
    public bool autoSlope = true;
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
    //Fare array per Numero di salti?
    /* PARAMETRI NECESSARI!
     * 1. Altezza Salto
     * 2. Modificatori: AirBrakeToZero, CannotAccelerateForward... Vedi, perché ci sarà da smanettare
     * 3. Ascending multiplier!
     * 4. Cutoff. Da clampare, eh!
     * 
     * 
     */

    [HideInInspector] public bool shouldDampenJump;
    // CREARE LO STRUCT!
    public float jumpHeight = 2f;
    public float jumpCutOffFactor = 2f;
    public float fallSpeedCap = 12f;
    //[Header("Dash")]
    //[Header("Juice")]

    public PlayerState state { get; private set; }
    private float sphereRadius;
    private Nullable<Vector3> groundNormal;

    Vector3 pos;

    public TextMeshProUGUI debugPanel;
    bool turningMod;
    /* ET AL.
     * PER JUMP
     * > ascending multiplier
     * > jump cutoff (intensità o time?)
     * > doubleJump?
     * Dash?
     * 
     * Juice?
     */


    /* TO DO LIST
     *      
     * 2. Idee di polish
     *      Il turning factor lo utilizziamo per le "sgommate"
     *      Proviamo la tecnica che rende figlio di un game object per lo squash and stretch
     *      
     *  SALTO    
     * 3. Ignore Forward input per il salto, in modo che venga percepito solo il momento frenante
     * 4. Can't turn, per evitare che si possa tornare indietro, ma solo fermarsi
     * 5. Lista di salti, per fare doppi, tripli, quadrupli salti! Quanti se ne vuole!
     * 
     * 6. Cercare 3D platform asset di vario tipo: ostacoli, VFX, se puoi SFX
     * 7. Ricorda che devi ripassarti di brutto probuilder!
     * 
     * 8. VA RIVISTO IL SALTO SUGLI SLOPE! SENNO' SI BARA SUGLI SLOPE!!!
     * 
     * 9. Creare per le piattaforme un parametro che immagazzina gli spostamenti, in modo da poter all'occorrenza fornire una velocity aggiuntiva alla sfera, se si ci stacca.
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
        debugPanel.text =   state + 
                            "<br> " + activeMovementData + 
                            "<br>Speed: " + Vector3.ProjectOnPlane(rb.velocity, GetMovementPlaneNormal()).magnitude +
                            "<br>Turning? " + turningMod;
    }

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
                break;
            default:
                break;
        }
    }

    public void GroundedMove(Vector3 desiredDirection)
    {
        // Ricavo la normale del piano. Se Airborne, ritorna la normale del piano opposta alla forza di gravità.
        var movementPlaneNormal = GetMovementPlaneNormal(); // In teoria... C'è già! è ground

        // Scompongo la velocità tra componente parallela e perpendicolare al piano. Il movimento verrà applicato solo alla componente parallela.
        var normalPlaneVelocity = Vector3.ProjectOnPlane(rb.velocity, movementPlaneNormal);
        var normalAlignedVelocity = Vector3.Project(rb.velocity, movementPlaneNormal);

        // Caloclo del modificatore di slope.
        /* Il modificatore viene applicato principalmente da autoSlope.
         * In caso di slope manuale, sotto maxSlopeAngle il modifier è sempre 1 -- vengono aggiornate le data. Che, a questo punto... VANNO AGGIORNATE DOPO!
         */

        if (autoSlope)
        {
            autoSlopeModifier = SetAutoSlopeModifier(movementPlaneNormal);
            Debug.Log(autoSlopeModifier);
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
        // Scompongo la velocità tra componente parallela e perpendicolare al piano. Il movimento verrà applicato solo alla componente parallela.
        var normalPlaneVelocity = Vector3.ProjectOnPlane(rb.velocity, movementNormal);
        var normalAlignedVelocity = Vector3.Project(rb.velocity, movementNormal);

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

    void EvaluateGroundedMovementData(Vector3 normal)
    {
        var angle = Vector3.Angle(Physics.gravity, -normal);

        if (autoSlope)
        {
            autoSlopeModifier = SetAutoSlopeModifier(angle);
            SetMovementData(groundMovement);
        }
        else
        {
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

    Vector3 GetMovementPlaneNormal() // La teniamo?
    {
        RaycastHit hitInfo;
        if (Physics.Raycast(transform.position, Physics.gravity, out hitInfo, 1f + sphereRadius / Mathf.Cos(maxSlopeAngle)))
        {
            return hitInfo.normal;
        }
        return -Physics.gravity.normalized; // Se Airborne, ritorna la normale del piano
    }

    public void ApplyJump()
    {
        // If COUNT
        
        SwitchState(PlayerState.Airborne);
        var jumpVector = Vector3.Project(Physics.gravity, GetMovementPlaneNormal());
        var velocity = Mathf.Sqrt(2 * jumpHeight * jumpVector.magnitude); // Salto in direzione della normale? Sarebbe più corretto. OK, ma va rivisto!
        rb.velocity += velocity * -Physics.gravity.normalized;
    }

    public void ApplyJumpCutoff()
    {
        var gravityVector = Physics.gravity;
        var upwardVelocity = Vector3.Dot(rb.velocity, gravityVector);
        if (upwardVelocity < 0f)
        {
            var cutOffAcceleration = gravityVector * jumpCutOffFactor - gravityVector;
            var projectedVelocity = Vector3.Project(rb.velocity, gravityVector);

            cutOffAcceleration = Vector3.ClampMagnitude(cutOffAcceleration, projectedVelocity.magnitude / Time.fixedDeltaTime);
            rb.AddForce(cutOffAcceleration, ForceMode.Acceleration);
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

