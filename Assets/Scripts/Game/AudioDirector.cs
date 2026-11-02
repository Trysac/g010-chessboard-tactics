using UnityEngine;

namespace ChessboardTactics.Game
{
    /// <summary>Cross-fades between the camp and battle themes and plays one-shot effects.</summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        const string MutedKey = "chessboard.muted";
        const float MusicVolume = 0.35f;

        AudioSource music;
        AudioSource effects;
        AudioClip wanted;
        float fade = 1f;
        bool ducked;

        public bool Muted { get; private set; }

        public void Init()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            Muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            AudioListener.volume = Muted ? 0f : 1f;
        }

        public void PlayMusic(AudioClip clip) => wanted = clip;

        public void SetDucked(bool value) => ducked = value;

        public void ToggleMute()
        {
            Muted = !Muted;
            AudioListener.volume = Muted ? 0f : 1f;
            PlayerPrefs.SetInt(MutedKey, Muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0.05f)
        {
            if (clip == null || effects == null)
            {
                return;
            }
            effects.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            effects.PlayOneShot(clip, volume);
        }

        void Update()
        {
            if (music == null)
            {
                return;
            }
            if (music.clip != wanted)
            {
                fade = Mathf.MoveTowards(fade, 0f, Time.unscaledDeltaTime * 2.5f);
                if (fade <= 0f || !music.isPlaying)
                {
                    music.clip = wanted;
                    if (wanted != null)
                    {
                        music.Play();
                    }
                }
            }
            else
            {
                fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime * 1.5f);
            }
            music.volume = MusicVolume * fade * (ducked ? 0.4f : 1f);
        }
    }
}
