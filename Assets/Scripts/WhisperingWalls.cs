using UnityEngine;

public class WhisperingWalls : MonoBehaviour
{
    [Header("Whisper Clips (drag all 4)")]
    public AudioClip[] whisperClips;

    [Header("Settings")]
    [Range(0f, 2f)] public float volume = 1f;
    public float fadeOutDuration = 2f;

    private AudioSource audioSource;
    private bool isPlaying = false;
    private bool isFadingOut = false;
    private float fadeOutTimer = 0f;
    private float clipDuration = 0f;
    private float playTimer = 0f;

    // Shuffled order
    private int[] shuffledOrder;
    private int currentIndex = 0;

    void Start()
    {
        // 2D audio — no spatialization, plays equally in both ears
        // This creates the "coming from everywhere" surround effect
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // Fully 2D — no positional source
        audioSource.volume = volume;

        ShuffleOrder();
    }

    void Update()
    {
        if (whisperClips == null || whisperClips.Length == 0) return;

        // Grip button on either controller triggers whisper
        if (!isPlaying)
        {
            if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ||
                OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch))
            {
                PlayNextWhisper();
            }
        }

        // Track playback and start fade out near the end
        if (isPlaying)
        {
            playTimer += Time.deltaTime;

            // Start fading out before the clip ends
            float fadeStartTime = clipDuration - fadeOutDuration;
            if (!isFadingOut && playTimer >= fadeStartTime)
            {
                isFadingOut = true;
                fadeOutTimer = 0f;
            }

            if (isFadingOut)
            {
                fadeOutTimer += Time.deltaTime;
                float fadeProgress = Mathf.Clamp01(fadeOutTimer / fadeOutDuration);
                audioSource.volume = volume * (1f - fadeProgress);
            }

            // Clip finished
            if (playTimer >= clipDuration)
            {
                audioSource.Stop();
                audioSource.volume = volume;
                isPlaying = false;
                isFadingOut = false;
            }
        }
    }

    void PlayNextWhisper()
    {
        AudioClip clip = whisperClips[shuffledOrder[currentIndex]];
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.Play();

        clipDuration = clip.length;
        playTimer = 0f;
        isPlaying = true;
        isFadingOut = false;

        currentIndex++;
        if (currentIndex >= whisperClips.Length)
        {
            currentIndex = 0;
            ShuffleOrder();
        }
    }

    void ShuffleOrder()
    {
        shuffledOrder = new int[whisperClips.Length];
        for (int i = 0; i < shuffledOrder.Length; i++)
            shuffledOrder[i] = i;

        // Fisher-Yates shuffle
        for (int i = shuffledOrder.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = shuffledOrder[i];
            shuffledOrder[i] = shuffledOrder[j];
            shuffledOrder[j] = temp;
        }
    }
}
