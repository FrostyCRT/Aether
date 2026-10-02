using UnityEngine;

// Bulbe cracheur : tireur FIXE. Ne bouge jamais une fois apparu ; dès que sa cible (joueur ou clone de Lyra) est à portée,
// il crache un éventail de projectiles à cadence lente (bien plus lente que le gobelin tireur, qui tire toutes les ~0,67 s).
// Tout le reste (PV, XP, or, mort, ralentissements, brûlure...) vient d'EnemyBase ; il suffit de le mettre dans un
// prefab avec un Collider, le tag "Enemy" et une entrée de pool (voir ObjectPool / EnemySpawner).
public class EnemyBulb : EnemyBase
{
    [Header("Éventail")]
    [Tooltip("Nombre de projectiles par salve (impair = un projectile pile en direction de la cible).")]
    [SerializeField] private int _projectilesPerVolley = 5;
    [Tooltip("Angle total de l'éventail, en degrés.")]
    [SerializeField] private float _fanAngle = 60f;

    [Header("Cadence")]
    [Tooltip("Secondes entre deux salves (gobelin tireur : ~0,67 s).")]
    [SerializeField] private float _fireInterval = 3f;
    [Tooltip("Délai avant la première salve après l'apparition (évite un tir instantané au spawn).")]
    [SerializeField] private float _firstShotDelay = 1.2f;
    [SerializeField] private float _attackRange = 14f;

    [Header("Visuel")]
    [SerializeField] private Transform _projectileSpawnPoint;
    [Tooltip("Vitesse de rotation vers la cible (degrés/s). 0 = ne pivote jamais.")]
    [SerializeField] private float _rotationSpeed = 240f;

    private float _fireTimer;
    private EnemyAnimatorController _bulbAnimator;

    protected override void OnEnable()
    {
        base.OnEnable();
        // Ennemi issu du pool : la première salve est retardée à chaque (ré)apparition.
        _fireTimer = -_firstShotDelay;
    }

    protected override void UpdateBehaviour(Transform target)
    {
        if (target == null) return;

        if (_bulbAnimator == null)
            _bulbAnimator = GetComponentInChildren<EnemyAnimatorController>();

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        bool inRange = toTarget.sqrMagnitude <= _attackRange * _attackRange;

        // Aucun déplacement : position fixe. Seule la rotation suit la cible.
        if (_rotationSpeed > 0f && toTarget.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(toTarget.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _rotationSpeed * Time.deltaTime);
        }

        if (_bulbAnimator != null)
            _bulbAnimator.SetAttacking(inRange);

        if (!inRange)
        {
            // Hors de portée : la salve se recharge quand même (pas de tir "gratuit" dès qu'on entre dans la zone).
            _fireTimer = Mathf.Min(_fireTimer + Time.deltaTime, _fireInterval);
            return;
        }

        _fireTimer += Time.deltaTime;
        if (_fireTimer >= _fireInterval)
        {
            FireFan(toTarget.normalized);
            _fireTimer = 0f;
        }
    }

    private void FireFan(Vector3 centerDirection)
    {
        if (ObjectPool.Instance == null) return;

        int count = Mathf.Max(1, _projectilesPerVolley);
        Vector3 origin = _projectileSpawnPoint != null ? _projectileSpawnPoint.position : transform.position;

        for (int i = 0; i < count; i++)
        {
            // Répartit les projectiles de -fan/2 à +fan/2 (un seul projectile = droit devant).
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float angle = Mathf.Lerp(-_fanAngle * 0.5f, _fanAngle * 0.5f, t);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * centerDirection;

            GameObject projectileGO = ObjectPool.Instance.Get("EnemyProjectile", origin, Quaternion.identity);
            if (projectileGO == null) continue;

            EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
            if (projectile != null) projectile.Init(direction);
        }
    }
}
