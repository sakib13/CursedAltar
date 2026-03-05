using UnityEngine;

public class DoorScare : MonoBehaviour
{
    [Header("Sound")]
    public AudioSource slamSound;

    [Header("Scare Chain")]
    public PoltergeistObject poltergeistStool;
    public float stoolDelay = 3f;

    [Header("Shake Settings")]
    public float shakeDuration = 0.5f;
    public float shakeAngle = 3f;

    private bool hasTriggered = false;
    private bool isShaking = false;
    private float shakeTimer = 0f;
    private Quaternion originalRotation;
    private bool waitingForStool = false;
    private float stoolTimer = 0f;

    void Start()
    {
        originalRotation = transform.localRotation;
    }

    void Update()
    {
        // Door shake animation
        if (isShaking)
        {
            shakeTimer += Time.deltaTime;
            if (shakeTimer < shakeDuration)
            {
                float angle = Mathf.Sin(shakeTimer * 40f) * shakeAngle;
                transform.localRotation = originalRotation * Quaternion.Euler(0f, angle, 0f);
            }
            else
            {
                transform.localRotation = originalRotation;
                isShaking = false;
                waitingForStool = true;
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

    // Called by LanternController after delay
    public void Trigger()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        if (slamSound != null)
            slamSound.Play();

        isShaking = true;
        shakeTimer = 0f;
    }
}
