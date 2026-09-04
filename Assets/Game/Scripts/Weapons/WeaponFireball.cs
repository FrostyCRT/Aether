using UnityEngine;
using System.Collections;
public class WeaponFireball : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage = 200f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _fireRate = 0.5f; // 1 tir toutes les 2 secondes
    [SerializeField] private float _detectionRange = 15f;

    // MODIFIE - l'explosion a l'impact est desormais TOUJOURS garantie (100%), plus
    // une chance aleatoire. C'est l'identite meme de l'arme : "impact qui explose",
    // pas "impact qui peut parfois exploser". La progression se fait maintenant sur
    // le RAYON (palier 1) et les degats (palier 2), la Brulure venant en palier 3.
    [Header("Explosion (toujours declenchee)")]
    [Tooltip("Degats de l'explosion en fraction des degats du tir principal (0.4 = 40%).")]
    [SerializeField] private float _fragmentDamageRatio = 0.4f;
    [SerializeField] private float _fragmentRadius = 2.25f; // MODIFIE - 1.5 -> 2.25 (x1.5), rayon de base juge trop petit en jeu

    // AJOUTE - Brulure, palier 3. Desactivee tant que le palier n'est pas pris.
    [Header("Brulure (palier 3)")]
    [SerializeField] private bool _burnEnabled = false;
    [Tooltip("Degats de brulure par seconde, en fraction des degats du tir principal au moment du tir (0.15 = 15%/s).")]
    [SerializeField] private float _burnDamagePerSecondRatio = 0.15f;
    [SerializeField] private float _burnDuration = 3f;

    private float _cooldownTimer = 0f;
    private static readonly Collider[] _detectionBuffer = new Collider[50];

    // AJOUTE - Fireball n'appliquait jamais le bonus de Reputation Degats,
    // contrairement a WeaponBase qui le fait deja dans son propre Start().
    private void Start()
    {
        if (MetaProgressionManager.Instance != null)
        {
            float bonusDamage = MetaProgressionManager.Instance.GetReputationBonusDamage();
            _damage += _damage * bonusDamage;
        }
    }

    // SUPPRIME - Double Tir retire de cette arme (voir note de session : un tir
    // lourd a explosion garantie n'a pas besoin de se dupliquer, Double Tir reste
    // reserve au tir de base sur WeaponBase). Si UpgradeData.cs essaie encore
    // d'appeler UnlockDoubleShot()/IsDoubleShotUnlocked() ici, la compilation
    // echouera a cet endroit precis - c'est volontaire, ca localise le bug de
    // ciblage signale plutot que de le corriger a l'aveugle sans voir ce fichier.

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
                FireProjectile(direction);
                _cooldownTimer = 0f;
            }
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

        GameObject projectileGO = ObjectPool.Instance.Get("ProjectileFireball", transform.position, Quaternion.identity);
        if (projectileGO == null) return;
        ProjectileBasic projectile = projectileGO.GetComponent<ProjectileBasic>();
        if (projectile != null)
        {
            projectile.Init(direction, _damage);

            // MODIFIE - explosion toujours a 100%, plus de chance meta/carte a combiner.
            projectile.SetFragmentation(1f, _damage * _fragmentDamageRatio, _fragmentRadius);

            if (_burnEnabled)
            {
                projectile.SetBurn(_damage * _burnDamagePerSecondRatio, _burnDuration);
            }
        }
    }

    // MODIFIE - palier 1 : augmente le rayon d'explosion plutot que la chance de
    // fragmentation (qui n'existe plus en tant que variable, l'explosion etant fixe).
    public void AddFragmentRadius(float value) => _fragmentRadius += value;

    // AJOUTE - palier 3 : debloque la Brulure.
    public void EnableBurn() => _burnEnabled = true;

    public void AddDamage(float value) => _damage += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;
}