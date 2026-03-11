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

    [Header("Rattle Settings")]
    public float rattleOpenAngle = 15f;  // How far door opens for rattle
    public float rattleSpeed = 8f;       // How fast the door rattles
    public float rattleAmplitude = 5f;   // Degrees of rattle oscillation
    public string rattleSound = "barndoor";

    // --- Private state ---
    private bool isArmed = false;
    private bool hasTriggered = false;
    private bool isSlamming = false;
    private float slamTimer = 0f;

    // Rattle state
    private bool isRattling = false;
    private bool isOpeningForRattle = false;
    private float rattleTimer = 0f;
    private float rattleOpenTimer = 0f;
    private Quaternion rattleBaseRotation;
    private AudioSource rattleAudioSource;

    // Slam from rattle
    private bool isSlamFromRattle = false;
    private float slamFromRattleTimer = 0f;
    private Quaternion slamFromRattleStart;

    // Rattle light — smooth pulse
    private Light rattleLight;
    private float rattleLightTimer = 0f;

    private GameObject hingeObject;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Transform playerCamera;

    public bool IsRattling => isRattling;
    public bool IsClosed => !isRattling && !isOpeningForRattle && !isSlamFromRattle;

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

        // Rattle audio source (looping)
        rattleAudioSource = gameObject.AddComponent<AudioSource>();
        rattleAudioSource.loop = true;
        rattleAudioSource.playOnAwake = false;
        rattleAudioSource.spatialBlend = 1f;

        if (SoundManager.Instance != null)
        {
            SoundManager.Sound s = System.Array.Find(SoundManager.Instance.sounds, x => x.name == rattleSound);
            if (s != null)
            {
                rattleAudioSource.clip = s.clip;
                rattleAudioSource.volume = s.volume;
            }
        }
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

        // Check if player is looking at the door (initial scare)
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

        // Slam the door shut (initial scare)
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

                if (poltergeistStool != null)
                    poltergeistStool.Arm();
            }
        }

        // Opening door for rattle
        if (isOpeningForRattle && hingeObject != null)
        {
            rattleOpenTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(rattleOpenTimer / 0.5f);

            rattleBaseRotation = closedRotation * Quaternion.Euler(0f, rattleOpenAngle, 0f);
            hingeObject.transform.rotation = Quaternion.Slerp(closedRotation, rattleBaseRotation, progress);

            if (progress >= 1f)
            {
                isOpeningForRattle = false;
                isRattling = true;
                rattleTimer = 0f;

                if (rattleAudioSource.clip != null && !rattleAudioSource.isPlaying)
                    rattleAudioSource.Play();
            }
        }

        // Rattling back and forth
        if (isRattling && hingeObject != null)
        {
            rattleTimer += Time.deltaTime;
            float angle = Mathf.Sin(rattleTimer * rattleSpeed) * rattleAmplitude;
            hingeObject.transform.rotation = rattleBaseRotation * Quaternion.Euler(0f, angle, 0f);

            // Smooth pulsing light
            if (rattleLight != null)
            {
                rattleLightTimer += Time.deltaTime;
                float pulse = (Mathf.Sin(rattleLightTimer * 1.5f) + 1f) * 0.5f; // 0 to 1 smooth
                rattleLight.intensity = Mathf.Lerp(0.5f, 3f, pulse);
            }
        }

        // Slam shut from rattle position
        if (isSlamFromRattle && hingeObject != null)
        {
            slamFromRattleTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(slamFromRattleTimer / 0.3f); // Fast slam

            hingeObject.transform.rotation = Quaternion.Slerp(slamFromRattleStart, closedRotation, progress);

            if (progress >= 1f)
            {
                isSlamFromRattle = false;
                hingeObject.transform.rotation = closedRotation;
            }
        }
    }

    // Called by LanternController when lantern is picked up
    public void Arm()
    {
        isArmed = true;
    }

    // Called by CursedSkull to start door rattle phase
    public void StartRattle()
    {
        if (isRattling || isOpeningForRattle) return;

        isOpeningForRattle = true;
        isSlamFromRattle = false;
        rattleOpenTimer = 0f;

        // Create pulsing light at door position
        if (rattleLight == null)
        {
            GameObject lightGO = new GameObject("RattleLight");
            lightGO.transform.position = transform.position + Vector3.up * 0.3f;
            rattleLight = lightGO.AddComponent<Light>();
            rattleLight.type = LightType.Point;
            rattleLight.color = new Color(0.9f, 0.3f, 0.1f); // warm orange-red
            rattleLight.intensity = 0f;
            rattleLight.range = 5f;
            rattleLight.shadows = LightShadows.None;
            rattleLightTimer = 0f;
        }

        Debug.Log("[DoorScare] Starting rattle");
    }

    // Called by CursedSkull when player approaches the door during stare contest
    public void SlamFromRattle()
    {
        if (!isRattling && !isOpeningForRattle) return;

        isRattling = false;
        isOpeningForRattle = false;
        isSlamFromRattle = true;
        slamFromRattleTimer = 0f;
        slamFromRattleStart = hingeObject.transform.rotation;

        if (rattleAudioSource.isPlaying)
            rattleAudioSource.Stop();

        DestroyRattleLight();
        SoundManager.Instance.Play("doorSlam");

        Debug.Log("[DoorScare] Slam from rattle");
    }

    // Stop rattling without slamming (for when stare contest completes)
    public void StopRattle()
    {
        isRattling = false;
        isOpeningForRattle = false;

        if (rattleAudioSource != null && rattleAudioSource.isPlaying)
            rattleAudioSource.Stop();

        DestroyRattleLight();
    }

    void DestroyRattleLight()
    {
        if (rattleLight != null)
        {
            Destroy(rattleLight.gameObject);
            rattleLight = null;
        }
    }
}
