using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioSource music;
    [SerializeField] private AudioSource sfx;
   public AudioClip backgroundMusic;
   [Tooltip("WORLDMUSIC: in a level with tori gates (changeWorld), the music for the winter spirit world (changeWorld.lWorld false); " +
            "Background Music plays in the living world. Empty = Background Music everywhere. Set to a PLACEHOLDER track for now.")]
   public AudioClip spiritWorldMusic;
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
   public AudioClip bossSlamSound; // SFX: a boss lands from a charge, leap or slam (falls back to bounceSound)
   public AudioClip bossDefeatSound; // SFX: a boss vanishes when beaten (falls back to powerUpSound)
   public AudioClip cloudBounceSound; // SFX: soft springy bounce on the Green Zone clouds (falls back to bounceSound)
   public AudioClip samuraiSlashSound; // SLASH: the samurai monkey's dashing sword slice (falls back to enemyDashDefeatSound)
   public AudioClip chestTrapSound; // CHEST: a trapped chest going off like a firework (falls back to bossSlamSound)
   public AudioClip chestFuseSound; // CHEST: the fuse fizzing in the moment before a trapped chest goes off
   public AudioClip chestRattleSound; // CHEST: the faint rattle of a trapped chest while Harvey is nearby
   private int lastFootstep = -1; // SFX:

   public AudioClip BossSlamSound => bossSlamSound != null ? bossSlamSound : bounceSound; // SFX:
   public AudioClip BossDefeatSound => bossDefeatSound != null ? bossDefeatSound : powerUpSound; // SFX:
   public AudioClip CloudBounceSound => cloudBounceSound != null ? cloudBounceSound : bounceSound; // SFX:
   public AudioClip SamuraiSlashSound => samuraiSlashSound != null ? samuraiSlashSound : enemyDashDefeatSound; // SLASH:
   public AudioClip ChestTrapSound => chestTrapSound != null ? chestTrapSound : BossSlamSound; // CHEST:
   public UnityEngine.Audio.AudioMixerGroup SfxGroup => sfx != null ? sfx.outputAudioMixerGroup : null; // BOULDER: for sounds that need their own looping source

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
    [Range(0f, 1f)] public float cloudBounceVolume = 1f; // MIX:
    [Range(0f, 1f)] public float chestTrapVolume = 1f; // MIX: CHEST: the trap bang and its fuse
    [Range(0f, 1f)] public float chestRattleVolume = 1f; // MIX: CHEST: keep it faint; it's a hint, not an alarm

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
        pitchedShots = gameObject.AddComponent<AudioSource>(); // CLASH: made here like oneShots (adding one mid-play warns)
        pitchedShots.playOnAwake = false;
        pitchedShots.loop = false;
        pitchedShots.outputAudioMixerGroup = sfx.outputAudioMixerGroup;
        pitchedShots.spatialBlend = sfx.spatialBlend;
        pitchedShots.priority = sfx.priority;
        // SFX: a few voices for coins, so quick pickups overlap and each can have its own pitch
        coinVoices = new AudioSource[4];
        for (int i = 0; i < coinVoices.Length; i++)
        {
            AudioSource v = gameObject.AddComponent<AudioSource>();
            v.playOnAwake = false;
            v.loop = false;
            v.outputAudioMixerGroup = sfx.outputAudioMixerGroup;
            v.spatialBlend = sfx.spatialBlend;
            v.priority = sfx.priority;
            coinVoices[i] = v;
        }
    }

    // SFX: coins collected in quick succession climb a bright major scale (and restart after a short gap),
    // so a run of pickups sounds like a rising chime rather than the same blip stacking up
    private AudioSource[] coinVoices;
    private int coinVoice;
    private int coinStep;
    private float lastCoinTime = -10f;
    private static readonly float[] CoinSteps = { 0f, 2f, 4f, 5f, 7f, 9f, 12f };

    public void PlayCoin()
    {
        if (coinSound == null || coinVoices == null) return;
        coinStep = Time.unscaledTime - lastCoinTime > 0.45f ? 0 : Mathf.Min(coinStep + 1, CoinSteps.Length - 1);
        lastCoinTime = Time.unscaledTime;
        AudioSource v = coinVoices[coinVoice];
        coinVoice = (coinVoice + 1) % coinVoices.Length;
        v.pitch = Mathf.Pow(2f, CoinSteps[coinStep] / 12f);
        v.PlayOneShot(coinSound, sfxBaseVolume * coinVolume);
    }

    // Start is called before the first frame update
    void Start()
    {
        music.volume = musicVolume; // MIX:
        if (musicRequested) return; // ZONEMUSIC: a script already picked this scene's music (the zone map)
        music.clip = WorldTrack(); // WORLDMUSIC: the world the level starts in
        music.Play();
        shownWorld = AnalogueWay.changeWorld.lWorld;
    }

    // WORLDMUSIC (2026-10-20): crossfade when Harvey goes through a tori gate into the other world
    private bool shownWorld;
    private bool? hasWorlds;

    private AudioClip WorldTrack()
    {
        if (spiritWorldMusic == null) return backgroundMusic;
        if (hasWorlds == null) hasWorlds = FindFirstObjectByType<AnalogueWay.changeWorld>(FindObjectsInactive.Include) != null;
        return hasWorlds == true && !AnalogueWay.changeWorld.lWorld ? spiritWorldMusic : backgroundMusic;
    }

    private void Update()
    {
        if (spiritWorldMusic == null || shownWorld == AnalogueWay.changeWorld.lWorld) return;
        shownWorld = AnalogueWay.changeWorld.lWorld;
        PlayMusic(WorldTrack(), 1.2f);
    }

    // ZONEMUSIC: switches the music, crossfading over fadeTime (0 = cut). The same clip already playing carries on.
    private bool musicRequested;
    private AudioSource musicFade; // the outgoing track while it fades out
    private Coroutine musicFadeCo;

    public void PlayMusic(AudioClip clip, float fadeTime = 1f)
    {
        musicRequested = true;
        if (clip == null || (music.clip == clip && music.isPlaying)) return;
        if (musicFadeCo != null) StopCoroutine(musicFadeCo);
        if (musicFade == null)
        {
            musicFade = gameObject.AddComponent<AudioSource>();
            musicFade.playOnAwake = false;
            musicFade.outputAudioMixerGroup = music.outputAudioMixerGroup;
            musicFade.spatialBlend = music.spatialBlend;
            musicFade.priority = music.priority;
        }
        // the current track moves to the fade source and the new one starts on the main source
        musicFade.Stop();
        if (music.isPlaying && fadeTime > 0f)
        {
            musicFade.clip = music.clip;
            musicFade.loop = music.loop;
            musicFade.volume = music.volume;
            musicFade.timeSamples = music.timeSamples;
            musicFade.Play();
        }
        music.clip = clip;
        music.loop = true;
        music.volume = fadeTime > 0f ? 0f : musicVolume;
        music.Play();
        if (fadeTime > 0f) musicFadeCo = StartCoroutine(CrossfadeMusic(fadeTime));
    }

    private System.Collections.IEnumerator CrossfadeMusic(float fadeTime)
    {
        float outStart = musicFade.volume;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            float k = t / fadeTime;
            music.volume = musicVolume * k;
            musicFade.volume = outStart * (1f - k);
            yield return null;
        }
        music.volume = musicVolume;
        musicFade.Stop();
        musicFadeCo = null;
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

    // CLASH: a one-shot at another pitch (its own voice, so the shared one-shots keep pitch 1)
    private AudioSource pitchedShots;
    public void PlaySfxOneShotPitched(AudioClip sound, float pitch, float volume = 1f)
    {
        if (sound == null || pitchedShots == null) return;
        pitchedShots.pitch = pitch;
        pitchedShots.PlayOneShot(sound, sfxBaseVolume * volume * VolumeFor(sound));
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
        if (sound == cloudBounceSound) return cloudBounceVolume;
        if (sound == chestTrapSound || sound == chestFuseSound) return chestTrapVolume; // CHEST:
        if (sound == chestRattleSound) return chestRattleVolume; // CHEST:
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
