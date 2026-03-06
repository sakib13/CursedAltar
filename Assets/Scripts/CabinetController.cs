using UnityEngine;

public class CabinetController : MonoBehaviour
{
    [Header("Fall Settings")]
    public Transform tableTransform;     // The table the cabinet falls toward
    public float fallDuration = 1.5f;    // How long the fall takes
    public float fallAngle = 35f;        // How far the cabinet tips (degrees)

    [Header("Impact Feedback")]
    public float hapticDuration = 0.5f;
    public float hapticStrength = 0.8f;

    [Header("Candle Flare")]
    public CandleController candle;

    [Header("Collision Particle")]
    public ParticleSystem collisionParticle;
    public float particleEmissionRate = 100f;

    [Header("Raycast Settings")]
    public float raycastRange = 10f;

    // --- Private state ---
    private bool isArmed = false;
    private bool hasTriggered = false;
    private bool isFalling = false;
    private bool isVibrating = false;
    private float fallTimer = 0f;
    private float hapticTimer = 0f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 endPosition;
    private Quaternion endRotation;

    private OVRCameraRig ovrRig;
    private Collider cabinetCollider;

    void Start()
    {
        cabinetCollider = GetComponent<Collider>();

        // Disable particle until impact
        if (collisionParticle != null)
            collisionParticle.Stop();
    }

    void Update()
    {
        // Find rig lazily
        if (ovrRig == null)
            ovrRig = FindObjectOfType<OVRCameraRig>();

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

                // Burst collision particle
                if (collisionParticle != null)
                {
                    var emission = collisionParticle.emission;
                    emission.rateOverTime = particleEmissionRate;
                    collisionParticle.Play();
                }

                // Controller haptic vibration for impact feel
                isVibrating = true;
                hapticTimer = 0f;

                // Flare the candle
                if (candle != null)
                    candle.Flare();
            }
        }

        // Haptic vibration — fades out over duration
        if (isVibrating)
        {
            hapticTimer += Time.deltaTime;

            if (hapticTimer < hapticDuration)
            {
                float fade = 1f - (hapticTimer / hapticDuration);
                float strength = hapticStrength * fade;
                OVRInput.SetControllerVibration(strength, strength, OVRInput.Controller.RTouch);
                OVRInput.SetControllerVibration(strength, strength, OVRInput.Controller.LTouch);
            }
            else
            {
                OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
                OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
                isVibrating = false;
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
