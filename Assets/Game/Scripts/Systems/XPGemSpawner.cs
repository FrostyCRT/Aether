using UnityEngine;
using System.Collections.Generic;

public class XPGemSpawner : MonoBehaviour
{
    public static XPGemSpawner Instance { get; private set; }

    // Unification du rayon d'attraction (0f par défaut, s'agrandit au fil du jeu)
    public float AttractionRadius { get; private set; } = 0f;

    // OPTIMISATION : Liste réutilisable mise en cache pour éviter le "new List" à chaque mort
    private readonly List<XPGem.GemType> _gemsCalculationCache = new List<XPGem.GemType>();

    // AJOUTÉ (2026-10-22, retour utilisateur : "pourquoi il y a des xp au sol et d'autres en hauteur ?") - la
    // position d'atterrissage reprenait `position.y` (la position RACINE de l'ennemi au moment de sa mort), jamais
    // flattenée. Comme pour le joueur ([[player-pivot-is-torso-height-not-ground]]), le pivot racine varie selon le
    // modèle/l'anim (torse, VAT en cours de pose...) et n'est PAS toujours au sol - un ennemi dont la racine est à
    // hauteur de torse à l'instant de sa mort faisait atterrir sa gemme (et son mesh ancré par le bas, voir
    // XPGem.BuildGemQuadMesh) en pleine hauteur plutôt qu'au sol. Même convention déjà utilisée ailleurs dans le
    // projet pour les effets au sol (WeaponMudPuddle._groundY, PlayerController._dashWindGroundY) : un Y de sol FIXE
    // plutôt que celui, peu fiable, de l'émetteur. Seule la cible d'ATTERRISSAGE est flattenée - le point de spawn
    // (origine du petit saut depuis le corps, voir XPGem._popOrigin) garde la vraie hauteur de mort, inchangé.
    // MODIFIÉ (2026-10-24, retour utilisateur : "les xp sont trop bas en Y, donc ils passent sous la boue ou nova, ils
    // devraient être plus haut pour les voir en permanence") - 0 -> 0,3 : la flaque de Boue (MudPuddleZone) et la
    // Nova (CrystalSystem._novaVFXPrefab) sont des cylindres OPAQUES posés à Y=0,2 et épais de 0,1 (dessus à Y=0,25,
    // mesuré sur les prefabs) - une gemme posée à 0 était entièrement cachée dessous (vérifié par capture en Play).
    // Toujours un Y FIXE, identique pour toutes les gemmes (le problème du 2026-10-22 ne revient pas). Sans effet
    // sur le ramassage : contact HORIZONTAL (XPGem.ContactRadiusXZ, Y ignoré). Essai abandonné le même jour :
    // remonter seulement le mesh de la gemme - face caméra quasi verticale, il "remontait" vers l'avant (Z) et pas
    // en hauteur (Y), gemmes toujours cachées.
    private const float GemLandY = 0.3f;

    // RETIRÉ (2026-10-27, retour utilisateur : "pour les xp qui disparaissent, enlève ça, je ferai des tests moi-même
    // pour voir s'il y a des problèmes de performance, mais je ne veux pas qu'ils soient détruits, donc enlève la limite
    // d'xp max sur la map") - plus aucun plafond de gemmes au sol (l'ancien MaxGemsOnGround, ajouté le 2026-10-24 pour
    // les FPS : ~120 fps vers 130 gemmes, ~100 vers 400, ~70 vers 890) : aucune gemme n'est plus jamais fusionnée ni
    // détruite, elles restent toutes là où elles tombent.

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Appelé par XPSystem quand le joueur level up
    public void OnLevelUp(int level)
    {
        if (level >= 3)
            AttractionRadius = 4f; // S'active ou s'agrandit
    }

    // MODIFIÉ (2026-10-22, retour utilisateur : "c'est ce que j'avais dit, je voulais retrouver le nombre d'xp des
    // ennemis mais juste augmenter le coût pour passer au niveau supérieur [...] mais du coup pour les xp, je veux
    // qu'ils en donnent tous") - l'ancien système (2026-10-16) reportait le reste < 10 de chaque mort SILENCIEUSEMENT
    // sur la mort suivante (aucune gemme visible tant que le report n'avait pas franchi 10) : l'XP totale de la
    // partie restait exacte, mais CERTAINS kills ne donnaient visuellement RIEN - exactement le symptôme déjà
    // signalé le 2026-10-21 ("il ne donne pas dans tous les cas de l'xp, ce qui n'est pas normal dans un survivor"),
    // seulement à moitié corrigé à l'époque (la pente avait été allégée via le COÛT d'un niveau, XPSystem._baseXP -
    // ça reste en place - mais le symptôme "kill sans gemme visible" lui-même n'avait jamais été traité). Chaque
    // mort représente maintenant SA PROPRE valeur intégralement et IMMÉDIATEMENT : les paliers 50/20/10 habituels,
    // PLUS une dernière gemme (visuellement Small, valeur réelle exacte via xpOverride) pour le reste < 10 au lieu
    // de le reporter invisible sur la prochaine mort. Plus aucun kill qui rapporte de l'XP ne reste sans gemme.
    // Appelé à la mort d'un ennemi — calcule et spawne les gemmes (XPGemField, 2026-10-28 : plus d'ObjectPool)
    public void SpawnGems(Vector3 position, float xpValue)
    {
        float remainder = CalculateGems(xpValue); // remplit le cache interne, renvoie le reste (< 10) de CETTE mort

        for (int i = 0; i < _gemsCalculationCache.Count; i++)
            SpawnOneGem(position, _gemsCalculationCache[i], null);

        if (remainder > 0.001f)
            SpawnOneGem(position, XPGem.GemType.Small, remainder);
    }

    private void SpawnOneGem(Vector3 position, XPGem.GemType gemType, float? xpOverride)
    {
        // Offset aléatoire pour espacer les gemmes au sol
        Vector3 offset = new Vector3(
            Random.Range(-0.8f, 0.8f),
            0f,
            Random.Range(-0.8f, 0.8f)
        );

        // MODIFIÉ (2026-10-04) - la gemme part PILE du point de mort (position), pas déjà décalée : XPGemField
        // l'anime en un petit arc depuis ce point jusqu'à sa position finale (position + offset), comme si elle
        // jaillissait de l'ennemi au lieu d'apparaître à plat sur le sol (retour utilisateur : "pas le côté
        // satisfaisant").
        Vector3 landTarget = position + offset;
        landTarget.y = GemLandY;
        XPGemField.Instance.Spawn(gemType, position, landTarget, xpOverride);
    }

    // Algorithme optimisé "rendre la monnaie" sans aucune allocation mémoire.
    // Renvoie l'XP non convertie en gemmes de 10/20/50 (reste < 10) - à la charge de l'appelant de la représenter
    // (jamais la jeter ni la reporter silencieusement, voir le commentaire de SpawnGems ci-dessus).
    private float CalculateGems(float xpValue)
    {
        _gemsCalculationCache.Clear(); // On vide le cache de la mort précédente
        int remaining = Mathf.FloorToInt(xpValue);
        float fraction = xpValue - remaining;

        while (remaining >= 50)
        {
            _gemsCalculationCache.Add(XPGem.GemType.Large);
            remaining -= 50;
        }
        while (remaining >= 20)
        {
            _gemsCalculationCache.Add(XPGem.GemType.Medium);
            remaining -= 20;
        }
        while (remaining >= 10)
        {
            _gemsCalculationCache.Add(XPGem.GemType.Small);
            remaining -= 10;
        }
        return remaining + fraction;
    }

    // AJOUTÉ (2026-10-04, suite 3) - retour utilisateur sur la mort d'un boss : "y'a juste un tas de gemme qui se
    // superpose, moi je veux que ce soit impressionnant et gratifiant visuellement". SpawnGems() (au-dessus) convient
    // pour un ennemi normal (peu de gemmes, pas d'enjeu visuel), mais un boss donne souvent assez d'XP pour produire
    // une dizaine de gemmes qui, avec un simple petit offset aléatoire, retombent toutes les unes sur les autres.
    // Ici : (1) un flash + anneau d'onde de choc au moment de l'impact, (2) un NOMBRE PLAFONNÉ de gemmes - au-delà du
    // plafond, on consolide le surplus dans une seule grosse gemme plutôt que d'en empiler davantage, (3) chaque
    // gemme est tirée dans une direction radiale distincte (fontaine à 360°) avec un retard en cascade, au lieu d'un
    // offset aléatoire local.
    private const int MaxBurstGems = 10;

    public void SpawnBossGemBurst(Vector3 position, float xpValue)
    {
        StartCoroutine(BossGemBurstRoutine(position, xpValue));
    }

    private System.Collections.IEnumerator BossGemBurstRoutine(Vector3 position, float xpValue)
    {
        ExpandingRingVFX.Spawn(position, 3.5f, new Color(1f, 0.93f, 0.6f), 0.5f);

        float remainder = CalculateGems(xpValue); // remplit _gemsCalculationCache (règle "rendre la monnaie")
        // CORRIGE (2026-10-16, audit - erreur relevée en Play) - cette coroutine lit la liste sur plusieurs frames
        // (une gemme toutes les 0,03 s) : un ennemi tué pendant la gerbe appelait SpawnGems(), qui VIDE et remplit ce
        // même cache partagé -> mauvais types de gemmes, ou ArgumentOutOfRangeException qui interrompait la gerbe
        // (XP du boss perdue). Copie locale propre à cette gerbe (une seule allocation par mort de boss).
        List<XPGem.GemType> gems = new List<XPGem.GemType>(_gemsCalculationCache);

        // Au-delà du plafond, on fusionne tout le surplus (du début de la liste, donc les plus petites valeurs
        // d'abord) dans UNE seule grosse gemme qui porte leur XP cumulée - jamais de gemme perdue, juste moins
        // d'objets à l'écran.
        int count = gems.Count;
        float mergedOverflowXP = 0f;
        int startIndex = 0;
        if (count > MaxBurstGems)
        {
            int overflowCount = count - (MaxBurstGems - 1);
            for (int i = 0; i < overflowCount; i++)
                mergedOverflowXP += GemTypeValue(gems[i]);
            startIndex = overflowCount;
        }

        // MODIFIÉ (2026-10-22, même retour utilisateur que SpawnGems ci-dessus) - le reste < 10 rejoint directement
        // cette gerbe au lieu d'être reporté silencieusement sur la prochaine mort - jamais d'XP de boss invisible.
        // Visuel Large si un vrai surplus de plafond a été fusionné dedans, sinon Small (juste le petit reste seul).
        float totalOverflowXP = mergedOverflowXP + remainder;
        XPGem.GemType overflowVisual = mergedOverflowXP > 0f ? XPGem.GemType.Large : XPGem.GemType.Small;

        int remainingGemCount = count - startIndex;
        int totalToSpawn = remainingGemCount + (totalOverflowXP > 0.001f ? 1 : 0);

        int spawned = 0;
        float startAngle = Random.Range(0f, Mathf.PI * 2f);

        if (totalOverflowXP > 0.001f)
        {
            SpawnBurstGem(position, overflowVisual, totalOverflowXP, startAngle, spawned, totalToSpawn);
            spawned++;
            yield return new WaitForSeconds(0.03f);
        }

        for (int i = startIndex; i < count; i++)
        {
            SpawnBurstGem(position, gems[i], null, startAngle, spawned, totalToSpawn);
            spawned++;
            yield return new WaitForSeconds(0.03f);
        }
    }

    private void SpawnBurstGem(Vector3 position, XPGem.GemType type, float? xpOverride, float startAngle, int index, int total)
    {
        float angle = startAngle + (Mathf.PI * 2f / Mathf.Max(1, total)) * index + Random.Range(-0.2f, 0.2f);
        float burstRadius = Random.Range(1.8f, 3.2f);
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * burstRadius;

        Vector3 landTarget = position + offset;
        landTarget.y = GemLandY;
        XPGemField.Instance.Spawn(type, position, landTarget, xpOverride);
    }

    private static float GemTypeValue(XPGem.GemType type) => XPGem.Value(type);
}