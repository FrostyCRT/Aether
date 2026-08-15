using UnityEngine;
using System.Collections.Generic;

// Instance poolée gérée par WeaponBouncingOrb. Rebondit indéfiniment sur les bords du
// rectangle visible par la caméra (recalculé chaque frame car la caméra suit le joueur
// et dézoome en combat de boss — voir GetCameraGroundBounds()). Traverse les ennemis sans
// jamais se détruire ni retourner au pool tant que l'arme existe.
public class BouncingOrbProjectile : MonoBehaviour
{
    [Header("Anti-spam de dégâts")]
    [Tooltip("Délai minimum entre deux dégâts sur le MÊME ennemi, puisque l'orbe le traverse au lieu de disparaître au contact (contrairement à un projectile classique).")]
    [SerializeField] private float _hitCooldown = 0.5f;

    [Header("Sécurité rebond")]
    [Tooltip("Si aucune caméra valide n'est trouvée, l'orbe utilise ce rayon de secours autour du joueur plutôt que de partir à l'infini.")]
    [SerializeField] private float _fallbackBoundsRadius = 12f;

    [Tooltip("Rayon utilisé si aucun SphereCollider n'est trouvé sur ce prefab pour calculer automatiquement où rebondit le BORD de l'orbe (pas son centre).")]
    [SerializeField] private float _fallbackOrbRadius = 0.5f;

    private float _damage;
    private float _speed;
    private Vector3 _direction;
    private float _groundY;
    private float _orbRadius; // AJOUTÉ — rayon réel de l'orbe, pour rebondir sur son BORD
    private string _poolKey;
    private Transform _playerTransform;

    private readonly Dictionary<int, float> _recentHits = new Dictionary<int, float>();
    private List<int> _hitCleanupBuffer;

    private void Awake()
    {
        _poolKey = name.Replace("(Clone)", "").Trim();

        // AJOUTÉ — lit le vrai rayon depuis le SphereCollider du prefab (en tenant compte
        // de son échelle), plutôt qu'une valeur codée en dur qui se désynchroniserait
        // silencieusement si tu changes la taille du prefab dans l'Inspector plus tard.
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        _orbRadius = sphereCollider != null
            ? sphereCollider.radius * transform.lossyScale.x
            : _fallbackOrbRadius;
    }

    public void Init(Vector3 direction, float damage, float speed, float groundY)
    {
        _direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.right;
        _damage = damage;
        _speed = speed;
        _groundY = groundY;
        _recentHits.Clear();

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }
    }

    public void SetStats(float damage, float speed)
    {
        _damage = damage;
        _speed = speed;
    }

    private void Update()
    {
        if (GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused)) return;

        // MODIFIÉ — combine la limite caméra ET la limite de map, rétrécies du rayon
        // de l'orbe (voir GetEffectiveBounds()).
        Bounds bounds = GetEffectiveBounds();
        Vector3 nextPos = transform.position + _direction * _speed * Time.deltaTime;

        // Rebond indépendant sur chaque axe (comme un pong classique), pour gérer
        // correctement le cas où l'orbe touche un coin sur les deux axes à la fois.
        if (nextPos.x <= bounds.min.x || nextPos.x >= bounds.max.x)
        {
            _direction.x = -_direction.x;
            nextPos.x = Mathf.Clamp(nextPos.x, bounds.min.x, bounds.max.x);
        }
        if (nextPos.z <= bounds.min.z || nextPos.z >= bounds.max.z)
        {
            _direction.z = -_direction.z;
            nextPos.z = Mathf.Clamp(nextPos.z, bounds.min.z, bounds.max.z);
        }

        nextPos.y = _groundY;
        transform.position = nextPos;

        CleanupHitCooldowns();
    }

    // Calcule le rectangle visible par la caméra actuelle, projeté sur le plan du sol.
    // HYPOTHÈSE — caméra orthographique (confirmé : CinemachineVirtualCamera, Ortho Size 10),
    // sol globalement plat au niveau Y du joueur. Recalculé chaque frame car la caméra suit
    // le joueur ET dézoome dynamiquement pendant les combats de boss (BossCameraZoom) — un
    // cache figé au spawn de l'orbe deviendrait rapidement faux.
    private Bounds GetCameraGroundBounds()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            // Filet de sécurité : pas de caméra trouvée (device en train de charger la scène,
            // caméra mal taguée MainCamera, etc.) → zone de secours autour du joueur plutôt
            // que de laisser l'orbe filer sans jamais rebondir.
            Vector3 center = _playerTransform != null ? _playerTransform.position : transform.position;
            return new Bounds(center, Vector3.one * (_fallbackBoundsRadius * 2f));
        }

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, _groundY, 0f));

        Vector3 corner0 = ViewportPointOnGround(cam, groundPlane, new Vector3(0f, 0f, 0f));
        Vector3 corner1 = ViewportPointOnGround(cam, groundPlane, new Vector3(1f, 1f, 0f));

        Bounds bounds = new Bounds();
        bounds.SetMinMax(
            new Vector3(Mathf.Min(corner0.x, corner1.x), _groundY, Mathf.Min(corner0.z, corner1.z)),
            new Vector3(Mathf.Max(corner0.x, corner1.x), _groundY, Mathf.Max(corner0.z, corner1.z))
        );
        return bounds;
    }

    // AJOUTÉ — fusionne la limite caméra avec la limite RÉELLE de la map (WaveManager.
    // MapBoundaryUtils.ZoneHalfSize), pour que l'orbe ne dépasse jamais les murs de jeu
    // même si la caméra affiche une zone qui va au-delà (joueur près d'un bord de map).
    // Rétrécit ensuite le résultat du rayon de l'orbe, pour que ce soit son BORD qui
    // touche la limite, pas son centre.
    private Bounds GetEffectiveBounds()
    {
        Bounds cameraBounds = GetCameraGroundBounds();
        float mapHalf = WaveManager.MapBoundaryUtils.ZoneHalfSize;

        float minX = Mathf.Max(cameraBounds.min.x, -mapHalf) + _orbRadius;
        float maxX = Mathf.Min(cameraBounds.max.x, mapHalf) - _orbRadius;
        float minZ = Mathf.Max(cameraBounds.min.z, -mapHalf) + _orbRadius;
        float maxZ = Mathf.Min(cameraBounds.max.z, mapHalf) - _orbRadius;

        // Garde-fou — si la zone effective devient dégénérée (min > max, ex: caméra très
        // dézoomée sur une carte plus petite que le diamètre de l'orbe, cas extrême), on
        // retombe sur le centre plutôt que produire un Bounds invalide qui ferait bugger
        // le Clamp plus haut.
        if (minX > maxX) { float c = (minX + maxX) * 0.5f; minX = maxX = c; }
        if (minZ > maxZ) { float c = (minZ + maxZ) * 0.5f; minZ = maxZ = c; }

        Bounds result = new Bounds();
        result.SetMinMax(new Vector3(minX, _groundY, minZ), new Vector3(maxX, _groundY, maxZ));
        return result;
    }

    private Vector3 ViewportPointOnGround(Camera cam, Plane groundPlane, Vector3 viewportPoint)
    {
        Ray ray = cam.ViewportPointToRay(viewportPoint);
        if (groundPlane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        // Le rayon caméra ne croise pas le plan du sol (config caméra inhabituelle) —
        // repli sur la position actuelle de l'orbe pour ne jamais planter/produire un
        // Bounds invalide (min > max).
        return transform.position;
    }

    private void OnTriggerEnter(Collider other) => TryHit(other);
    private void OnTriggerStay(Collider other) => TryHit(other);

    private void TryHit(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;

        int id = other.GetInstanceID();
        if (_recentHits.TryGetValue(id, out float lastHitTime) && Time.time - lastHitTime < _hitCooldown)
            return;

        _recentHits[id] = Time.time;

        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeDamage(_damage, DamageNumberSpawner.ColorProjectile);
            return;
        }
        BossBase boss = other.GetComponent<BossBase>();
        if (boss != null) boss.TakeDamage(_damage);
    }

    // Nettoie régulièrement le dictionnaire de cooldowns pour éviter qu'il ne grossisse
    // indéfiniment sur 15 minutes de run avec un flux constant d'ennemis différents traversés.
    private void CleanupHitCooldowns()
    {
        if (_recentHits.Count == 0) return;

        _hitCleanupBuffer ??= new List<int>();
        _hitCleanupBuffer.Clear();

        foreach (var kvp in _recentHits)
        {
            if (Time.time - kvp.Value >= _hitCooldown)
                _hitCleanupBuffer.Add(kvp.Key);
        }
        for (int i = 0; i < _hitCleanupBuffer.Count; i++)
            _recentHits.Remove(_hitCleanupBuffer[i]);
    }
}