using UnityEngine;

public class DoorScare : MonoBehaviour
{
    [Header("Door Settings")]
    public float openAngle = 30f;     // How far the door is open at start (degrees)
    public float slamDuration = 1f; // How long the slam takes (seconds)

    [Header("Gaze Detection")]
    public float lookAngleThreshold = 35f;

    [Header("Scare Chain")]
    public PoltergeistObject poltergeistStool;
    public float stoolDelay = 3f;

    // --- Private state ---
    private bool isArmed = false;
    private bool hasTriggered = false;
    private bool isSlamming = false;
    private bool waitingForStool = false;
    private float slamTimer = 0f;
    private float stoolTimer = 0f;

    private GameObject hingeObject;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Transform playerCamera;

    void Start()
    {
        // Find the hinge edge using the BoxCollider bounds
        BoxCollider col = GetComponent<BoxCollider>();
        float hingeZ = 0f;
        if (col != null)
            hingeZ = col.center.z + col.size.z / 2f;

        // Calculate hinge position in world space
        Vector3 hingeWorldPos = transform.TransformPoint(new Vector3(0f, 0f, hingeZ));

        // Create an invisible hinge pivot at the door's edge
        hingeObject = new GameObject("DoorHinge");
        hingeObject.transform.position = hingeWorldPos;
        hingeObject.transform.rotation = transform.rotation;

        // Make the door a child of the hinge so it swings around it
        transform.SetParent(hingeObject.transform);

        // Save closed rotation, then swing door open
        closedRotation = hingeObject.transform.rotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        hingeObject.transform.rotation = openRotation;
    }

    void Update()
    {
        // Find camera lazily — OVR cameras may not be ready in Start()
        if (playerCamera == null)
        {
            OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
                playerCamera = rig.centerEyeAnchor;
        }

        // Check if player is looking at the door
        if (isArmed && !hasTriggered && playerCamera != null)
        {
            Vector3 directionToDoor = (transform.position - playerCamera.position).normalized;
            float angle = Vector3.Angle(playerCamera.forward, directionToDoor);

            if (angle < lookAngleThreshold)
            {
                hasTriggered = true;
                isSlamming = true;
                slamTimer = 0f;
                SoundManager.Instance.Play("doorSlam");
            }
        }

        // Slam the door shut by rotating the hinge
        if (isSlamming && hingeObject != null)
        {
            slamTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(slamTimer / slamDuration);

            hingeObject.transform.rotation = Quaternion.Slerp(openRotation, closedRotation, progress);

            if (progress >= 1f)
            {
                isSlamming = false;
                hingeObject.transform.rotation = closedRotation;
                SoundManager.Instance.Play("doorLock");
                waitingForStool = true;
                stoolTimer = 0f;
            }
        }

        // Wait then trigger stool
        if (waitingForStool)
        {
            stoolTimer += Time.deltaTime;
            if (stoolTimer >= stoolDelay)
            {
                waitingForStool = false;
                if (poltergeistStool != null)
                    poltergeistStool.Trigger();
            }
        }
    }

    void PlayLockSound()
    {
        SoundManager.Instance.Play("doorLock");
    }

    // Called by LanternController when lantern is picked up
    public void Arm()
    {
        isArmed = true;
    }
}
