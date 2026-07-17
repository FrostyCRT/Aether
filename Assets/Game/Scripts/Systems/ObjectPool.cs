using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    public static ObjectPool Instance { get; private set; }

    [System.Serializable]
    public class Pool
    {
        public string tag;        // Identifiant du pool ex: "Enemy", "Projectile"
        public GameObject prefab;     // Le prefab à pooler
        public int size;       // Nombre d'objets pré-créés
    }

    [SerializeField] private List<Pool> _pools;

    // Dictionnaire : tag → file d'objets disponibles
    private Dictionary<string, Queue<GameObject>> _poolDictionary;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializePools();
    }

    private void InitializePools()
    {
        _poolDictionary = new Dictionary<string, Queue<GameObject>>();

        foreach (Pool pool in _pools)
        {
            Queue<GameObject> objectQueue = new Queue<GameObject>();

            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab);
                obj.SetActive(false);
                objectQueue.Enqueue(obj);
            }

            _poolDictionary.Add(pool.tag, objectQueue);
        }
    }

    // Récupère un objet du pool
    public GameObject Get(string tag, Vector3 position, Quaternion rotation)
    {
        if (!_poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool '{tag}' introuvable !");
            return null;
        }
        Queue<GameObject> queue = _poolDictionary[tag];
        GameObject obj = null;
        if (queue.Count > 0)
        {
            obj = queue.Dequeue();
        }
        else
        {
            Pool foundPool = _pools.Find(p => p.tag == tag);
            if (foundPool != null && foundPool.prefab != null)
            {
                obj = Instantiate(foundPool.prefab);
            }
            else
            {
                return null;
            }
        }

        // MODIFIÉ — position et rotation appliquées AVANT SetActive, pour que OnEnable() voie la bonne position
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        return obj;
    }

    // Remet un objet dans le pool
    // Remet un objet dans le pool
    public void ReturnToPool(string tag, GameObject obj)
    {
        if (!_poolDictionary.ContainsKey(tag))
        {
            Destroy(obj);
            return;
        }

        // CORRECTION : On récupère d'abord la file d'attente
        Queue<GameObject> queue = _poolDictionary[tag];

        // Sécurité pour éviter d'ajouter deux fois le même objet dans la file
        if (queue.Contains(obj)) return;

        obj.SetActive(false);
        queue.Enqueue(obj);
    }

    // AJOUT : Méthode ClearPool requise par le GameManager pour nettoyer l'écran à la victoire
    public void ClearPool(string tag)
    {
        if (!_poolDictionary.ContainsKey(tag)) return;

        if (tag == "EnemyProjectile")
        {
            EnemyProjectile[] activeProjectiles = FindObjectsOfType<EnemyProjectile>();
            foreach (EnemyProjectile proj in activeProjectiles)
            {
                if (proj.gameObject.activeSelf)
                {
                    ReturnToPool(tag, proj.gameObject);
                }
            }
        }
        else if (tag == "Projectile")
        {
            ProjectileBasic[] activeProjectiles = FindObjectsOfType<ProjectileBasic>();
            foreach (ProjectileBasic proj in activeProjectiles)
            {
                if (proj.gameObject.activeSelf)
                {
                    ReturnToPool(tag, proj.gameObject);
                }
            }
        }
    }
}