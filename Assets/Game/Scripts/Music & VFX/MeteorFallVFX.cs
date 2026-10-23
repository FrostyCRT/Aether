using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// AJOUTÉ (2026-10-22, retour utilisateur sur la fusion "Terre Calcinée" : "il faut revoir cette fusion, il faut
// trouver une idée originale qui se démarque des autres fusions [...] météorite qui tombe du ciel ?") - jusqu'ici
// cette fusion réutilisait tel quel le prefab de flaque de la Boue (juste teinté orange, en spirale) : aucune
// identité visuelle propre. Ce composant gère UNIQUEMENT l'objet qui tombe du ciel pendant la fenêtre de télégraphe
// au sol (voir WeaponFusionScorchedEarth, qui pilote GroundTelegraphFX en parallèle pour le cercle de danger) -
// l'impact lui-même (déflagration) réutilise FireExplosionVFX tel quel, déjà au niveau de qualité visé (retour
// utilisateur sur la Boule de feu : "ça tu l'as bien réussi en terme de graphismes"). Billboard caméra directe
// (transform.rotation = cam.rotation), même convention que BossFX.UpdatePillar - pas FXSprites.SetFlat (qui pose à
// PLAT au sol, review pour un effet aérien qui tombe, pas un décalque au sol). Pool statique de Runner réutilisés,
// même patron anti-churn que FireExplosionVFX/ImpactSparkVFX/ExpandingRingVFX.
public static class MeteorFallVFX
{
    private const int MaxConcurrent = 12;

    private static readonly Stack<MeteorFallRunner> _pool = new Stack<MeteorFallRunner>();
    private static int _activeCount = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void HookSceneReset()
    {
        ResetState();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => ResetState();

    private static void ResetState()
    {
        _activeCount = 0;
        _pool.Clear();
    }

    // landPosition : point d'impact au sol (déjà choisi par l'appelant, même position que le GroundTelegraphFX).
    // fallDuration : doit correspondre à la fenêtre de windup du télégraphe - le météore touche le sol pile quand
    // le télégraphe atteint sa progression 1 (voir WeaponFusionScorchedEarth.StrikeRoutine).
    // Retourne le Runner (MODIFIÉ 2026-10-23, retour utilisateur : "je veux que ça cible un ennemi en permanence")
    // pour que l'appelant puisse recaler la cible en vol via UpdateLandPosition() tant que l'ennemi verrouillé bouge.
    public static MeteorFallRunner Spawn(Vector3 landPosition, float fallDuration, float startHeight = 13f)
    {
        if (_activeCount >= MaxConcurrent) return null;

        MeteorFallRunner runner = _pool.Count > 0 ? _pool.Pop() : null;
        if (runner == null)
        {
            GameObject go = new GameObject("MeteorFallVFX");
            runner = go.AddComponent<MeteorFallRunner>();
        }
        else
        {
            runner.gameObject.SetActive(true);
        }

        _activeCount++;
        runner.Play(landPosition, fallDuration, startHeight, ReturnToPool);
        return runner;
    }

    private static void ReturnToPool(MeteorFallRunner runner)
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
        runner.gameObject.SetActive(false);
        _pool.Push(runner);
    }
}

public class MeteorFallRunner : MonoBehaviour
{
    // MODIFIÉ (2026-10-23, retour utilisateur : "ça ressemble trop à une attaque de boss, rend le plus marron
    // boueux") - les teintes jaune/orange/rouge vif (quasi identiques aux télégraphes de boss existants) remplacées
    // par une palette terre/boue, cohérente avec le matériau "Maron" déjà utilisé par les flaques de Boue normale
    // (Assets/Game/Materials/Maron.mat, RGB ~0.59/0.30/0.21) - un bloc de terre incandescent, pas une boule de feu.
    private static readonly Color CoreColor = new Color(0.82f, 0.66f, 0.42f);
    private static readonly Color FlameHotColor = new Color(0.56f, 0.34f, 0.17f);
    private static readonly Color FlameColdColor = new Color(0.28f, 0.17f, 0.09f);

    private const float FadeInDuration = 0.12f;

    private SpriteRenderer _core;
    private SpriteRenderer _flameInner;
    private SpriteRenderer _flameOuter;
    private Transform _cam;

    private Vector3 _landPosition;
    private float _startHeight;
    private float _duration;
    private float _spinInner, _spinOuter;
    private System.Action<MeteorFallRunner> _onComplete;
    private Coroutine _co;

    public void Play(Vector3 landPosition, float duration, float startHeight, System.Action<MeteorFallRunner> onComplete)
    {
        _landPosition = landPosition;
        _duration = Mathf.Max(0.05f, duration);
        _startHeight = startHeight;
        _onComplete = onComplete;

        EnsureBuilt();

        _spinInner = Random.Range(0f, 360f);
        _spinOuter = _spinInner + 40f + Random.Range(-10f, 10f);

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Animate());
    }

    // AJOUTÉ (2026-10-23, retour utilisateur : "je veux que ça cible un ennemi en permanence") - l'appelant peut
    // recaler le point de chute frame par frame tant que la cible verrouillée est vivante, sans relancer Play()
    // (donc sans réinitialiser la hauteur/le fondu en cours).
    public void UpdateLandPosition(Vector3 landPosition)
    {
        _landPosition = landPosition;
    }

    private void EnsureBuilt()
    {
        if (_core != null) return;

        _core = FXSprites.MakeRenderer(transform, "Core", FXSprites.Glow(), 20, 0f);
        _flameInner = FXSprites.MakeRenderer(transform, "FlameInner", FXSprites.FlameBurst(), 19, 0f);
        _flameOuter = FXSprites.MakeRenderer(transform, "FlameOuter", FXSprites.FlameBurst(), 18, 0f);
    }

    // Même technique que BossFX.UpdatePillar (_pillar.transform.rotation = _cam.rotation) : un billboard simple,
    // le sprite entier tourne autour de son axe face-caméra pour un effet de flamme qui tournoie en tombant.
    private void SetBillboardScale(SpriteRenderer sr, float diameter, float spinDeg)
    {
        float s = diameter / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
        sr.transform.localScale = new Vector3(s, s, 1f);
        if (_cam != null)
            sr.transform.rotation = _cam.rotation * Quaternion.Euler(0f, 0f, spinDeg);
    }

    private IEnumerator Animate()
    {
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;

        float t = 0f;
        while (t < _duration)
        {
            t += Time.deltaTime;
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;

            float p = Mathf.Clamp01(t / _duration);
            float fallP = p * p; // accélère en tombant, comme une vraie chute
            float height = Mathf.Lerp(_startHeight, 0f, fallP);
            transform.position = _landPosition + Vector3.up * height;

            // AGRANDI (2026-10-23, retour utilisateur : "je veux que visuellement, la boule de feu qui tombe soit
            // bien plus grosse, celle qui est graphique, pas le telegraphe au sol") - uniquement ce visuel aérien ;
            // le rayon du GroundTelegraphFX et de l'impact (_impactRadius) ne changent pas.
            float coreSize = Mathf.Lerp(0.65f, 1.2f, p);
            float flameSize = Mathf.Lerp(1.4f, 2.6f, p);

            float fadeIn = Mathf.Clamp01(t / FadeInDuration);

            SetBillboardScale(_core, coreSize, 0f);
            _core.color = FXSprites.A(CoreColor, fadeIn);

            SetBillboardScale(_flameInner, flameSize, _spinInner + p * 260f);
            _flameInner.color = FXSprites.A(Color.Lerp(FlameHotColor, FlameColdColor, 0.25f), fadeIn * 0.9f);

            SetBillboardScale(_flameOuter, flameSize * 1.3f, _spinOuter - p * 190f);
            _flameOuter.color = FXSprites.A(FlameColdColor, fadeIn * 0.75f);

            yield return null;
        }

        _co = null;
        _onComplete?.Invoke(this);
    }
}
