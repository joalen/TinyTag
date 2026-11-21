using UnityEngine;

/// <summary>
/// Cheap procedural animation for the toy character model (a child of the player).
/// Waddle = duck sways side to side; Hop = frog bounces up and down while moving.
/// Also squashes on landing and stretches while in the air.
/// </summary>
public class ToyWaddle : MonoBehaviour
{
    public enum Style { Waddle, Hop }

    public Style style = Style.Waddle;
    public float stepRate = 9f;      // steps per second-ish at walking speed
    public float swayAngle = 12f;    // waddle side-to-side tilt
    public float bobHeight = 0.12f;  // up/down while moving

    Rigidbody body;
    PlayerMovement movement;
    Vector3 baseScale, basePos;
    float phase, squash;
    bool wasGrounded = true;

    void Awake()
    {
        body = GetComponentInParent<Rigidbody>();
        movement = GetComponentInParent<PlayerMovement>();
        baseScale = transform.localScale;
        basePos = transform.localPosition;
    }

    void LateUpdate()
    {
        if (body == null) return;
        float dt = Time.deltaTime;

        Vector3 v = body.linearVelocity;
        float speed = new Vector2(v.x, v.z).magnitude;
        bool grounded = movement == null || movement.IsGrounded;
        float moving = grounded ? Mathf.Clamp01(speed / 4f) : 0f;

        phase += dt * stepRate * Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01(speed / 10f)) * (moving > 0.05f ? 1f : 0f);

        float tilt = 0f, bob = 0f;
        if (style == Style.Waddle)
        {
            tilt = Mathf.Sin(phase) * swayAngle * moving;
            bob = Mathf.Abs(Mathf.Sin(phase)) * bobHeight * moving;
        }
        else
        {
            bob = Mathf.Abs(Mathf.Sin(phase * 0.6f)) * bobHeight * 2.5f * moving;
        }

        // squash on landing, stretch in the air
        if (grounded && !wasGrounded) squash = 1f;
        wasGrounded = grounded;
        squash = Mathf.MoveTowards(squash, 0f, dt * 5f);
        float stretch = grounded ? 0f : Mathf.Clamp(v.y / 10f, -0.15f, 0.15f);
        float sy = 1f - 0.25f * squash + stretch;
        float sxz = 1f + 0.15f * squash - stretch * 0.5f;

        transform.localScale = new Vector3(baseScale.x * sxz, baseScale.y * sy, baseScale.z * sxz);
        transform.localPosition = basePos + Vector3.up * bob;
        transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }
}
