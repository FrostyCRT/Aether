using UnityEngine;
using System.Collections.Generic;

public class DamageNumberSpawner : MonoBehaviour
{
    public static DamageNumberSpawner Instance { get; private set; }

    // MODIFIÉ (2026-10-04) - retour utilisateur : "je voulais une couleur différente par type d'arme... sauf pour
    // double tir parce que c'est juste le tir de base en x2... et la couleur doit correspondre à l'icône de la
    // carte/upgrade". Les 5 couleurs génériques d'avant (un bucket par TYPE de dégâts - projectile/zone/orbital/
    // critique/joueur, partagé par plusieurs armes sans rapport) sont remplacées par une couleur PAR ARME, choisie
    // en échantillonnant la vraie teinte dominante de chaque icône de carte (Assets/Game/Assets 2D/UI/UpgradeCard)
    // via un script d'analyse de pixels (histogramme de teinte pondéré par saturation, pour ignorer les reflets/
    // paillettes isolés et capter la couleur réellement dominante du dessin), puis ramenée à une saturation/
    // luminosité lisible en jeu (un pixel d'icône à pleine résolution n'a pas besoin d'être aussi lisible qu'un
    // chiffre qui défile sur l'herbe). Tir de base ET Double Tir partagent volontairement `ColorProjectile`
    // (Double Tir ne fait QUE dupliquer le tir de base, sans arme propre - demande explicite de l'utilisateur).
    public static readonly Color ColorProjectile = new Color(0f, 0.8f, 1f);   // tir de base + Double Tir
    public static readonly Color ColorFireball = new Color(1f, 0.42f, 0.08f); // Boule de Feu - orange flamme
    public static readonly Color ColorKnives = new Color(0.82f, 0.22f, 0.9f); // Couteaux - magenta/violet (aura du dessin)
    public static readonly Color ColorAOE = new Color(0.4f, 0.9f, 0.2f);      // Aura (Kael) - vert, déjà établi
    public static readonly Color ColorOrbital = new Color(0.95f, 0.55f, 0.75f); // Orbitaux - rose (accent dominant du dessin)
    // MODIFIÉ (2026-10-04) - retour utilisateur : "il y a beaucoup de bleu, pas assez de couleur chaude... peut-être
    // qu'il faudrait repasser la foudre en jaune et qu'on change l'icône". Redevient jaune/or électrique (teinte
    // d'origine du projet avant le chantier couleurs-par-icône) - l'icône actuelle (bleue) sera remplacée pour
    // correspondre (voir prompt MetaIA fourni à l'utilisateur), donc la couleur recommence à "correspondre à
    // l'icône" une fois celle-ci régénérée.
    public static readonly Color ColorLightning = new Color(1f, 0.92f, 0.3f);   // Foudre - jaune/or électrique
    public static readonly Color ColorMud = new Color(0.75f, 0.58f, 0.15f);     // Boue - ambre/kaki
    // MODIFIÉ (2026-10-04) - retour utilisateur : "la couleur de l'orbe rebondissant ressemble presque à celle des
    // couteaux" (les deux étaient dans la même famille violette). La Foudre libère le bleu pur en redevenant jaune
    // (ligne au-dessus) - l'Orbe rebondissant en profite : toujours tiré du même dessin (le cristal bleu de l'icône,
    // pas son halo orange), mais recentré sur un bleu pur/azur au lieu de bleu-violet, loin de Couteaux (violet) ET
    // de ManaVortex (turquoise)/tir de base (cyan pâle).
    public static readonly Color ColorBouncingOrb = new Color(0.15f, 0.55f, 0.95f); // Orbe rebondissant - bleu azur (cristal du dessin)
    public static readonly Color ColorManaVortex = new Color(0.25f, 0.85f, 0.65f); // Fusion ManaVortex - turquoise
    public static readonly Color ColorCritical = new Color(1f, 0.6f, 0f);   // critique générique / éclats... - inchangé
    // AJOUTÉ (2026-10-04) - retour utilisateur : Nova et Ultime (Cristal) n'ont pas d'icône de carte à échantillonner
    // (pas des upgrades piochées, des capacités du Cristal) - couleurs choisies à la main pour rester distinctes de
    // toutes les armes ci-dessus. Ultime : rouge, comble l'espace libre entre Orbitaux (rose) et Boule de Feu
    // (orange) sur la roue des teintes. Nova : indigo/violet-bleu, comble l'espace libre entre le nouveau bleu de
    // l'Orbe rebondissant et le violet des Couteaux - look "éclat arcane", cohérent avec un Cristal.
    public static readonly Color ColorUltimate = new Color(0.95f, 0.15f, 0.1f);
    public static readonly Color ColorNova = new Color(0.4f, 0.25f, 0.95f);
    public static readonly Color ColorPlayer = new Color(1f, 0.2f, 0.2f);

    [SerializeField] private float _fuseWindow = 0.15f; // fenêtre de fusion en secondes

    // MODIFIÉ (2026-10-04) - retour utilisateur : "les dégâts qui ne sont pas de la même source mais qui se
    // cumulent ont une couleur qui ne va pas avec l'arme en question, vu que ça prend la couleur de la première
    // arme qui a eu un contact avec la cible". La fusion fusionnait TOUT ce qui touchait la même cible dans la
    // fenêtre, peu importe l'arme - un coup d'Aura (vert) arrivant juste après un coup Orbital (blanc) gardait le
    // blanc du premier arrivé. Clé de fusion élargie à (cible, couleur) : chaque type d'arme fusionne ses propres
    // coups rapprochés entre eux (toujours un seul chiffre par arme, pas de spam), mais reste un indicateur séparé
    // et correctement coloré des autres armes qui touchent en même temps - "plusieurs indicateurs selon le type
    // d'arme avec sa couleur spécifique", comme demandé.
    private Dictionary<(Transform, Color), DamageNumber> _activeNumbers = new Dictionary<(Transform, Color), DamageNumber>();

    // Décalage FIXE par couleur (jamais aléatoire d'un coup à l'autre) : quand plusieurs armes touchent la même
    // cible en même temps, leurs indicateurs se répartissent proprement au lieu de s'empiler pile au même endroit.
    // Attribué à la première apparition de chaque couleur.
    // CORRIGÉ (2026-10-04) - retour utilisateur : "il y a un problème avec l'affichage des dégâts du boss".
    // Vérifié en jeu : un décalage purement HORIZONTAL (±0,3 à ±1 unité) suffisait pour de petits ennemis vus de
    // près, mais sur un boss - où les nombres flottent bien plus haut (voir BossBase.DamageNumberHeight) et sont
    // vus de plus loin - cet écart ne représente plus que quelques pixels à l'écran : avec plusieurs armes qui le
    // frappent en même temps (très fréquent en combat de boss réel), tous les chiffres retombaient quasiment les
    // uns sur les autres, illisibles. Décalage étalé en X ET EN Y (motif dispersé, pas juste une ligne) avec des
    // valeurs plus franches - reste lisible à n'importe quelle distance de caméra, petit ennemi ou boss.
    // CORRIGÉ (2026-10-06, retour utilisateur "UI du pause panel... espacement des chiffres de dégâts en combat
    // de boss") - seulement 7 décalages pour 13 couleurs d'armes distinctes (voir les constantes ColorXXX
    // ci-dessus) : au-delà de 7 armes différentes actives en même temps (courant en combat de boss avec un build
    // avancé), 2 couleurs se partageaient le même lane et leurs chiffres se superposaient (vérifié en jeu :
    // "100" cyan et "100!" orange quasi illisibles l'un sur l'autre). 16 décalages (13 couleurs actuelles + marge
    // pour de futures armes/fusions), toujours dispersés en X ET Y.
    private readonly Dictionary<Color, int> _laneByColor = new Dictionary<Color, int>();
    private int _nextLane = 0;
    private static readonly Vector2[] LaneOffsets =
    {
        new Vector2(0f, 0f),
        new Vector2(0.9f, 0.5f),
        new Vector2(-0.9f, -0.4f),
        new Vector2(1.7f, -0.7f),
        new Vector2(-1.7f, 0.5f),
        new Vector2(0.5f, 1.1f),
        new Vector2(-0.5f, -1.1f),
        new Vector2(1.3f, 1.3f),
        new Vector2(-1.3f, -1.3f),
        new Vector2(2.0f, 0.2f),
        new Vector2(-2.0f, -0.2f),
        new Vector2(0.2f, -1.7f),
        new Vector2(-0.2f, 1.7f),
        new Vector2(1.5f, -1.5f),
        new Vector2(-1.5f, 1.5f),
        new Vector2(0f, 2.0f),
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // target est optionnel : null = comportement d'avant, jamais de fusion (utile pour le joueur par ex.)
    public void Spawn(Vector3 position, float damage, Color color, Transform target = null, bool isCritical = false)
    {
        if (ObjectPool.Instance == null) return;

        // Réglages du joueur (Paramètres > Jeu) : nombres de dégâts infligés / subis (le joueur = rouge)
        bool taken = color == ColorPlayer;
        if (!GameSettings.GetBool(taken ? GameSettings.DamageTaken : GameSettings.DamageDealt)) return;

        var key = (target, color);
        if (target != null && _activeNumbers.TryGetValue(key, out DamageNumber existing)
            && existing != null && existing.gameObject.activeSelf
            && existing.ElapsedTime <= _fuseWindow)
        {
            existing.AddDamage(damage, isCritical);
            return;
        }

        if (!_laneByColor.TryGetValue(color, out int lane))
        {
            lane = _nextLane % LaneOffsets.Length;
            _nextLane++;
            _laneByColor[color] = lane;
        }

        Vector2 offset = LaneOffsets[lane];
        Vector3 spawnPos = position + Vector3.up * (1.5f + offset.y) + Vector3.right * offset.x;
        GameObject go = ObjectPool.Instance.Get("DamageNumber", spawnPos, Quaternion.identity);
        if (go == null) return;

        DamageNumber dn = go.GetComponent<DamageNumber>();
        if (dn != null)
        {
            dn.Init(damage, color, target, isCritical);
            if (target != null) _activeNumbers[key] = dn;
        }
    }
}