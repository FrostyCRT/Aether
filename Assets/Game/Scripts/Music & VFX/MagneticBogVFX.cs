using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// AJOUTÉ (2026-10-27, retour utilisateur : "tu peux faire des visuels comme tu as fait pour la déflagration de boule de
// feu ou fragmentation de Aether") - effets propres à la Tourbière Magnétique (WeaponFusionCursedSwamp), au même niveau
// de qualité que FireExplosionVFX / ArcaneBurstVFX, en deux temps :
//
// CHARGE (toute la durée de la tourbière) - "ça aspire et ça se charge" :
//  1. Voile bleu nuit d'orage sous la tourbière (contraste sur le sol clair, et couleur de la tempête).
//  2. Traînées de cristal aspirées en spirale depuis le bord de la zone d'attraction vers le centre, de plus en plus
//     rapides : le joueur VOIT l'aspiration, même sans ennemi dedans.
//  3. Fragments de cristal en orbite qui tournent de plus en plus vite en se resserrant vers le centre.
//  4. Cœur : deux étoiles de cristal contrarotatives qui grossissent et battent comme un cœur, de plus en plus vite.
//  5. Implosion finale très brève (tout se contracte et blanchit) juste avant la surcharge.
// SURCHARGE (l'impact) :
//  1. Flash blanc doré.  2. Double étoile de cristal qui jaillit en tournant.  3. Fourches de foudre cristallisée
//  (éclats longs et fins) projetées jusqu'au rayon de dégâts ; à leur pointe, un fragment de cristal se forme puis vole
//  en éclats : la portée réelle se lit sans aucun anneau.  4. Poussières dorées qui montent.
// Grammaire visuelle du projet : aucun anneau lisse ni gradué (réservés aux télégraphes ennemis), seulement des formes
// de cristal. Or électrique + bleu nuit : distinct du rubis de Fragmentation et de l'orange de la Boule de feu.
// Même modèle que les autres effets : pools statiques de Runner, matériaux mis en cache par FXSprites, plafonds.
public static class MagneticBogVFX
{
    private const int MaxConcurrentCharges = 12;
    private const int MaxConcurrentBursts = 12;

    public static readonly Color Gold = new Color(1f, 0.86f, 0.3f);
    public static readonly Color WhiteHot = new Color(1f, 0.98f, 0.88f);
    public static readonly Color Amber = new Color(0.82f, 0.5f, 0.1f);
    public static readonly Color StormShade = new Color(0.05f, 0.06f, 0.17f);

    private static readonly Stack<MagneticBogChargeRunner> _chargePool = new Stack<MagneticBogChargeRunner>();
    private static readonly Stack<MagneticBogBurstRunner> _burstPool = new Stack<MagneticBogBurstRunner>();
    private static int _activeCharges, _activeBursts;

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
        _activeCharges = 0;
        _activeBursts = 0;
        _chargePool.Clear();
        _burstPool.Clear();
    }

    public static void SpawnCharge(Vector3 position, float bogRadius, float pullRadius, float duration)
    {
        if (_activeCharges >= MaxConcurrentCharges) return;
        MagneticBogChargeRunner runner = _chargePool.Count > 0 ? _chargePool.Pop() : null;
        if (runner == null) runner = new GameObject("MagneticBogCharge").AddComponent<MagneticBogChargeRunner>();
        else runner.gameObject.SetActive(true);
        _activeCharges++;
        runner.Play(position, bogRadius, pullRadius, duration, ReturnCharge);
    }

    public static void SpawnBurst(Vector3 position, float radius)
    {
        if (_activeBursts >= MaxConcurrentBursts) return;
        MagneticBogBurstRunner runner = _burstPool.Count > 0 ? _burstPool.Pop() : null;
        if (runner == null) runner = new GameObject("MagneticBogBurst").AddComponent<MagneticBogBurstRunner>();
        else runner.gameObject.SetActive(true);
        _activeBursts++;
        runner.Play(position, radius, ReturnBurst);
    }

    private static void ReturnCharge(MagneticBogChargeRunner runner)
    {
        _activeCharges = Mathf.Max(0, _activeCharges - 1);
        runner.gameObject.SetActive(false);
        _chargePool.Push(runner);
    }

    private static void ReturnBurst(MagneticBogBurstRunner runner)
    {
        _activeBursts = Mathf.Max(0, _activeBursts - 1);
        runner.gameObject.SetActive(false);
        _burstPool.Push(runner);
    }

    // Partie en pause (montée de niveau, menu) : les effets longs se figent avec le jeu au lieu de continuer seuls.
    public static bool GameFrozen =>
        GameManager.Instance != null && (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver);
}

public class MagneticBogChargeRunner : MonoBehaviour
{
    private const int StreakCount = 16;
    private const int OrbitCount = 6;
    private const float ImplodeDuration = 0.2f;   // dernière fraction de la charge : tout se contracte

    private SpriteRenderer _shade, _halo, _coreA, _coreB;
    private SpriteRenderer[] _streaks, _orbits;
    private float[] _streakAngle, _streakRadius, _streakSpeed, _streakLength;
    private float[] _orbitPhase, _orbitSpin, _orbitSize;

    private float _bogRadius, _pullRadius, _duration;
    private System.Action<MagneticBogChargeRunner> _onComplete;
    private Coroutine _co;

    public void Play(Vector3 position, float bogRadius, float pullRadius, float duration, System.Action<MagneticBogChargeRunner> onComplete)
    {
        transform.position = position;
        _bogRadius = Mathf.Max(0.5f, bogRadius);
        _pullRadius = Mathf.Max(_bogRadius, pullRadius);
        _duration = Mathf.Max(0.3f, duration);
        _onComplete = onComplete;

        EnsureBuilt();
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Animate());
    }

    private void EnsureBuilt()
    {
        if (_shade != null) return;

        _shade = FXSprites.MakeRenderer(transform, "Shade", FXSprites.Glow(), -19, 0f);
        _halo = FXSprites.MakeRenderer(transform, "Halo", FXSprites.Glow(), -17, 0.02f);
        _coreB = FXSprites.MakeRenderer(transform, "CoreB", FXSprites.CrystalShard(), -15, 0.05f);
        _coreA = FXSprites.MakeRenderer(transform, "CoreA", FXSprites.CrystalShard(), -14, 0.06f);

        _streaks = new SpriteRenderer[StreakCount];
        _streakAngle = new float[StreakCount];
        _streakRadius = new float[StreakCount];
        _streakSpeed = new float[StreakCount];
        _streakLength = new float[StreakCount];
        for (int i = 0; i < StreakCount; i++)
            _streaks[i] = FXSprites.MakeRenderer(transform, "Streak" + i, FXSprites.Splinter(), -16, 0.04f);

        _orbits = new SpriteRenderer[OrbitCount];
        _orbitPhase = new float[OrbitCount];
        _orbitSpin = new float[OrbitCount];
        _orbitSize = new float[OrbitCount];
        for (int i = 0; i < OrbitCount; i++)
            _orbits[i] = FXSprites.MakeRenderer(transform, "Orbit" + i, FXSprites.CrystalFragment(i), -13, 0.08f);
    }

    private void ResetStreak(int i, bool anywhere)
    {
        _streakAngle[i] = Random.Range(0f, Mathf.PI * 2f);
        _streakRadius[i] = anywhere ? Random.Range(0.3f, 1f) * _pullRadius : Random.Range(0.85f, 1f) * _pullRadius;
        _streakSpeed[i] = Random.Range(0.8f, 1.2f);
        _streakLength[i] = Random.Range(0.55f, 0.95f);
    }

    private IEnumerator Animate()
    {
        for (int i = 0; i < StreakCount; i++) ResetStreak(i, true);
        float fragmentScale = Mathf.Clamp(_bogRadius / 3f, 0.7f, 1.3f);
        for (int i = 0; i < OrbitCount; i++)
        {
            _orbitPhase[i] = (360f / OrbitCount) * i + Random.Range(-15f, 15f);
            _orbitSpin[i] = Random.Range(0f, 360f);
            _orbitSize[i] = Random.Range(0.4f, 0.55f) * fragmentScale;
        }
        float coreSpin = Random.Range(0f, 360f);
        float orbitAngle = 0f;
        float beat = 0f;

        float t = 0f;
        while (t < _duration)
        {
            if (MagneticBogVFX.GameFrozen) { yield return null; continue; }
            float dt = Time.deltaTime;
            t += dt;
            float charge = Mathf.Clamp01(t / _duration);
            float implode = Mathf.Clamp01((t - (_duration - ImplodeDuration)) / ImplodeDuration);
            float appear = FXSprites.Smooth(Mathf.Clamp01(t / 0.25f));

            // Battement de cœur : de plus en plus rapide (1,5 -> 7 battements/s), pic bref puis retombée.
            beat += dt * Mathf.Lerp(1.5f, 7f, charge * charge);
            float pulse = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(beat * Mathf.PI * 2f), 6f);

            // 1. Voile d'orage + halo doré qui monte avec la charge.
            FXSprites.SetFlat(_shade, _bogRadius * 2.5f, 0f);
            _shade.color = FXSprites.A(MagneticBogVFX.StormShade, appear * Mathf.Lerp(0.35f, 0.6f, charge) * (1f - implode * 0.5f));
            FXSprites.SetFlat(_halo, _bogRadius * Mathf.Lerp(0.8f, 1.6f, charge) * (1f - 0.6f * implode), 0f);
            _halo.color = FXSprites.A(Color.Lerp(MagneticBogVFX.Gold, MagneticBogVFX.WhiteHot, implode), appear * (0.12f + 0.3f * charge + 0.2f * pulse));

            // 2. Traînées aspirées en spirale : vitesse vers le centre ET rotation, toutes deux accélérées par la charge.
            float inflow = Mathf.Lerp(2.5f, 9f, charge) * (1f + 2f * implode);
            float swirl = Mathf.Lerp(0.6f, 2.2f, charge);
            for (int i = 0; i < StreakCount; i++)
            {
                _streakRadius[i] -= inflow * _streakSpeed[i] * dt;
                _streakAngle[i] += swirl * _streakSpeed[i] * dt * (_pullRadius / Mathf.Max(0.6f, _streakRadius[i])) * 0.35f;
                if (_streakRadius[i] < 0.35f && implode <= 0f) ResetStreak(i, false);

                float r = Mathf.Max(0f, _streakRadius[i]);
                Vector3 radial = new Vector3(Mathf.Cos(_streakAngle[i]), 0f, Mathf.Sin(_streakAngle[i]));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                Vector3 velocity = -radial * inflow + tangent * swirl * r * 0.35f;
                float dirDeg = Mathf.Atan2(velocity.z, velocity.x) * Mathf.Rad2Deg;

                SpriteRenderer sr = _streaks[i];
                sr.transform.localPosition = radial * r + Vector3.up * 0.04f;
                // Le sprite pointe vers +X ; SetFlat(angle) l'oriente vers (cos, 0, sin) de cet angle (comme ArcaneBurstVFX).
                FXSprites.SetFlat(sr, _streakLength[i] * Mathf.Lerp(0.8f, 1.3f, charge), dirDeg);
                float edgeFade = FXSprites.Smooth(Mathf.Clamp01((_pullRadius - r) / 0.8f)) * FXSprites.Smooth(Mathf.Clamp01((r - 0.2f) / 0.4f));
                float nearCenter = 1f - Mathf.Clamp01(r / _pullRadius);
                Color c = Color.Lerp(MagneticBogVFX.Gold, MagneticBogVFX.WhiteHot, nearCenter * 0.7f);
                sr.color = FXSprites.A(c, appear * edgeFade * Mathf.Lerp(0.45f, 0.95f, charge));
            }

            // 3. Fragments en orbite qui se resserrent et accélèrent, puis plongent au centre à l'implosion.
            orbitAngle += Mathf.Lerp(50f, 420f, charge * charge) * dt;
            float orbitRadius = _bogRadius * Mathf.Lerp(0.85f, 0.45f, charge * charge) * (1f - 0.9f * FXSprites.EaseOutCubic(implode));
            for (int i = 0; i < OrbitCount; i++)
            {
                float deg = (_orbitPhase[i] + orbitAngle) * Mathf.Deg2Rad;
                SpriteRenderer sr = _orbits[i];
                sr.transform.localPosition = new Vector3(Mathf.Cos(deg), 0f, Mathf.Sin(deg)) * orbitRadius
                    + Vector3.up * (0.08f + 0.06f * Mathf.Sin(t * 5f + i));
                float pop = FXSprites.EaseOutBack(Mathf.Clamp01((t - 0.05f * i) / 0.25f), 1.8f);
                FXSprites.SetFlat(sr, _orbitSize[i] * pop * (1f - 0.5f * implode), _orbitSpin[i] + t * 160f);
                Color c = Color.Lerp(MagneticBogVFX.Amber, MagneticBogVFX.Gold, charge);
                c = Color.Lerp(c, MagneticBogVFX.WhiteHot, 0.6f * pulse + implode);
                sr.color = FXSprites.A(c, 0.92f);
            }

            // 4. Cœur : deux étoiles contrarotatives qui grossissent avec la charge, battent, puis se contractent.
            float coreSize = Mathf.Lerp(0.7f, 1.7f, charge) * (1f + 0.22f * pulse) * Mathf.Lerp(1f, 0.45f, FXSprites.EaseOutCubic(implode));
            float coreGrow = FXSprites.EaseOutBack(Mathf.Clamp01(t / 0.3f), 1.4f);
            FXSprites.SetFlat(_coreA, coreSize * coreGrow, coreSpin + t * Mathf.Lerp(40f, 200f, charge));
            FXSprites.SetFlat(_coreB, coreSize * 1.25f * coreGrow, coreSpin + 22f - t * Mathf.Lerp(30f, 150f, charge));
            _coreA.color = FXSprites.A(Color.Lerp(MagneticBogVFX.Gold, MagneticBogVFX.WhiteHot, 0.25f + 0.5f * pulse + implode), 1f);
            _coreB.color = FXSprites.A(Color.Lerp(MagneticBogVFX.Amber, MagneticBogVFX.Gold, charge), 0.75f);

            yield return null;
        }

        _co = null;
        _onComplete?.Invoke(this);
    }
}

public class MagneticBogBurstRunner : MonoBehaviour
{
    private const int ForkCount = 10;
    private const int MoteCount = 12;

    private const float FlashDuration = 0.13f;
    private const float CoreDuration = 0.45f;
    private const float ForkDuration = 0.16f;      // les fourches atteignent le bord de la zone
    private const float ForkFadeDuration = 0.2f;
    private const float TipPop = 0.07f;
    private const float TipHold = 0.16f;
    private const float TipBreak = 0.16f;
    private const float MoteDuration = 0.75f;
    private const float TotalDuration = 0.85f;

    private SpriteRenderer _flash, _shade, _coreA, _coreB;
    private SpriteRenderer[] _forks, _tips, _motes;
    private float[] _forkAngle, _forkReach, _tipSpin, _tipBreakAt;
    private Vector3[] _moteStart, _moteDrift;
    private float[] _motePhase;

    private float _radius;
    private System.Action<MagneticBogBurstRunner> _onComplete;
    private Coroutine _co;

    public void Play(Vector3 position, float radius, System.Action<MagneticBogBurstRunner> onComplete)
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

        _shade = FXSprites.MakeRenderer(transform, "Shade", FXSprites.Glow(), -19, 0f);
        _coreB = FXSprites.MakeRenderer(transform, "CoreB", FXSprites.CrystalShard(), -15, 0.04f);
        _coreA = FXSprites.MakeRenderer(transform, "CoreA", FXSprites.CrystalShard(), -14, 0.05f);
        _flash = FXSprites.MakeRenderer(transform, "Flash", FXSprites.Glow(), -10, 0.07f);

        _forks = new SpriteRenderer[ForkCount];
        _tips = new SpriteRenderer[ForkCount];
        _forkAngle = new float[ForkCount];
        _forkReach = new float[ForkCount];
        _tipSpin = new float[ForkCount];
        _tipBreakAt = new float[ForkCount];
        for (int i = 0; i < ForkCount; i++)
        {
            _forks[i] = FXSprites.MakeRenderer(transform, "Fork" + i, FXSprites.Splinter(), -16, 0.03f);
            _tips[i] = FXSprites.MakeRenderer(transform, "Tip" + i, FXSprites.CrystalFragment(i), -13, 0.06f);
        }

        _motes = new SpriteRenderer[MoteCount];
        _moteStart = new Vector3[MoteCount];
        _moteDrift = new Vector3[MoteCount];
        _motePhase = new float[MoteCount];
        for (int i = 0; i < MoteCount; i++)
            _motes[i] = FXSprites.MakeRenderer(transform, "Mote" + i, FXSprites.Glow(), -11, 0.1f);
    }

    private IEnumerator Animate()
    {
        float coreSpin = Random.Range(0f, 360f);
        for (int i = 0; i < ForkCount; i++)
        {
            _forkAngle[i] = (360f / ForkCount) * i + Random.Range(-14f, 14f);
            _forkReach[i] = Random.Range(0.85f, 1f);
            _tipSpin[i] = Random.Range(0f, 360f);
            _tipBreakAt[i] = ForkDuration * _forkReach[i] + TipPop + TipHold + Random.Range(-0.04f, 0.05f);
        }
        for (int i = 0; i < MoteCount; i++)
        {
            float rad = Random.Range(0f, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            _moteStart[i] = dir * Random.Range(0.1f, 0.7f) * _radius;
            _moteDrift[i] = dir * Random.Range(0.15f, 0.4f) * _radius + Vector3.up * Random.Range(0.8f, 1.4f);
            _motePhase[i] = Random.Range(0f, Mathf.PI * 2f);
        }

        float t = 0f;
        while (t < TotalDuration)
        {
            t += Time.deltaTime;
            UpdateFlash(t);
            UpdateCore(t, coreSpin);
            UpdateForks(t);
            UpdateMotes(t);
            yield return null;
        }

        _co = null;
        _onComplete?.Invoke(this);
    }

    private void UpdateFlash(float t)
    {
        float p = Mathf.Clamp01(t / FlashDuration);
        FXSprites.SetFlat(_flash, Mathf.Lerp(_radius * 0.4f, _radius * 1.5f, FXSprites.EaseOutCubic(p)), 0f);
        _flash.color = FXSprites.A(MagneticBogVFX.WhiteHot, Mathf.Lerp(1f, 0f, p * p));
    }

    private void UpdateCore(float t, float spin)
    {
        float p = Mathf.Clamp01(t / CoreDuration);
        float grow = FXSprites.EaseOutBack(Mathf.Clamp01(t / (CoreDuration * 0.45f)), 1.4f);
        float diameter = _radius * 1.5f * grow;
        FXSprites.SetFlat(_coreA, diameter, spin + 50f * FXSprites.EaseOutCubic(p));
        FXSprites.SetFlat(_coreB, diameter * 1.2f, spin + 22f - 35f * FXSprites.EaseOutCubic(p));

        float fade = 1f - FXSprites.Smooth(Mathf.Clamp01((t - CoreDuration * 0.3f) / (CoreDuration * 0.7f)));
        _coreA.color = FXSprites.A(Color.Lerp(MagneticBogVFX.WhiteHot, MagneticBogVFX.Gold, Mathf.Clamp01(p * 2f)), fade);
        _coreB.color = FXSprites.A(Color.Lerp(MagneticBogVFX.Gold, MagneticBogVFX.Amber, p), 0.8f * fade);

        // Ombre bleu nuit : détache l'or du sol clair et rappelle l'orage.
        FXSprites.SetFlat(_shade, _radius * 2.3f, 0f);
        _shade.color = FXSprites.A(MagneticBogVFX.StormShade, 0.55f * (1f - FXSprites.Smooth(Mathf.Clamp01((t - 0.15f) / 0.55f))));
    }

    // Fourches : éclats longs et fins qui filent du centre jusqu'au bord de la zone (la portée réelle), puis s'éteignent ;
    // à l'arrivée, un fragment de cristal se forme à la pointe, s'illumine et vole en éclats.
    private void UpdateForks(float t)
    {
        for (int i = 0; i < ForkCount; i++)
        {
            float arrive = ForkDuration * _forkReach[i];
            float p = Mathf.Clamp01(t / arrive);
            float reach = FXSprites.EaseOutCubic(p) * _forkReach[i] * _radius;
            float rad = _forkAngle[i] * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

            SpriteRenderer fork = _forks[i];
            float length = Mathf.Max(0.2f, reach * 0.9f);
            fork.transform.localPosition = dir * (reach - length * 0.5f) + Vector3.up * 0.03f;
            FXSprites.SetFlat(fork, length, _forkAngle[i]);
            float fade = 1f - FXSprites.Smooth(Mathf.Clamp01((t - arrive) / ForkFadeDuration));
            float flicker = 0.75f + 0.25f * Mathf.Sin(t * 70f + i * 1.7f);
            fork.color = FXSprites.A(Color.Lerp(MagneticBogVFX.WhiteHot, MagneticBogVFX.Gold, p), fade * flicker);

            SpriteRenderer tip = _tips[i];
            float local = t - arrive;
            if (local < 0f) { tip.color = Color.clear; continue; }
            float pop = FXSprites.EaseOutBack(Mathf.Clamp01(local / TipPop), 1.8f);
            float breakP = Mathf.Clamp01((t - _tipBreakAt[i]) / TipBreak);
            tip.transform.localPosition = dir * (_forkReach[i] * _radius * (1f + 0.08f * breakP)) + Vector3.up * (0.06f + 0.15f * breakP);
            FXSprites.SetFlat(tip, 0.55f * Mathf.Clamp(_radius / 3.5f, 0.7f, 1.3f) * pop * Mathf.Lerp(1f, 0.3f, FXSprites.Smooth(breakP)),
                _tipSpin[i] + local * 120f * (1f + 2f * breakP));
            float glint = Mathf.Clamp01(1f - Mathf.Abs(t - _tipBreakAt[i]) / 0.05f);
            Color c = Color.Lerp(MagneticBogVFX.WhiteHot, MagneticBogVFX.Gold, FXSprites.Smooth(Mathf.Clamp01(local / 0.12f)));
            c = Color.Lerp(c, MagneticBogVFX.WhiteHot, 0.8f * glint);
            tip.color = FXSprites.A(c, 0.92f * (1f - FXSprites.Smooth(breakP)));
        }
    }

    private void UpdateMotes(float t)
    {
        float p = Mathf.Clamp01(t / MoteDuration);
        float eased = FXSprites.EaseOutQuad(p);
        for (int i = 0; i < MoteCount; i++)
        {
            SpriteRenderer sr = _motes[i];
            sr.transform.localPosition = _moteStart[i] + _moteDrift[i] * eased + Vector3.up * 0.1f;
            float twinkle = 0.6f + 0.4f * Mathf.Sin(t * 34f + _motePhase[i]);
            FXSprites.SetFlat(sr, Mathf.Lerp(0.45f, 0.15f, p), 0f);
            float alpha = FXSprites.Smooth(Mathf.Clamp01(t / 0.08f)) * (1f - FXSprites.Smooth(Mathf.Clamp01((p - 0.35f) / 0.65f)));
            sr.color = FXSprites.A(Color.Lerp(MagneticBogVFX.WhiteHot, MagneticBogVFX.Gold, p), alpha * twinkle);
        }
    }
}
