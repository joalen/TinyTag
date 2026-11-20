using UnityEngine;

/// <summary>
/// Simple orbit camera that follows one player. Look input rotates it; when the
/// player isn't looking around it slowly swings back behind them. A sphere cast
/// pulls the camera in front of walls and furniture so it never clips through.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public PlayerInputHandler input;

    [Header("Framing")]
    public float distance = 6f;
    public float pivotHeight = 1.2f;
    public float minPitch = -25f;
    public float maxPitch = 70f;
    public float defaultPitch = 15f;

    [Header("Look")]
    public float mouseSensitivity = 0.12f;
    public float stickSensitivity = 160f;
    [Tooltip("Seconds without look input before the camera swings behind the player")]
    public float autoAlignDelay = 1.5f;
    public float autoAlignSpeed = 90f;

    [Header("Collision")]
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.3f;

    float yaw, pitch;
    float currentDistance;
    float lastLookTime;
    Rigidbody targetBody;

    void Start()
    {
        if (target != null) targetBody = target.GetComponent<Rigidbody>();
        SnapBehind();
    }

    public void SnapBehind()
    {
        if (target != null) yaw = target.eulerAngles.y;
        pitch = defaultPitch;
        currentDistance = distance;
        lastLookTime = Time.time;
    }

    void LateUpdate()
    {
        if (target == null) return;
        float dt = Time.deltaTime;

        Vector2 look = input != null && input.enabled ? input.Look : Vector2.zero;
        if (look.sqrMagnitude > 0.0001f)
        {
            float scale = input.LookIsMouse ? mouseSensitivity : stickSensitivity * dt;
            yaw += look.x * scale;
            pitch -= look.y * scale;
            lastLookTime = Time.time;
        }
        else if (Time.time - lastLookTime > autoAlignDelay && IsTargetMoving())
        {
            yaw = Mathf.MoveTowardsAngle(yaw, target.eulerAngles.y, autoAlignSpeed * dt);
        }
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Vector3 pivot = target.position + Vector3.up * pivotHeight;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 back = rot * Vector3.back;

        float wanted = distance;
        if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            wanted = Mathf.Max(0.4f, hit.distance);

        // snap in instantly (no clipping), ease back out
        currentDistance = wanted < currentDistance ? wanted : Mathf.Lerp(currentDistance, wanted, 5f * dt);

        transform.SetPositionAndRotation(pivot + back * currentDistance, rot);
    }

    bool IsTargetMoving()
    {
        if (targetBody == null) return false;
        Vector3 v = targetBody.linearVelocity; v.y = 0f;
        return v.sqrMagnitude > 0.5f;
    }
}
