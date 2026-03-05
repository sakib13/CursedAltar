using UnityEngine;

public class CandleController : MonoBehaviour
{
    [Header("Light")]
    public Light candleLight;
    public ParticleSystem candleFlame;

    [Header("Audio")]
    public AudioSource whisperAudio;

    [Header("References")]
    public LanternController lantern;
    public GameObject skull;
    public ESP32Connection esp32;

    [Header("Distance Thresholds (cm)")]
    public float farDistance = 150f;
    public float mediumDistance = 100f;
    public float closeDistance = 50f;

    [Header("Intensity Settings")]
    public float dimIntensity = 0.5f;
    public float mediumIntensity = 1.5f;
    public float brightIntensity = 3.0f;
    public float reigniteIntensity = 2.0f;

    [Header("Blackout Settings")]
    public float blackoutDuration = 2f;

    private bool skullActivated = false;
    private bool inBlackout = false;
    private float blackoutTimer = 0f;
    private float currentDistance = 999f;

    void Update()
    {
        // Get distance from ESP32 connection
        if (esp32 != null)
            currentDistance = esp32.GetDistance();

        // Handle blackout sequence
        if (inBlackout)
        {
            blackoutTimer += Time.deltaTime;
            if (blackoutTimer >= blackoutDuration)
            {
                inBlackout = false;
                ActivateSkull();
            }
            return;
        }

        if (skullActivated) return;

        // Map distance to candle behavior
        if (currentDistance > farDistance)
        {
            // Far — dim gentle flicker
            float flicker = 1f - (Mathf.PerlinNoise(Time.time * 3f, 0f) * 0.2f);
            candleLight.intensity = dimIntensity * flicker;
            SetWhisperVolume(0f);
            if (lantern != null) lantern.SetFlickering(false);
        }
        else if (currentDistance > mediumDistance)
        {
            // Medium — brighter, pulsing, whispers start
            float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.3f;
            candleLight.intensity = mediumIntensity * pulse;
            SetWhisperVolume(0.3f);
            if (lantern != null) lantern.SetFlickering(false);
        }
        else if (currentDistance > closeDistance)
        {
            // Close — bright, fast pulse, whispers loud, lantern flickers
            float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.5f;
            candleLight.intensity = brightIntensity * pulse;
            SetWhisperVolume(0.7f);
            if (lantern != null) lantern.SetFlickering(true);
        }
        else
        {
            // Very close — trigger blackout
            StartBlackout();
        }
    }

    void StartBlackout()
    {
        inBlackout = true;
        blackoutTimer = 0f;

        // Kill all light
        candleLight.intensity = 0f;
        if (candleFlame != null)
            candleFlame.Stop();

        SetWhisperVolume(0f);

        // Kill lantern light too
        if (lantern != null)
            lantern.SetFlickering(false);
    }

    void ActivateSkull()
    {
        skullActivated = true;

        // Reignite candle
        candleLight.intensity = reigniteIntensity;
        if (candleFlame != null)
            candleFlame.Play();

        // Show the skull
        if (skull != null)
            skull.SetActive(true);

        // Silence whispers
        SetWhisperVolume(0f);

        // Stop lantern flicker
        if (lantern != null)
            lantern.SetFlickering(false);
    }

    void SetWhisperVolume(float volume)
    {
        if (whisperAudio != null)
        {
            whisperAudio.volume = volume;
            if (volume > 0f && !whisperAudio.isPlaying)
                whisperAudio.Play();
            else if (volume <= 0f && whisperAudio.isPlaying)
                whisperAudio.Stop();
        }
    }

    // Called by CabinetController to briefly flare the candle
    public void Flare()
    {
        if (!skullActivated && candleLight != null)
        {
            StartCoroutine(FlareRoutine());
        }
    }

    System.Collections.IEnumerator FlareRoutine()
    {
        float original = candleLight.intensity;
        candleLight.intensity = brightIntensity;
        yield return new WaitForSeconds(0.5f);
        candleLight.intensity = original;
    }
}
