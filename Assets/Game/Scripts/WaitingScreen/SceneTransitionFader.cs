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

    // AJOUTE (2026-09-15) - duree du fondu de retour a la fin d'une
    // transition rapide (le noir -> la nouvelle scene qui apparait).
    [SerializeField] private float _quickTransitionFadeInDuration = 0.3f;

    // AJOUTE (2026-09-15) - temps garanti d'ecran NOIR PLEIN entre le fondu
    // d'entree et le fondu de sortie, meme si le chargement lui-meme est
    // quasi instantane (scene deja en cache, peu d'assets a liberer) - sans
    // ca, un chargement tres rapide pouvait faire japparaitre-disparaitre le
    // noir en une fraction de frame, imperceptible (retour utilisateur :
    // "je ne vois plus la fondue noir").
    [SerializeField] private float _minBlackHoldDuration = 0.15f;

    // AJOUTE (2026-09-15) - un SEUL fondu actif a la fois, quelle que soit son
    // origine (FadeOut() explicite, retour auto depuis OnSceneLoaded, ou
    // LoadScene()) : avant, le fondu de retour declenche par OnSceneLoaded et
    // un nouveau LoadScene() appele juste apres (ex. clic sur Jouer tres vite
    // apres l'arrivee sur le menu) tournaient TOUS LES DEUX en meme temps sur
    // le meme CanvasGroup.alpha, se marchant dessus frame par frame - resultat
    // impredictible, potentiellement un noir jamais clairement visible
    // (retour utilisateur : "je ne vois plus la fondue noir"). Chaque appel
    // annule desormais explicitement le fondu precedent avant de demarrer,
    // et part TOUJOURS de l'alpha ACTUEL (pas d'un 0/1 suppose) pour ne
    // jamais sauter visuellement.
    private Coroutine _fadeCoroutine;

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
            StartFade(0f, _quickTransitionFadeInDuration);
    }

    public IEnumerator FadeOut(float duration)
    {
        yield return RunFade(duration, 1f);
    }

    // AJOUTE (2026-09-14) - transition rapide utilisee par SceneLoader pour
    // toute navigation EN COURS DE JEU (pas le tout premier demarrage, qui
    // passe par LoadingScreen). Fondu noir bref, vrai chargement asynchrone
    // et liberation des assets de la scene quittee, le tout cache derriere le
    // fondu - le joueur voit juste une coupure fluide, jamais un "ecran de
    // chargement" a part entiere.
    public void LoadScene(string sceneName)
    {
        // AJOUTE (2026-09-15) - coupe la musique de la scene qu'on quitte
        // IMMEDIATEMENT (meme frame que le clic sur Jouer/Abandonner/etc.),
        // avant meme que le fondu au noir ne commence (retour utilisateur :
        // "la musique ne s'arrete pas instantanement... pendant l'ecran noir,
        // il ne doit pas y avoir de musique, c'est comme un moment de pause").
        // MODIFIE (2026-09-15) - fondu tres court (MusicStarter._stopFadeDuration)
        // plutot qu'un Stop() sec : voir MusicStarter, meme retour utilisateur,
        // un arret net s'entendait comme une coupure ("effet de coupe pas bien").
        if (MusicStarter.Instance != null)
            MusicStarter.Instance.Stop();

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        yield return RunFade(_quickTransitionFadeDuration, 1f);

        // AJOUTE - garantit un moment d'ecran noir plein VISIBLE, meme si le
        // chargement qui suit est quasi instantane.
        yield return new WaitForSecondsRealtime(_minBlackHoldDuration);

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
        // ci-dessus des que la nouvelle scene termine de charger - MAIS si par
        // hasard cette coroutine-ci est encore techniquement "active" a cet
        // instant (ne devrait pas arriver), StartFade() dans OnSceneLoaded
        // l'annulera proprement de toute facon (meme _fadeCoroutine).
    }

    // AJOUTE (2026-09-15) - point d'entree unique pour demarrer un fondu :
    // annule systematiquement le precedent et part de l'alpha REEL actuel,
    // jamais d'une valeur supposee - voir le commentaire sur _fadeCoroutine.
    private void StartFade(float to, float duration)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(RunFade(duration, to));
    }

    private IEnumerator RunFade(float duration, float to)
    {
        if (_canvasGroup == null) yield break;

        float from = _canvasGroup.alpha;
        _canvasGroup.blocksRaycasts = to > 0f;
        float elapsed = 0f;

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
