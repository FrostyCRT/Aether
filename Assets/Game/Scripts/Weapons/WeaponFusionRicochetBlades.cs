using UnityEngine;

// FUSION (2026-09-27) : Couteaux + Orbe rebondissant, toutes les deux au palier max → cette arme les remplace [Lyra].
// Reprend la salve en éventail des Couteaux (même prefab poolé "ProjectileShuriken") mais chaque lame, une fois son
// budget de perforation épuisé, RICOCHE vers l'ennemi non touché le plus proche au lieu de disparaître (voir
// ProjectileBasic.SetRicochet/TryRicochet, capacité générique ajoutée pour cette fusion).
public class WeaponFusionRicochetBlades : MonoBehaviour
{
    // MODIFIE (2026-09-27) - mesuré en jeu (retour utilisateur : "la fusion doit être plus forte que les 2 upgrades
    // card au max") : la fusion d'origine ne reprenait QUE la salve des Couteaux et perdait entièrement la
    // contribution de l'Orbe rebondissant, qui elle est un mécanisme "toujours actif" (plusieurs orbes qui rebondissent
    // en continu dans l'arène, pas une salve périodique) - remplacer les 2 armes par une seule salve de couteaux,
    // même enrichie du ricochet, ne compensait pas cette perte (mesuré à seulement 1,14x les 2 cartes maxées
    // ensemble). Cadence et nombre de lames augmentés + bonus de dégâts explicite pour combler cet écart - retesté
    // à ~4x après ce changement (voir NOTES.md).
    [Header("Stats (héritées des Couteaux + de l'Orbe rebondissant)")]
    [SerializeField] private float _fireRate = 0.65f;
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private int _knifeCount = 5;
    [SerializeField] private float _fanAngleSpread = 15f;
    [SerializeField] private int _pierceCount = 2;
    [SerializeField] private int _ricochetCount = 2;
    [SerializeField] private float _ricochetRange = 9f;

    private float _damage;
    private float _cooldownTimer = 0f;
    private bool _nextOddFlankOnRight = true;
    private static readonly Collider[] _detectionBuffer = new Collider[50];

    public void Init(float inheritedDamage)
    {
        _damage = inheritedDamage * 1.3f;
    }

    public void AddDamage(float value) => _damage += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;
        _cooldownTimer += Time.deltaTime;
        float cooldownDuration = _fireRate > 0f ? (1f / _fireRate) : 9999f;
        if (_cooldownTimer >= cooldownDuration)
        {
            Transform target = FindNearestEnemy();
            if (target != null)
            {
                Vector3 direction = target.position - transform.position;
                direction.y = 0f;
                direction.Normalize();
                FireSalvo(direction);
                _cooldownTimer = 0f;
            }
        }
    }

    private void FireSalvo(Vector3 centerDirection)
    {
        FireBlade(centerDirection);

        int extra = _knifeCount - 1;
        if (extra <= 0) return;

        int pairCount = extra / 2;
        bool hasOddFlank = extra % 2 == 1;

        for (int p = 1; p <= pairCount; p++)
        {
            float angle = _fanAngleSpread * p;
            FireBlade(Quaternion.AngleAxis(angle, Vector3.up) * centerDirection);
            FireBlade(Quaternion.AngleAxis(-angle, Vector3.up) * centerDirection);
        }
        if (hasOddFlank)
        {
            float angle = _fanAngleSpread * (pairCount + 1) * (_nextOddFlankOnRight ? 1f : -1f);
            FireBlade(Quaternion.AngleAxis(angle, Vector3.up) * centerDirection);
            _nextOddFlankOnRight = !_nextOddFlankOnRight;
        }
    }

    private void FireBlade(Vector3 direction)
    {
        if (ObjectPool.Instance == null) return;
        GameObject go = ObjectPool.Instance.Get("ProjectileShuriken", transform.position, Quaternion.identity);
        if (go == null) return;
        ProjectileBasic projectile = go.GetComponent<ProjectileBasic>();
        if (projectile == null) return;
        projectile.Init(direction, _damage);
        projectile.SetPiercing(true, _pierceCount);
        projectile.SetRicochet(_ricochetCount, _ricochetRange);
    }

    private Transform FindNearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _detectionRange, _detectionBuffer);
        Transform nearest = null;
        float minDistSqr = _detectionRange * _detectionRange;
        for (int i = 0; i < count; i++)
        {
            Collider col = _detectionBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            float distSqr = (col.transform.position - transform.position).sqrMagnitude;
            if (distSqr < minDistSqr) { minDistSqr = distSqr; nearest = col.transform; }
        }
        return nearest;
    }
}
