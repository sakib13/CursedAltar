using UnityEngine;
using TMPro;

public class CrossRitual : MonoBehaviour
{
    [Header("Cross Reference")]
    public GameObject cross;

    [Header("Gaze Settings")]
    public float gazeAngle = 20f;

    [Header("Altar / Table")]
    public Transform table;
    public float altarApproachDistance = 1.5f;

    [Header("Text (TMP) — set inactive in scene")]
    public GameObject breathText;
    public float textFadeDuration = 2f;

    [Header("Audio (SoundManager names)")]
    public string wallsRememberSound = "wallsremembercrosswaits";
    public string altarCallSound = "altarcall";

    [Header("Timings")]
    public float wallsRememberDelay = 10f;
    public float wallsRememberInterval = 30f;
    public float altarCallInterval = 10f;

    // State machine
    private enum State { WaitingForCabinet, WallsWhisper, RitualActive, CrossFlipping, AltarCall, TextFadeIn, Complete }
    private State currentState = State.WaitingForCabinet;

    // Thumbstick sequence: Up, Left, Down, Right, Up
    private enum Dir { Up, Left, Down, Right }
    private Dir[] correctSequence = { Dir.Up, Dir.Left, Dir.Down, Dir.Right, Dir.Up };
    private int currentStep = 0;

    // Thumbstick input
    private bool thumbstickReleased = true;
    private float thumbstickThreshold = 0.7f;

    // Walls remember whisper
    private float wallsTimer = 0f;
    private float wallsIntervalTimer = 0f;
    private bool firstWallsPlayed = false;
    private AudioSource ritualAudioSource;

    // Cross glow (point light)
    private Light crossGlowLight;
    private float glowTimer = -1f;
    private float glowDuration = 1.5f;
    private float glowMaxIntensity = 5f;

    // Cross flip (hinge at bottom)
    private GameObject crossHinge;
    private Quaternion crossBaseRotation;
    private float flipTimer = 0f;
    private float flipDuration = 2f;
    private Quaternion flipStartRotation;
    private Quaternion flipEndRotation;

    // Altar call
    private float altarCallTimer = 0f;

    // Text fade
    private float textFadeTimer = 0f;
    private TextMeshPro tmpText;
    private Color textOriginalColor;

    // Camera ref
    private OVRCameraRig ovrRig;

    void Start()
    {
        ritualAudioSource = gameObject.AddComponent<AudioSource>();
        ritualAudioSource.spatialBlend = 0f;
        ritualAudioSource.playOnAwake = false;

        // Create glow light at cross position (starts off)
        if (cross != null)
        {
            GameObject lightGO = new GameObject("CrossGlowLight");
            lightGO.transform.position = cross.transform.position;
            lightGO.transform.SetParent(cross.transform);
            crossGlowLight = lightGO.AddComponent<Light>();
            crossGlowLight.type = LightType.Point;
            crossGlowLight.color = new Color(0.8f, 0.05f, 0.02f);
            crossGlowLight.range = 3f;
            crossGlowLight.intensity = 0f;
            crossGlowLight.shadows = LightShadows.None;
        }

        if (breathText != null)
        {
            breathText.SetActive(false);
            tmpText = breathText.GetComponent<TextMeshPro>();
            if (tmpText != null)
                textOriginalColor = tmpText.color;
        }
    }

    void Update()
    {
        if (ovrRig == null)
            ovrRig = FindObjectOfType<OVRCameraRig>();

        // Cross glow timer
        if (glowTimer >= 0f)
        {
            glowTimer += Time.deltaTime;
            float glowProgress = 1f - Mathf.Clamp01(glowTimer / glowDuration);
            SetCrossGlow(glowProgress);
            if (glowTimer >= glowDuration)
                glowTimer = -1f;
        }

        switch (currentState)
        {
            case State.WaitingForCabinet:
                break;
            case State.WallsWhisper:
                UpdateWallsWhisper();
                break;
            case State.RitualActive:
                UpdateRitualActive();
                break;
            case State.CrossFlipping:
                UpdateCrossFlipping();
                break;
            case State.AltarCall:
                UpdateAltarCall();
                break;
            case State.TextFadeIn:
                UpdateTextFadeIn();
                break;
            case State.Complete:
                break;
        }
    }

    // Called by CandleController.Flare() when cabinet hits table
    public void OnCabinetHit()
    {
        currentState = State.WallsWhisper;
        wallsTimer = 0f;
        firstWallsPlayed = false;
        Debug.Log("[CrossRitual] Cabinet hit -> WallsWhisper (waiting " + wallsRememberDelay + "s)");
    }

    // --- WallsWhisper: play "wallsremembercrosswaits" after delay, repeat every 30s ---
    void UpdateWallsWhisper()
    {
        wallsTimer += Time.deltaTime;

        // First play after delay
        if (!firstWallsPlayed && wallsTimer >= wallsRememberDelay)
        {
            firstWallsPlayed = true;
            wallsIntervalTimer = 0f;
            PlayRitualSound(wallsRememberSound);
            Debug.Log("[CrossRitual] First wallsRemember played");
        }

        // Repeat every interval
        if (firstWallsPlayed)
        {
            wallsIntervalTimer += Time.deltaTime;
            if (wallsIntervalTimer >= wallsRememberInterval)
            {
                wallsIntervalTimer = 0f;
                PlayRitualSound(wallsRememberSound);
            }
        }

        // Check if player is looking at the cross — transition to ritual
        if (firstWallsPlayed && IsPlayerLookingAtCross())
        {
            currentState = State.RitualActive;
            currentStep = 0;
            thumbstickReleased = true;
            Debug.Log("[CrossRitual] Player looking at cross -> RitualActive");
        }
    }

    // --- RitualActive: accept thumbstick input ---
    void UpdateRitualActive()
    {
        // If player looks away from cross, keep state but don't accept input
        if (!IsPlayerLookingAtCross())
            return;

        // Read left thumbstick
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);

        // Also check right thumbstick
        Vector2 stickR = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
        if (stickR.magnitude > stick.magnitude)
            stick = stickR;

        // Wait for thumbstick to return to center between inputs
        if (!thumbstickReleased)
        {
            if (stick.magnitude < 0.3f)
                thumbstickReleased = true;
            return;
        }

        // Check for directional input
        if (stick.magnitude >= thumbstickThreshold)
        {
            Dir inputDir = GetDirection(stick);
            thumbstickReleased = false;

            if (inputDir == correctSequence[currentStep])
            {
                // Correct input
                currentStep++;
                glowTimer = 0f; // Trigger red glow
                OVRInput.SetControllerVibration(0.5f, 0.5f, OVRInput.Controller.LTouch);
                OVRInput.SetControllerVibration(0.5f, 0.5f, OVRInput.Controller.RTouch);

                Debug.Log("[CrossRitual] Correct step " + currentStep + "/5");

                // Brief vibration then stop
                Invoke("StopVibration", 0.2f);

                if (currentStep >= correctSequence.Length)
                {
                    // All correct — start cross flip
                    Debug.Log("[CrossRitual] Sequence complete -> CrossFlipping");
                    StartCrossFlip();
                }
            }
            else
            {
                // Wrong input — reset
                currentStep = 0;
                Debug.Log("[CrossRitual] Wrong input! Reset to 0");

                // Error feedback — quick double vibration
                OVRInput.SetControllerVibration(1f, 1f, OVRInput.Controller.LTouch);
                OVRInput.SetControllerVibration(1f, 1f, OVRInput.Controller.RTouch);
                Invoke("StopVibration", 0.3f);
            }
        }
    }

    void StopVibration()
    {
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }

    Dir GetDirection(Vector2 stick)
    {
        // Determine which direction is dominant
        if (Mathf.Abs(stick.y) > Mathf.Abs(stick.x))
            return stick.y > 0 ? Dir.Up : Dir.Down;
        else
            return stick.x > 0 ? Dir.Right : Dir.Left;
    }

    // --- Cross Flip ---
    void StartCrossFlip()
    {
        currentState = State.CrossFlipping;
        flipTimer = 0f;

        if (cross != null)
        {
            // Create hinge at the bottom of the cross
            Renderer[] renderers = cross.GetComponentsInChildren<Renderer>(true);
            float minY = cross.transform.position.y;
            foreach (Renderer r in renderers)
            {
                if (r.bounds.min.y < minY)
                    minY = r.bounds.min.y;
            }

            crossHinge = new GameObject("CrossHinge");
            crossHinge.transform.position = new Vector3(cross.transform.position.x, minY, cross.transform.position.z);
            crossHinge.transform.rotation = Quaternion.identity;

            cross.transform.SetParent(crossHinge.transform);

            flipStartRotation = crossHinge.transform.rotation;
            // Rotate 180 degrees on X axis for clockwise flip (top toward player)
            flipEndRotation = flipStartRotation * Quaternion.Euler(180f, 0f, 0f);
        }
    }

    void UpdateCrossFlipping()
    {
        if (crossHinge == null) return;

        flipTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(flipTimer / flipDuration);

        // Smooth ease in/out
        float eased = progress * progress * (3f - 2f * progress);
        crossHinge.transform.rotation = Quaternion.Slerp(flipStartRotation, flipEndRotation, eased);

        if (progress >= 1f)
        {
            crossHinge.transform.rotation = flipEndRotation;
            currentState = State.AltarCall;
            altarCallTimer = 0f;

            // Play first altar call immediately
            PlayRitualSound(altarCallSound);

            Debug.Log("[CrossRitual] Cross flipped -> AltarCall");
        }
    }

    // --- AltarCall: play sound every 10s until player approaches altar ---
    void UpdateAltarCall()
    {
        altarCallTimer += Time.deltaTime;

        if (altarCallTimer >= altarCallInterval)
        {
            altarCallTimer = 0f;
            PlayRitualSound(altarCallSound);
        }

        // Check player distance to altar (XZ only — table Y is at its base, not surface)
        if (ovrRig != null && table != null)
        {
            Vector3 playerPos = ovrRig.centerEyeAnchor.position;
            Vector3 tablePos = table.position;
            float dist = Vector2.Distance(
                new Vector2(playerPos.x, playerPos.z),
                new Vector2(tablePos.x, tablePos.z));
            if (dist < altarApproachDistance)
            {
                // Stop altar call, show text
                if (ritualAudioSource.isPlaying)
                    ritualAudioSource.Stop();

                currentState = State.TextFadeIn;
                textFadeTimer = 0f;

                if (breathText != null)
                {
                    breathText.SetActive(true);
                    if (tmpText != null)
                    {
                        Color c = textOriginalColor;
                        c.a = 0f;
                        tmpText.color = c;
                    }
                }

                Debug.Log("[CrossRitual] Player at altar -> TextFadeIn");
            }
        }
    }

    // --- TextFadeIn ---
    void UpdateTextFadeIn()
    {
        textFadeTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(textFadeTimer / textFadeDuration);

        if (tmpText != null)
        {
            Color c = textOriginalColor;
            c.a = progress;
            tmpText.color = c;
        }

        if (progress >= 1f)
        {
            currentState = State.Complete;
            Debug.Log("[CrossRitual] Text fully visible -> Complete (B button ready)");
        }
    }

    // Public check for CandleController
    public bool IsComplete()
    {
        return currentState == State.Complete;
    }

    // --- Helpers ---
    bool IsPlayerLookingAtCross()
    {
        if (ovrRig == null || cross == null) return false;

        Transform head = ovrRig.centerEyeAnchor;
        Vector3 dirToCross = (cross.transform.position - head.position).normalized;
        float angle = Vector3.Angle(head.forward, dirToCross);
        return angle < gazeAngle;
    }

    void SetCrossGlow(float intensity)
    {
        if (crossGlowLight != null)
            crossGlowLight.intensity = intensity * glowMaxIntensity;
    }

    void PlayRitualSound(string soundName)
    {
        if (ritualAudioSource != null && ritualAudioSource.isPlaying) return;
        if (SoundManager.Instance == null) return;

        SoundManager.Sound s = System.Array.Find(SoundManager.Instance.sounds, x => x.name == soundName);
        if (s == null || s.clip == null) return;

        ritualAudioSource.clip = s.clip;
        ritualAudioSource.volume = s.volume;
        ritualAudioSource.Play();
    }
}
