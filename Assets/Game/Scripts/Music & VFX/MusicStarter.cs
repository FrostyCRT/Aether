using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicStarter : MonoBehaviour
{
    public static MusicStarter Instance { get; private set; }

    [SerializeField] private float _startDelay = 0.5f;
    [SerializeField] private float _switchFadeDuration = 1f;

    // AJOUTE - petit delai avant meme de commencer le fondu de transition, pour
    // eviter que le changement de musique semble se declencher pile au moment
    // du trigger (spawn/mort du boss) - laisse un instant de respiration avant
    // que quoi que ce soit ne bouge au niveau du son.
    [SerializeField] private float _switchDelay = 0.5f;

    private AudioSource _audioSource;
    private AudioClip _defaultClip;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = GetComponent<AudioSource>();
        _defaultClip = _audioSource.clip; // memorise la musique normale assignee dans l'Inspector
    }

    private void Start()
    {
        Invoke(nameof(PlayMusic), _startDelay);
    }

    private void PlayMusic()
    {
        if (_audioSource != null) _audioSource.Play();
    }

    public void SwitchTo(AudioClip newClip)
    {
        if (newClip == null || _audioSource.clip == newClip) return;
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(SwitchRoutine(newClip));
    }

    public void RevertToDefault()
    {
        SwitchTo(_defaultClip);
    }

    public void FadeOutAndStop(float duration)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator SwitchRoutine(AudioClip newClip)
    {
        // AJOUTE - WaitForSecondsRealtime plutot qu'un timer classique : reste
        // correct meme si Time.timeScale est modifie ailleurs au meme moment
        // (ex. le hitstop de l'ultime dans CrystalSystem).
        yield return new WaitForSecondsRealtime(_switchDelay);

        float startVolume = _audioSource.volume;
        float elapsed = 0f;
        float halfDuration = _switchFadeDuration * 0.5f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / halfDuration);
            yield return null;
        }

        _audioSource.clip = newClip;
        // AJOUTE - force le loop explicitement au moment du switch, plutot que
        // de dependre de l'etat du composant dans l'Inspector au moment ou tu
        // lis ce commentaire - garanti quel que soit le prefab/la config.
        _audioSource.loop = true;
        _audioSource.Play();

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _audioSource.volume = Mathf.Lerp(0f, startVolume, elapsed / halfDuration);
            yield return null;
        }

        _audioSource.volume = startVolume;
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startVolume = _audioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        _audioSource.volume = 0f;
        _audioSource.Stop();
    }
}