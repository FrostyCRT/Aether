using UnityEngine;
using System.Collections.Generic;

// PREMIÈRE FUSION (2026-09-27) : Boule de feu + Boue, toutes les deux au palier max → cette arme les remplace.
// Réutilise entièrement le prefab poolé "MudPuddleZone" (mêmes zones au sol, même logique de ralentissement/DPS que
// WeaponMudPuddle) : aucun nouvel asset visuel nécessaire pour ce prototype. Les dégâts de départ sont hérités de la
// Boule de feu ET de la Boue au moment de la fusion (voir UpgradeData.ApplyFusionResult), puis l'arme continue de
// profiter des futurs achats de "Dégâts+" comme toute autre arme (voir UpgradeData.Apply, case Damage).
public class WeaponFusionScorchedEarth : MonoBehaviour
{
    // CORRIGÉ (2026-10-05) - retour utilisateur : "les fusions [...] pas plus puissant que 2 upgrades améliorées
    // au max". Mesuré en jeu avec des ennemis DISPERSÉS ET MOBILES (pas des mannequins groupés comme la passe du
    // 2026-09-27) : ×0,46 - plus FAIBLE que les 2 cartes maxées. Cause : _puddleCount=4 était en dessous du max de
    // la Boue SEULE (6, via ses 3 upgrades +1), et la Boule de feu perd son identité de burst single-target en
    // se diluant dans un DPS de zone continu sans aucun bonus pour compenser cette perte. Nombre de flaques porté
    // à 8 (nettement au-dessus du max solo) + bonus de dégâts explicite (voir Init).
    // CORRIGÉ (2026-10-18, audit complet des fusions demandé par l'utilisateur : "une fusion doit être plus
    // puissante que ses 2 sous-upgrades améliorées au max") - remesuré avec la méthode [[balance-testing-dummies]]
    // (ennemis dispersés 3-18 unités, PV x300, 15s) : malgré le ×3 de dégâts de 2026-10-05, la fusion ne faisait
    // QUE 777 DPS contre 2542 pour Boule de feu + Boue maxées séparément (×0,3, toujours en dessous). Cause RÉELLE
    // trouvée par instrumentation (compteur de ticks sur MudPuddleZone) : les ennemis chassent le joueur jusqu'au
    // contact (~1 unité) en 2-3 secondes puis y restent plaqués pour le reste du combat - l'anneau de flaques à
    // 6,5 unités n'est traversé qu'UNE SEULE FOIS à l'approche, jamais revisité ensuite (41 ticks de dégâts
    // relevés sur toute la fenêtre de 15s, contre ~390 attendus si les flaques étaient réellement occupées). Le
    // nombre de flaques/leur dégât n'étaient donc jamais le problème : l'anneau visait une zone que les ennemis ne
    // fréquentent quasiment pas. _spawnDistance rapproché à 2,8 (couvre la zone de corps-à-corps où les ennemis
    // s'agglutinent réellement, mesuré à ~1,2 unité du joueur mais avec du jeu - testé à 4 unités d'abord, DPS
    // retombé à peine au-dessus de la référence, la portée intérieure ne couvrait plus assez souvent le tas
    // d'ennemis). _puddleRadius réduit (2,4 -> 1,6) à la place pour retrouver de la portée SANS recréer le bloc
    // continu indifférencié observé à spawnDistance=2,8/radius=2,4 (les 8 flaques fusionnaient visuellement en une
    // seule masse orange, en contradiction avec la description "des zones [...] apparaissent en cercle").
    [Header("Stats (zones plus grandes/plus fréquentes/plus fortes qu'une Boue simple)")]
    [SerializeField] private float _spawnInterval = 2.5f;
    [SerializeField] private float _puddleDuration = 4f;
    [SerializeField] private float _puddleRadius = 1.6f;
    [SerializeField] private float _slowMultiplier = 0.55f;
    [SerializeField] private int _puddleCount = 8;
    [SerializeField] private Color _scorchedTint = new Color(0.85f, 0.35f, 0.08f); // braises - cohérent avec ColorFireball
    [SerializeField] private float _spawnDistance = 2.8f;
    [SerializeField] private float _groundY = 0.2f;
    [SerializeField] private float _rotationPerWave = 25f;

    private float _damagePerSecond;
    private GameObject _puddlePrefab;
    private float _waveTimer = 0f;
    private float _currentWaveRotation = 0f;
    private readonly List<GameObject> _activePuddles = new List<GameObject>();

    // Appelé une fois par UpgradeData.ApplyFusionResult, juste après AddComponent : startingDamagePerSecond vient
    // de la somme des dégâts hérités des deux armes fusionnées (déjà boostés par la Réputation/les picks précédents,
    // donc PAS de second bonus de Réputation ici, contrairement aux autres armes qui le lisent dans leur Awake()).
    public void Init(GameObject puddlePrefab, float startingDamagePerSecond)
    {
        _puddlePrefab = puddlePrefab;
        // CORRIGÉ (2026-10-18) - le ×3 datait d'avant la correction de _spawnDistance ci-dessus (voir commentaire) :
        // une fois les flaques capables d'atteindre les ennemis, ×3 donnait ~10,8x la référence (2173 DPS mesurés
        // pour Boule de feu + Boue maxées séparément) - bien au-delà de "très puissant". Après le passage à
        // _puddleRadius=1,6 (pour ne plus fusionner visuellement en un bloc, voir commentaire plus haut), la
        // portée perdue a fait retomber la mesure à ×1,4 seulement (trop juste) - remonté à ×1,5 pour compenser,
        // remesuré à ~6500 DPS (×3 la référence) : net, confortable, sans être absurde.
        _damagePerSecond = startingDamagePerSecond * 1.5f;
    }

    public void AddDamage(float value) => _damagePerSecond += _damagePerSecond * value;

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        _waveTimer += Time.deltaTime;
        if (_waveTimer >= _spawnInterval)
        {
            SpawnWave();
            _waveTimer = 0f;
        }
    }

    private void SpawnWave()
    {
        if (ObjectPool.Instance == null || _puddlePrefab == null) return;

        ForceExpireAllActivePuddles();

        int slotCount = Mathf.Max(1, _puddleCount);
        float angleStep = 360f / slotCount;

        for (int i = 0; i < slotCount; i++)
        {
            float angle = _currentWaveRotation + angleStep * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * _spawnDistance;
            Vector3 spawnPos = transform.position + offset;
            spawnPos = WaveManager.MapBoundaryUtils.ClampToZone(spawnPos);
            spawnPos.y = _groundY;

            GameObject puddleGO = ObjectPool.Instance.Get("MudPuddleZone", spawnPos, Quaternion.identity);
            if (puddleGO == null) continue;

            MudPuddleZone zone = puddleGO.GetComponent<MudPuddleZone>();
            if (zone != null)
            {
                zone.Init(_puddleDuration, _puddleRadius, _slowMultiplier, _damagePerSecond);
                // AJOUTÉ (2026-10-05) - retour utilisateur : sans ça, cette fusion (Boule de feu + Boue) affichait
                // des flaques en tout point identiques à la Boue normale - aucun moyen de la reconnaître à l'œil.
                // Teinte "braises" (orange/rouge, cohérent avec ColorFireball) pour un vrai look "terre calcinée".
                zone.SetColorTint(_scorchedTint);
            }

            _activePuddles.Add(puddleGO);
        }

        _currentWaveRotation = (_currentWaveRotation + _rotationPerWave) % 360f;
    }

    private void ForceExpireAllActivePuddles()
    {
        for (int i = 0; i < _activePuddles.Count; i++)
        {
            if (_activePuddles[i] == null) continue;
            MudPuddleZone zone = _activePuddles[i].GetComponent<MudPuddleZone>();
            if (zone != null) zone.ForceExpire();
        }
        _activePuddles.Clear();
    }
}
