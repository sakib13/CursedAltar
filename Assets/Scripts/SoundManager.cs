using UnityEngine;
using System;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Background Music")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float musicVolume = 0.3f;

    [Header("Sound Effects")]
    public Sound[] sounds;

    private AudioSource musicSource;
    private AudioSource sfxSource;

    [Serializable]
    public class Sound
    {
        public string name;       // e.g. "doorSlam", "whisper", "scraping"
        public AudioClip clip;
        [Range(0f, 5f)] public float volume = 1f;
    }

    void Awake()
    {
        // Singleton so any script can access SoundManager.Instance
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        // Create two AudioSources: one for music, one for sound effects
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.volume = musicVolume;

        sfxSource = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        // Play background music if assigned
        if (backgroundMusic != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.Play();
        }
    }

    // Play a sound effect by name
    public void Play(string soundName)
    {
        Sound s = Array.Find(sounds, x => x.name == soundName);
        if (s == null)
        {
            Debug.LogWarning("Sound not found: " + soundName);
            return;
        }
        // Set AudioSource volume to amplify beyond 1 if needed
        sfxSource.volume = s.volume;
        sfxSource.PlayOneShot(s.clip, 1f);
    }

    // Play a sound at a specific position in 3D space
    public void PlayAtPosition(string soundName, Vector3 position)
    {
        Sound s = Array.Find(sounds, x => x.name == soundName);
        if (s == null)
        {
            Debug.LogWarning("Sound not found: " + soundName);
            return;
        }
        AudioSource.PlayClipAtPoint(s.clip, position, Mathf.Min(s.volume, 1f));
    }

    public void SetMusicVolume(float vol)
    {
        musicVolume = vol;
        if (musicSource != null)
            musicSource.volume = vol;
    }
}
