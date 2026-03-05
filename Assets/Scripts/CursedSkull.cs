using UnityEngine;
using Oculus.Interaction;

public class CursedSkull : MonoBehaviour
{
    [Header("Table Reference")]
    public Transform table;
    public float placementDistance = 0.5f;
    public float placementHeight = 0.3f;

    [Header("Audio")]
    public AudioSource heartbeatAudio;
    public AudioSource sealSound;

    [Header("References")]
    public CandleController candle;
    public ESP32Connection esp32;
    public Light candleLight;

    [Header("End Sequence")]
    public float buzzerDuration = 1.5f;
    public float lightReturnDuration = 2f;

    private Grabbable grabbable;
    private bool isHeld = false;
    private bool curseSealed = false;
    private bool endSequenceStarted = false;
    private float endTimer = 0f;
    private float targetLightIntensity = 1f;

    void Start()
    {
        grabbable = GetComponentInChildren<Grabbable>();
    }

    void Update()
    {
        if (curseSealed)
        {
            RunEndSequence();
            return;
        }

        if (grabbable == null) return;

        bool currentlyHeld = grabbable.SelectingPointsCount > 0;

        // Just grabbed
        if (currentlyHeld && !isHeld)
        {
            isHeld = true;
            if (heartbeatAudio != null)
                heartbeatAudio.Play();
        }

        // Just released — check if near table
        if (!currentlyHeld && isHeld)
        {
            isHeld = false;
            if (heartbeatAudio != null)
                heartbeatAudio.Stop();

            if (IsNearTable())
            {
                SealTheCurse();
            }
        }

        // Pulse candle with heartbeat while held
        if (isHeld && candleLight != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.5f;
            candleLight.intensity = 2f * pulse;
        }
    }

    bool IsNearTable()
    {
        if (table == null) return false;

        // Check horizontal distance
        Vector3 skullPos = transform.position;
        Vector3 tablePos = table.position;
        float xzDistance = Vector2.Distance(
            new Vector2(skullPos.x, skullPos.z),
            new Vector2(tablePos.x, tablePos.z)
        );

        // Check height — skull should be near table surface
        float yDiff = Mathf.Abs(skullPos.y - tablePos.y);

        return xzDistance < placementDistance && yDiff < placementHeight;
    }

    void SealTheCurse()
    {
        curseSealed = true;
        endSequenceStarted = true;
        endTimer = 0f;

        // Fire the buzzer
        if (esp32 != null)
            esp32.SendBuzzerOn();

        // Play seal sound in VR
        if (sealSound != null)
            sealSound.Play();
    }

    void RunEndSequence()
    {
        endTimer += Time.deltaTime;

        // Stop buzzer after duration
        if (endTimer >= buzzerDuration && esp32 != null)
        {
            esp32.SendBuzzerOff();
            esp32 = null; // Only send once
        }

        // Gradually return lighting to normal
        if (candleLight != null)
        {
            float progress = Mathf.Clamp01(endTimer / lightReturnDuration);
            candleLight.intensity = Mathf.Lerp(2f, targetLightIntensity, progress);
        }
    }
}
