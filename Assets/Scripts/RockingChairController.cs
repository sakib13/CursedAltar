using UnityEngine;

public class RockingChairController : MonoBehaviour
{
    [Header("Rocking Settings")]
    public float rockAngle = 10f;
    public float rockSpeed = 2f;
    public float stopDuration = 1f;
    public float fadeOutDuration = 1f;

    [Header("Sound Cycle")]
    public float soundPlayDuration = 5f;
    public float soundPauseDuration = 5f;
    public float soundFadeDuration = 1f;

    [Header("Gaze Detection")]
    public float startLookingAngle = 55f;    // Angle to detect player is looking at chair
    public float stopLookingAngle = 100f;    // Angle before chair considers player fully looked away
    public float lookAwayDelay = 1f;          // Player must look away for 1 second before rocking starts

    [Header("Voodoo Doll (optional)")]
    public GameObject voodooDoll;

    private bool isArmed = false;
    private bool isRocking = false;
    private bool isStopping = false;
    private bool isFadingOut = false;
    private bool playerIsLooking = true;     // Start as looking so it doesn't rock immediately
    private float stopTimer = 0f;
    private float fadeOutTimer = 0f;
    private float rockTimer = 0f;
    private float currentAmplitude = 0f;
    private float lookAwayTimer = 0f;

    // Sound cycle state
    private float soundCycleTimer = 0f;
    private bool soundsPlaying = false;
    private bool soundsFadingOut = false;
    private float soundFadeTimer = 0f;

    private Quaternion baseRotation;
    private Transform playerCamera;
    private AudioSource rockingSound;
    private AudioSource laughSound;
    private float rockingSoundVolume;
    private float laughSoundVolume;

    void Start()
    {
        baseRotation = transform.localRotation;

        rockingSound = gameObject.AddComponent<AudioSource>();
        rockingSound.loop = true;
        rockingSound.playOnAwake = false;

        laughSound = gameObject.AddComponent<AudioSource>();
        laughSound.loop = true;
        laughSound.playOnAwake = false;

        SoundManager.Sound rockClip = System.Array.Find(SoundManager.Instance.sounds, x => x.name == "rockingchair");
        if (rockClip != null)
        {
            rockingSound.clip = rockClip.clip;
            rockingSound.volume = rockClip.volume;
            rockingSoundVolume = rockClip.volume;
        }

        SoundManager.Sound laughClip = System.Array.Find(SoundManager.Instance.sounds, x => x.name == "girllaugh");
        if (laughClip != null)
        {
            laughSound.clip = laughClip.clip;
            laughSound.volume = laughClip.volume;
            laughSoundVolume = laughClip.volume;
        }
    }

    void Update()
    {
        if (!isArmed) return;

        // When forced rocking (chaos phase), skip gaze detection — just keep rocking
        if (forcedRocking)
        {
            if (isRocking)
            {
                rockTimer += Time.deltaTime * rockSpeed;
                float rockOffset = Mathf.Sin(rockTimer) * currentAmplitude;
                transform.localRotation = baseRotation * Quaternion.Euler(rockOffset, 0f, 0f);
            }
            return;
        }

        if (playerCamera == null)
        {
            OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
                playerCamera = rig.centerEyeAnchor;
            return;
        }

        Vector3 dirToChair = (transform.position - playerCamera.position).normalized;
        float angle = Vector3.Angle(playerCamera.forward, dirToChair);

        // Hysteresis: different thresholds for start/stop looking
        if (playerIsLooking)
        {
            // Player is currently "looking" — only count as "not looking" at a much wider angle
            if (angle > stopLookingAngle)
            {
                // Start counting how long they've been looking away
                lookAwayTimer += Time.deltaTime;

                // Only actually switch after sustained look-away
                if (lookAwayTimer >= lookAwayDelay)
                    playerIsLooking = false;
            }
            else
            {
                lookAwayTimer = 0f; // Reset — still looking
            }
        }
        else
        {
            // Player is "not looking" — snap back to looking at a tighter angle
            if (angle < startLookingAngle)
            {
                playerIsLooking = true;
                lookAwayTimer = 0f;
            }
        }

        // Looking → stop the chair
        if (playerIsLooking)
        {
            if (isRocking && !isStopping && !isFadingOut)
            {
                isStopping = true;
                stopTimer = 0f;
            }

            if (isStopping)
            {
                stopTimer += Time.deltaTime;
                if (stopTimer >= stopDuration)
                {
                    isStopping = false;
                    isFadingOut = true;
                    fadeOutTimer = 0f;
                }
            }
        }
        // Not looking → start rocking
        else
        {
            if (!isRocking)
            {
                isRocking = true;
                isStopping = false;
                isFadingOut = false;
                currentAmplitude = rockAngle;
                soundCycleTimer = 0f;
                soundsFadingOut = false;
                soundsPlaying = true;
                StartSounds();
            }
            else if (isStopping || isFadingOut)
            {
                isStopping = false;
                isFadingOut = false;
                currentAmplitude = rockAngle;
                if (soundsPlaying)
                    SetSoundVolumes(1f);
            }
        }

        // Smooth fade out on gaze
        if (isFadingOut)
        {
            fadeOutTimer += Time.deltaTime;
            float fadeProgress = Mathf.Clamp01(fadeOutTimer / fadeOutDuration);
            float easedFade = 1f - (fadeProgress * fadeProgress);

            currentAmplitude = rockAngle * easedFade;
            if (soundsPlaying)
                SetSoundVolumes(easedFade);

            if (fadeProgress >= 1f)
            {
                isFadingOut = false;
                isRocking = false;
                currentAmplitude = 0f;
                transform.localRotation = baseRotation;
                StopSounds();
                soundsPlaying = false;
                soundsFadingOut = false;
            }
        }

        // Sound cycle — play, fade out, pause, repeat
        if (isRocking && !isFadingOut)
        {
            soundCycleTimer += Time.deltaTime;

            if (soundsPlaying && !soundsFadingOut)
            {
                if (soundCycleTimer >= soundPlayDuration - soundFadeDuration)
                {
                    soundsFadingOut = true;
                    soundFadeTimer = 0f;
                }
            }

            if (soundsFadingOut)
            {
                soundFadeTimer += Time.deltaTime;
                float fadeProg = Mathf.Clamp01(soundFadeTimer / soundFadeDuration);
                SetSoundVolumes(1f - fadeProg);

                if (fadeProg >= 1f)
                {
                    soundsFadingOut = false;
                    soundsPlaying = false;
                    soundCycleTimer = 0f;
                    StopSounds();
                }
            }

            if (!soundsPlaying && !soundsFadingOut)
            {
                if (soundCycleTimer >= soundPauseDuration)
                {
                    soundCycleTimer = 0f;
                    soundsPlaying = true;
                    StartSounds();
                }
            }
        }

        // Apply rocking motion
        if (isRocking)
        {
            rockTimer += Time.deltaTime * rockSpeed;
            float rockOffset = Mathf.Sin(rockTimer) * currentAmplitude;
            transform.localRotation = baseRotation * Quaternion.Euler(rockOffset, 0f, 0f);
        }
    }

    void StartSounds()
    {
        if (rockingSound.clip != null && !rockingSound.isPlaying)
        {
            rockingSound.volume = rockingSoundVolume;
            rockingSound.Play();
        }
        if (laughSound.clip != null && !laughSound.isPlaying)
        {
            laughSound.volume = laughSoundVolume;
            laughSound.Play();
        }
    }

    void SetSoundVolumes(float multiplier)
    {
        rockingSound.volume = rockingSoundVolume * multiplier;
        laughSound.volume = laughSoundVolume * multiplier;
    }

    void StopSounds()
    {
        if (rockingSound.isPlaying)
            rockingSound.Stop();
        if (laughSound.isPlaying)
            laughSound.Stop();
    }

    public void Arm()
    {
        isArmed = true;
    }

    // Force aggressive rocking immediately, ignoring gaze logic
    private bool forcedRocking = false;

    public void ForceRock()
    {
        isArmed = true;
        forcedRocking = true;
        playerIsLooking = false;
        lookAwayTimer = lookAwayDelay;
        isRocking = true;
        isStopping = false;
        isFadingOut = false;

        // Aggressive rocking — faster and wider than normal
        rockSpeed = rockSpeed * 2.5f;
        rockAngle = rockAngle * 2f;
        currentAmplitude = rockAngle;

        soundCycleTimer = 0f;
        soundsFadingOut = false;
        soundsPlaying = true;
        StartSounds();
    }
}
