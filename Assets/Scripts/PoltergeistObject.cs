using UnityEngine;

public class PoltergeistObject : MonoBehaviour
{
    [Header("Movement")]
    public Transform endPoint;          // Empty GameObject marking where the stool ends up
    public float slideDuration = 5f;   // How long the slide takes (seconds)

    [Header("Gaze Detection")]
    public float lookAngleThreshold = 35f;

    [Header("Sound Fade")]
    public float fadeDuration = 1.5f; // How long the sound fades after stopping

    [Header("Scare Chain")]
    public CabinetController cabinet;

    // --- Private state ---
    private bool isArmed = false;
    private bool hasTriggered = false;
    private bool isSliding = false;
    private bool isFading = false;
    private float slideTimer = 0f;
    private float fadeTimer = 0f;

    private Vector3 startPosition;
    private Vector3 endPosition;
    private Transform playerCamera;
    private AudioSource audioSource;
    private float originalVolume;

    void Start()
    {
        // Create AudioSource for the scraping sound (looped while sliding)
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
    }

    void Update()
    {
        // Find camera lazily
        if (playerCamera == null)
        {
            OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
                playerCamera = rig.centerEyeAnchor;
        }

        // Check if player is looking at the stool
        if (isArmed && !hasTriggered && playerCamera != null)
        {
            Vector3 directionToStool = (transform.position - playerCamera.position).normalized;
            float angle = Vector3.Angle(playerCamera.forward, directionToStool);

            if (angle < lookAngleThreshold)
            {
                hasTriggered = true;
                StartSlide();
            }
        }

        // Slide the stool
        if (isSliding)
        {
            slideTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(slideTimer / slideDuration);

            // Linear movement — constant speed to match sound
            transform.position = Vector3.Lerp(startPosition, endPosition, progress);

            if (progress >= 1f)
            {
                isSliding = false;
                transform.position = endPosition;
                isFading = true;
                fadeTimer = 0f;
            }
        }

        // Fade the scraping sound out after stool stops
        if (isFading)
        {
            fadeTimer += Time.deltaTime;
            float fadeProgress = Mathf.Clamp01(fadeTimer / fadeDuration);
            audioSource.volume = Mathf.Lerp(originalVolume, 0f, fadeProgress);

            if (fadeProgress >= 1f)
            {
                isFading = false;
                audioSource.Stop();

                // Arm the cabinet for the next scare
                if (cabinet != null)
                    cabinet.Arm();
            }
        }
    }

    void StartSlide()
    {
        startPosition = transform.position;

        if (endPoint != null)
            endPosition = endPoint.position;
        else
            endPosition = startPosition + Vector3.forward * 2f;

        isSliding = true;
        slideTimer = 0f;

        // Get the sound clip from SoundManager
        SoundManager.Sound s = System.Array.Find(SoundManager.Instance.sounds, x => x.name == "poltergeistStool");
        if (s != null)
        {
            audioSource.clip = s.clip;
            audioSource.volume = s.volume;
            originalVolume = s.volume;
            audioSource.Play();
        }
    }

    // Called by DoorScare after door locks
    public void Arm()
    {
        isArmed = true;
    }
}
