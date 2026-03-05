using UnityEngine;

public class CabinetController : MonoBehaviour
{
    [Header("Movement")]
    public Transform slideTarget;
    public float slideDuration = 1f;

    [Header("Sound")]
    public AudioSource creakSound;

    [Header("Candle Flare")]
    public CandleController candle;

    private bool hasTriggered = false;
    private bool isSliding = false;
    private float slideTimer = 0f;
    private Vector3 startPosition;

    void Update()
    {
        if (isSliding)
        {
            slideTimer += Time.deltaTime;
            float progress = slideTimer / slideDuration;

            if (progress < 1f)
            {
                transform.position = Vector3.Lerp(startPosition, slideTarget.position, progress);
            }
            else
            {
                transform.position = slideTarget.position;
                isSliding = false;
            }
        }
    }

    // Called by InteractableUnityEventWrapper WhenSelect
    public void OnCabinetSelected()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        startPosition = transform.position;
        isSliding = true;

        if (creakSound != null)
            creakSound.Play();

        // Flare the candle to draw attention to the altar
        if (candle != null)
            candle.Flare();
    }
}
