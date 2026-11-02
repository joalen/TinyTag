using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public Transform mainCamera;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float turnSpeed = 12f;
    public float sprintMultiplier = 1.6f;
    public KeyCode sprintKey = KeyCode.LeftShift;

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
    public KeyCode climbKey = KeyCode.E;
    public LayerMask climbMask;

    /* 
     * Game state vars
     */
    Rigidbody rigidBody;
    CapsuleCollider capsule;
    Vector2 input;
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
    }

    void Update()
    {
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (Input.GetButtonDown("Jump")) jumpPressed = true;
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
        Vector3 fwd = mainCamera.forward; fwd.y = 0; fwd.Normalize();
        Vector3 right = mainCamera.right; right.y = 0; right.Normalize();
        Vector3 dir = (fwd * input.y + right * input.x).normalized;

        // left sprinting
        float speed = moveSpeed;
        if (Input.GetKey(sprintKey)) speed *= sprintMultiplier;

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
        bool holding = Input.GetKey(climbKey);

        if (!climbing && wallAhead && holding && stamina > 0f && regrabTimer <= 0f) // only if enough stamina and didn't just let go
        {
            climbing = true;
        }

        if (!climbing) return;

        if (!holding || stamina <= 0f || !wallAhead) // ran out of stamina or sliding from wall
        {
            StopClimbing();
            return;
        }

        // ledge climbing
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


    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 250, 25), "Stamina: " + stamina.ToString("0.0"));
    }
}