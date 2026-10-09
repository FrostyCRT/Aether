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

    // AJOUTE (2026-10-07, retour utilisateur FPS) - ReturnToPool() utilisait queue.Contains(obj) (scan O(n)) comme
    // garde anti-doublon. Un pool ne RÉTRÉCIT jamais une fois agrandi au-delà de sa taille pré-chauffée (voir Get
    // ci-dessous, branche Instantiate) : plus une run dure et plus un pic de demande passé a pu arriver, plus ce
    // scan devient coûteux pour TOUT retour futur sur ce tag - y compris pendant un combat de boss presque vide,
    // où les projectiles du joueur/du boss continuent de revenir au pool en continu. Un HashSet parallèle (lookup
    // O(1)) remplace ce scan sans changer le comportement.
    private Dictionary<string, HashSet<GameObject>> _poolMembership;

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
        _poolMembership = new Dictionary<string, HashSet<GameObject>>();

        foreach (Pool pool in _pools)
        {
            Queue<GameObject> objectQueue = new Queue<GameObject>();
            HashSet<GameObject> membership = new HashSet<GameObject>();

            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab);
                obj.SetActive(false);
                objectQueue.Enqueue(obj);
                membership.Add(obj);
            }

            _poolDictionary.Add(pool.tag, objectQueue);
            _poolMembership.Add(pool.tag, membership);
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
            _poolMembership[tag].Remove(obj);
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
    public void ReturnToPool(string tag, GameObject obj)
    {
        if (!_poolDictionary.ContainsKey(tag))
        {
            Destroy(obj);
            return;
        }

        HashSet<GameObject> membership = _poolMembership[tag];

        // Sécurité pour éviter d'ajouter deux fois le même objet dans la file (O(1) au lieu d'un scan de la file)
        if (!membership.Add(obj)) return;

        obj.SetActive(false);
        _poolDictionary[tag].Enqueue(obj);
    }

    // AJOUT : Méthode ClearPool requise par le GameManager pour nettoyer l'écran à la victoire
    public void ClearPool(string tag)
    {
        if (!_poolDictionary.ContainsKey(tag)) return;

        if (tag == "EnemyProjectile")
        {
            EnemyProjectile[] activeProjectiles = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
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
            ProjectileBasic[] activeProjectiles = FindObjectsByType<ProjectileBasic>(FindObjectsSortMode.None);
            foreach (ProjectileBasic proj in activeProjectiles)
            {
                if (proj.gameObject.activeSelf)
                {
                    ReturnToPool(tag, proj.gameObject);
                }
            }
        }
    }

    // Dans ObjectPool.cs — nouvelle surcharge, n'entrave pas l'existant
    public GameObject Get(string tag, Vector3 position, Quaternion rotation, float healthMultiplier)
    {
        if (!_poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool '{tag}' introuvable !");
            return null;
        }

        Queue<GameObject> queue = _poolDictionary[tag];
        GameObject obj = null;
        bool isNewInstance = false;

        if (queue.Count > 0)
        {
            obj = queue.Dequeue();
            _poolMembership[tag].Remove(obj);
        }
        else
        {
            Pool foundPool = _pools.Find(p => p.tag == tag);
            if (foundPool != null && foundPool.prefab != null)
            {
                obj = Instantiate(foundPool.prefab);
                isNewInstance = true; // AJOUTÉ
            }
            else
            {
                return null;
            }
        }

        if (isNewInstance)
            obj.SetActive(false); // AJOUTÉ — Instantiate() a pu déjà déclencher OnEnable() une fois si le prefab est actif par défaut ; on repart propre avec le multiplicateur déjà posé avant le prochain SetActive(true)

        obj.transform.position = position;
        obj.transform.rotation = rotation;

        EnemyBase eb = obj.GetComponent<EnemyBase>();
        if (eb != null) eb.SetHealthMultiplier(healthMultiplier);

        obj.SetActive(true);
        return obj;
    }
}