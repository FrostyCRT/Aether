using UnityEngine;
using System.Collections.Generic;

public class ObjectPool : MonoBehaviour
{
    public static ObjectPool Instance { get; private set; }

    [System.Serializable]
    public class Pool
    {
        public string     tag;        // Identifiant du pool ex: "Enemy", "Projectile"
        public GameObject prefab;     // Le prefab à pooler
        public int        size;       // Nombre d'objets pré-créés
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
                // On crée l'objet, on le désactive et on le met en file d'attente
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

        // Si le pool est vide on prend le premier objet quand même
        // (il sera actif mais c'est mieux que de lagger)
        GameObject obj = null;
        if (queue.Count > 0)
        {
            obj = queue.Dequeue();
        }
        else
        {
            // Pool épuisé → on en crée un nouveau
            obj = Instantiate(_pools.Find(p => p.tag == tag).prefab);
            Debug.Log($"Pool '{tag}' épuisé, création d'un objet supplémentaire");
        }

        obj.SetActive(true);
        obj.transform.position = position;
        obj.transform.rotation = rotation;

        return obj;
    }

    // Remet un objet dans le pool
    public void ReturnToPool(string tag, GameObject obj)
    {
        if (!_poolDictionary.ContainsKey(tag))
        {
            Destroy(obj);
            return;
        }

        obj.SetActive(false);
        _poolDictionary[tag].Enqueue(obj);
    }
}