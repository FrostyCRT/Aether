using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionFader : MonoBehaviour
{
    public static SceneTransitionFader Instance { get; private set; }

    [SerializeField] private CanvasGroup _canvasGroup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Des qu'une nouvelle scene termine de charger, on efface le fondu noir
        // qui a servi a masquer la coupure - reutilisable pour n'importe quelle
        // future transition de scene dans le jeu, pas seulement celle-ci.
        if (_canvasGroup != null && _canvasGroup.alpha > 0f)
            StartCoroutine(Fade(1f, 0f, 0.6f));
    }

    public IEnumerator FadeOut(float duration)
    {
        yield return Fade(0f, 1f, duration);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (_canvasGroup == null) yield break;

        _canvasGroup.blocksRaycasts = to > 0f;
        float elapsed = 0f;
        _canvasGroup.alpha = from;

        while (elapsed < duration)
        {
            // unscaledDeltaTime : reste correct meme si Time.timeScale est deja
            // modifie ailleurs (ex. le hitstop de l'ultime dans CrystalSystem).
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = to;
    }
}