using UnityEngine;
using System.Collections.Generic;

public class XPGemSpawner : MonoBehaviour
{
    public static XPGemSpawner Instance { get; private set; }

    [Header("Prefab")]
    [SerializeField] private GameObject _gemPrefab; // Sphere Unity simple

    private Transform _playerTransform;
    private float _attractionRadius = 0f; // 0 = pas d'attraction

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
    public float AttractionRadius { get; private set; } = 0f;

    public void OnLevelUp(int level)
    {
        if (level >= 3)
            AttractionRadius = 4f;
    }

    // Appelé à la mort d'un ennemi — calcule et spawne les gemmes
    public void SpawnGems(Vector3 position, float xpValue)
    {
        List<XPGem.GemType> gems = CalculateGems(xpValue);

        foreach (XPGem.GemType gemType in gems)
        {
            // Offset aléatoire pour que les gemmes ne spawent pas toutes au même endroit
            Vector3 offset = new Vector3(
                Random.Range(-1f, 1f), 0f,
                Random.Range(-1f, 1f));

            GameObject gemGO = Instantiate(_gemPrefab, position + offset, Quaternion.identity);

            XPGem gem = gemGO.GetComponent<XPGem>();
            if (gem != null)
            {
                gem.Init(gemType, _playerTransform);
                gem.EnableAttraction(_attractionRadius);
            }
        }
    }

    // Algorithme "rendre la monnaie" — décompose l'XP en gemmes
    private List<XPGem.GemType> CalculateGems(float xpValue)
    {
        List<XPGem.GemType> result = new List<XPGem.GemType>();
        int remaining = Mathf.RoundToInt(xpValue);

        while (remaining >= 50)
        {
            result.Add(XPGem.GemType.Large);
            remaining -= 50;
        }
        while (remaining >= 20)
        {
            result.Add(XPGem.GemType.Medium);
            remaining -= 20;
        }
        while (remaining >= 10)
        {
            result.Add(XPGem.GemType.Small);
            remaining -= 10;
        }

        return result;
    }
}