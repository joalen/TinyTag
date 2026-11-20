using UnityEngine;

/// <summary>
/// Tag state for one player: whether they are "it" (shown by a spinning red
/// marker above their head; the body keeps the player's own colour), and
/// reporting collisions with the other player to the GameManager.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerTagger : MonoBehaviour
{
    public int playerIndex;
    [Tooltip("This player's own colour (never changes)")]
    public Color normalColor = new Color(0.25f, 0.5f, 1f);
    [Tooltip("Colour of the marker floating above whoever is it")]
    public Color itColor = new Color(1f, 0.2f, 0.15f);

    public bool IsIt { get; private set; }
    public bool IsFrozen => Time.time < frozenUntil;
    public PlayerMovement Movement { get; private set; }

    Transform itMarker;
    float frozenUntil;

    void Awake()
    {
        Movement = GetComponent<PlayerMovement>();
        var body = GetComponent<Renderer>();
        if (body != null) body.material.color = normalColor;
        itMarker = CreateItMarker(body);
        itMarker.gameObject.SetActive(IsIt);
    }

    void Update()
    {
        if (!IsIt) return;
        // bob and spin so it's easy to spot across the room
        itMarker.localPosition = new Vector3(0f, 1.9f + Mathf.Sin(Time.time * 4f) * 0.12f, 0f);
        itMarker.Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);
    }

    public void SetIt(bool it)
    {
        IsIt = it;
        if (itMarker != null) itMarker.gameObject.SetActive(it);
    }

    public void Freeze(float seconds) => frozenUntil = Time.time + seconds;
    public void Unfreeze() => frozenUntil = 0f;

    // a glowing red diamond above the head
    Transform CreateItMarker(Renderer body)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "It Marker";
        Destroy(marker.GetComponent<Collider>());
        marker.layer = gameObject.layer;
        var t = marker.transform;
        t.SetParent(transform, false);
        t.localPosition = new Vector3(0f, 1.9f, 0f);
        t.localRotation = Quaternion.Euler(45f, 0f, 45f);
        t.localScale = Vector3.one * 0.7f;

        var r = marker.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (body != null)
        {
            var mat = new Material(body.sharedMaterial) { color = itColor };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", itColor * 0.5f); // a soft glow; higher washes out to pink
            r.sharedMaterial = mat;
        }
        return t;
    }

    void OnCollisionEnter(Collision c) => CheckTag(c);
    void OnCollisionStay(Collision c) => CheckTag(c);

    void CheckTag(Collision c)
    {
        if (!IsIt || GameManager.Instance == null) return;
        var other = c.collider.GetComponentInParent<PlayerTagger>();
        if (other != null && other != this) GameManager.Instance.TryTag(this, other);
    }
}
