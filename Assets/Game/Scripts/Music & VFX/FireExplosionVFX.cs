using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// AJOUTÉ (2026-10-21, retour utilisateur sur la refonte de la Boule de feu : "je veux qu'on revoit la forme de
// l'explosion visuellement, parce que là c'est juste un rond [...] ça doit être très reconnaissable [qu'un joueur
// qui ne sait pas ce que c'est sache que c'est une déflagration de boule de feu]") - jusqu'ici, l'impact de la
// Boule de feu réutilisait ExpandingRingVFX (un simple anneau qui grandit, générique, partagé avec le nœud
// "Fragmentation" du tir de base de n'importe quel personnage - voir WeaponBase.FireProjectile). Remplacé, POUR LA
// BOULE DE FEU UNIQUEMENT (voir ProjectileBasic._useFireExplosionVisual/WeaponFireball.FireProjectile), par un
// vrai effet de déflagration enflammée en plusieurs couches :
//  1. Un flash blanc-jaune très bref au centre (le "coup" de la détonation).
//  2. 2 couches de FXSprites.FlameBurst() (forme en pointes de flamme, pas un disque/anneau) superposées, tournées
//     différemment, colorées blanc->orange->braise, avec un petit "punch" de dépassement à l'arrivée.
//  3. Des braises individuelles qui jaillissent vers l'extérieur ET vers le haut (le feu monte), tournoient et
//     s'éteignent en vol - lecture "matière qui explose", pas juste "une forme qui grandit sur place".
//  4. De la fumée qui apparaît juste après le flash et s'estompe plus lentement que le feu - signe classique
//     "quelque chose vient de brûler ici", renforce la lecture même après que les flammes se soient éteintes.
// Repris du même modèle anti-churn que ses cousins (ExpandingRingVFX/ImpactSparkVFX/LightningBoltVFX) : pool
// statique de Runner réutilisés (SetActive au lieu de Destroy/Instantiate), matériaux mis en cache par
// FXSprites.SpriteMaterial (jamais recréés), plafond dur d'instances simultanées.
public static class FireExplosionVFX
{
    private const int MaxConcurrent = 20;

    private static readonly Stack<FireExplosionRunner> _pool = new Stack<FireExplosionRunner>();
    private static int _activeCount = 0;

    // Même raison que ImpactSparkVFX.ResetState() : repart à zéro à chaque scène pour ne jamais garder la trace
    // d'instances détruites par un changement de scène (le plafond finirait sinon par bloquer l'effet pour de bon).
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

    public static void Spawn(Vector3 position, float radius)
    {
        if (_activeCount >= MaxConcurrent) return;

        FireExplosionRunner runner = _pool.Count > 0 ? _pool.Pop() : null;
        if (runner == null)
        {
            GameObject go = new GameObject("FireExplosionVFX");
            runner = go.AddComponent<FireExplosionRunner>();
        }
        else
        {
            runner.gameObject.SetActive(true);
        }

        _activeCount++;
        runner.Play(position, radius, ReturnToPool);
    }

    private static void ReturnToPool(FireExplosionRunner runner)
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
        runner.gameObject.SetActive(false);
        _pool.Push(runner);
    }
}

public class FireExplosionRunner : MonoBehaviour
{
    private const int EmberCount = 9;
    private const int SmokeCount = 4;

    // Teintes - du cœur blanc-chaud vers la braise sombre, cohérent avec DamageNumberSpawner.ColorFireball
    // (1, 0.42, 0.08) utilisé comme couleur "milieu" plutôt que comme seule couleur de l'effet.
    private static readonly Color FlashColor = new Color(1f, 0.95f, 0.78f);
    private static readonly Color HotColor = new Color(1f, 0.85f, 0.35f);
    private static readonly Color MidColor = new Color(1f, 0.42f, 0.08f); // DamageNumberSpawner.ColorFireball
    private static readonly Color EmberDarkColor = new Color(0.55f, 0.12f, 0.03f);
    private static readonly Color SmokeColor = new Color(0.16f, 0.14f, 0.13f);

    private SpriteRenderer _flash;
    private SpriteRenderer _burstInner;
    private SpriteRenderer _burstOuter;
    private SpriteRenderer[] _embers;
    private Vector3[] _emberDirs;
    private float[] _emberSpin;
    private SpriteRenderer[] _smoke;
    private Vector3[] _smokeDirs;

    private float _radius;
    private System.Action<FireExplosionRunner> _onComplete;
    private Coroutine _co;

    private const float FlashDuration = 0.12f;
    private const float BurstDuration = 0.40f;
    private const float EmberDuration = 0.42f;
    private const float SmokeDelay = 0.08f;
    private const float SmokeDuration = 0.55f;
    private float TotalDuration => SmokeDelay + SmokeDuration;

    public void Play(Vector3 position, float radius, System.Action<FireExplosionRunner> onComplete)
    {
        transform.position = position;
        _radius = Mathf.Max(0.5f, radius);
        _onComplete = onComplete;

        EnsureBuilt();

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Animate());
    }

    private void EnsureBuilt()
    {
        if (_flash != null) return;

        _flash = FXSprites.MakeRenderer(transform, "Flash", FXSprites.Glow(), 20, 0f);

        _burstInner = FXSprites.MakeRenderer(transform, "BurstInner", FXSprites.FlameBurst(), 16, 0f);
        _burstOuter = FXSprites.MakeRenderer(transform, "BurstOuter", FXSprites.FlameBurst(), 15, 0f);

        _embers = new SpriteRenderer[EmberCount];
        _emberDirs = new Vector3[EmberCount];
        _emberSpin = new float[EmberCount];
        for (int i = 0; i < EmberCount; i++)
            _embers[i] = FXSprites.MakeRenderer(transform, "Ember" + i, FXSprites.Glow(), 18, 0f);

        _smoke = new SpriteRenderer[SmokeCount];
        _smokeDirs = new Vector3[SmokeCount];
        for (int i = 0; i < SmokeCount; i++)
            _smoke[i] = FXSprites.MakeRenderer(transform, "Smoke" + i, FXSprites.Glow(), 13, 0f);
    }

    private IEnumerator Animate()
    {
        // Angle de départ aléatoire par explosion : 2 déflagrations au même endroit ne se ressemblent jamais trait
        // pour trait, même si la forme sous-jacente (FlameBurst) est toujours la même texture mise en cache.
        float burstInnerSpin = Random.Range(0f, 360f);
        float burstOuterSpin = burstInnerSpin + 25f + Random.Range(-8f, 8f);

        for (int i = 0; i < EmberCount; i++)
        {
            float angle = (360f / EmberCount) * i + Random.Range(-18f, 18f);
            _emberDirs[i] = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            _emberSpin[i] = Random.Range(-540f, 540f);
        }
        for (int i = 0; i < SmokeCount; i++)
        {
            float angle = (360f / SmokeCount) * i + Random.Range(-30f, 30f) + 45f;
            _smokeDirs[i] = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        }

        float t = 0f;
        float total = TotalDuration;

        while (t < total)
        {
            t += Time.deltaTime;

            UpdateFlash(t);
            UpdateBurst(_burstInner, t, 1f, burstInnerSpin, HotColor, MidColor);
            UpdateBurst(_burstOuter, t, 1.25f, burstOuterSpin, MidColor, EmberDarkColor);
            UpdateEmbers(t);
            UpdateSmoke(t);

            yield return null;
        }

        _co = null;
        _onComplete?.Invoke(this);
    }

    private void UpdateFlash(float t)
    {
        float p = Mathf.Clamp01(t / FlashDuration);
        float diameter = Mathf.Lerp(_radius * 0.3f, _radius * 1.3f, FXSprites.EaseOutCubic(p));
        FXSprites.SetFlat(_flash, diameter, 0f);
        _flash.color = FXSprites.A(FlashColor, Mathf.Lerp(1f, 0f, p * p));
    }

    // Une seule méthode pour les 2 couches de flammes (inner/outer) : même logique d'animation, seuls le
    // multiplicateur de taille, la rotation et le dégradé de couleur diffèrent entre les 2 appels.
    private void UpdateBurst(SpriteRenderer sr, float t, float sizeMul, float spinDeg, Color colorStart, Color colorEnd)
    {
        float p = Mathf.Clamp01(t / BurstDuration);
        float growP = Mathf.Clamp01(t / (BurstDuration * 0.55f));
        float diameter = Mathf.Lerp(0f, _radius * 2f * sizeMul, FXSprites.EaseOutBack(growP, 1.15f));

        sr.transform.localRotation = Quaternion.Euler(90f, 0f, spinDeg);
        float s = diameter / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
        sr.transform.localScale = new Vector3(s, s, 1f);

        float fadeP = Mathf.Clamp01((t - BurstDuration * 0.25f) / (BurstDuration * 0.75f));
        Color c = Color.Lerp(colorStart, colorEnd, Mathf.Clamp01(p * 1.6f));
        sr.color = FXSprites.A(c, Mathf.Lerp(0.95f, 0f, FXSprites.Smooth(fadeP)));
    }

    private void UpdateEmbers(float t)
    {
        float p = Mathf.Clamp01(t / EmberDuration);
        float eased = FXSprites.EaseOutCubic(p);
        // Le feu monte : léger arc vertical en plus de la dispersion radiale, retombe doucement en fin de vol.
        float rise = Mathf.Sin(p * Mathf.PI) * 0.35f;

        for (int i = 0; i < EmberCount; i++)
        {
            Vector3 outward = _emberDirs[i] * eased * _radius * 0.85f;
            _embers[i].transform.localPosition = outward + Vector3.up * rise;
            _embers[i].transform.localRotation = Quaternion.Euler(90f, 0f, _emberSpin[i] * p);

            float size = Mathf.Lerp(0.35f, 0.12f, p);
            FXSprites.SetFlat(_embers[i], size, 0f);

            Color c = Color.Lerp(HotColor, EmberDarkColor, p);
            _embers[i].color = FXSprites.A(c, Mathf.Lerp(1f, 0f, p * p));
        }
    }

    private void UpdateSmoke(float t)
    {
        float local = t - SmokeDelay;
        float p = Mathf.Clamp01(local / SmokeDuration);
        if (local < 0f) p = 0f;

        float fadeIn = Mathf.Clamp01(local / 0.15f);
        float fadeOut = 1f - FXSprites.Smooth(p);
        float alpha = (local < 0f) ? 0f : Mathf.Min(fadeIn, fadeOut) * 0.5f;

        for (int i = 0; i < SmokeCount; i++)
        {
            Vector3 outward = _smokeDirs[i] * Mathf.Clamp01(local / SmokeDuration) * _radius * 0.5f;
            _smoke[i].transform.localPosition = outward + Vector3.up * (0.2f + p * 0.6f);
            float size = Mathf.Lerp(0.5f, 1.1f, Mathf.Clamp01(local / SmokeDuration)) * (_radius / 2.25f);
            FXSprites.SetFlat(_smoke[i], size, 0f);
            _smoke[i].color = FXSprites.A(SmokeColor, alpha);
        }
    }
}
