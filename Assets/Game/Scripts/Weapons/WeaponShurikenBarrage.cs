using UnityEngine;
public class WeaponShurikenBarrage : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage = 120f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _fireRate = 0.4f;
    [SerializeField] private float _detectionRange = 15f;

    // MODIFIE - reduit de 3 a 2 par couteau : la salve tire maintenant PLUSIEURS
    // couteaux simultanement (voir _knifeCount ci-dessous), donc chacun perce un
    // peu moins pour ne pas cumuler une perforation totale demesuree des le depart.
    [SerializeField] private int _maxPierceCount = 2;

    // AJOUTE - c'est le coeur de la nouvelle identite "salve" : plusieurs couteaux
    // partent en meme temps en eventail, plutot qu'un seul tir precis comme le tir
    // de base. Base = 2, +1 au palier 1 de la carte.
    [Header("Salve (eventail)")]
    [SerializeField] private int _knifeCount = 2;
    [Tooltip("Ecart angulaire entre deux couteaux adjacents de la salve, en degres.")]
    [SerializeField] private float _fanAngleSpread = 15f;

    private float _cooldownTimer = 0f;
    private static readonly Collider[] _detectionBuffer = new Collider[50];

    // AJOUTE - alterne le cote du couteau "surnumeraire" (celui qui ne forme pas
    // une paire symetrique complete, ex: le 2e couteau sur une salve de base a 2
    // couteaux) d'un tir a l'autre, pour rester equilibre visuellement sur la
    // duree plutot que de toujours tirer du meme cote.
    private bool _nextOddFlankOnRight = true;

    // AJOUTE - Knives n'appliquait jamais le bonus de Reputation Degats, meme
    // manque que Fireball et Aura.
    private void Start()
    {
        if (MetaProgressionManager.Instance != null)
        {
            float bonusDamage = MetaProgressionManager.Instance.GetReputationBonusDamage();
            _damage += _damage * bonusDamage;
        }
    }

    // SUPPRIME - Double Tir retire de cette arme. Une salve deja multi-projectiles
    // n'a pas besoin de se dupliquer en plus - Double Tir reste reserve au tir de
    // base sur WeaponBase. Si UpgradeData.cs essaie encore d'appeler
    // UnlockDoubleShot()/IsDoubleShotUnlocked() ici, la compilation echouera a cet
    // endroit precis - volontaire, ca localise le bug de ciblage signale.

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

    // MODIFIE - un couteau part TOUJOURS pile au centre (garanti de toucher la
    // cible verrouillee par FindNearestEnemy), les couteaux supplementaires
    // s'ajoutent ensuite en PAIRES symetriques de part et d'autre. Avant ce
    // correctif, une repartition purement symetrique sur un nombre PAIR de
    // couteaux (2 a la base) ne tirait jamais droit sur la cible - les deux
    // partaient decales de chaque cote (+7.5/-7.5 degres), ratant systematiquement
    // un ennemi isole droit devant. Desormais : 2 couteaux -> [0, +15] (centre +
    // 1 flanc, alterne de cote a chaque tir). 3 couteaux -> [0, +15, -15], un
    // eventail symetrique complet avec le centre garanti.
    // HYPOTHESE - rotation autour de Vector3.up, coherent avec un jeu sur le plan
    // XZ (comme le reste du code, direction.y = 0f partout). Si le jeu est en 2D
    // pur (plan XY), dis-le-moi et j'adapte l'axe de rotation.
    private void FireSalvo(Vector3 centerDirection)
    {
        // Le couteau central, toujours tire, quel que soit _knifeCount.
        FireProjectile(centerDirection);

        int extraKnives = _knifeCount - 1;
        if (extraKnives <= 0) return;

        int pairCount = extraKnives / 2;
        bool hasOddFlank = extraKnives % 2 == 1;

        for (int p = 1; p <= pairCount; p++)
        {
            float angle = _fanAngleSpread * p;
            FireProjectile(Quaternion.AngleAxis(angle, Vector3.up) * centerDirection);
            FireProjectile(Quaternion.AngleAxis(-angle, Vector3.up) * centerDirection);
        }

        if (hasOddFlank)
        {
            float angle = _fanAngleSpread * (pairCount + 1) * (_nextOddFlankOnRight ? 1f : -1f);
            FireProjectile(Quaternion.AngleAxis(angle, Vector3.up) * centerDirection);
            _nextOddFlankOnRight = !_nextOddFlankOnRight;
        }
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
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                nearest = col.transform;
            }
        }
        return nearest;
    }
    private void FireProjectile(Vector3 direction)
    {
        if (ObjectPool.Instance == null) return;
        GameObject projectileGO = ObjectPool.Instance.Get("ProjectileShuriken", transform.position, Quaternion.identity);
        if (projectileGO == null) return;
        ProjectileBasic projectile = projectileGO.GetComponent<ProjectileBasic>();
        if (projectile != null)
        {
            projectile.Init(direction, _damage);
            projectile.SetPiercing(true, _maxPierceCount);
        }
    }
    public void AddDamage(float value) => _damage += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;

    // AJOUTE - palier 1 : +1 couteau a la salve.
    public void AddKnife() => _knifeCount += 1;

    // Appele par le palier 3 de la carte Couteaux
    public void AddPierce(int amount) => _maxPierceCount += Mathf.Max(1, amount);
}