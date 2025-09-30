using UnityEngine;

namespace MORTIS.Runtime
{
    // MonoBehaviour utility for SFX/UI.
    // Attach an AudioListener on your Camera as usual.
    public class AudioBus : MonoBehaviour
    {
        [SerializeField] private AudioSource oneShotSource;

        void Awake()
        {
            if (!oneShotSource)
            {
                oneShotSource = gameObject.AddComponent<AudioSource>();
                oneShotSource.playOnAwake = false;
            }
        }

        public void PlayUI(AudioClip clip, float volume = 1f)
        {
            if (clip) oneShotSource.PlayOneShot(clip, volume);
        }

        public void PlayAt(AudioClip clip, Vector3 pos, float volume = 1f)
        {
            if (clip) AudioSource.PlayClipAtPoint(clip, pos, volume);
        }
    }
}
