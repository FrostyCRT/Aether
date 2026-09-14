using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionFader : MonoBehaviour
{
    public static SceneTransitionFader Instance { get; private set; }

    [SerializeField] private CanvasGroup _canvasGroup;

    // AJOUTE (2026-09-14) - duree du fondu utilise par LoadScene() pour les
    // transitions EN COURS DE JEU (Jouer/Abandonner/Recommencer/Retour menu) :
    // volontairement courte, juste assez pour masquer la coupure de scene sans
    // se sentir comme un ecran de chargement a part entiere (retour
    // utilisateur : "ça remet le loading screen pendant 1 sec, j'aime pas ça").
    [SerializeField] private float _quickTransitionFadeDuration = 0.25f;

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

    // AJOUTE (2026-09-14) - transition rapide utilisee par SceneLoader pour
    // toute navigation EN COURS DE JEU (pas le tout premier demarrage, qui
    // passe par LoadingScreen). Fondu noir bref, vrai chargement asynchrone
    // et liberation des assets de la scene quittee, le tout cache derriere le
    // fondu - le joueur voit juste une coupure fluide, jamais un "ecran de
    // chargement" a part entiere.
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        yield return Fade(0f, 1f, _quickTransitionFadeDuration);

        // Libere les assets (textures, materiaux...) qui n'appartenaient qu'a
        // la scene qu'on vient de masquer, AVANT de charger la suivante -
        // evite que les deux scenes cohabitent en memoire pendant le
        // chargement (source de hitch, surtout vers la scene Jeu, la plus
        // lourde).
        yield return Resources.UnloadUnusedAssets();

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
            yield return null;

        // Le fondu de retour est declenche automatiquement par OnSceneLoaded
        // ci-dessus des que la nouvelle scene termine de charger.
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