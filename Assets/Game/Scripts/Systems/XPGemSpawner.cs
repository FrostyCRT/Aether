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

        CalculateGems(xpValue); // Remplit le cache interne

        for (int i = 0; i < _gemsCalculationCache.Count; i++)
        {
            XPGem.GemType gemType = _gemsCalculationCache[i];

            // Offset aléatoire pour espacer les gemmes au sol
            Vector3 offset = new Vector3(
                Random.Range(-0.8f, 0.8f),
                0f,
                Random.Range(-0.8f, 0.8f)
            );

            // CORRECTION CRITIQUE : Utilisation de l'ObjectPool au lieu d'Instantiate
            GameObject gemGO = ObjectPool.Instance.Get("XPGem", position + offset, Quaternion.identity);

            if (gemGO != null)
            {
                XPGem gem = gemGO.GetComponent<XPGem>();
                if (gem != null)
                {
                    gem.Init(gemType, _playerTransform);
                }
            }
        }
    }

    // Algorithme optimisé "rendre la monnaie" sans aucune allocation mémoire
    private void CalculateGems(float xpValue)
    {
        _gemsCalculationCache.Clear(); // On vide le cache de la mort précédente
        int remaining = Mathf.RoundToInt(xpValue);

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
    }
}