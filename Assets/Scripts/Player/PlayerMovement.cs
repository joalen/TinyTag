using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public Transform mainCamera;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float turnSpeed = 12f;

    [Header("Jumping")]
    public float jumpForce = 7f;
    public LayerMask groundMask;
    public float fallMultiplier = 2;

    [Header("Climbing")]
    public float climbSpeed = 3f;
    public float climbCheckDist = 1f;
    public float wallJumpPush = 5f;
    public KeyCode climbKey = KeyCode.E;
    public LayerMask climbMask;

    Rigidbody rigidBody;
    CapsuleCollider capsule;
    Vector2 input;
    bool jumpPressed;
    bool grounded;
    bool climbing;
    RaycastHit wallHit;

    void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        rigidBody.freezeRotation = true;
        rigidBody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Update()
    {
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (Input.GetButtonDown("Jump")) jumpPressed = true;
    }

    void FixedUpdate()
    {
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
        }

        jumpPressed = false;
    }

    // sphere check at the bottom of the capsule
    void CheckGround()
    {
        Bounds b = capsule.bounds;
        Vector3 feet = new Vector3(b.center.x, b.min.y + 0.1f, b.center.z);
        grounded = Physics.CheckSphere(feet, 0.25f, groundMask);
    }

    void Move()
    {
        Vector3 fwd = mainCamera.forward; fwd.y = 0; fwd.Normalize();
        Vector3 right = mainCamera.right; right.y = 0; right.Normalize();
        Vector3 dir = (fwd * input.y + right * input.x).normalized;

        Vector3 vel = rigidBody.linearVelocity;
        vel.x = dir.x * moveSpeed;
        vel.z = dir.z * moveSpeed;
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
        bool wallAhead = Physics.Raycast(capsule.bounds.center, transform.forward,
                                         out wallHit, climbCheckDist, climbMask);
        bool holding = Input.GetKey(climbKey);

        if (!climbing && wallAhead && holding) climbing = true;
        if (climbing && (!wallAhead || !holding)) StopClimbing();
    }

    void Climb()
    {
        rigidBody.useGravity = false;

        transform.rotation = Quaternion.LookRotation(-wallHit.normal); // facing wall
        rigidBody.linearVelocity = (Vector3.up * input.y + transform.right * input.x) * climbSpeed;

        // jumping off wall
        if (jumpPressed)
        {
            StopClimbing();
            rigidBody.linearVelocity = wallHit.normal * wallJumpPush + Vector3.up * jumpForce;
        }
    }

    void StopClimbing()
    {
        climbing = false;
        rigidBody.useGravity = true;
    }
}