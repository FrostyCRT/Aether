using System.Collections.Generic;
using UnityEngine;

// AJOUTÉ (2026-10-29, demande utilisateur : "Quand on clique sur un onglet, je veux qu'on voit d'abord le fond d'écran
// puis les éléments spawn la seconde d'après en fondue [...] ça permet de voir le fond d'écran entier, car c'est des vrai
// backgrounds du lore") - apparition d'une page du menu principal : d'abord le décor seul, puis le contenu en fondu.
//   - Chaque page a son propre minuteur, qui démarre quand on la QUITTE : l'apparition ne se rejoue que si on revient
//     sur la page au moins RevealCooldown secondes plus tard (ou à sa toute première ouverture). Rester sur une page,
//     même longtemps, ne relance rien.
//   - Les minuteurs survivent aux changements de scène (statiques) : revenir d'une partie de plus d'une minute rejoue
//     l'apparition de l'Accueil.
//   - Le décor = les enfants listés dans BackgroundNames (fond, voile sombre, particules d'ambiance) ; tout le reste est
//     du contenu, masqué par un CanvasGroup (alpha 0, ni clics ni navigation) pendant l'attente puis révélé en fondu.
//     Un enfant qui a déjà son propre CanvasGroup (bulle de confirmation, panneau de détail) n'est pas touché : il gère
//     lui-même sa visibilité.
// Posé tout seul sur chaque page par MainMenuManager, rien à placer dans la scène. Temps réel (timeScale ignoré).
public class MenuPageReveal : MonoBehaviour
{
    public const float RevealCooldown = 60f;   // secondes hors de la page avant de rejouer l'apparition
    private const float Delay = 0.5f;          // décor seul (1 s jugé trop long par l'utilisateur, 2026-10-29)
    private const float FadeDuration = 0.6f;   // fondu du contenu

    private static readonly HashSet<string> BackgroundNames = new HashSet<string>
    {
        "MenuBackground", "SettingsBackground", "PanelBackground", "Background", "Dim", "Ambient"
    };

    // Moment où chaque page a été quittée (Time.realtimeSinceStartup), par nom de page.
    private static readonly Dictionary<string, float> _leftAt = new Dictionary<string, float>();

    private readonly List<CanvasGroup> _groups = new List<CanvasGroup>();
    private float _startTime = -1f;

    // À appeler juste après avoir activé la page : rejoue l'apparition si la page n'a jamais été ouverte ou si elle a
    // été quittée il y a au moins RevealCooldown secondes.
    public void OnEnter()
    {
        bool play = !_leftAt.TryGetValue(name, out float left) || Time.realtimeSinceStartup - left >= RevealCooldown;
        if (!play) { Finish(); return; }

        _startTime = Time.unscaledTime;
        Collect();
        Apply(0f);
    }

    // À appeler quand on quitte la page (autre onglet, lancement d'une partie) : démarre son minuteur.
    public void OnLeave()
    {
        _leftAt[name] = Time.realtimeSinceStartup;
        Finish();
    }

    private void Collect()
    {
        foreach (Transform child in transform)
        {
            if (BackgroundNames.Contains(child.name)) continue;
            CanvasGroup g = child.GetComponent<CanvasGroup>();
            if (g != null && !_groups.Contains(g)) continue;   // CanvasGroup propre à l'élément : on n'y touche pas
            if (g == null)
            {
                g = child.gameObject.AddComponent<CanvasGroup>();
                _groups.Add(g);
            }
        }
    }

    private void Update()
    {
        if (_startTime < 0f) return;
        float k = Mathf.Clamp01((Time.unscaledTime - _startTime - Delay) / FadeDuration);
        if (k >= 1f) { Finish(); return; }
        Apply(Mathf.SmoothStep(0f, 1f, k));
    }

    private void Apply(float alpha)
    {
        bool interactive = alpha > 0f;
        foreach (CanvasGroup g in _groups)
        {
            if (g == null) continue;
            g.alpha = alpha;
            g.blocksRaycasts = interactive;
            g.interactable = interactive;
        }
    }

    // Contenu pleinement visible (fin du fondu, page quittée en cours d'apparition) ; les CanvasGroup ajoutés restent,
    // neutres, pour la prochaine apparition.
    private void Finish()
    {
        _startTime = -1f;
        Apply(1f);
    }

    private void OnDisable()
    {
        if (_startTime >= 0f) Finish();
    }
}
