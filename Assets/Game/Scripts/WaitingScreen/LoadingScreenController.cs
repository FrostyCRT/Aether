using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LoadingScreenController : MonoBehaviour
{
    // MODIFIE (2026-09-14) - cette scene ne sert plus qu'au TOUT PREMIER
    // demarrage du jeu (scene de boot dans les Build Settings). Les
    // transitions en cours de jeu (Jouer/Abandonner/Recommencer/Retour menu)
    // passent desormais par SceneLoader -> SceneTransitionFader.LoadScene(),
    // un simple fondu noir rapide sans repasser par cette scene "vitrine"
    // (retour utilisateur : la revivre a chaque fois se sentait comme un vrai
    // ecran de chargement, pas adapte a une simple relance de partie).
    [Header("Scene a charger")]
    [SerializeField] private string _nextSceneName = "MainMenu";

    [Header("Duree minimale d'affichage")]
    [SerializeField] private float _minimumDisplayDuration = 3.5f;

    [Header("Barre de chargement")]
    [SerializeField] private Slider _loadingBar;
    [SerializeField] private GameObject _loadingBarContainer;

    [Header("Appuyer sur une touche")]
    [SerializeField] private CanvasGroup _pressKeyGroup;
    [SerializeField] private float _pulseSpeed = 1.2f;
    [SerializeField] private float _pulseMinAlpha = 0.08f;
    [SerializeField] private float _pulseMaxAlpha = 1f;

    [Header("Transition")]
    [SerializeField] private float _fadeDuration = 0.6f;

    private AsyncOperation _asyncLoad;
    private Coroutine _pulseCoroutine;

    private void Start()
    {
        if (_pressKeyGroup != null)
            _pressKeyGroup.alpha = 0f;

        StartCoroutine(LoadRoutine());
    }

    private IEnumerator LoadRoutine()
    {
        // AJOUTE - libere explicitement les assets (textures, materiaux, etc.)
        // qui n'appartenaient qu'a la scene qu'on vient de quitter, AVANT de
        // charger la suivante : sans ca, ils restent en memoire jusqu'au
        // prochain passage du GC d'Unity.
        yield return Resources.UnloadUnusedAssets();

        _asyncLoad = SceneManager.LoadSceneAsync(_nextSceneName);
        _asyncLoad.allowSceneActivation = false;

        float elapsed = 0f;
        bool isReady = false;

        while (!isReady)
        {
            elapsed += Time.deltaTime;

            // Unity plafonne la progression reelle a 0.9 tant que
            // allowSceneActivation est a false - on la ramene sur une echelle 0-1.
            float realProgress = Mathf.Clamp01(_asyncLoad.progress / 0.9f);
            float timeProgress = Mathf.Clamp01(elapsed / _minimumDisplayDuration);

            // Le plus PETIT des deux pilote la barre : elle ne peut pas depasser
            // le plus lent des deux facteurs. Si le chargement reel est instantane,
            // c'est le temps minimum qui freine la barre. Si le chargement reel
            // dure plus longtemps que la duree minimale, c'est lui qui prend le
            // relais - la barre ne ment jamais sur l'etat reel du chargement.
            float displayProgress = Mathf.Min(realProgress, timeProgress);

            if (_loadingBar != null)
                _loadingBar.value = displayProgress;

            if (displayProgress >= 1f)
                isReady = true;

            yield return null;
        }

        if (_loadingBarContainer != null)
            _loadingBarContainer.SetActive(false);

        yield return StartCoroutine(FadeCanvasGroup(_pressKeyGroup, 0f, 1f, 0.4f));
        _pulseCoroutine = StartCoroutine(PulsePressKeyText());

        bool keyPressed = false;
        while (!keyPressed)
        {
            if (Input.anyKeyDown)
                keyPressed = true;

            yield return null;
        }

        if (_pulseCoroutine != null)
            StopCoroutine(_pulseCoroutine);

        // AJOUTE - fondu de la musique d'ambiance en sortie, synchronise avec
        // le fondu visuel ci-dessous, au lieu d'une coupure nette au changement
        // de scene. Utilise le MusicStarter deja existant dans le projet.
        if (MusicStarter.Instance != null)
            MusicStarter.Instance.FadeOutAndStop(_fadeDuration);

        if (SceneTransitionFader.Instance != null)
            yield return StartCoroutine(SceneTransitionFader.Instance.FadeOut(_fadeDuration));

        _asyncLoad.allowSceneActivation = true;
    }

    private IEnumerator PulsePressKeyText()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            _pressKeyGroup.alpha = Mathf.Lerp(_pulseMinAlpha, _pulseMaxAlpha, t);
            yield return null;
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;

        float elapsed = 0f;
        group.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        group.alpha = to;
    }
}