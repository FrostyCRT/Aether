using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// AJOUTÉ (2026-11-21, retour utilisateur : « rendre l'arrivée des boss un peu plus impressionnante ») - mise en scène de
// l'arrivée d'un boss. Déroulé complet (piloté par WaveManager.BossArrivalRoutine) :
//   1. l'heure du boss arrive : la musique s'éteint en fondu (0,8 s), plus d'apparitions d'ennemis ;
//   2. silence : le boss tombe (onde de choc, secousse, cri), le jeu entier se fige FreezeDuration secondes
//      (GameManager.SetIntroLock : joueur, boss, armes, projectiles, chrono) ; pendant ce gel, bandes de cinéma en haut et
//      en bas, liseré rouge qui pulse sur les bords au moment du cri, nom du boss en fondu long, barre de vie qui se remplit ;
//   3. le jeu reprend et la musique du boss démarre d'un coup.
// Pendant le gel, Active = vrai : les effets d'arrivée (BossFX, ExpandingRingVFX, ImpactSparkVFX), la secousse et le zoom de
// caméra passent en temps réel via DeltaTime / TimeNow, tout le reste est figé par Time.timeScale = 0.
// Les cris : un clip par boss (BossBase._roarClip, vide tant que l'utilisateur ne les a pas fournis : alors pas de cri).
// Les bandes passent SOUS le HUD (canevas -100, HUD à 0) et au-dessus du voile de nuit de l'Éclipse (-500).
public class BossIntro : MonoBehaviour
{
    public const float MusicFadeOut = 0.8f;
    public const float FreezeDuration = 1.5f;

    private const float BandHeightFraction = 0.08f;   // hauteur de chaque bande, en part de l'écran (0,11 mordait sur le nom du boss)
    private const float BandsIn = 0.3f, BandsOut = 0.4f;

    public static bool Active { get; private set; }
    public static float DeltaTime => Active ? Time.unscaledDeltaTime : Time.deltaTime;
    public static float TimeNow => Active ? Time.unscaledTime : Time.time;

    private static BossIntro _instance;

    private RectTransform _top, _bottom;
    private Image _vignette;
    private AudioSource _audio;
    private Coroutine _routine;

    // Début du gel : bandes, liseré, cri. Le gel lui-même (SetIntroLock) est posé par l'appelant.
    public static void Begin(AudioClip roar)
    {
        Active = true;
        Ensure();
        if (_instance._routine != null) _instance.StopCoroutine(_instance._routine);
        _instance._routine = _instance.StartCoroutine(_instance.Play(roar));
    }

    // Fin du gel : les bandes et le liseré repartent tout seuls (fin de Play).
    public static void End() => Active = false;

    private void OnDestroy()
    {
        if (_instance != this) return;
        _instance = null;
        Active = false;   // changement de scène en plein gel : rien ne doit rester en temps réel
    }

    private static void Ensure()
    {
        if (_instance != null) return;
        GameObject go = new GameObject("BossIntro");
        _instance = go.AddComponent<BossIntro>();
        _instance.Build();
    }

    private void Build()
    {
        Canvas c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = -100;
        CanvasScaler sc = gameObject.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;

        _vignette = NewImage("RedVignette", VignetteSprite(), new Color(0.6f, 0f, 0.02f, 0f));
        RectTransform vr = _vignette.rectTransform;
        vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;

        _top = NewBand("BandTop", 1f);
        _bottom = NewBand("BandBottom", 0f);

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _audio.ignoreListenerPause = true;
        AudioMixer mixer = Resources.Load<AudioMixer>("MainMixer");
        if (mixer != null)
        {
            AudioMixerGroup[] groups = mixer.FindMatchingGroups("SFX");
            if (groups != null && groups.Length > 0) _audio.outputAudioMixerGroup = groups[0];
        }
        SetBands(0f);
    }

    private Image NewImage(string name, Sprite sprite, Color color)
    {
        Image img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(transform, false);
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private RectTransform NewBand(string name, float anchorY)
    {
        Image img = NewImage(name, null, Color.black);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, anchorY);
        rt.anchorMax = new Vector2(1f, anchorY);
        rt.pivot = new Vector2(0.5f, anchorY);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    // k = 0 : bandes rentrées ; 1 : bandes entièrement sorties.
    private void SetBands(float k)
    {
        RectTransform root = (RectTransform)transform;
        float h = root.rect.height > 1f ? root.rect.height : 1080f;
        float bh = h * BandHeightFraction * k;
        _top.sizeDelta = new Vector2(0f, bh);
        _bottom.sizeDelta = new Vector2(0f, bh);
    }

    private IEnumerator Play(AudioClip roar)
    {
        if (roar != null && _audio != null) _audio.PlayOneShot(roar);

        float total = FreezeDuration + BandsOut;
        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;

            // bandes : glissent en BandsIn, restent pendant le gel, repartent en BandsOut une fois le jeu repris
            float bands = t < FreezeDuration
                ? FXSprites.EaseOutCubic(Mathf.Clamp01(t / BandsIn))
                : 1f - FXSprites.EaseOutCubic(Mathf.Clamp01((t - FreezeDuration) / BandsOut));
            SetBands(bands);

            // liseré rouge : coup franc au cri, retombe, respire doucement, s'efface avec les bandes
            float a;
            if (t < 0.08f) a = Mathf.Lerp(0f, 0.75f, t / 0.08f);
            else if (t < 0.7f) a = Mathf.Lerp(0.75f, 0.3f, (t - 0.08f) / 0.62f);
            else a = 0.3f + 0.08f * Mathf.Sin((t - 0.7f) * 9f);
            if (t > FreezeDuration) a *= 1f - Mathf.Clamp01((t - FreezeDuration) / BandsOut);
            Color c = _vignette.color; c.a = a; _vignette.color = c;

            yield return null;
        }
        SetBands(0f);
        Color end = _vignette.color; end.a = 0f; _vignette.color = end;
        _routine = null;
    }

    // Bords rouges, centre transparent (dégradé radial elliptique, calculé une fois).
    private static Sprite _vignetteSprite;
    private static Sprite VignetteSprite()
    {
        if (_vignetteSprite != null) return _vignetteSprite;
        const int n = 256;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;   // 0 au centre, 1 dans les coins
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.95f, d));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        _vignetteSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return _vignetteSprite;
    }
}
