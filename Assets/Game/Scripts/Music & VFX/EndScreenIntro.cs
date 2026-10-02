using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A poser sur le panneau de Victoire ET sur celui de Défaite (les GameObjects assignés aux champs "Victory Panel" et
// "Game Over Panel" de GameUI). Quand le panneau s'affiche : le fond fait un fondu, la carte centrale ("CenterCard")
// monte avec un petit rebond, puis ses éléments apparaissent en cascade (titre, portrait, stats, boutons...), même
// famille d'animation que le panel de level-up.
//
// Robuste face au reste de l'interface : les déplacements/échelles sont appliqués en DIFFÉRENCE (on ajoute puis retire
// exactement ce qu'on a ajouté), donc une mise en page recalculée en cours de route (textes qui changent, chips de défi...)
// n'est jamais écrasée. L'animation démarre une image après l'activation pour que GameUI ait fini de préparer le panneau,
// et le fond est masqué pendant cette image (aucun flash). Temps réel (unscaled) : indépendant du ralenti / de la pause.
public class EndScreenIntro : MonoBehaviour
{
    [Header("Général")]
    [Tooltip("Décoche pour revenir à l'apparition instantanée d'avant.")]
    [SerializeField] private bool _introEnabled = true;
    [Tooltip("La carte centrale du panneau. Vide = l'enfant nommé \"CenterCard\" est cherché automatiquement.")]
    [SerializeField] private RectTransform _card;
    [Tooltip("Durée du fondu du panneau entier (fond compris).")]
    [SerializeField] private float _panelFadeDuration = 0.25f;

    [Header("Carte centrale")]
    [SerializeField] private float _cardDuration = 0.4f;
    [Tooltip("Distance (pixels du Canvas) depuis laquelle la carte monte.")]
    [SerializeField] private float _cardSlide = 60f;
    [Tooltip("Échelle de départ de la carte (1 = pas de variation).")]
    [SerializeField] private float _cardStartScale = 0.9f;

    [Header("Éléments de la carte (cascade)")]
    [Tooltip("Fait aussi apparaître en cascade les éléments affichés de la carte (titre, portrait, stats...).")]
    [SerializeField] private bool _cascadeCardChildren = true;
    [SerializeField] private float _childFirstDelay = 0.12f;
    [SerializeField] private float _childStagger = 0.07f;
    [SerializeField] private float _childDuration = 0.3f;
    [SerializeField] private float _childSlide = 28f;
    [SerializeField] private float _childStartScale = 0.95f;
    [Tooltip("Nombre maximum d'éléments de la carte animés individuellement.")]
    [SerializeField] private int _maxCascadedChildren = 8;

    private class IntroTarget
    {
        public RectTransform rt;
        public CanvasGroup group;
        public float delay, duration, slide, startScale;
        public Vector2 appliedOffset;
        public float appliedScale = 1f;
    }

    private readonly List<IntroTarget> _targets = new List<IntroTarget>();
    private CanvasGroup _rootGroup;
    private Coroutine _routine;

    private void OnEnable()
    {
        if (!_introEnabled) return;

        if (_rootGroup == null)
        {
            _rootGroup = GetComponent<CanvasGroup>();
            if (_rootGroup == null) _rootGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Masque tout pendant l'image d'attente (GameUI prépare encore le contenu juste après l'activation).
        _rootGroup.alpha = 0f;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Play());
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        for (int i = 0; i < _targets.Count; i++)
        {
            IntroTarget t = _targets[i];
            if (t.rt == null) continue;
            Apply(t, 1f);
            if (t.group != null) t.group.alpha = 1f;
        }
        _targets.Clear();

        if (_rootGroup != null) _rootGroup.alpha = 1f;
    }

    private IEnumerator Play()
    {
        yield return null;   // laisse GameUI finir de préparer le panneau (textes, éléments affichés ou non)

        _targets.Clear();

        RectTransform card = _card;
        if (card == null)
        {
            Transform found = transform.Find("CenterCard");
            if (found != null) card = found as RectTransform;
        }

        if (card != null)
        {
            AddTarget(card, 0.05f, _cardDuration, _cardSlide, _cardStartScale);

            if (_cascadeCardChildren)
            {
                int n = 0;
                for (int i = 0; i < card.childCount && n < _maxCascadedChildren; i++)
                {
                    Transform child = card.GetChild(i);
                    if (!child.gameObject.activeSelf) continue;
                    RectTransform rt = child as RectTransform;
                    if (rt == null) continue;

                    AddTarget(rt, _childFirstDelay + n * _childStagger, _childDuration, _childSlide, _childStartScale);
                    n++;
                }
            }
        }

        // Etat de départ appliqué avant le premier rendu.
        float total = _panelFadeDuration;
        for (int i = 0; i < _targets.Count; i++)
        {
            Apply(_targets[i], 0f);
            total = Mathf.Max(total, _targets[i].delay + _targets[i].duration);
        }

        float elapsed = 0f;
        while (elapsed < total)
        {
            elapsed += Time.unscaledDeltaTime;

            _rootGroup.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _panelFadeDuration));

            for (int i = 0; i < _targets.Count; i++)
            {
                IntroTarget t = _targets[i];
                if (t.rt == null) continue;
                Apply(t, Mathf.Clamp01((elapsed - t.delay) / t.duration));
            }

            yield return null;
        }

        for (int i = 0; i < _targets.Count; i++)
        {
            IntroTarget t = _targets[i];
            if (t.rt == null) continue;
            Apply(t, 1f);
            if (t.group != null) t.group.alpha = 1f;
        }
        _rootGroup.alpha = 1f;

        _targets.Clear();
        _routine = null;
    }

    private void AddTarget(RectTransform rt, float delay, float duration, float slide, float startScale)
    {
        CanvasGroup group = rt.GetComponent<CanvasGroup>();
        if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();

        _targets.Add(new IntroTarget
        {
            rt = rt,
            group = group,
            delay = delay,
            duration = Mathf.Max(0.01f, duration),
            slide = slide,
            startScale = startScale
        });
    }

    // p : avancement 0 -> 1. Montée + rebond d'échelle + fondu, appliqués en DIFFÉRENCE par rapport à ce qui a déjà été
    // appliqué (jamais en valeur absolue) pour ne pas écraser une mise en page qui change pendant l'animation.
    private static void Apply(IntroTarget t, float p)
    {
        Vector2 newOffset = new Vector2(0f, Mathf.Lerp(-t.slide, 0f, EaseOutCubic(p)));
        float newScale = Mathf.LerpUnclamped(t.startScale, 1f, EaseOutBack(p));

        t.rt.anchoredPosition += newOffset - t.appliedOffset;
        t.appliedOffset = newOffset;

        t.rt.localScale *= newScale / Mathf.Max(0.0001f, t.appliedScale);
        t.appliedScale = newScale;

        if (t.group != null) t.group.alpha = Mathf.Clamp01(p * 2f);
    }

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.3f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}