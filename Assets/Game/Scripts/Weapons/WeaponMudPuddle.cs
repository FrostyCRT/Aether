using UnityEngine;
using System.Collections.Generic;

// MODIFIÉ EN PROFONDEUR — passage d'un modèle "remplissage continu individuel"
// (une flaque expire → une nouvelle apparaît ailleurs, cycles désynchronisés) à un
// modèle "vague groupée" : toutes les flaques d'un cycle apparaissent ensemble,
// vivent la même durée, disparaissent ensemble, puis le cycle recommence.
public class WeaponMudPuddle : MonoBehaviour
{
    [Header("Stats")]
    [Tooltip("Temps entre le début d'une vague et le début de la suivante. Si cette valeur est INFÉRIEURE à la durée de vie d'une flaque (_puddleDuration), la vague précédente sera forcée à expirer immédiatement pour garantir la synchronisation — voir SpawnWave().")]
    [SerializeField] private float _spawnInterval = 4f;
    [SerializeField] private float _puddleDuration = 3f;
    [SerializeField] private float _puddleRadius = 1.3f;
    [SerializeField] private float _slowMultiplier = 0.6f; // -40%

    [Tooltip("Dégâts par seconde infligés par une flaque. Le doc de design ne mentionnait que du ralentissement — ce léger DPS a été ajouté pour que l'upgrade ait une contribution offensive sur 15 min, cohérent avec la fusion 'Marécage Maudit' qui parle de DPS accru. Mets à 0 si tu préfères un pur outil de contrôle.")]
    [SerializeField] private float _damagePerSecond = 4f;

    [Header("Placement (anneau déterministe, pas de hasard)")]
    [SerializeField] private float _spawnDistance = 5f;
    [Tooltip("Hauteur Y ABSOLUE au sol (pas relative au joueur). Le joueur/les ennemis sont à hauteur de centre de collider, donc hériter de leur Y plaçait la flaque au milieu des ennemis au lieu d'être plaquée au sol.")]
    [SerializeField] private float _groundY = 0.2f;

    [Tooltip("Fait tourner légèrement l'angle de départ de l'anneau à chaque nouvelle vague (déterministe, PAS aléatoire) pour que le motif ne soit pas parfaitement identique à chaque fois. Mets à 0 pour un anneau toujours identique en orientation.")]
    [SerializeField] private float _rotationPerWave = 25f;
    private float _currentWaveRotation = 0f;

    // Point de départ au déblocage (1er pick) = 3 flaques par vague. Les 3 paliers
    // suivants ajoutent +1 chacune → max 6 par vague. Même logique de compte
    // qu'Orbital/Lightning, appliquée ici au nombre de flaques PAR VAGUE.
    [SerializeField] private int _puddleCount = 3;

    [Header("Références")]
    [SerializeField] private GameObject _puddlePrefab;

    private float _waveTimer = 0f;
    private readonly List<GameObject> _activePuddles = new List<GameObject>();

    public void Init(GameObject puddlePrefab)
    {
        _puddlePrefab = puddlePrefab;
    }

    public void AddPuddle()
    {
        _puddleCount++;
    }

    // AJOUTÉ — permet à la carte générique Dégâts+ de toucher aussi cette arme
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

        // IMPORTANT — force l'expiration immédiate de toute flaque restante de la vague
        // précédente, plutôt que de compter sur "_spawnInterval >= _puddleDuration" pour
        // que ça tombe juste. Garantit la synchronisation même si les deux valeurs sont
        // mal calées entre elles dans l'Inspector (protection contre une erreur de config
        // qui ferait chevaucher deux vagues et casserait le comportement voulu).
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
            if (puddleGO == null) continue; // pool épuisé sur ce slot précis, on continue les autres plutôt que d'abandonner toute la vague

            MudPuddleZone zone = puddleGO.GetComponent<MudPuddleZone>();
            if (zone != null)
                zone.Init(_puddleDuration, _puddleRadius, _slowMultiplier, _damagePerSecond);

            _activePuddles.Add(puddleGO);
        }

        // Rotation déterministe (pas aléatoire) pour varier l'orientation de l'anneau
        // d'une vague à l'autre, bornée à [0, 360) pour éviter une dérive de float
        // sur une run de 15 minutes avec potentiellement des dizaines de vagues.
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