using System.Collections;
using System.Collections.Generic;
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
    
    // Start is called before the first frame update
    void Start()
    {
        music.clip = backgroundMusic;
        music.Play();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlaySfx(AudioClip sound)
    {
        sfx.clip = sound;
        sfx.Play();
    }
}
