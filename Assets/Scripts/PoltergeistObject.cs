using UnityEngine;

public class PoltergeistObject : MonoBehaviour
{
    [Header("Movement")]
    public Transform targetPosition;
    public float slideDuration = 2.5f;

    [Header("Sound")]
    public AudioSource scrapingSound;

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
                // Smooth slide from start to target
                transform.position = Vector3.Lerp(startPosition, targetPosition.position, progress);
            }
            else
            {
                transform.position = targetPosition.position;
                isSliding = false;
            }
        }
    }

    // Called by DoorScare after door slam
    public void Trigger()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        startPosition = transform.position;
        isSliding = true;

        if (scrapingSound != null)
            scrapingSound.Play();
    }
}
