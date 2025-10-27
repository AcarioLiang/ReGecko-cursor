using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [System.Serializable]
    public class Sound
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)]
        public float volume = 1f;
        [Range(0.1f, 3f)]
        public float pitch = 1f;
        public bool loop = false;
        public AudioSource source;
    }
    
    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;
    
    [Header("Sound Effects")]
    public Sound[] sounds;
    
    [Header("Music")]
    public AudioClip[] musicTracks;
    public float musicFadeTime = 1f;
    
    private Dictionary<string, Sound> soundDictionary;
    private int currentMusicIndex = 0;
    private bool isMusicFading = false;
    
    // Singleton pattern
    public static AudioManager Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        InitializeAudio();
    }
    
    void Start()
    {
        // Load settings
        LoadAudioSettings();
        
        // Start playing music
        if (musicTracks.Length > 0)
        {
            PlayMusic(0);
        }
    }
    
    void InitializeAudio()
    {
        // Create audio sources if they don't exist
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }
        
        // Initialize sound dictionary
        soundDictionary = new Dictionary<string, Sound>();
        
        foreach (Sound sound in sounds)
        {
            if (sound.source == null)
            {
                sound.source = gameObject.AddComponent<AudioSource>();
            }
            
            sound.source.clip = sound.clip;
            sound.source.volume = sound.volume;
            sound.source.pitch = sound.pitch;
            sound.source.loop = sound.loop;
            
            soundDictionary[sound.name] = sound;
        }
    }
    
    public void PlaySound(string name)
    {
        if (soundDictionary.ContainsKey(name))
        {
            Sound sound = soundDictionary[name];
            sound.source.Play();
        }
        else
        {
            Debug.LogWarning($"Sound {name} not found!");
        }
    }
    
    public void PlaySoundAtPosition(string name, Vector3 position)
    {
        if (soundDictionary.ContainsKey(name))
        {
            Sound sound = soundDictionary[name];
            AudioSource.PlayClipAtPoint(sound.clip, position, sound.volume);
        }
        else
        {
            Debug.LogWarning($"Sound {name} not found!");
        }
    }
    
    public void StopSound(string name)
    {
        if (soundDictionary.ContainsKey(name))
        {
            Sound sound = soundDictionary[name];
            sound.source.Stop();
        }
    }
    
    public void StopAllSounds()
    {
        foreach (Sound sound in sounds)
        {
            if (sound.source != null)
            {
                sound.source.Stop();
            }
        }
    }
    
    public void PlayMusic(int trackIndex)
    {
        if (trackIndex < 0 || trackIndex >= musicTracks.Length)
        {
            Debug.LogWarning($"Music track index {trackIndex} out of range!");
            return;
        }
        
        if (currentMusicIndex == trackIndex && musicSource.isPlaying)
            return;
        
        currentMusicIndex = trackIndex;
        StartCoroutine(FadeMusic(musicTracks[trackIndex]));
    }
    
    public void PlayNextMusic()
    {
        int nextIndex = (currentMusicIndex + 1) % musicTracks.Length;
        PlayMusic(nextIndex);
    }
    
    public void PlayPreviousMusic()
    {
        int prevIndex = (currentMusicIndex - 1 + musicTracks.Length) % musicTracks.Length;
        PlayMusic(prevIndex);
    }
    
    public void StopMusic()
    {
        StartCoroutine(FadeMusic(null));
    }
    
    System.Collections.IEnumerator FadeMusic(AudioClip newClip)
    {
        isMusicFading = true;
        
        // Fade out current music
        if (musicSource.isPlaying)
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;
            
            while (elapsed < musicFadeTime)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeTime);
                yield return null;
            }
        }
        
        // Change clip if provided
        if (newClip != null)
        {
            musicSource.clip = newClip;
            musicSource.Play();
            
            // Fade in new music
            float elapsed = 0f;
            float targetVolume = GameSettings.Instance.GetMusicVolume();
            
            while (elapsed < musicFadeTime)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / musicFadeTime);
                yield return null;
            }
        }
        
        isMusicFading = false;
    }
    
    public void SetMusicVolume(float volume)
    {
        if (!isMusicFading)
        {
            musicSource.volume = volume;
        }
    }
    
    public void SetSFXVolume(float volume)
    {
        sfxSource.volume = volume;
        
        foreach (Sound sound in sounds)
        {
            if (sound.source != null)
            {
                sound.source.volume = sound.volume * volume;
            }
        }
    }
    
    public void SetMasterVolume(float volume)
    {
        musicSource.volume = volume * GameSettings.Instance.GetMusicVolume();
        sfxSource.volume = volume * GameSettings.Instance.GetSFXVolume();
        
        foreach (Sound sound in sounds)
        {
            if (sound.source != null)
            {
                sound.source.volume = sound.volume * volume * GameSettings.Instance.GetSFXVolume();
            }
        }
    }
    
    public void PauseMusic()
    {
        if (musicSource.isPlaying)
        {
            musicSource.Pause();
        }
    }
    
    public void ResumeMusic()
    {
        if (musicSource.clip != null && !musicSource.isPlaying)
        {
            musicSource.UnPause();
        }
    }
    
    public void SetMusicPitch(float pitch)
    {
        musicSource.pitch = pitch;
    }
    
    public void SetSFXPitch(float pitch)
    {
        sfxSource.pitch = pitch;
        
        foreach (Sound sound in sounds)
        {
            if (sound.source != null)
            {
                sound.source.pitch = sound.pitch * pitch;
            }
        }
    }
    
    void LoadAudioSettings()
    {
        float masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
        float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        
        SetMasterVolume(masterVolume);
        SetMusicVolume(musicVolume);
        SetSFXVolume(sfxVolume);
    }
    
    // Method to play random sound from a category
    public void PlayRandomSound(string[] soundNames)
    {
        if (soundNames.Length > 0)
        {
            string randomSound = soundNames[Random.Range(0, soundNames.Length)];
            PlaySound(randomSound);
        }
    }
    
    // Method to play sound with custom volume and pitch
    public void PlaySoundCustom(string name, float volume, float pitch)
    {
        if (soundDictionary.ContainsKey(name))
        {
            Sound sound = soundDictionary[name];
            sound.source.volume = volume;
            sound.source.pitch = pitch;
            sound.source.Play();
        }
    }
    
    // Method to check if a sound is playing
    public bool IsSoundPlaying(string name)
    {
        if (soundDictionary.ContainsKey(name))
        {
            return soundDictionary[name].source.isPlaying;
        }
        return false;
    }
    
    // Method to get current music track info
    public string GetCurrentMusicName()
    {
        if (currentMusicIndex >= 0 && currentMusicIndex < musicTracks.Length)
        {
            return musicTracks[currentMusicIndex].name;
        }
        return "No Music";
    }
    
    // Method to get music progress
    public float GetMusicProgress()
    {
        if (musicSource.clip != null && musicSource.isPlaying)
        {
            return musicSource.time / musicSource.clip.length;
        }
        return 0f;
    }
}