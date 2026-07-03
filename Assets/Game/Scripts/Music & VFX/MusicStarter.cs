using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicStarter : MonoBehaviour
{
    [SerializeField] private float _startDelay = 0.5f;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        Invoke(nameof(PlayMusic), _startDelay);
    }

    private void PlayMusic()
    {
        if (_audioSource != null) _audioSource.Play();
    }
}