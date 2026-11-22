using System.Collections.Generic;
using UnityEngine;

// AJOUTÉ (2026-11-22, lot 1 des modèles Blender) - éclaboussure d'eau à l'impact du tir de base (goutte d'eau, voir
// ProjectileBasic._waterImpact) : une gerbe de gouttelettes 3D (même modèle que la traînée, SM_WaterDroplet) qui jaillit
// et retombe, avec une petite couronne d'eau aplatie au sol. Aucune lueur (shader Aether/StylizedWater). Le prefab
// Resources/FX/WaterSplash porte le ParticleSystem réglé ; ici, seulement un pool (plafonné) pour ne jamais en instancier
// en cours de partie après les premiers tirs.
public static class WaterSplashVFX
{
    private const int MaxConcurrent = 60;
    private const float Lifetime = 0.9f;

    private static GameObject _prefab;
    private static readonly Stack<ParticleSystem> _free = new Stack<ParticleSystem>();
    private static readonly List<(ParticleSystem ps, float until)> _busy = new List<(ParticleSystem, float)>();
    private static SplashTicker _ticker;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        _free.Clear();
        _busy.Clear();
        _ticker = null;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // objets détruits au changement de scène : on repart de zéro
    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        _free.Clear();
        _busy.Clear();
    }

    public static void Spawn(Vector3 position, float scale = 1f)
    {
        if (_busy.Count >= MaxConcurrent) return;
        if (_prefab == null) _prefab = Resources.Load<GameObject>("FX/WaterSplash");
        if (_prefab == null) return;
        if (_ticker == null)
        {
            var t = new GameObject("[WaterSplashVFX]");
            _ticker = t.AddComponent<SplashTicker>();
        }

        ParticleSystem ps = null;
        while (_free.Count > 0 && ps == null) ps = _free.Pop();
        if (ps == null) ps = Object.Instantiate(_prefab).GetComponent<ParticleSystem>();
        ps.gameObject.SetActive(true);
        ps.transform.position = position;
        ps.transform.localScale = Vector3.one * scale;
        ps.Clear(true);
        ps.Play(true);
        _busy.Add((ps, Time.time + Lifetime));
    }

    private class SplashTicker : MonoBehaviour
    {
        private void Update()
        {
            float now = Time.time;
            for (int i = _busy.Count - 1; i >= 0; i--)
            {
                var (ps, until) = _busy[i];
                if (ps == null) { _busy.RemoveAt(i); continue; }
                if (now < until) continue;
                ps.gameObject.SetActive(false);
                _free.Push(ps);
                _busy.RemoveAt(i);
            }
        }
    }
}
