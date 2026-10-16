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
   private int lastFootstep = -1; // SFX:
    
    // Start is called before the first frame update
    void Start()
    {
        music.clip = backgroundMusic;
        music.Play();
    }

    public void PlaySfx(AudioClip sound)
    {
        sfx.clip = sound;
        sfx.Play();
    }

    // SFX: plays on top of other sounds instead of replacing them (PlaySfx swaps the clip and restarts)
    public void PlaySfxOneShot(AudioClip sound, float volume = 1f)
    {
        if (sound != null) sfx.PlayOneShot(sound, volume);
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
