using UnityEngine;

public class CabinetController : MonoBehaviour
{
    [Header("Fall Settings")]
    public Transform tableTransform;     // The table the cabinet falls toward
    public float fallDuration = 1.5f;    // How long the fall takes
    public float fallAngle = 35f;        // How far the cabinet tips (degrees)

    [Header("Camera Shake")]
    public float shakeDuration = 1f;
    public float shakeIntensity = 0.03f;

    [Header("Candle Flare")]
    public CandleController candle;

    [Header("Raycast Settings")]
    public float raycastRange = 10f;

    // --- Private state ---
    private bool isArmed = false;
    private bool hasTriggered = false;
    private bool isFalling = false;
    private bool isShaking = false;
    private float fallTimer = 0f;
    private float shakeTimer = 0f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 endPosition;
    private Quaternion endRotation;

    private OVRCameraRig ovrRig;
    private Transform shakeParent;
    private Collider cabinetCollider;

    void Start()
    {
        cabinetCollider = GetComponent<Collider>();
    }

    void Update()
    {
        // Find rig lazily
        if (ovrRig == null)
        {
            ovrRig = FindObjectOfType<OVRCameraRig>();

            // Create a parent wrapper for camera shake
            // OVR tracking overrides rig position, so we shake a parent instead
            if (ovrRig != null && shakeParent == null)
            {
                shakeParent = new GameObject("CameraShakeParent").transform;
                shakeParent.position = ovrRig.transform.position;
                shakeParent.rotation = ovrRig.transform.rotation;
                shakeParent.SetParent(ovrRig.transform.parent);
                ovrRig.transform.SetParent(shakeParent);
            }
        }

        // Ray-cast from either controller — trigger press fires the cabinet fall
        if (isArmed && !hasTriggered && ovrRig != null)
        {
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
                TryRaycast(ovrRig.rightHandAnchor);

            if (!hasTriggered && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
                TryRaycast(ovrRig.leftHandAnchor);
        }

        // Cabinet falling animation
        if (isFalling)
        {
            fallTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(fallTimer / fallDuration);

            // Accelerate like gravity — ease in
            float easedProgress = progress * progress;

            transform.position = Vector3.Lerp(startPosition, endPosition, easedProgress);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, easedProgress);

            if (progress >= 1f)
            {
                isFalling = false;
                transform.position = endPosition;
                transform.rotation = endRotation;

                // Disable collider so the fallen cabinet doesn't interfere with physics
                if (cabinetCollider != null)
                    cabinetCollider.enabled = false;

                // Sound plays at moment of impact
                SoundManager.Instance.Play("cabinetFalls");

                // Start camera shake
                isShaking = true;
                shakeTimer = 0f;

                // Flare the candle
                if (candle != null)
                    candle.Flare();
            }
        }

        // Camera shake — shakes the parent wrapper
        if (isShaking && shakeParent != null)
        {
            shakeTimer += Time.deltaTime;

            if (shakeTimer < shakeDuration)
            {
                float fade = 1f - (shakeTimer / shakeDuration);
                float offsetX = Random.Range(-shakeIntensity, shakeIntensity) * fade;
                float offsetY = Random.Range(-shakeIntensity, shakeIntensity) * fade;
                float offsetZ = Random.Range(-shakeIntensity, shakeIntensity) * fade;
                shakeParent.localPosition = new Vector3(offsetX, offsetY, offsetZ);
            }
            else
            {
                shakeParent.localPosition = Vector3.zero;
                isShaking = false;
            }
        }
    }

    void TryRaycast(Transform hand)
    {
        if (hand == null) return;

        Ray ray = new Ray(hand.position, hand.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, raycastRange))
        {
            if (hit.collider == cabinetCollider)
                TriggerFall();
        }
    }

    void TriggerFall()
    {
        hasTriggered = true;

        startPosition = transform.position;
        startRotation = transform.rotation;

        // Calculate fall direction toward the table (horizontal only)
        Vector3 dirToTable = (tableTransform.position - transform.position);
        dirToTable.y = 0f;
        dirToTable.Normalize();

        // End position — cabinet tips in place, no base shift
        endPosition = startPosition;

        // End rotation — tip toward the table by fallAngle degrees
        Vector3 tipAxis = Vector3.Cross(Vector3.up, dirToTable);
        endRotation = Quaternion.AngleAxis(fallAngle, tipAxis) * startRotation;

        isFalling = true;
        fallTimer = 0f;
    }

    // Called by PoltergeistObject after stool slide
    public void Arm()
    {
        isArmed = true;
    }
}
