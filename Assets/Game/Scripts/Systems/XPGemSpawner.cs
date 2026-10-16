using UnityEngine;
using System.Collections.Generic;

public class XPGemSpawner : MonoBehaviour
{
    public static XPGemSpawner Instance { get; private set; }

    private Transform _playerTransform;

    // Unification du rayon d'attraction (0f par défaut, s'agrandit au fil du jeu)
    public float AttractionRadius { get; private set; } = 0f;

    // OPTIMISATION : Liste réutilisable mise en cache pour éviter le "new List" à chaque mort
    private readonly List<XPGem.GemType> _gemsCalculationCache = new List<XPGem.GemType>();

    // AJOUTÉ (2026-10-16, audit version finale) - les gemmes valent 10/20/50 : le reste < 10 de chaque mort était
    // jeté (ex. 15 XP -> une gemme de 10, 5 perdus ; jusqu'à ~12-15 % de l'XP entre 2 et 8 min selon les paliers).
    // Ce reste est maintenant reporté sur la mort suivante : l'XP totale de la partie est exacte, sans créer de
    // gemme supplémentaire (aucun coût en objets à l'écran). La courbe de niveaux (XPSystem._baseXP) a été
    // relevée en conséquence.
    private float _xpCarry = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;
    }

    // Appelé par XPSystem quand le joueur level up
    public void OnLevelUp(int level)
    {
        if (level >= 3)
            AttractionRadius = 4f; // S'active ou s'agrandit
    }

    // Appelé à la mort d'un ennemi — calcule et spawne les gemmes via l'ObjectPool
    public void SpawnGems(Vector3 position, float xpValue)
    {
        if (ObjectPool.Instance == null) return;

        _xpCarry = CalculateGems(xpValue + _xpCarry); // Remplit le cache interne, garde le reste pour la prochaine mort

        for (int i = 0; i < _gemsCalculationCache.Count; i++)
        {
            XPGem.GemType gemType = _gemsCalculationCache[i];

            // Offset aléatoire pour espacer les gemmes au sol
            Vector3 offset = new Vector3(
                Random.Range(-0.8f, 0.8f),
                0f,
                Random.Range(-0.8f, 0.8f)
            );

            // MODIFIÉ (2026-10-04) - la gemme spawne maintenant PILE au point de mort (position), pas déjà décalée :
            // XPGem.Init() l'anime en un petit arc depuis ce point jusqu'à sa position finale (position + offset),
            // comme si elle jaillissait de l'ennemi au lieu d'apparaître à plat sur le sol (retour utilisateur :
            // "pas le côté satisfaisant").
            GameObject gemGO = ObjectPool.Instance.Get("XPGem", position, Quaternion.identity);

            if (gemGO != null)
            {
                XPGem gem = gemGO.GetComponent<XPGem>();
                if (gem != null)
                {
                    gem.Init(gemType, _playerTransform, position + offset);
                }
            }
        }
    }

    // Algorithme optimisé "rendre la monnaie" sans aucune allocation mémoire
    // Renvoie l'XP non convertie en gemmes (reste < 10), à reporter.
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
        if (ObjectPool.Instance == null) return;
        StartCoroutine(BossGemBurstRoutine(position, xpValue));
    }

    private System.Collections.IEnumerator BossGemBurstRoutine(Vector3 position, float xpValue)
    {
        ExpandingRingVFX.Spawn(position, 3.5f, new Color(1f, 0.93f, 0.6f), 0.5f);

        _xpCarry = CalculateGems(xpValue + _xpCarry); // remplit _gemsCalculationCache normalement (règle "rendre la monnaie"), reste reporté
        // CORRIGE (2026-10-16, audit - erreur relevée en Play) - cette coroutine lit la liste sur plusieurs frames
        // (une gemme toutes les 0,03 s) : un ennemi tué pendant la gerbe appelait SpawnGems(), qui VIDE et remplit ce
        // même cache partagé -> mauvais types de gemmes, ou ArgumentOutOfRangeException qui interrompait la gerbe
        // (XP du boss perdue). Copie locale propre à cette gerbe (une seule allocation par mort de boss).
        List<XPGem.GemType> gems = new List<XPGem.GemType>(_gemsCalculationCache);

        // Au-delà du plafond, on fusionne tout le surplus (du début de la liste, donc les plus petites valeurs
        // d'abord) dans UNE seule grosse gemme qui porte leur XP cumulée - jamais de gemme perdue, juste moins
        // d'objets à l'écran.
        int count = gems.Count;
        float overflowXP = 0f;
        int burstCount = count;
        int startIndex = 0;
        if (count > MaxBurstGems)
        {
            int overflowCount = count - (MaxBurstGems - 1);
            for (int i = 0; i < overflowCount; i++)
                overflowXP += GemTypeValue(gems[i]);
            startIndex = overflowCount;
            burstCount = MaxBurstGems - 1 + 1; // les restantes + la gemme consolidée
        }

        int spawned = 0;
        int totalToSpawn = burstCount;
        float angleStep = Mathf.PI * 2f / Mathf.Max(1, totalToSpawn);
        float angleJitterRange = angleStep * 0.3f;
        float startAngle = Random.Range(0f, Mathf.PI * 2f);

        if (overflowXP > 0f)
        {
            SpawnBurstGem(position, XPGem.GemType.Large, overflowXP, startAngle, spawned, totalToSpawn);
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

        GameObject gemGO = ObjectPool.Instance.Get("XPGem", position, Quaternion.identity);
        if (gemGO == null) return;

        XPGem gem = gemGO.GetComponent<XPGem>();
        if (gem != null)
            gem.Init(type, _playerTransform, position + offset, xpOverride);
    }

    private static float GemTypeValue(XPGem.GemType type)
    {
        switch (type)
        {
            case XPGem.GemType.Large: return 50f;
            case XPGem.GemType.Medium: return 20f;
            default: return 10f;
        }
    }
}