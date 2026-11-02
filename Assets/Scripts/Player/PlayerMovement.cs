using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public Transform mainCamera;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float turnSpeed = 12f;

    Rigidbody rigidBody;
    Vector2 input;

    void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        rigidBody.freezeRotation = true;
        rigidBody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Update()
    {
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    void FixedUpdate()
    {
        Move();
    }

    void Move()
    {
        // flattened camera-relative direction to prevent slowing down via looking up or down in perspective
        Vector3 fwd = mainCamera.forward; fwd.y = 0; fwd.Normalize();
        Vector3 right = mainCamera.right; right.y = 0; right.Normalize();
        Vector3 dir = (fwd * input.y + right * input.x).normalized;

        // setting horizontal velocity
        Vector3 vel = rigidBody.linearVelocity;
        vel.x = dir.x * moveSpeed;
        vel.z = dir.z * moveSpeed;
        rigidBody.linearVelocity = vel;

        // turn to face movement direction
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.fixedDeltaTime);
        }
    }
}