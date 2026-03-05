using UnityEngine;
using Oculus.Interaction;

public class LanternController : MonoBehaviour
{
    // --- Assign these in the Inspector ---
    [Header("Light & Flame")]
    public Light lanternLight;
    public ParticleSystem lanternFlame;

    [Header("Scare Chain")]
    public DoorScare doorScare;
    public float doorScareDelay = 5f;

    [Header("Flicker Settings")]
    public float normalIntensity = 1.5f;
    public float flickerSpeed = 10f;
    public float flickerAmount = 0.3f;

    // --- Private state ---
    private bool isGrabbed = false;
    private bool doorScareTriggered = false;
    private float grabTimer = 0f;
    private bool isFlickering = false;
    private Grabbable grabbable;

    void Start()
    {
        // Find the Grabbable component on this object or its children
        grabbable = GetComponentInChildren<Grabbable>();

        // Make sure light and flame are off at start
        if (lanternLight != null)
            lanternLight.enabled = false;

        if (lanternFlame != null)
            lanternFlame.Stop();
    }

    void Update()
    {
        // Check if the lantern is being grabbed
        if (grabbable != null)
        {
            bool currentlyGrabbed = grabbable.SelectingPointsCount > 0;

            // Just grabbed — turn on the light
            if (currentlyGrabbed && !isGrabbed)
            {
                OnGrabbed();
            }

            // Just released — turn off the light
            if (!currentlyGrabbed && isGrabbed)
            {
                OnReleased();
            }

            isGrabbed = currentlyGrabbed;
        }

        // Count time after grab to trigger door scare
        if (isGrabbed && !doorScareTriggered)
        {
            grabTimer += Time.deltaTime;
            if (grabTimer >= doorScareDelay)
            {
                doorScareTriggered = true;
                if (doorScare != null)
                    doorScare.Trigger();
            }
        }

        // Handle light flickering
        if (isGrabbed && lanternLight != null)
        {
            if (isFlickering)
            {
                // Erratic flicker when close to altar
                float flicker = Mathf.PerlinNoise(Time.time * flickerSpeed * 2f, 0f);
                lanternLight.intensity = normalIntensity * flicker;
            }
            else
            {
                // Gentle natural flicker
                float flicker = 1f - (Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * flickerAmount);
                lanternLight.intensity = normalIntensity * flicker;
            }
        }
    }

    void OnGrabbed()
    {
        if (lanternLight != null)
            lanternLight.enabled = true;

        if (lanternFlame != null)
            lanternFlame.Play();
    }

    void OnReleased()
    {
        if (lanternLight != null)
            lanternLight.enabled = false;

        if (lanternFlame != null)
            lanternFlame.Stop();
    }

    // Called by CandleController when player is close to the altar
    public void SetFlickering(bool flicker)
    {
        isFlickering = flicker;
    }
}
