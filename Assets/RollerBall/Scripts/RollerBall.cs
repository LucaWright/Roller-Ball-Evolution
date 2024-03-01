using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using CustomInspector;

public enum PlayerState { Grounded, Airborne }

public class RollerBall : MonoBehaviour
{
    [CustomInspector.HorizontalLine("References", 5, FixedColor.Gray)]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private GameObject model;

    private MovementData activeMovementData;
    private float acceleration, maxSpeed, deceleration, turningFactor;
    private Vector3 spinVelocity;

    public PlayerState state { get; private set; }
    private float stateTimeStamp;
    private float sphereRadius;
    //private Nullable<Vector3> groundNormal;
    private Vector3 groundNormal, previousGroundNormal;
    private Vector3 overslopeNormal;

    [CustomInspector.HorizontalLine("Movement", 5, FixedColor.Gray)]
    [SerializeField] private MovementData groundMovement;
    [SerializeField] private MovementData airMovement;
    [SerializeField] private UnityEvent OnBurnout;


    [CustomInspector.HorizontalLine("Slopes", 5, FixedColor.Gray)]
    [Tooltip("The minimum slope angle (valui included) the player can climb."),
    SerializeField, Range(0f, 89.9f)] private float minSlopeAngle = 0f;
    [Tooltip("The maximum slope angle (value excluded) the player can climb."),
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

    [CustomInspector.HorizontalLine("Debug", 3, FixedColor.Red)]
    public TextMeshProUGUI debugPanel;
    bool turningMod;
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

            Debug.Log("Jump Max Speed: " + groundMovement.maxSpeed * (Mathf.Sqrt(2f * jump.jumpHeight / gravity.magnitude * jump.gravityDampOnRise) + Mathf.Sqrt(2f * jump.jumpHeight / gravity.magnitude)));
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
        debugPanel.text =   state + 
                            "<br> " + activeMovementData + 
                            "<br>Speed: " + Vector3.ProjectOnPlane(rb.velocity, (Vector3)groundNormal).magnitude +
                            "<br>Turning? " + turningMod;
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
                EvaluateGroundedMovementData();
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
                jumpCount = 0;
                dashCount = 0;
                //shouldDampenJump = false;
                break;
            default:
                break;
        }
    }
    #endregion
    #region COLLISIONS
    // COME DEVE CAMBIARE TUTTO IL SISTEMA?
    /* È facile che cose esplodano, quindi meglio creare un backup.
     * L'idea è questa: le collisioni verranno gestite solo da OnCollisionEnter e OnCollisionExit
     * ENTER:
     * Controlla le normali e vedi se sono già storate. Altrimenti, sostituiscile.
     * EXIT:
     * elimina dallo storing la variabile corrispondente. Questo significa che i vettori devono essere identici e potrebbe dare adito ad errori.
     * Ci proverò domani
     */
    private void OnCollisionEnter(Collision collision)
    {
        switch (state)
        {
            case PlayerState.Grounded:
                //if (collision.contactCount > 1)
                //{
                //    FindNormals(collision.contacts);
                //    EvaluateGroundedMovementData();
                //}
                FindNormals(collision.contacts);
                EvaluateGroundedMovementData();
                //Debug.Log("EnterGround: " + groundNormal.magnitude + "; EnterOverslope: " + overslopeNormal.magnitude);
                //Debug.Break();
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
        previousGroundNormal = groundNormal; // Workaround bug unity, per evitare errori durante i salti a contatto con pareti vericali

        switch (state)
        {
            case PlayerState.Grounded:
                /* I SEGUENTI CONTROLLI DOVREBBERO ESSERE FATTI IN OnCollisionEnter(), MA...
                 * grazie a un bug unity noto da anni, in cui la funzione non riesce a riempire la lista di ContactPoint con più di un collision info alla volta,
                 * devo farlo anche qui e accettare l'errore del salto quando si è a contatto con un muro verticale.
                 */
                FindNormals(collision.contacts);
                EvaluateGroundedMovementData();
                //Debug.Log("StayGround: " + groundNormal.magnitude + "; StayOverslope: " + overslopeNormal.magnitude);
                if (groundNormal == Vector3.zero && previousGroundNormal == Vector3.zero) SwitchState(PlayerState.Airborne);
                break;
            case PlayerState.Airborne:
                FindNormals(collision.contacts); // No return
                if (groundNormal != Vector3.zero) SwitchState(PlayerState.Grounded);
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
            overslopeNormal = Vector3.zero;
            SwitchState(PlayerState.Airborne);
            //Debug.Log("N: " + collision.contactCount);
            //Debug.Break();
        }
    }

    void FindNormals(ContactPoint[] contacts) // Questa funzione serve a poco, se non può ciclare i ContactPoint
    {
        float minAngle = maxSlopeAngle;
        float maxAngle = maxSlopeAngle;

        groundNormal = Vector3.zero;
        overslopeNormal = Vector3.zero;

        foreach (var contact in contacts)
        {
            var negativeNormal = -contact.normal;
            float angle = Vector3.Angle(Physics.gravity, negativeNormal);

            if (angle >= maxAngle && angle <= 90)
            {
                maxAngle = angle;
                overslopeNormal = contact.normal;
                contactPoint = contact.point;
            }

            if (angle < minAngle)
            {
                minAngle = angle;
                groundNormal = contact.normal;
                contactPoint = contact.point;
            }
        }
    }
    #endregion
    #region GROUNDED & AIRBORN MOVEMENT
    public void GroundedMove(Vector3 desiredDirection)
    {
        if (shouldJump) { ApplyJump(); return; }

        var movementPlaneNormal = groundNormal != Vector3.zero ? groundNormal : -Physics.gravity.normalized; //AGGIRA BUG UNITY SU ONCOLLISION

        var normalPlaneVelocity = Vector3.ProjectOnPlane(rb.velocity, movementPlaneNormal);
        var normalAlignedVelocity = Vector3.Project(rb.velocity, movementPlaneNormal);

        if (autoSlope) autoSlopeModifier = AutoSlopeModifier(movementPlaneNormal);
        else { autoSlopeModifier = 1f; /*EvaluateGroundedMovementData(movementPlaneNormal);*/ }

        // Set sulla base della Movement Data attiva
        maxSpeed = activeMovementData.maxSpeed;
        acceleration = activeMovementData.acceleration;
        deceleration = activeMovementData.deceleration;
        turningFactor = activeMovementData.turningFactor;

        normalPlaneVelocity = ApplyGroundMovement(desiredDirection, movementPlaneNormal, normalPlaneVelocity);

        CalculateSpin(desiredDirection, normalPlaneVelocity, turningFactor);

        RotatateSquashAndStretchController(movementPlaneNormal);

        rb.velocity = normalPlaneVelocity + normalAlignedVelocity;
        Debug.DrawRay(transform.position, rb.velocity, Color.blue);
    }
    private Vector3 ApplyGroundMovement(Vector3 desiredDirection, Vector3 movementPlaneNormal, Vector3 normalPlaneVelocity)
    {
        var gravityProjectionOnNormalPlane = Vector3.ProjectOnPlane(Physics.gravity, movementPlaneNormal);
        var isSloping = gravityProjectionOnNormalPlane != Vector3.zero ? true : false;
        var directionDotClimb = Vector3.Dot(desiredDirection, -gravityProjectionOnNormalPlane.normalized); // >= 0 ... IN SALITA. Else: DISCESA

        if (isSloping && directionDotClimb < 0) return normalPlaneVelocity;

        if (desiredDirection * autoSlopeModifier != Vector3.zero)
        {
            // Modificatori
            maxSpeed *= autoSlopeModifier;
            if (directionDotClimb >= 0) acceleration = Mathf.Lerp(groundMovement.acceleration, acceleration, directionDotClimb);
            acceleration *= autoSlopeModifier * turningFactor;
            var isTurning = true;
            if (Vector3.Dot(desiredDirection, normalPlaneVelocity) >= 0f)
            {
                turningFactor = 1f;
                isTurning = false;
                //TurningAnimation(normalPlaneVelocity, 1f, 1f);
            }
            else
            {
                PlayBurnoutEvent();
                //TurningAnimation(normalPlaneVelocity, .66f, 1f);
            }   /*DEBUG*/ turningMod = isTurning;

            rb.AddForce(-gravityProjectionOnNormalPlane, ForceMode.Acceleration); // Counteract gravity acceleration on slopes.
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, desiredDirection * maxSpeed, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
            normalPlaneVelocity = Vector3.ClampMagnitude(normalPlaneVelocity, fallSpeedCap);

            //TurningAnimation(normalPlaneVelocity, 1f, 1f);
        }

        return normalPlaneVelocity;
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

        //var movementNormal = -Physics.gravity.normalized;
        var isOverslope = overslopeNormal != Vector3.zero ? true : false;
        Vector3 movementNormal;

        if (isOverslope)
        {
            movementNormal = overslopeNormal;
            SwitchMovementData(groundMovement); // C'è da considerare che: OLTRE AL CONTATTO, C'È BISOGNO CHE IL MOVIMENTO SIA VERSO IL PIANO!

            //NEW > Poco elegante, perché è praticamente uno stato a parte, ma tant'è.
            jumpCount = 0; // Se vogliamo permettere i salti in overslope a contatto coi muri
            dashCount = 0;
            stateTimeStamp = Time.time;
        }
        else
        {
            movementNormal = -Physics.gravity.normalized;
            SwitchMovementData(airMovement);
        }

        maxSpeed = activeMovementData.maxSpeed;
        acceleration = activeMovementData.acceleration;
        deceleration = activeMovementData.deceleration;
        turningFactor = activeMovementData.turningFactor;

        var normalPlaneVelocity = Vector3.ProjectOnPlane(rb.velocity, movementNormal); // Questa è la direzione del movimento!
        var normalAlignedVelocity = Vector3.Project(rb.velocity, movementNormal);

        HandleGravityModifiersOnJump(movementNormal, normalAlignedVelocity);

        if (isOverslope)
        {
            normalPlaneVelocity = ApplyOverslopeMovement(desiredDirection, normalPlaneVelocity);
        }
        else
        {
            normalPlaneVelocity = ApplyAirMovement(desiredDirection, normalPlaneVelocity); // Se volessi evitare la possibilità accelerare in aria?
        }

        //CapToTermninalFallVelocity(ref normalAlignedVelocity);

        CalculateSpin(desiredDirection);

        rb.velocity = normalPlaneVelocity + normalAlignedVelocity;

        CapToTermninalFallVelocity();

        Debug.DrawRay(transform.position, rb.velocity, Color.blue);
    }

    private Vector3 ApplyAirMovement(Vector3 desiredDirection, Vector3 normalPlaneVelocity)
    {   
        if (desiredDirection != Vector3.zero)
        {
            turningFactor = Vector3.Dot(desiredDirection, normalPlaneVelocity) >= 0f ? 1f : turningFactor;
            /*DEBUG*/ turningMod = turningFactor > 1f ? true : false;

            acceleration *= turningFactor;

            var accelerationForce = normalPlaneVelocity.sqrMagnitude <= Mathf.Pow(maxSpeed, 2) ? acceleration : deceleration;

            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, desiredDirection * maxSpeed, accelerationForce * Time.fixedDeltaTime);
        }
        else
        {
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }

        return normalPlaneVelocity;
    }

    private Vector3 ApplyOverslopeMovement(Vector3 desiredDirection, Vector3 normalPlaneVelocity)
    {
        var directionNormal = -Physics.gravity.normalized;

        // Calcola l'asse di rotazione come il prodotto vettoriale delle due normali.
        Vector3 rotationAxis = Vector3.Cross(directionNormal, overslopeNormal);
        // Calcola l'angolo tra le due normali.
        float angle = Mathf.Acos(Vector3.Dot(directionNormal, overslopeNormal)) * Mathf.Rad2Deg;
        // Crea una rotazione attorno all'asse di rotazione per l'angolo calcolato.
        Quaternion rotation = Quaternion.AngleAxis(angle, rotationAxis);

        var directionOnPlane = rotation * desiredDirection;
        //Debug.DrawRay(transform.position, directionOnPlane * acceleration, Color.green);

        if (Vector3.Dot(directionOnPlane, directionNormal) >= 0) // Se la direzione è verso il muro
        {
            acceleration *= 1f / turningFactor;
            acceleration = Mathf.Clamp(acceleration, 0, Physics.gravity.magnitude);
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, directionOnPlane * maxSpeed, acceleration * Time.fixedDeltaTime);

            PlayBurnoutEvent();
        }
        else
        {
            normalPlaneVelocity = Vector3.MoveTowards(normalPlaneVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }
        return normalPlaneVelocity;
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
    void CalculateSpin(Vector3 desiredDirection, Vector3 normalPlaneVelocity, float turningFactor)
    {   
        // Ground
        if (Vector3.Dot(desiredDirection, normalPlaneVelocity) >= 0f)
        {
            spinVelocity = Vector3.MoveTowards(spinVelocity, normalPlaneVelocity, acceleration / sphereRadius * Time.fixedDeltaTime); // acceleration / radius!
            ModelRotation(spinVelocity);
            TurningAnimation(normalPlaneVelocity, 1f, 1f);
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
    void EvaluateGroundedMovementData()
    {
        var angle = Vector3.Angle(Physics.gravity, -groundNormal);

        if (angle < minSlopeAngle)
        {
            SwitchMovementData(groundMovement);
        }
        else
        {
            for (int i = 0; i < manualSlopes.Length; i++)
            {
                var slope = manualSlopes[i];

                if (angle < slope.higherAngle && angle >= slope.lowerAngle)
                {
                    SwitchMovementData(slope.slopeMovementData);
                }
            }
        }      
    }

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
                break;
            case PlayerState.Airborne:
                if (jumpCount == 0 && Time.time - stateTimeStamp <= coyoteTime) 
                {
                    shouldJump = true; 
                    shouldDampenJump = false; 
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

        var jumpNormal =    groundNormal != Vector3.zero ? groundNormal :
                            previousGroundNormal != Vector3.zero ? previousGroundNormal :
                            overslopeNormal != Vector3.zero ? overslopeNormal : -gravity.normalized; // Da qui posso ricavare la dash direction?

        Debug.Log("GN: " + groundNormal.magnitude + "; PGN: " + previousGroundNormal.magnitude + "; ON: " + overslopeNormal.magnitude);

        previousGroundNormal = Vector3.zero;

        var jumpVelocity = Mathf.Sqrt(2 * jumpHeight * gravity.magnitude * gravityDampOnRise);

        var velocity = rb.velocity;
        var angle = Vector3.Angle(gravity, -jumpNormal);
        var normalPlaneVelocity = angle >= minSlopeAngle ? Vector3.ProjectOnPlane(velocity, jumpNormal) : Vector3.zero;
        rb.velocity += jumpVelocity * jumpNormal - normalPlaneVelocity;

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
        StopCoroutine(activeSquashAndStretchCoroutine);
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
        if (overslopeNormal != Vector3.zero) yield break;

        var cooldown = dashCount < maxDashChain ? chainedDashCooldown : dashSeriesCooldown;

        if (Time.time - dashTimeStamp < dashDuration + dashFreezeFrames * Time.fixedDeltaTime + cooldown) yield break;

        yield return new WaitForFixedUpdate();

        StopCoroutine(activeSquashAndStretchCoroutine);
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
}

