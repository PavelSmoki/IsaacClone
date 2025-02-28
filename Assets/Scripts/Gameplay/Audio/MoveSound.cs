using UnityEngine;

namespace Game.Gameplay.Audio
{
    public class MoveSound : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClipBundle _moveClipBundle;

        public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;
        
        private AudioClip _previousClip;
        
        public void PlayMove() => Play(_moveClipBundle);
        

        private void Play(AudioClipBundle bundle)
        {
            if (_audioSource == null || bundle == null)
            {
                return;
            }

            if (bundle.HasAnyClip())
            {
                _audioSource.volume = Random.Range(0.95f, 1.05f);
                _audioSource.pitch = Random.Range(0.95f, 1.05f);
                _audioSource.clip = bundle.GetRandomClip(_previousClip);
                _previousClip = _audioSource.clip;
                _audioSource.Play();
            }
        }
    }
}