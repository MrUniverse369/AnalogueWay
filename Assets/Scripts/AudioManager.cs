using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioSource music;
    [SerializeField] private AudioSource sfx;
   public AudioClip backgroundMusic;
   public AudioClip coinSound;
   public AudioClip winSound;
   public AudioClip jumpSound;
   public AudioClip powerUpSound;
   public AudioClip bounceSound;
   public AudioClip gameOverSound;
   public AudioClip worldSwitchToLivingSound; // SFX: tori gate into the living world (leave empty for no sound)
   public AudioClip worldSwitchToSpiritSound; // SFX: tori gate into the spirit world (falls back to the living one)
   public AudioClip[] footstepSounds; // SFX: played in random order, never the same one twice in a row
   [Range(0f, 1f)] public float footstepVolume = 0.6f; // SFX:
   public AudioClip enemyStompDefeatSound; // SFX:
   public AudioClip enemyDashDefeatSound; // SFX:
   public AudioClip checkpointSound; // SFX:
   public AudioClip chestOpenSound; // SFX:
   public AudioClip dragonSummonSound; // SFX:
   public AudioClip playerHurtSound; // SFX: a life lost with lives remaining (losing the last life plays gameOverSound)
   private int lastFootstep = -1; // SFX:

    // MIX: one place to balance everything. 1 = the clip as recorded.
    // Defaults were measured so each sound sits at a set level against the music at musicVolume 0.35:
    // footsteps well under the music, jump/coin slightly under, defeats/chest/checkpoint level with it,
    // dragon/game over/win just above. The generated clips (tori gates, defeats, etc.) are already
    // rendered at their level, so their volumes start at 1.
    [Header("Mix")]
    [Range(0f, 1f)] public float musicVolume = 0.35f; // MIX: overrides the music AudioSource volume (was 0.2)
    [Range(0f, 1f)] public float coinVolume = 0.18f; // MIX:
    [Range(0f, 1f)] public float winVolume = 0.54f; // MIX:
    [Range(0f, 1f)] public float jumpVolume = 0.82f; // MIX:
    [Range(0f, 1f)] public float powerUpVolume = 0.79f; // MIX:
    [Range(0f, 1f)] public float bounceVolume = 0.30f; // MIX:
    [Range(0f, 1f)] public float gameOverVolume = 1f; // MIX: the clip is quiet; 1 is as loud as it goes
    [Range(0f, 1f)] public float worldSwitchVolume = 1f; // MIX:
    [Range(0f, 1f)] public float enemyDefeatVolume = 1f; // MIX:
    [Range(0f, 1f)] public float checkpointVolume = 1f; // MIX:
    [Range(0f, 1f)] public float chestOpenVolume = 1f; // MIX:
    [Range(0f, 1f)] public float dragonSummonVolume = 1f; // MIX:
    [Range(0f, 1f)] public float playerHurtVolume = 1f; // MIX:

    // PERF: one shared reference instead of every script searching the scene by tag.
    private static AudioManager instance;
    public static AudioManager Instance
    {
        get
        {
            if (instance == null) instance = FindObjectOfType<AudioManager>();
            return instance;
        }
    }

    private AudioSource oneShots; // MIX: separate source so one-shot volumes don't affect PlaySfx (and vice versa)
    private float sfxBaseVolume = 1f; // MIX:

    // MIX:
    private void Awake()
    {
        instance = this; // PERF:
        sfxBaseVolume = sfx.volume;
        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        oneShots.loop = false;
        oneShots.outputAudioMixerGroup = sfx.outputAudioMixerGroup;
        oneShots.spatialBlend = sfx.spatialBlend;
        oneShots.priority = sfx.priority;
    }

    // Start is called before the first frame update
    void Start()
    {
        music.volume = musicVolume; // MIX:
        music.clip = backgroundMusic;
        music.Play();
    }

    public void PlaySfx(AudioClip sound)
    {
        sfx.volume = sfxBaseVolume * VolumeFor(sound); // MIX:
        sfx.clip = sound;
        sfx.Play();
    }

    // SFX: plays on top of other sounds instead of replacing them (PlaySfx swaps the clip and restarts)
    public void PlaySfxOneShot(AudioClip sound, float volume = 1f)
    {
        if (sound != null) oneShots.PlayOneShot(sound, sfxBaseVolume * volume * VolumeFor(sound)); // MIX:
    }

    // MIX: the volume for a clip based on which slot it is in (clips in no slot, like talisman sounds, play at 1)
    public float VolumeFor(AudioClip sound)
    {
        if (sound == null) return 1f;
        if (sound == coinSound) return coinVolume;
        if (sound == winSound) return winVolume;
        if (sound == jumpSound) return jumpVolume;
        if (sound == powerUpSound) return powerUpVolume;
        if (sound == bounceSound) return bounceVolume;
        if (sound == gameOverSound) return gameOverVolume;
        if (sound == worldSwitchToLivingSound || sound == worldSwitchToSpiritSound) return worldSwitchVolume;
        if (sound == enemyStompDefeatSound || sound == enemyDashDefeatSound) return enemyDefeatVolume;
        if (sound == checkpointSound) return checkpointVolume;
        if (sound == chestOpenSound) return chestOpenVolume;
        if (sound == dragonSummonSound) return dragonSummonVolume;
        if (sound == playerHurtSound) return playerHurtVolume;
        return 1f;
    }

    // SFX:
    public void PlayWorldSwitch(bool toLivingWorld)
    {
        AudioClip clip = toLivingWorld || worldSwitchToSpiritSound == null ? worldSwitchToLivingSound : worldSwitchToSpiritSound;
        PlaySfxOneShot(clip);
    }

    // SFX:
    public void PlayFootstep()
    {
        if (footstepSounds == null || footstepSounds.Length == 0) return;
        int i = Random.Range(0, footstepSounds.Length);
        if (footstepSounds.Length > 1 && i == lastFootstep) i = (i + 1) % footstepSounds.Length;
        lastFootstep = i;
        PlaySfxOneShot(footstepSounds[i], footstepVolume);
    }
}
