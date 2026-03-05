using UnityEngine;

public class LanternController : MonoBehaviour
{
    // --- Assign these in the Inspector ---
    [Header("Scare Chain")]
    public DoorScare doorScare;

    [Header("Grab Settings")]
    public float grabRange = 0.5f; // How close hand must be to pick up

    [Header("Hold Offset (tweak in Inspector)")]
    public Vector3 holdPositionOffset = new Vector3(0f, -0.15f, 0.08f);
    public Vector3 holdRotationOffset = new Vector3(0f, 0f, 0f);

    [Header("Flicker Settings")]
    public float normalIntensity = 1.5f;
    public float flickerSpeed = 10f;
    public float flickerAmount = 0.3f;

    // --- Private state ---
    private bool isHeld = false;
    private bool doorScareArmed = false;
    private bool isFlickering = false;

    private Light lanternLight;
    private ParticleSystem lanternFlame;
    private GameObject lightObject;
    private Collider lanternCollider;

    private Transform rightHandAnchor;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Transform originalParent;

    void Start()
    {
        // Find the right hand anchor from OVRCameraRig
        OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null)
            rightHandAnchor = rig.rightHandAnchor;

        // Auto-find light and flame from children
        lanternLight = GetComponentInChildren<Light>(true); // true = include inactive
        lanternFlame = GetComponentInChildren<ParticleSystem>();

        // Save the light's GameObject so we can toggle it
        if (lanternLight != null)
            lightObject = lanternLight.gameObject;

        // Get the collider to disable during grab
        lanternCollider = GetComponent<Collider>();

        // Save original transform so we can put it back on release
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalParent = transform.parent;

        // Light off at start, but flame stays on as a visual hint
        if (lightObject != null)
            lightObject.SetActive(false);
        if (lanternFlame != null)
            lanternFlame.Play();
    }

    void Update()
    {
        // Right index trigger press toggles grab
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            if (!isHeld)
            {
                // Only pick up if hand is close enough
                if (rightHandAnchor != null)
                {
                    float dist = Vector3.Distance(transform.position, rightHandAnchor.position);
                    if (dist <= grabRange)
                        GrabLantern();
                }
            }
            else
            {
                ReleaseLantern();
            }
        }

        // Arm the door scare once lantern is grabbed
        if (isHeld && !doorScareArmed)
        {
            doorScareArmed = true;
            if (doorScare != null)
                doorScare.Arm();
        }

        // Handle light flickering while held
        if (isHeld && lanternLight != null)
        {
            if (isFlickering)
            {
                // Erratic flicker when close to altar
                float flicker = Mathf.PerlinNoise(Time.time * flickerSpeed * 2f, 0f);
                lanternLight.intensity = normalIntensity * flicker;
            }
            else
            {
                // Gentle natural flicker
                float flicker = 1f - (Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * flickerAmount);
                lanternLight.intensity = normalIntensity * flicker;
            }
        }
    }

    void GrabLantern()
    {
        isHeld = true;

        // Attach lantern to right hand with offset so it hangs below like a real lantern
        transform.SetParent(rightHandAnchor);
        transform.localPosition = holdPositionOffset;
        transform.localRotation = Quaternion.Euler(holdRotationOffset);

        // Disable collider so it can't push through floor/walls
        if (lanternCollider != null) lanternCollider.enabled = false;

        // Turn on light and flame
        if (lightObject != null) lightObject.SetActive(true);
        if (lanternFlame != null) lanternFlame.Play();
    }

    void ReleaseLantern()
    {
        isHeld = false;

        // Put lantern back to original spot
        transform.SetParent(originalParent);
        transform.position = originalPosition;
        transform.rotation = originalRotation;

        // Re-enable collider and turn off light
        if (lanternCollider != null) lanternCollider.enabled = true;
        if (lightObject != null) lightObject.SetActive(false);
    }

    // Called by CandleController when player is close to the altar
    public void SetFlickering(bool flicker)
    {
        isFlickering = flicker;
    }
}
