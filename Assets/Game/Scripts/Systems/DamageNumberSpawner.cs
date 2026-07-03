using UnityEngine;
using System.Collections.Generic;

public class DamageNumberSpawner : MonoBehaviour
{
    public static DamageNumberSpawner Instance { get; private set; }

    public static readonly Color ColorProjectile = new Color(0f, 0.8f, 1f);
    public static readonly Color ColorAOE = new Color(0.4f, 0.9f, 0.2f);
    public static readonly Color ColorOrbital = new Color(0.9f, 0.9f, 0.9f);
    public static readonly Color ColorCritical = new Color(1f, 0.6f, 0f);
    public static readonly Color ColorPlayer = new Color(1f, 0.2f, 0.2f);

    [SerializeField] private float _fuseWindow = 0.15f; // fenêtre de fusion en secondes

    private Dictionary<Transform, DamageNumber> _activeNumbers = new Dictionary<Transform, DamageNumber>();

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

        if (target != null && _activeNumbers.TryGetValue(target, out DamageNumber existing)
            && existing != null && existing.gameObject.activeSelf
            && existing.ElapsedTime <= _fuseWindow)
        {
            existing.AddDamage(damage, isCritical);
            return;
        }

        Vector3 spawnPos = position + Vector3.up * 1.5f;
        GameObject go = ObjectPool.Instance.Get("DamageNumber", spawnPos, Quaternion.identity);
        if (go == null) return;

        DamageNumber dn = go.GetComponent<DamageNumber>();
        if (dn != null)
        {
            dn.Init(damage, color, target, isCritical);
            if (target != null) _activeNumbers[target] = dn;
        }
    }
}