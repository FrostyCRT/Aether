using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// AJOUTÉ (2026-10-05) - retour utilisateur : "il faut travailler sur les animations des compétences [...] la
// Boue et la Foudre c'est bien fait, mais pour les autres il y a rien". La Foudre a son éclair (LightningBoltVFX),
// la Boue a sa flaque qui grandit - mais Boule de Feu/Couteaux/Orbitaux/Orbe rebondissant n'avaient RIEN au moment
// de l'impact (juste le chiffre de dégâts qui apparaît). Effet générique, léger et RAPIDE (0,18s) : un petit éclat
// de 4 traits qui jaillissent du point d'impact et s'estompent - donne enfin un "poids" visuel à chaque coup, sans
// jamais encombrer l'écran (même esprit que LightningBoltVFX : pooled, plafonné, procédural via FXSprites - aucun
// nouvel asset). Couleur toujours celle de l'arme qui frappe (mêmes constantes que DamageNumberSpawner), pour que
// chaque impact reste identifiable d'un coup d'œil, exactement comme les chiffres de dégâts.
public static class ImpactSparkVFX
{
    private const int MaxConcurrent = 80;

    private static readonly Stack<ImpactSparkRunner> _pool = new Stack<ImpactSparkRunner>();
    private static int _activeCount = 0;

    // CORRIGE (2026-10-16, audit) - compteur et pile STATIQUES : les éclats d'impact encore actifs au changement de scène
    // sont détruits sans jamais repasser par ReturnToPool, donc _activeCount ne redescendait jamais et la pile gardait
    // des objets détruits. Partie après partie, le plafond finissait par bloquer tous les effets. Remis à zéro à chaque
    // chargement de scène (et au lancement, pour le Play Mode sans rechargement de domaine).
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

    public static void Spawn(Vector3 position, Color color, float scale = 1f)
    {
        if (_activeCount >= MaxConcurrent) return;

        ImpactSparkRunner runner = _pool.Count > 0 ? _pool.Pop() : null;
        if (runner == null)
        {
            GameObject go = new GameObject("ImpactSparkVFX");
            runner = go.AddComponent<ImpactSparkRunner>();
        }
        else
        {
            runner.gameObject.SetActive(true);
        }

        _activeCount++;
        runner.Play(position, color, scale, ReturnToPool);
    }

    private static void ReturnToPool(ImpactSparkRunner runner)
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
        runner.gameObject.SetActive(false);
        _pool.Push(runner);
    }
}

public class ImpactSparkRunner : MonoBehaviour
{
    private const int SparkCount = 4;
    private const float Duration = 0.18f;

    private SpriteRenderer[] _sparks;
    private Vector3[] _dirs;
    private System.Action<ImpactSparkRunner> _onComplete;
    private Coroutine _co;

    public void Play(Vector3 position, Color color, float scale, System.Action<ImpactSparkRunner> onComplete)
    {
        transform.position = position;
        _onComplete = onComplete;

        if (_sparks == null)
        {
            _sparks = new SpriteRenderer[SparkCount];
            _dirs = new Vector3[SparkCount];
            for (int i = 0; i < SparkCount; i++)
                _sparks[i] = FXSprites.MakeRenderer(transform, "Spark" + i, FXSprites.Glow(), 15, 0f);
        }

        for (int i = 0; i < SparkCount; i++)
        {
            float angle = (360f / SparkCount) * i + Random.Range(-20f, 20f);
            _dirs[i] = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            _sparks[i].transform.localPosition = Vector3.zero;
            _sparks[i].transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _sparks[i].color = FXSprites.A(color, 0.9f);
            FXSprites.SetFlat(_sparks[i], 0.22f * scale, 0f);
        }

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Animate(scale));
    }

    private IEnumerator Animate(float scale)
    {
        float t = 0f;
        while (t < Duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / Duration);
            float eased = FXSprites.EaseOutCubic(p);
            for (int i = 0; i < _sparks.Length; i++)
            {
                _sparks[i].transform.localPosition = _dirs[i] * eased * 0.55f * scale;
                Color c = _sparks[i].color;
                c.a = Mathf.Lerp(0.9f, 0f, p);
                _sparks[i].color = c;
            }
            yield return null;
        }
        _onComplete?.Invoke(this);
    }
}
