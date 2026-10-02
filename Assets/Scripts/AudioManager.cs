using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Müzik")]
    public AudioClip Music;
    [Range(0f, 1f)] public float MusicVolume = 0.3f;

    [Header("Efektler")]
    [Range(0f, 1f)] public float SfxVolume = 0.8f;
    public AudioClip IngredientAdded;
    public AudioClip CustomerArrived;
    public AudioClip Served;
    public AudioClip Wrong;
    public AudioClip Trashed;
    public AudioClip CustomerAngry;
    public AudioClip DayEnd;
    public AudioClip GameOver;
    public AudioClip Upgrade;
    public AudioClip Denied;

    AudioSource musicSource;
    AudioSource sfxSource;

    void Awake()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
    }

    void Start()
    {
        if (WrapManager.Instance != null) WrapManager.Instance.SoundRequested += OnSound;

        if (Music != null)
        {
            musicSource.clip = Music;
            musicSource.volume = MusicVolume;
            musicSource.Play();
        }
    }

    void OnDestroy()
    {
        if (WrapManager.Instance != null) WrapManager.Instance.SoundRequested -= OnSound;
    }

    void Update()
    {
        // Oyun çalışırken Inspector'dan müzik sesini ayarlayabil
        musicSource.volume = MusicVolume;
    }

    void OnSound(GameSound sound)
    {
        AudioClip clip = null;

        switch (sound)
        {
            case GameSound.IngredientAdded: clip = IngredientAdded; break;
            case GameSound.CustomerArrived: clip = CustomerArrived; break;
            case GameSound.Served: clip = Served; break;
            case GameSound.Wrong: clip = Wrong; break;
            case GameSound.Trashed: clip = Trashed; break;
            case GameSound.CustomerAngry: clip = CustomerAngry; break;
            case GameSound.DayEnd: clip = DayEnd; break;
            case GameSound.GameOver: clip = GameOver; break;
            case GameSound.Upgrade: clip = Upgrade; break;
            case GameSound.Denied: clip = Denied; break;
        }

        if (clip != null) sfxSource.PlayOneShot(clip, SfxVolume);
    }
}