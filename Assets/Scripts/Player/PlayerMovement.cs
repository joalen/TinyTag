using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [Tooltip("This player's camera; movement is relative to it. Falls back to Camera.main.")]
    public Transform mainCamera;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float turnSpeed = 12f;
    public float sprintMultiplier = 1.6f;

    [Header("Jumping")]
    public float jumpForce = 7f;
    public LayerMask groundMask;
    public float fallMultiplier = 2;
    public float coyoteTime = 0.12f;

    [Header("Climbing")]
    public float climbSpeed = 3f;
    public float climbCheckDist = 1f;
    public float wallJumpPush = 5f;
    public float maxStamina = 5f;
    public float ledgeHopUp = 7f;
    public float ledgeHopForward = 4f;
    public LayerMask climbMask;

    [Header("Game control")]
    [Tooltip("Set by the GameManager: off during menus, countdown, and after being tagged")]
    public bool controlsEnabled = true;
    public float speedMultiplier = 1f;

    public float Stamina => stamina;
    public bool IsClimbing => climbing;
    public bool IsGrounded => grounded;

    /*
     * Game state vars
     */
    Rigidbody rigidBody;
    CapsuleCollider capsule;
    PlayerInputHandler playerInput;
    Vector2 input;
    bool sprintHeld;
    bool climbHeld;
    bool jumpPressed;
    bool grounded;
    float coyoteCounter;
    bool climbing;
    RaycastHit wallHit;

    float stamina;
    float regrabTimer;
    float controlLock;

    void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        rigidBody.freezeRotation = true;
        rigidBody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        stamina = maxStamina;

        playerInput = GetComponent<PlayerInputHandler>();
        if (playerInput == null) playerInput = gameObject.AddComponent<PlayerInputHandler>();

        if (mainCamera == null && Camera.main != null) mainCamera = Camera.main.transform;

        // tops of climbable furniture are also walkable ground
        groundMask |= climbMask;
    }

    /// <summary>Teleport to a spawn point and clear all movement state.</summary>
    public void ResetTo(Vector3 position, Quaternion rotation)
    {
        if (climbing) StopClimbing();
        rigidBody.position = position;
        rigidBody.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        rigidBody.linearVelocity = Vector3.zero;
        stamina = maxStamina;
        input = Vector2.zero;
        jumpPressed = false;
    }

    void Update()
    {
        if (!playerInput.enabled || !controlsEnabled)
        {
            input = Vector2.zero;
            sprintHeld = climbHeld = false;
            return;
        }

        input = Vector2.ClampMagnitude(playerInput.Move, 1f);
        sprintHeld = playerInput.SprintHeld;
        climbHeld = playerInput.ClimbHeld;

        if (playerInput.JumpPressed) jumpPressed = true; // consumed in FixedUpdate
    }

    void FixedUpdate()
    {
        regrabTimer -= Time.fixedDeltaTime;
        controlLock -= Time.fixedDeltaTime;

        CheckGround();
        CheckClimb();

        if (climbing)
        {
            Climb();
        }
        else
        {
            Move();
            Jump();
            ApplyFallGravity();

            if (grounded) stamina = Mathf.Min(maxStamina, stamina + 2f * Time.fixedDeltaTime);
        }

        jumpPressed = false;
    }

    // sphere check at the bottom of the capsule
    void CheckGround()
    {
        Bounds b = capsule.bounds;
        Vector3 feet = new Vector3(b.center.x, b.min.y + 0.1f, b.center.z);
        grounded = Physics.CheckSphere(feet, 0.25f, groundMask);

        // refill coyote time when standing and airbone should quickly decrement
        if (grounded && rigidBody.linearVelocity.y <= 0.1f) coyoteCounter = coyoteTime;
        else coyoteCounter -= Time.fixedDeltaTime;
    }

    void Move()
    {
        Transform view = mainCamera != null ? mainCamera : transform;
        Vector3 fwd = view.forward; fwd.y = 0; fwd.Normalize();
        Vector3 right = view.right; right.y = 0; right.Normalize();
        // keep analog stick magnitude so a light push walks slowly
        Vector3 dir = Vector3.ClampMagnitude(fwd * input.y + right * input.x, 1f);

        float speed = moveSpeed * speedMultiplier;
        if (sprintHeld) speed *= sprintMultiplier;

        Vector3 vel = rigidBody.linearVelocity;
        if (controlLock <= 0f)
        {
            vel.x = dir.x * speed;
            vel.z = dir.z * speed;
        }
        rigidBody.linearVelocity = vel;

        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.fixedDeltaTime);
        }
    }

    void Jump()
    {
        if (jumpPressed && grounded)
        {
            Vector3 vel = rigidBody.linearVelocity;
            vel.y = jumpForce;
            rigidBody.linearVelocity = vel;
            coyoteCounter = 0f; // use up window to prevent double jump
        }
    }

    void ApplyFallGravity()
    {
        if (rigidBody.linearVelocity.y < 0f)
        {
            rigidBody.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    void CheckClimb()
    {
        bool wallAhead = Physics.Raycast(capsule.bounds.center, transform.forward, out wallHit, climbCheckDist, climbMask);
        bool holding = climbHeld;

        if (!climbing && wallAhead && holding && stamina > 0f && regrabTimer <= 0f) // only if enough stamina and didn't just let go
        {
            climbing = true;
        }

        if (!climbing) return;

        if (!holding || stamina <= 0f) // let go or ran out of stamina
        {
            StopClimbing();
            return;
        }

        // wall ended under us: climbed past the top edge -> hop over it, otherwise slid off the side
        if (!wallAhead)
        {
            if (input.y > 0f) LedgeHop();
            else StopClimbing();
            return;
        }

        if (grounded && input.y < 0f) StopClimbing(); // pressing down while standing on ground

    }

    void Climb()
    {
        rigidBody.useGravity = false;

        transform.rotation = Quaternion.LookRotation(-wallHit.normal); // facing wall
        rigidBody.linearVelocity = (Vector3.up * input.y + transform.right * input.x) * climbSpeed;


        float drain = input.sqrMagnitude > 0.01f ? 1f : 0.3f; // draining stamina away for vertical climb
        stamina -= drain * Time.fixedDeltaTime;

        // jumping off wall
        if (jumpPressed)
        {
            StopClimbing();
            rigidBody.linearVelocity = wallHit.normal * wallJumpPush + Vector3.up * jumpForce;
            controlLock = 0.25f;
        }
    }

    void StopClimbing()
    {
        climbing = false;
        rigidBody.useGravity = true;
        regrabTimer = 0.3f;
    }

    void LedgeHop()
    {
        StopClimbing();
        rigidBody.linearVelocity = transform.forward * ledgeHopForward + Vector3.up * ledgeHopUp;
        controlLock = 0.3f;
    }
}