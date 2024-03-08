using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using CustomInspector;

public enum PlayerState { Grounded, Airborne } // Deve essere accessibile anche al character controller!

public class RollerBall : MonoBehaviour
{
    [CustomInspector.HorizontalLine("References", 5, FixedColor.Gray)]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private GameObject model;

    private MovementData activeMovementData;
    private float acceleration, maxSpeed, deceleration, turningFactor;
    private Vector3 spinVelocity;

    public PlayerState state { get; private set; }
    public enum GroundTilt { Flat, Slope, Steep }
    private GroundTilt groundType;
    private float stateTimeStamp;
    private float sphereRadius;
    private Vector3 groundNormal, previousGroundNormal;

    [CustomInspector.HorizontalLine("Movement", 5, FixedColor.Gray)]
    [SerializeField] private MovementData groundMovement;
    // On STEEP!
    [Tooltip("When colliding with a steep wall, always applies a minimal impulse that changes momentum."),
    SerializeField] private bool shiftMomentumOnCollision;
    [SerializeField] private float dynamicFriction;
    [Space]
    [SerializeField] private MovementData airMovement;
    public enum AerialControlType { Total, SpeedCapped }
    [SerializeField] private AerialControlType aerialControlType = AerialControlType.Total;
    [SerializeField] bool canChangeDirectionOnAir = true;
    private float takeoffSpeed;
    [Space]
    [SerializeField] private UnityEvent OnBurnout;


    [CustomInspector.HorizontalLine("Slopes", 5, FixedColor.Gray)]
    [Tooltip("The minimum slope angle (value excluded) the player can climb."),
    SerializeField, Range(0f, 89.9f)] private float minSlopeAngle = 0f;
    [Tooltip("The maximum slope angle (value included) the player can climb."),
    SerializeField, Range(0f, 89.9f)] private float maxSlopeAngle = 45f;

    [Tooltip("Automatically handles the forces acting on the player during slopes."),
    SerializeField] private bool autoSlope = true;
    private float autoSlopeModifier;
    [Tooltip("Manually handles slopes using Movement Data at setted angle range."),
    SerializeField] private SlopeHandler[] manualSlopes;
    [System.Serializable] public struct SlopeHandler
    {
        [Range(0f, 89.9f)] public float lowerAngle;
        [Range(0f, 89.9f)] public float higherAngle;
        public MovementData slopeMovementData;
    }

    [CustomInspector.HorizontalLine("Jumps & Falls", 5, FixedColor.Gray)]
    [SerializeField] private float fallSpeedCap = 12f;
    [SerializeField] private float maxFallTime = 7f;
    [SerializeField, Min(0)] private float coyoteTime =.1f;
    [SerializeField, Min(0)] private float inputBuffer = .1f;
    [SerializeField] private JumpHandler[] jumpController = new JumpHandler[] //e se facessimo con scriptable anche questi?? Sono solo contenitori di dati, dopotutto.
    {
        new JumpHandler {   jumpHeight = 2.5f,
                            gravityDampOnRise = 1f,
                            jumpCutOffFactor = 3f
                        }
    };
    [System.Serializable]
    public struct JumpHandler
    {
        [Min (0.1f)]        public float jumpHeight;
        [Tooltip("The factor by which gravity is dampened during the upward phase of the jump."),
        Range (0.1f, 1f)]   public float gravityDampOnRise;
        [Tooltip("The factor by which the player's upward velocity is cut off when the jump button is released."),
        Min (1f)]           public float jumpCutOffFactor;
                            public AnimationCurve jumpAnimationCurve;
        //Qui ci mettiamo anche un'animation curve!
        //Mettere un override per la movementData? Naaah!
    }
    private bool shouldJump;
    //private bool isJumping;
    private bool shouldDampenJump; // VEDI DI RENDERE PRIVATE
    private Vector3 cutOffAcceleration;
    private int jumpCount;
    private float timeToApexJump;

    [CustomInspector.HorizontalLine("Dash", 5, FixedColor.Gray)]
    [SerializeField, Min(0.1f)] private float dashSpeed;
    [SerializeField, Min(0.1f)] private float dashDuration;
    [Tooltip("On dash start, freeze the player for this number of frames"),
    SerializeField, Min(0)] private int dashFreezeFrames;
    [SerializeField] private AnimationCurve dashAnimationCurve;

    [SerializeField] private DashHandler[] dashDatas;
    [System.Serializable]
    public struct DashHandler
    {
        [SerializeField] private DashData dashDatas;
        [SerializeField] private UnityEvent OnDash;
    }

    private int dashCount;
    private float dashTimeStamp;
    [Tooltip("The maximum number of dashes the player can chain together."),
    SerializeField] private int maxDashChain;
    [Tooltip("The cooldown between chained dashes."),
    SerializeField] private float chainedDashCooldown;
    [SerializeField] private bool rechargeDashSeriesOnGround;
    [Tooltip("The cooldown between a completed dash chain and a new one."),
    CustomInspector.ShowIfNot(nameof(rechargeDashSeriesOnGround)),
    SerializeField] private float dashSeriesCooldown; 
    [Tooltip("When true, the dash will be performed in the direction of the movement input rather than the direction of motion."),
    SerializeField] private bool dashTowardsInputDirection;
    [SerializeField] private UnityEvent OnDash;

    /* COME RIORGANIZZEREI IL DASH???
     * Dash come una serie di scriptable.
     * Per ogni dash...
     * Freeze - Speed - Cooldown (Domanda: il cooldown c'è o c'è solo la recharge?)
     * Per l'intera serie...
     * Recharge time e Se recharge è solo a terra.
     */

    [CustomInspector.HorizontalLine("Debug", 3, FixedColor.Red)]
    public TextMeshProUGUI debugPanel;
    bool isDebug;
    public Transform squashAndStretchController;


    Vector3 contactPoint;
    public ParticleSystem particles;

    IEnumerator activeSquashAndStretchCoroutine;

    #region SETUP
    void OnValidate()
    {
        SetupSlopes();
        SetupDash();
    }
    private void Start()
    {
        rb.drag = rb.angularDrag = 0f;
        sphereRadius = GetComponent<SphereCollider>().radius;
        SetupMovement();
        SetupCoroutine();

        var gravity = Physics.gravity;

        for (int i = 0; i < jumpController.Length; i++)
        {
            var jump = jumpController[i];

            //Debug.Log("Jump Max Speed: " + groundMovement.maxSpeed * (Mathf.Sqrt(2f * jump.jumpHeight / gravity.magnitude * jump.gravityDampOnRise) + Mathf.Sqrt(2f * jump.jumpHeight / gravity.magnitude)));
        }
    }
    private void SetupMovement()
    {
        groundNormal = Vector3.up;
        SwitchMovementData(groundMovement);
    }
    private void SetupSlopes()
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
                higherAngle = Mathf.Max(lowerAngle, higherAngle);
            }
            else
            {
                higherAngle = Mathf.Min(higherAngle, maxSlopeAngle);
                var previousSlope = manualSlopes[Mathf.Max(0, i - 1)];
                lowerAngle = previousSlope.higherAngle;
                higherAngle = Mathf.Max(lowerAngle, higherAngle);
            }

            manualSlopes[i].lowerAngle = lowerAngle;
            manualSlopes[i].higherAngle = higherAngle;
        }
    }
    private void SetupDash()
    {
        if (rechargeDashSeriesOnGround) dashSeriesCooldown = maxFallTime;
        //else dashSeriesCooldown = dashSeriesCooldown;
        dashTimeStamp = -dashSeriesCooldown;
    }
    private void SetupCoroutine()
    {
        activeSquashAndStretchCoroutine = TryDash(Vector3.forward, Vector3.forward);
    }

    private void Update() //debug
    {
        var groundNormal = this.groundNormal != Vector3.zero ? this.groundNormal : -Physics.gravity.normalized;
        debugPanel.text = state +
                            "<br> " + /*activeMovementData*/ groundType +
                            "<br>Speed: " + Vector3.ProjectOnPlane(rb.velocity, (Vector3)groundNormal).magnitude;
    }
    #endregion
    #region STATE MACHINE
    public void SwitchState(PlayerState newState)
    {
        if (state == newState) return; 
        OnExitState(state);
        state = newState;
        OnEnterState(newState);
    }

    void OnEnterState(PlayerState state)
    {
        stateTimeStamp = Time.time;
        switch (state)
        {
            case PlayerState.Grounded:                
                SetGroundedMovementData();
                jumpCount = 0;
                dashCount = 0;
                break;
            case PlayerState.Airborne:
                SwitchMovementData(airMovement);
                //shouldDampenJump = false;
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
                //isJumping = false;
                break;
            case PlayerState.Airborne:
                //shouldDampenJump = false;
                break;
            default:
                break;
        }
    }
    #endregion
    #region COLLISIONS
    private void OnCollisionEnter(Collision collision)
    {
        switch (state)
        {
            case PlayerState.Grounded:
                FindGroundType(collision.contacts);
                SetGroundedMovementData();
                if (shiftMomentumOnCollision)
                    CalculateVelocityOnCollisionEnter(collision.contacts[0].normal);
                break;
            case PlayerState.Airborne:
                // Ci pensa OnCollisionStay
                break;
            default:
                break;
        }
    }

    void CalculateVelocityOnCollisionEnter(Vector3 collisionNormal)
    {
        if (GroundNormal() == collisionNormal) return;

        var velocityProjectionOnPlane = Vector3.ProjectOnPlane(rb.velocity, collisionNormal);
        var minVelocitySqrMagnitude = 2f / 5f * spinVelocity.sqrMagnitude;

        if (velocityProjectionOnPlane.sqrMagnitude < minVelocitySqrMagnitude)
        {
            var velocityDirection = RotateVector(spinVelocity, collisionNormal).normalized;
            rb.velocity = velocityDirection * Mathf.Sqrt(minVelocitySqrMagnitude);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        previousGroundNormal = groundNormal; // Workaround bug unity, per evitare errori durante i salti a contatto con pareti vericali

        switch (state)
        {
            case PlayerState.Grounded:
                FindGroundType(collision.contacts);
                SetGroundedMovementData();
                break;
            case PlayerState.Airborne:
                FindGroundType(collision.contacts);
                if (GroundNormal() != Vector3.zero) SwitchState(PlayerState.Grounded);
                break;
            default:
                break;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.contactCount == 0)
        {
            groundNormal = Vector3.zero;
            SwitchState(PlayerState.Airborne);
        }
    }

    void FindGroundType(ContactPoint[] contacts)
    {        
        var gravity = Physics.gravity;
        //float terrainAngle = 90f; // Devo pensare anche ai casi in cui la collisione è col soffitto!
        float terrainAngle = 180f; // Devo pensare anche ai casi in cui la collisione è col soffitto!

        groundNormal = Vector3.zero;

        foreach (var contact in contacts)
        {
            float angle = Vector3.Angle(-gravity, contact.normal);

            if (angle <= terrainAngle)
            {
                terrainAngle = angle;
                groundNormal = contact.normal;
                contactPoint = contact.point;
            }
        }
    }

    Vector3 GroundNormal()
    {
        // Workaround OnCollisionEnter\Stay bug
        var gravityNormal = -Physics.gravity.normalized;
        var groundNormalDot = Vector3.Dot(groundNormal, gravityNormal);
        var previousGroundNormalDot = Vector3.Dot(previousGroundNormal, gravityNormal);

        if (groundNormalDot >= previousGroundNormalDot)
        {
            return groundNormal;
        }
        else
        {
            return previousGroundNormal;
        }
    }

    void SwitchGroundTilt(GroundTilt newGroundType)
    {
        groundType = newGroundType; // Funzioni di enter?

        switch (newGroundType)
        {
            case GroundTilt.Flat:
            case GroundTilt.Steep: // Valutare se mettere una movemente data per steep
                SwitchMovementData(groundMovement);
                break;
            case GroundTilt.Slope:
                if (autoSlope) SwitchMovementData(groundMovement);
                else EvaluateSlopeMovementData();
                break;
        }
    }

    void SetGroundedMovementData()
    {
        if (GroundNormal() != Vector3.zero) // Tecnicamente, mai zero
        {
            var terrainAngle = Vector3.Angle(GroundNormal(), -Physics.gravity);

            if (terrainAngle <= minSlopeAngle)
            {
                SwitchGroundTilt(GroundTilt.Flat);
            }
            else if (terrainAngle > minSlopeAngle && terrainAngle <= maxSlopeAngle)
            {
                SwitchGroundTilt(GroundTilt.Slope);
            }
            else if (terrainAngle > maxSlopeAngle && terrainAngle <= 90f)
            {
                SwitchGroundTilt(GroundTilt.Steep);
            }
            else // terrain > 90
            {
                groundNormal = Vector3.zero;
            }
        }
        else
        {
            //Airborne
            Debug.Log("Airborne");
        }
    }

    void EvaluateSlopeMovementData()
    {
        var angle = Vector3.Angle(Physics.gravity, -groundNormal); // Non va bene! Se ground normal è zero, angle è zero!

        for (int i = 0; i < manualSlopes.Length; i++)
        {
            var slope = manualSlopes[i];

            if (angle > slope.lowerAngle && angle <= slope.higherAngle)
            {
                SwitchMovementData(slope.slopeMovementData);
                return;
            }
        }
    }
    #endregion
    #region GROUNDED & AIRBORN MOVEMENT
    private Vector3 RotateVector(Vector3 desiredDirection, Vector3 planeNormal)
    {
        var directionNormal = -Physics.gravity.normalized;
        Vector3 rotationAxis = Vector3.Cross(directionNormal, planeNormal);
        float angle = Mathf.Acos(Vector3.Dot(directionNormal, planeNormal)) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.AngleAxis(angle, rotationAxis);

        var newDesiredDir = rotation * desiredDirection;
        //Debug.DrawRay(transform.position, newDesiredDir, Color.cyan);

        return newDesiredDir;
    }
    public void GroundedMove(Vector3 desiredDirection)
    {
        if (shouldJump) { ApplyJump(); return; }

        var movementPlaneNormal = GroundNormal() != Vector3.zero ? GroundNormal() : -Physics.gravity.normalized; // Teoricamente, non è mai zero.

        if (movementPlaneNormal != Vector3.up) desiredDirection = RotateVector(desiredDirection, movementPlaneNormal);

        var normalPlaneVelocity = Vector3.ProjectOnPlane(rb.velocity, movementPlaneNormal); // OnEnter: Ruota la normalPlaneVelocity?
        var normalAlignedVelocity = Vector3.Project(rb.velocity, movementPlaneNormal);

        if (autoSlope) autoSlopeModifier = AutoSlopeModifier(movementPlaneNormal);
        else autoSlopeModifier = 1f;

        // Set sulla base della Movement Data attiva
        maxSpeed = activeMovementData.maxSpeed;
        acceleration = activeMovementData.acceleration;
        deceleration = activeMovementData.deceleration;
        turningFactor = activeMovementData.turningFactor;

        switch (groundType)
        {
            case GroundTilt.Flat:
                UpdateVelocityOnFlatGround(ref normalPlaneVelocity, desiredDirection, movementPlaneNormal);
                CalculateSpin(desiredDirection, normalPlaneVelocity, turningFactor);
                RotatateSquashAndStretchController(movementPlaneNormal);
                break;
            case GroundTilt.Slope:
                UpdateVelocityOnSlopeGround(ref normalPlaneVelocity, desiredDirection, movementPlaneNormal);
                CalculateSpin(desiredDirection, normalPlaneVelocity, turningFactor);
                RotatateSquashAndStretchController(movementPlaneNormal);
                break;
            case GroundTilt.Steep:
                UpdateVelocityOnSteepGround(ref normalPlaneVelocity, desiredDirection, movementPlaneNormal);
                CalculateSpin(desiredDirection);
                break;
        }      

        rb.velocity = normalPlaneVelocity + normalAlignedVelocity;
        Debug.DrawRay(transform.position, rb.velocity, Color.blue);
    }
    private void UpdateVelocityOnFlatGround(ref Vector3 normalPlaneVelocity, Vector3 desiredDirection, Vector3 movementPlaneNormal)
    {
        if (desiredDirection * autoSlopeModifier != Vector3.zero) // Serve? In teoria, no.
        {
            if (Vector3.Dot(desiredDirection, normalPlaneVelocity) >= 0f) turningFactor = 1f;
            else PlayBurnoutEvent();

            maxSpeed *= autoSlopeModifier;
            acceleration *= autoSlopeModifier * turningFactor;
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, desiredDirection * maxSpeed, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }

        rb.AddForce(-1f * GravityProjectionOnPlane(movementPlaneNormal), ForceMode.Acceleration);
    }

    private void UpdateVelocityOnSlopeGround(ref Vector3 normalPlaneVelocity, Vector3 desiredDirection, Vector3 movementPlaneNormal)
    {
        var directionDotClimb = Vector3.Dot(desiredDirection, -1 * GravityProjectionOnPlane(movementPlaneNormal).normalized); // >= 0 ... IN SALITA. Else: DISCESA. Verificare il valore

        if (desiredDirection * autoSlopeModifier != Vector3.zero && directionDotClimb >= 0)
        {
            // Modificatori
            if (Vector3.Dot(desiredDirection, normalPlaneVelocity) >= 0f) turningFactor = 1f;
            else PlayBurnoutEvent();

            maxSpeed *= autoSlopeModifier;
            acceleration = Mathf.Lerp(groundMovement.acceleration, acceleration, directionDotClimb); // Se si muove perpendicolarmente alla salita, va alla maxSpeed grounded
            acceleration *= autoSlopeModifier * turningFactor;
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, desiredDirection * maxSpeed, acceleration * Time.fixedDeltaTime);
            rb.AddForce(-1f * GravityProjectionOnPlane(movementPlaneNormal), ForceMode.Acceleration);
        }
        else
        {
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
            normalPlaneVelocity = Vector3.ClampMagnitude(normalPlaneVelocity, fallSpeedCap);
        }
    }

    private void UpdateVelocityOnSteepGround(ref Vector3 normalPlaneVelocity, Vector3 desiredDirection, Vector3 movementPlaneNormal)
    {
        if (desiredDirection != Vector3.zero)
        {
            acceleration *= 1f / turningFactor; //Vedere se mettere un valore diverso o una movement data
            acceleration = Mathf.Clamp(acceleration, 0, Physics.gravity.magnitude);
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, desiredDirection * maxSpeed, acceleration * Time.fixedDeltaTime);
            PlayBurnoutEvent();
        }
        else
        {
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
            normalPlaneVelocity = Vector3.ClampMagnitude(normalPlaneVelocity, fallSpeedCap);
        }
    }

    private Vector3 GravityProjectionOnPlane(Vector3 planeNormal)
    {
        return Vector3.ProjectOnPlane(Physics.gravity, planeNormal);
    }

    void TurningAnimation(Vector3 velocity, float maxDeformation, float delta)
    {
        var scaleY = transform.localScale.y;
        scaleY = Mathf.MoveTowards(scaleY, Mathf.Lerp(1f, maxDeformation, Mathf.Clamp01(velocity.magnitude / Physics.gravity.magnitude)), delta * Time.fixedDeltaTime);
        var collider = GetComponent<SphereCollider>().radius = sphereRadius * scaleY;
        transform.localScale = new Vector3(1, scaleY, 1);

        /* ASSE: quello perpendicolare alla normale.
         * squash and stretch, e forse lo possiamo fare fuori da una coroutine
         */

        //RotatateSquashAndStretchController(velocity);

        //Camera.onPreRender += SquashAndStretchPreRender;
        //Camera.onPostRender += SquashAndStretchPostRender;
        //var forwardScale = curve.Evaluate((Time.time - timeStamp) / animationTime);
        //var planeScale = 1f + (1f - forwardScale);
        //collider.radius = sphereRadius / forwardScale;
        //if (forwardScale == float.NaN) Debug.Break();
        //squashAndStretchController.position = transform.position; // riposizionamento
        //transform.parent = squashAndStretchController; // parentela
        //squashAndStretchController.localScale = new Vector3(planeScale, planeScale, forwardScale); // scala secondo l'asse del parent
        //this.transform.parent = null;
        //Camera.onPreRender -= SquashAndStretchPreRender;
        //Camera.onPostRender -= SquashAndStretchPostRender;

    }

    public void AirborneMove(Vector3 desiredDirection)
    {
        if (shouldJump) { ApplyJump(); return; }

        var movementNormal = -Physics.gravity.normalized;

        maxSpeed = activeMovementData.maxSpeed;
        acceleration = activeMovementData.acceleration;
        deceleration = activeMovementData.deceleration;
        turningFactor = activeMovementData.turningFactor;

        var normalPlaneVelocity = Vector3.ProjectOnPlane(rb.velocity, movementNormal); // Questa è la direzione del movimento!
        var normalAlignedVelocity = Vector3.Project(rb.velocity, movementNormal);

        HandleGravityModifiersOnJump(movementNormal, normalAlignedVelocity);

        UpdateAerialVelocity(ref normalPlaneVelocity, desiredDirection); // Se volessi evitare la possibilità accelerare in aria?

        CalculateSpin(desiredDirection);

        rb.velocity = normalPlaneVelocity + normalAlignedVelocity;

        CapToTermninalFallVelocity();

        Debug.DrawRay(transform.position, rb.velocity, Color.blue);
    }

    private void UpdateAerialVelocity(ref Vector3 normalPlaneVelocity, Vector3 desiredDirection)
    {
        //if (desiredDirection != Vector3.zero)
        //{
        //    turningFactor = Vector3.Dot(desiredDirection, normalPlaneVelocity) >= 0f ? 1f : turningFactor;
        //    ///*DEBUG*/ turningMod = turningFactor > 1f ? true : false;

        //    acceleration *= turningFactor;

        //    var accelerationForce = normalPlaneVelocity.sqrMagnitude <= Mathf.Pow(maxSpeed, 2) ? acceleration : deceleration;

        //    normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, desiredDirection * maxSpeed, accelerationForce * Time.fixedDeltaTime);
        //}
        //else
        //{
        //    normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        //}

        if (desiredDirection != Vector3.zero)
        {
            turningFactor = Vector3.Dot(desiredDirection, normalPlaneVelocity) >= 0f ? 1f : turningFactor;
            acceleration *= turningFactor;
            if (aerialControlType == AerialControlType.SpeedCapped) maxSpeed = takeoffSpeed;

            var accelerationForce = normalPlaneVelocity.sqrMagnitude <= Mathf.Pow(maxSpeed, 2) ? acceleration : deceleration;

            var targetSpeed = canChangeDirectionOnAir ? desiredDirection * maxSpeed : Vector3.zero; // La differenza con l'altra funzione è MaxSpeed.

            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, targetSpeed, accelerationForce * Time.fixedDeltaTime);
            takeoffSpeed = normalPlaneVelocity.magnitude;
        }
        else
        {
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }

        
    }

    private void HandleGravityModifiersOnJump(Vector3 movementNormal, Vector3 normalAlignedVelocity)
    {
        if (Vector3.Dot(normalAlignedVelocity, movementNormal) > 0)
        {
            if (shouldDampenJump)
            {
                rb.AddForce(cutOffAcceleration, ForceMode.Acceleration); //Apply cut off
            }
            else if (jumpCount > 0)
            {
                var gravityDampOnRise = jumpController[jumpCount - 1].gravityDampOnRise;
                rb.AddForce(-Physics.gravity * (1f - gravityDampOnRise), ForceMode.Acceleration); // Ci vorrebbe un sistema di controllo per vedere a quali ascensioni applicarlo e a quali no?
            }
        }
        else
        {
            shouldDampenJump = false;
        }
    }
    void SwitchMovementData(MovementData newMovementData)
    {
        activeMovementData = newMovementData;
    } 
    #endregion
    #region SPHERE ROTATION
    void CalculateSpin(Vector3 desiredDirection, Vector3 normalPlaneVelocity, float turningFactor) // Se trasferissi qui la direction della velocità?
    {   
        // Ground
        if (Vector3.Dot(desiredDirection, normalPlaneVelocity) >= 0f)
        {
            spinVelocity = Vector3.MoveTowards(spinVelocity, normalPlaneVelocity, acceleration / sphereRadius * Time.fixedDeltaTime); // acceleration / radius!
            ModelRotation(spinVelocity);
            TurningAnimation(normalPlaneVelocity, 1f, 1f);

            //switch (groundType)
            //{
            //    case GroundType.Flat:
            //    case GroundType.Slope:
            //        spinVelocity = Vector3.MoveTowards(spinVelocity, normalPlaneVelocity, acceleration / sphereRadius * Time.fixedDeltaTime); // acceleration / radius!
            //        ModelRotation(spinVelocity);
            //        TurningAnimation(normalPlaneVelocity, 1f, 1f);
            //        break;
            //    case GroundType.Steep:
            //        spinVelocity = Vector3.MoveTowards(spinVelocity, normalPlaneVelocity, acceleration / sphereRadius * Time.fixedDeltaTime); // acceleration / radius!
            //        normalPlaneVelocity = spinVelocity.magnitude * .66f *  desiredDirection;
            //        break;
            //    default:
            //        break;
            //}
        }
        else
        {
            // Secondo me, rende meglio con l'accelerazione.
            if (Vector3.Dot(desiredDirection, spinVelocity) < 0f) spinVelocity = Vector3.zero;
            spinVelocity = Vector3.MoveTowards(spinVelocity, desiredDirection * maxSpeed * turningFactor, acceleration / sphereRadius * Time.fixedDeltaTime); // velocity: cap arbitrario o maxSpeed * turningFactor? - acceleration / radius
            ModelRotation(spinVelocity);

            if (state == PlayerState.Grounded) TurningAnimation(normalPlaneVelocity, .66f, 1f);
        }

    }

    void CalculateSpin(Vector3 desiredDirection)
    {
        // AIR!
        if (desiredDirection != Vector3.zero) // ATTENZIONE! GROUNDED MOVEMENT!
        {
            spinVelocity = Vector3.MoveTowards(spinVelocity, desiredDirection * dashSpeed, groundMovement.acceleration + Physics.gravity.magnitude * Time.fixedDeltaTime); // acceleration + gravity! Ma la maxSpeed? Cappata a caso? Tecnicamente... velocità di dash.
        }
        else
        {
            spinVelocity = Vector3.MoveTowards(spinVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime); // Air deceleration
        }
        ModelRotation(spinVelocity);

    }

    public void ModelRotation(Vector3 velocity)
    {
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, velocity);

        float angularVelocity = velocity.magnitude / sphereRadius * Mathf.Rad2Deg;
        float angle = angularVelocity * Time.fixedDeltaTime;

        model.transform.Rotate(rotationAxis, angle, Space.World);
    }
    #endregion
    #region EVENTS
    private void PlayBurnoutEvent()
    {
        if (!particles.isPlaying)
        {
            particles.transform.position = transform.position + Vector3.down * sphereRadius;
            particles.Play();
        }

        OnBurnout.Invoke();
    }
    private void PlayOnDashEvent()
    {
        OnDash.Invoke();
    }
    #endregion
    #region EVALUATE SLOPE
    float AutoSlopeModifier(Vector3 normal)
    {
        var angle = Vector3.Angle(Physics.gravity, -normal);
        //var value = Mathf.Clamp01((angle - minSlopeAngle) / (maxSlopeAngle - minSlopeAngle));
        var value = Mathf.Clamp((angle - minSlopeAngle) / (maxSlopeAngle - minSlopeAngle), 0, 1f / turningFactor );
        return 1f - value;
    }

    float SetAutoSlopeModifier(float angle)
    {
        var value = Mathf.Clamp01((angle - minSlopeAngle) / (maxSlopeAngle - minSlopeAngle));
        return 1f - value;
    }
    #endregion
    #region JUMP & FALL
    public IEnumerator TryJump()
    {
        //if (groundNormal == Vector3.zero && overslopeNormal != Vector3.zero) yield break;

        switch (state)
        {
            case PlayerState.Grounded:
                if (jumpCount >= jumpController.Length) yield break; // può verificarsi? In teoria, no.
                shouldJump = true;
                shouldDampenJump = false;
                Debug.Log("TryJump: grounded jump");
                break;
            case PlayerState.Airborne:
                if (jumpCount == 0 && Time.time - stateTimeStamp <= coyoteTime) 
                {
                    shouldJump = true; 
                    shouldDampenJump = false;
                    //groundNormal = -Physics.gravity.normalized;
                    Debug.Log("TryJump: coyote jump! GN: " + groundNormal + "; PGN: " + previousGroundNormal); // ma perché anche previous è zero? Chi lo azzera? quando? Beh, semplice: la funzione exit di grounded!
                }
                else if (jumpCount > 0 && jumpCount < jumpController.Length)
                {
                    shouldJump = true;
                    shouldDampenJump = false;
                }
                else if (jumpCount >= jumpController.Length)
                {
                    var waitFrames = Mathf.RoundToInt(inputBuffer / Time.fixedDeltaTime);
                    for (int i = 0; i < waitFrames; i++)
                    {
                        if (state == PlayerState.Grounded) // diventerà Vector3.zero 
                        {
                            shouldJump = true;
                            shouldDampenJump = false;
                            Debug.Log("TryJump: input buffer jump!");
                            yield break;
                        }
                        else
                        {
                            yield return new WaitForEndOfFrame();
                        }
                    }                    
                }
                break;
            default:
                break;
        }
    }

    void ApplyJump()
    {
        shouldJump = false;

        var gravity = Physics.gravity;
        var jumpHeight = jumpController[jumpCount].jumpHeight;
        var gravityDampOnRise = jumpController[jumpCount].gravityDampOnRise;
        var animationCurve = jumpController[jumpCount].jumpAnimationCurve;

        var jumpNormal = GroundNormal() != Vector3.zero ? GroundNormal() : -gravity.normalized; // Misura di sicurezza

        Debug.DrawRay(transform.position, GroundNormal(), Color.white, 3f); 

        //previousGroundNormal = Vector3.zero;

        var jumpVelocity = Mathf.Sqrt(2 * jumpHeight * gravity.magnitude * gravityDampOnRise);

        var velocity = rb.velocity;
        var angle = Vector3.Angle(gravity, -jumpNormal);
        var normalPlaneVelocity = angle >= minSlopeAngle ? Vector3.ProjectOnPlane(velocity, jumpNormal) : Vector3.zero;
        rb.velocity += jumpVelocity * jumpNormal - normalPlaneVelocity;

        takeoffSpeed = Vector3.ProjectOnPlane(velocity, gravity).magnitude;
        // Squash and stretch
        JumpAnimation(gravity, jumpNormal, jumpVelocity, animationCurve);

        jumpCount++;
    }

    private void JumpAnimation(Vector3 gravity, Vector3 jumpNormal, float jumpVelocity, AnimationCurve animationCurve)
    {
        //RotatateSquashAndStretchController(jumpNormal);
        //var jumpGravityAlignedVelocity = Vector3.Project(jumpVelocity * jumpNormal, gravity); //l'errore è qui
        //timeToApexJump = jumpGravityAlignedVelocity.magnitude / gravity.magnitude;
        //SwitchCoroutine(SquashAndStretch(animationCurve, timeToApexJump));

        RotatateSquashAndStretchController(jumpNormal);
        timeToApexJump = jumpVelocity / gravity.magnitude;
        ResetCoroutine(SquashAndStretch(animationCurve, timeToApexJump));
    }

    private void ResetCoroutine(IEnumerator newCoroutine)
    {
        if (activeSquashAndStretchCoroutine != null) StopCoroutine(activeSquashAndStretchCoroutine);
        activeSquashAndStretchCoroutine = newCoroutine;
        StartCoroutine(newCoroutine);
    }

    public void EvaluateJumpCutOff()
    {
        var gravity = Physics.gravity;
        var upwardVelocity = Vector3.Dot(rb.velocity, gravity);
        var normalAlignedVelocity = Vector3.Project(rb.velocity, gravity.normalized);

        if (upwardVelocity < 0f)
        {
            shouldDampenJump = true;

            var cutoffCount = Math.Clamp(jumpCount, 1, jumpController.Length);
            var jumpCutOffFactor = jumpController[cutoffCount - 1].jumpCutOffFactor;
            var newGravity = gravity * jumpCutOffFactor;

            newGravity = Vector3.ClampMagnitude(newGravity, normalAlignedVelocity.magnitude / Time.fixedDeltaTime);
            cutOffAcceleration = newGravity - gravity;
            cutOffAcceleration = Vector2.Dot(cutOffAcceleration, gravity) >= 0f ? cutOffAcceleration : Vector3.zero; // Serve come sicurezza per gli errori di virgola mobile
        }
    }

    void CapToTermninalFallVelocity()
    {
        var velocity = rb.velocity;

        if (Vector2.Dot(velocity, Physics.gravity) <= 0) return;

        Vector3.ClampMagnitude(velocity, fallSpeedCap);

        rb.velocity = velocity;
    }

    #endregion
    #region DASH
    public IEnumerator TryDash(Vector3 desiredDirection, Vector3 camForward) // E se lasciassi la scelta?
    {
        if (groundNormal != Vector3.zero) yield break;

        var cooldown = dashCount < maxDashChain ? chainedDashCooldown : dashSeriesCooldown;

        if (Time.time - dashTimeStamp < dashDuration + dashFreezeFrames * Time.fixedDeltaTime + cooldown) yield break;

        yield return new WaitForFixedUpdate();

        if (activeSquashAndStretchCoroutine != null) StopCoroutine(activeSquashAndStretchCoroutine);
        UndoPrevCoroutineChanges();

        dashCount %= maxDashChain;
        dashCount++;

        dashTimeStamp = Time.time;

        Vector3 dashDirection;
        if (dashTowardsInputDirection)
        {
            dashDirection = desiredDirection != Vector3.zero ? desiredDirection : camForward;
        }
        else
        {
            var movementPlaneVelocity = Vector3.ProjectOnPlane(rb.velocity, -Physics.gravity.normalized);
            dashDirection = movementPlaneVelocity != Vector3.zero ? movementPlaneVelocity.normalized : camForward;
        }        

        var activeDashFrames = Mathf.RoundToInt(dashDuration / Time.fixedDeltaTime);
        var gravity = Physics.gravity;

        PlayOnDashEvent();

        for (int i = 0; i < dashFreezeFrames; i++)
        {
            rb.velocity = Vector3.zero;
            rb.AddForce(-gravity, ForceMode.Acceleration);
            yield return new WaitForFixedUpdate();
        }

        RotatateSquashAndStretchController(dashDirection);
        activeSquashAndStretchCoroutine = SquashAndStretch(dashAnimationCurve, dashDuration * 2f);
        StartCoroutine(activeSquashAndStretchCoroutine);

        for (int i = 0; i < activeDashFrames ; i++)
        {
            rb.velocity = dashDirection * dashSpeed;
            ModelRotation(rb.velocity);
            rb.AddForce(-gravity, ForceMode.Acceleration);
            yield return new WaitForFixedUpdate();
        }
    }
    #endregion
    #region ANIMATIONS
    public void RotatateSquashAndStretchController(Vector3 movementDirection)
    {
        if (movementDirection == Vector3.zero) return;
        var targetRotation = Quaternion.LookRotation(movementDirection);
        squashAndStretchController.transform.rotation = targetRotation;
        //this.transform.rotation = targetRotation;
    }

    IEnumerator SquashAndStretch(AnimationCurve curve, float animationTime)
    {
        UndoPrevCoroutineChanges();
        var collider = GetComponent<SphereCollider>();
        var timeStamp = Time.time;
        squashAndStretchController.transform.localScale = Vector3.one;

        Camera.onPreRender += SquashAndStretchPreRender;
        Camera.onPostRender += SquashAndStretchPostRender;

        while ((Time.time - timeStamp) <= animationTime) // FORSE POSSO ELIMINARE FUNCTION TIME
        {
            yield return new WaitForFixedUpdate();
            var forwardScale = curve.Evaluate((Time.time - timeStamp) / animationTime);
            var planeScale = 1f + (1f - forwardScale);
            collider.radius = forwardScale >= 1 ?  sphereRadius / forwardScale : sphereRadius / planeScale;
            squashAndStretchController.position = transform.position; // riposizionamento
            transform.parent = squashAndStretchController; // parentela
            squashAndStretchController.localScale = new Vector3(planeScale, planeScale, forwardScale); // scala secondo l'asse del parent
        }
        this.transform.parent = null;
        Camera.onPreRender -= SquashAndStretchPreRender;
        Camera.onPostRender -= SquashAndStretchPostRender;


        squashAndStretchController.localScale = Vector3.one;
        transform.localScale = Vector3.one;
        collider.radius = sphereRadius;
    }
    #endregion
    #region RENDER
    void SquashAndStretchPreRender(Camera cam)
    {
        //if (this == null) return;
        squashAndStretchController.position = transform.position; // riposizionamento
        transform.parent = squashAndStretchController; // parentela
    }

    void SquashAndStretchPostRender(Camera cam)
    {
        //if (this == null) return;
        this.transform.parent = null;
    }

    private void OnDisable()
    {
        Camera.onPreRender -= SquashAndStretchPreRender;
        Camera.onPostRender -= SquashAndStretchPostRender;
    }
    #endregion
    #region MISCELLANEOUS
    void UndoPrevCoroutineChanges()
    {
        GetComponent<SphereCollider>().radius = sphereRadius;
        transform.localScale = Vector3.one;
        squashAndStretchController.localScale = Vector3.one;
        this.transform.parent = null;
        Camera.onPreRender -= SquashAndStretchPreRender;
        Camera.onPostRender -= SquashAndStretchPostRender;
    }
    #endregion

    public void DrawGroundNormal()
    {
        Debug.DrawRay(transform.position, GroundNormal(), Color.green);
    }
}

