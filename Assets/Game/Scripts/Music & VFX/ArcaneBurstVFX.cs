using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// AJOUTÉ (2026-10-27, retour utilisateur sur le nœud Réputation "Fragmentation" : "je veux que ce soit visible, parce que
// là c'est juste un prefab moche qui grossit, tu peux faire une animation de la même qualité que [...] la déflagration de
// la boule de feu") - explosion du tir de base (1 projectile sur 4, voir WeaponBase.FireProjectile), qui remplace
// l'anneau orange générique d'ExpandingRingVFX. Identité "cristal d'aether" (teinte du tir de base, cyan), pour ne
// jamais se confondre avec la déflagration de la Boule de feu (FireExplosionVFX).
// MODIFIÉ (2026-10-27, retour utilisateur : "elle ressemble beaucoup à ce qui a déjà été vu dans les autres télégraphes")
// - la 1re version reprenait l'anneau fin (ThinRing) et le cercle gradué (TickRing) des TÉLÉGRAPHES ENNEMIS
// (GroundTelegraphFX), qui veulent dire "danger, sors de là" : un tir du joueur ne doit jamais envoyer ce signal. Plus
// aucune forme du télégraphe ici : ni anneau lisse, ni graduations, ni disque plein. Déroulé :
//  1. Flash blanc-bleu très bref au centre (le "coup" de la détonation).
//  2. Étoile de cristal (FXSprites.CrystalShard) qui jaillit avec un petit dépassement, tourne et s'éteint, sur une
//     ombre bleu nuit qui la détache du sol clair.
//  3. Onde de cristal BRISÉE (FXSprites.ShatterRing : couronne fendue, dents de cristal tournées vers le centre) qui
//     s'arrête EXACTEMENT au rayon de dégâts, sur un voile doux : le joueur lit la portée réelle de l'explosion.
//  4. Couronne de fragments (FXSprites.CrystalFragment) : en passant, l'onde "cristallise" des fragments séparés au bord
//     de la zone, qui apparaissent un à un, dérivent vers l'extérieur en tournant, s'illuminent puis volent en éclats.
//  5. Éclats de cristal allongés (FXSprites.Splinter) projetés vers l'extérieur, orientés dans leur direction de vol.
//  6. Poussières lumineuses qui montent en scintillant et s'éteignent plus lentement que le reste.
// Même modèle que FireExplosionVFX : pool statique de Runner réutilisés, matériaux mis en cache par
// FXSprites.SpriteMaterial, plafond d'instances simultanées.
public static class ArcaneBurstVFX
{
    private const int MaxConcurrent = 24;

    private static readonly Stack<ArcaneBurstRunner> _pool = new Stack<ArcaneBurstRunner>();
    private static int _activeCount = 0;

    // Même raison que FireExplosionVFX : repart à zéro à chaque scène (instances détruites par le changement de scène).
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

    public static void Spawn(Vector3 position, float radius, Color tint)
    {
        if (_activeCount >= MaxConcurrent) return;

        ArcaneBurstRunner runner = _pool.Count > 0 ? _pool.Pop() : null;
        if (runner == null)
        {
            GameObject go = new GameObject("ArcaneBurstVFX");
            runner = go.AddComponent<ArcaneBurstRunner>();
        }
        else
        {
            runner.gameObject.SetActive(true);
        }

        _activeCount++;
        runner.Play(position, radius, tint, ReturnToPool);
    }

    private static void ReturnToPool(ArcaneBurstRunner runner)
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
        runner.gameObject.SetActive(false);
        _pool.Push(runner);
    }
}

public class ArcaneBurstRunner : MonoBehaviour
{
    private const int SplinterCount = 12;
    private const int MoteCount = 10;
    private const int FragmentCount = 12;

    private const float FlashDuration = 0.11f;
    private const float CoreDuration = 0.38f;
    private const float WaveDuration = 0.26f;
    private const float WaveFadeDuration = 0.16f;   // après l'arrivée de l'onde au bord
    private const float WaveStartFraction = 0.2f;   // rayon de départ de l'onde, en fraction du rayon de dégâts
    private const float SplinterDuration = 0.42f;
    private const float MoteDuration = 0.7f;

    // Fragments : apparition (pop), vie (dérive + rotation), puis éclatement.
    private const float FragmentPopDuration = 0.07f;
    private const float FragmentHold = 0.22f;   // court : l'effet revient tous les 4 tirs, il ne doit pas encombrer l'écran
    private const float FragmentBreakDuration = 0.16f;
    private const float FragmentDrift = 0.09f;      // dérive vers l'extérieur, en fraction du rayon
    private const float TotalDuration = 0.8f;       // la dernière couche (fragments les plus tardifs / poussières) a fini

    private SpriteRenderer _flash;
    private SpriteRenderer _shade;
    private SpriteRenderer _core;
    private SpriteRenderer _veil;
    private SpriteRenderer _wave;
    private SpriteRenderer[] _splinters;
    private Vector3[] _splinterDirs;
    private float[] _splinterAngles;
    private float[] _splinterReach;
    private SpriteRenderer[] _motes;
    private Vector3[] _moteStart;
    private Vector3[] _moteDrift;
    private float[] _motePhase;
    private SpriteRenderer[] _fragments;
    private Vector3[] _fragmentDirs;
    private float[] _fragmentRadius;   // fraction du rayon de dégâts où le fragment se dépose
    private float[] _fragmentBorn;     // instant d'apparition (passage de l'onde)
    private float[] _fragmentBreak;    // instant où il vole en éclats
    private float[] _fragmentSpin;     // angle de départ
    private float[] _fragmentSpinSpeed;
    private float[] _fragmentSize;

    // Teintes dérivées de la couleur du tir : cœur presque blanc, teinte franche, bleu profond en fin de vie.
    private Color _flashColor, _hotColor, _midColor, _deepColor;
    private static readonly Color ShadeColor = new Color(0.03f, 0.08f, 0.3f);

    private float _radius;
    private float _waveSpin;
    private System.Action<ArcaneBurstRunner> _onComplete;
    private Coroutine _co;

    public void Play(Vector3 position, float radius, Color tint, System.Action<ArcaneBurstRunner> onComplete)
    {
        transform.position = position;
        _radius = Mathf.Max(0.5f, radius);
        _onComplete = onComplete;

        // Teinte du tir un peu plus saturée vers le bleu : sur l'herbe claire, le cyan pur paraissait délavé.
        _midColor = Color.Lerp(new Color(tint.r, tint.g, tint.b, 1f), new Color(0.1f, 0.55f, 1f), 0.35f);
        _flashColor = Color.Lerp(_midColor, Color.white, 0.85f);
        _hotColor = Color.Lerp(_midColor, Color.white, 0.5f);
        _deepColor = Color.Lerp(_midColor, new Color(0.12f, 0.2f, 0.75f), 0.65f);

        EnsureBuilt();

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Animate());
    }

    // Ordres de tri négatifs : les chiffres de dégâts (TextMeshPro, ordre 0) restent lisibles par-dessus l'explosion.
    private void EnsureBuilt()
    {
        if (_flash != null) return;

        _shade = FXSprites.MakeRenderer(transform, "Shade", FXSprites.Glow(), -19, 0f);
        _veil = FXSprites.MakeRenderer(transform, "Veil", FXSprites.Glow(), -18, 0f);
        _wave = FXSprites.MakeRenderer(transform, "ShatterWave", FXSprites.ShatterRing(), -16, 0.03f);
        _core = FXSprites.MakeRenderer(transform, "Core", FXSprites.CrystalShard(), -14, 0.05f);
        _flash = FXSprites.MakeRenderer(transform, "Flash", FXSprites.Glow(), -10, 0.06f);

        _fragments = new SpriteRenderer[FragmentCount];
        _fragmentDirs = new Vector3[FragmentCount];
        _fragmentRadius = new float[FragmentCount];
        _fragmentBorn = new float[FragmentCount];
        _fragmentBreak = new float[FragmentCount];
        _fragmentSpin = new float[FragmentCount];
        _fragmentSpinSpeed = new float[FragmentCount];
        _fragmentSize = new float[FragmentCount];
        for (int i = 0; i < FragmentCount; i++)
            _fragments[i] = FXSprites.MakeRenderer(transform, "Fragment" + i, FXSprites.CrystalFragment(i), -13, 0.07f);

        _splinters = new SpriteRenderer[SplinterCount];
        _splinterDirs = new Vector3[SplinterCount];
        _splinterAngles = new float[SplinterCount];
        _splinterReach = new float[SplinterCount];
        for (int i = 0; i < SplinterCount; i++)
            _splinters[i] = FXSprites.MakeRenderer(transform, "Splinter" + i, FXSprites.Splinter(), -12, 0.08f);

        _motes = new SpriteRenderer[MoteCount];
        _moteStart = new Vector3[MoteCount];
        _moteDrift = new Vector3[MoteCount];
        _motePhase = new float[MoteCount];
        for (int i = 0; i < MoteCount; i++)
            _motes[i] = FXSprites.MakeRenderer(transform, "Mote" + i, FXSprites.Glow(), -11, 0.1f);
    }

    private IEnumerator Animate()
    {
        // Angles tirés à chaque explosion : deux explosions au même endroit ne se ressemblent jamais trait pour trait.
        float coreSpin = Random.Range(0f, 360f);
        _waveSpin = Random.Range(0f, 360f);

        for (int i = 0; i < SplinterCount; i++)
        {
            float deg = (360f / SplinterCount) * i + Random.Range(-12f, 12f);
            float rad = deg * Mathf.Deg2Rad;
            _splinterDirs[i] = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            _splinterAngles[i] = deg;                       // SetFlat : +X du sprite tourné de deg = direction de vol
            _splinterReach[i] = Random.Range(0.75f, 1.0f);  // longueurs inégales : pas une étoile trop régulière
        }
        for (int i = 0; i < MoteCount; i++)
        {
            float rad = Random.Range(0f, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            _moteStart[i] = dir * Random.Range(0.15f, 0.6f) * _radius;
            _moteDrift[i] = dir * Random.Range(0.2f, 0.45f) * _radius + Vector3.up * Random.Range(0.6f, 1.1f);
            _motePhase[i] = Random.Range(0f, Mathf.PI * 2f);
        }

        // Fragments : décalés d'un demi-pas par rapport aux éclats (jamais deux couches alignées), chacun déposé au
        // moment précis où l'onde passe à son rayon, puis brisé un peu plus tard, chacun à son propre instant.
        float fragmentScale = Mathf.Clamp(_radius / 4f, 0.6f, 1.4f);
        for (int i = 0; i < FragmentCount; i++)
        {
            float deg = (360f / FragmentCount) * (i + 0.5f) + Random.Range(-9f, 9f);
            float rad = deg * Mathf.Deg2Rad;
            _fragmentDirs[i] = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            _fragmentRadius[i] = Random.Range(0.8f, 0.9f);
            _fragmentBorn[i] = WaveArrivalTime(_fragmentRadius[i]) + Random.Range(0f, 0.02f);
            _fragmentBreak[i] = _fragmentBorn[i] + FragmentPopDuration + FragmentHold + Random.Range(-0.05f, 0.06f);
            _fragmentSpin[i] = Random.Range(0f, 360f);
            _fragmentSpinSpeed[i] = Random.Range(60f, 140f) * (Random.value < 0.5f ? -1f : 1f);
            _fragmentSize[i] = Random.Range(0.75f, 1.0f) * fragmentScale;
            _fragments[i].color = Color.clear;
        }

        float t = 0f;
        while (t < TotalDuration)
        {
            t += Time.deltaTime;

            UpdateFlash(t);
            UpdateCore(t, coreSpin);
            UpdateWave(t);
            UpdateFragments(t);
            UpdateSplinters(t);
            UpdateMotes(t);

            yield return null;
        }

        _co = null;
        _onComplete?.Invoke(this);
    }

    private void UpdateFlash(float t)
    {
        float p = Mathf.Clamp01(t / FlashDuration);
        FXSprites.SetFlat(_flash, Mathf.Lerp(_radius * 0.3f, _radius * 1.1f, FXSprites.EaseOutCubic(p)), 0f);
        _flash.color = FXSprites.A(_flashColor, Mathf.Lerp(1f, 0f, p * p));
    }

    private void UpdateCore(float t, float spinDeg)
    {
        float p = Mathf.Clamp01(t / CoreDuration);
        float growP = Mathf.Clamp01(t / (CoreDuration * 0.5f));
        float diameter = Mathf.Lerp(0f, _radius * 1.6f, FXSprites.EaseOutBack(growP, 1.3f));
        FXSprites.SetFlat(_core, diameter, spinDeg + 40f * FXSprites.EaseOutCubic(p));

        float fadeP = Mathf.Clamp01((t - CoreDuration * 0.35f) / (CoreDuration * 0.65f));
        Color c = Color.Lerp(_hotColor, _deepColor, Mathf.Clamp01(p * 1.2f));
        _core.color = FXSprites.A(c, Mathf.Lerp(1f, 0f, FXSprites.Smooth(fadeP)));

        // Ombre bleu nuit sous l'étoile : donne du contraste sur le sol clair (sans elle, le cyan se fond dans l'herbe).
        FXSprites.SetFlat(_shade, diameter * 1.15f, 0f);
        _shade.color = FXSprites.A(ShadeColor, 0.5f * (1f - FXSprites.Smooth(fadeP)));
    }

    // Rayon de l'onde (fraction du rayon de dégâts) à l'instant t, et instant où elle atteint une fraction donnée
    // (inverse exact, pour déposer chaque fragment pile au passage de l'onde).
    private static float WaveFraction(float t)
    {
        return Mathf.Lerp(WaveStartFraction, 1f, FXSprites.EaseOutCubic(Mathf.Clamp01(t / WaveDuration)));
    }

    private static float WaveArrivalTime(float fraction)
    {
        float x = Mathf.Clamp01((fraction - WaveStartFraction) / (1f - WaveStartFraction));
        return WaveDuration * (1f - Mathf.Pow(1f - x, 1f / 3f));   // inverse de EaseOutCubic
    }

    // Le bord EXTÉRIEUR de ShatterRing est à ShatterRingOuter du rayon du sprite : diamètre 2R / ShatterRingOuter pour
    // qu'il tombe pile sur le rayon de dégâts une fois l'onde arrivée.
    private void UpdateWave(float t)
    {
        float full = _radius * 2f / FXSprites.ShatterRingOuter;
        float fraction = WaveFraction(t);
        float p = Mathf.Clamp01(t / WaveDuration);
        // Légère torsion pendant l'expansion : la couronne "claque" en s'ouvrant, elle ne fait pas que grossir.
        FXSprites.SetFlat(_wave, full * fraction, _waveSpin + 14f * FXSprites.EaseOutCubic(p));

        float fade = 1f - FXSprites.Smooth(Mathf.Clamp01((t - WaveDuration) / WaveFadeDuration));
        float appear = FXSprites.Smooth(Mathf.Clamp01(t / 0.04f));
        Color c = Color.Lerp(_flashColor, _midColor, FXSprites.Smooth(p));
        _wave.color = FXSprites.A(c, appear * fade);

        // Voile : remplit doucement la zone touchée (halo diffus, jamais un disque net comme le remplissage des
        // télégraphes), apparaît avec l'onde et s'efface juste après elle.
        FXSprites.SetFlat(_veil, full * Mathf.Lerp(0.35f, 1.1f, FXSprites.EaseOutCubic(p)), 0f);
        _veil.color = FXSprites.A(_midColor, 0.3f * (1f - FXSprites.Smooth(Mathf.Clamp01((t - 0.06f) / (WaveDuration + WaveFadeDuration)))));
    }

    private void UpdateFragments(float t)
    {
        for (int i = 0; i < FragmentCount; i++)
        {
            SpriteRenderer sr = _fragments[i];
            float local = t - _fragmentBorn[i];
            if (local < 0f) { sr.color = Color.clear; continue; }

            float pop = FXSprites.EaseOutBack(Mathf.Clamp01(local / FragmentPopDuration), 1.8f);
            float breakP = Mathf.Clamp01((t - _fragmentBreak[i]) / FragmentBreakDuration);
            float life = Mathf.Clamp01(local / (_fragmentBreak[i] - _fragmentBorn[i] + FragmentBreakDuration));

            // Dérive vers l'extérieur (ralentie), accélérée au moment de l'éclatement.
            float radial = _fragmentRadius[i] + FragmentDrift * FXSprites.EaseOutCubic(life) + 0.08f * breakP * breakP;
            sr.transform.localPosition = _fragmentDirs[i] * (radial * _radius) + Vector3.up * (0.07f + 0.15f * breakP);

            float angle = _fragmentSpin[i] + _fragmentSpinSpeed[i] * local * (1f + 2.5f * breakP);
            float size = _fragmentSize[i] * pop * Mathf.Lerp(1f, 0.35f, FXSprites.Smooth(breakP));
            FXSprites.SetFlat(sr, size, angle);

            // Né blanc (cristallisé par l'onde), prend la teinte, puis s'illumine juste avant de se briser.
            Color c = Color.Lerp(_flashColor, _midColor, FXSprites.Smooth(Mathf.Clamp01(local / 0.14f)));
            float glint = Mathf.Clamp01(1f - Mathf.Abs(t - _fragmentBreak[i]) / 0.05f);
            c = Color.Lerp(c, Color.white, 0.75f * glint);
            sr.color = FXSprites.A(c, 0.92f * (1f - FXSprites.Smooth(breakP)));   // légèrement translucide : du cristal
        }
    }

    private void UpdateSplinters(float t)
    {
        float p = Mathf.Clamp01(t / SplinterDuration);
        float eased = FXSprites.EaseOutCubic(p);
        float lift = Mathf.Sin(p * Mathf.PI) * 0.25f;   // léger arc : les éclats "volent" au lieu de glisser au sol

        for (int i = 0; i < SplinterCount; i++)
        {
            SpriteRenderer sr = _splinters[i];
            sr.transform.localPosition = _splinterDirs[i] * (eased * _radius * _splinterReach[i]) + Vector3.up * (0.08f + lift);
            // Long et fin au départ (vitesse), se raccourcit en ralentissant.
            FXSprites.SetFlat(sr, Mathf.Lerp(2.3f, 0.9f, eased) * Mathf.Clamp(_radius / 4f, 0.6f, 1.4f), _splinterAngles[i]);
            Color c = Color.Lerp(_flashColor, _midColor, Mathf.Clamp01(p * 1.5f));
            sr.color = FXSprites.A(c, p < 0.08f ? p / 0.08f : Mathf.Lerp(1f, 0f, (p - 0.08f) * (p - 0.08f) / 0.846f));
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
            float twinkle = 0.65f + 0.35f * Mathf.Sin(t * 30f + _motePhase[i]);
            FXSprites.SetFlat(sr, Mathf.Lerp(0.45f, 0.15f, p), 0f);
            float alpha = FXSprites.Smooth(Mathf.Clamp01(t / 0.1f)) * (1f - FXSprites.Smooth(Mathf.Clamp01((p - 0.35f) / 0.65f)));
            sr.color = FXSprites.A(Color.Lerp(_flashColor, _midColor, p), alpha * twinkle);
        }
    }
}
