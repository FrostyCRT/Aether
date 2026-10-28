using UnityEngine;
using System.Collections.Generic;

public class OrbitalProjectile : MonoBehaviour
{
    [SerializeField] private float _damage = 15f;
    // AJOUTÉ (2026-10-03) - retour utilisateur : un petit repoussement léger montre qu'un ennemi vient de se faire
    // percuter par l'orbe, au lieu de l'encaisser sans aucune réaction.
    // MODIFIÉ (2026-10-04) - 6 -> 4 (retour : "trop fort"). Voir aussi EnemyBase.MaxKnockbackSpeed : le vrai
    // problème était surtout le cumul entre plusieurs orbes différents, maintenant plafonné séparément.
    [SerializeField] private float _knockbackForce = 4f;
    private readonly float _hitCooldown = 0.5f;

    // Cache des timestamps de dernière frappe : ZÉRO UPDATE requis pour nettoyer
    private readonly Dictionary<int, float> _lastHitTimestamps = new Dictionary<int, float>();

    // AJOUTÉ (2026-10-05) - retour utilisateur : "il faut travailler sur les animations des compétences [...]
    // pour les autres il y a rien" - les orbitaux (Orbitaux, Vortex de Mana, Orbes Foudroyants) n'avaient aucune
    // traînée ni éclat d'impact, contrairement à la Boue/la Foudre. Traînée rose (ColorOrbital, même teinte que
    // les chiffres de dégâts) créée une fois en Awake - jamais recréée, juste vidée à chaque retour au pool.
    private TrailRenderer _trail;

    private void Awake()
    {
        _trail = gameObject.AddComponent<TrailRenderer>();
        _trail.time = 0.15f;
        _trail.minVertexDistance = 0.04f;
        _trail.widthMultiplier = 0.14f;
        _trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        _trail.material = new Material(Shader.Find("Sprites/Default"));
        _trail.startColor = FXSprites.A(DamageNumberSpawner.ColorOrbital, 0.6f);
        _trail.endColor = FXSprites.A(DamageNumberSpawner.ColorOrbital, 0f);
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;
        _trail.sortingOrder = 3;
    }

    public void SetDamage(float damage)
    {
        _damage = damage;
    }

    // AJOUTÉ (2026-10-27, hitbox des boss - voir BossBodyShape et ProjectileBasic._pendingBosses) - un orbe qui entre
    // dans la boîte d'un boss sans toucher son corps attend (OnTriggerStay) de toucher vraiment le corps : un seul coup
    // par passage, comme avant.
    private const float BodyHitMargin = 0.3f;
    private readonly System.Collections.Generic.List<Collider> _pendingBosses = new System.Collections.Generic.List<Collider>(2);

    private void OnTriggerStay(Collider other)
    {
        if (!_pendingBosses.Contains(other)) return;
        if (!other.CompareTag("Enemy")) { _pendingBosses.Remove(other); return; }
        if (other.TryGetComponent(out BossBase boss) && boss.IsOnBody(transform.position, BodyHitMargin))
        {
            _pendingBosses.Remove(other);
            OnTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        _pendingBosses.Remove(other);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;
        if (other.TryGetComponent(out BossBase bodyCheck) && !bodyCheck.IsOnBody(transform.position, BodyHitMargin))
        {
            if (!_pendingBosses.Contains(other)) _pendingBosses.Add(other);
            return;
        }

        // Utilisation de l'InstanceID unique (int) au lieu du GameObject complet pour éviter les fuites de mémoire
        int enemyId = other.GetInstanceID();
        float currentTime = Time.time;

        if (_lastHitTimestamps.TryGetValue(enemyId, out float lastHitTime))
        {
            if (currentTime - lastHitTime < _hitCooldown)
            {
                return; // L'ennemi est encore sous le coup du cooldown (I-frame)
            }
        }

        // Buffs dynamiques du joueur (Concentration) lus à l'impact. 1f si inactif.
        float dmg = _damage * PlayerBuffs.OutgoingDamageMultiplier;

        // On cherche d'abord la base commune d'ennemi normal
        other.TryGetComponent(out EnemyBase enemy);
        if (enemy != null)
        {
            enemy.TakeDamage(dmg, DamageNumberSpawner.ColorOrbital);
            // CORRIGÉ (2026-10-03) - retour utilisateur : "il faut que ça repousse à l'extérieur, donc à l'opposé
            // du joueur". Calculer "away" depuis la position de L'ORBE (transform.position) était faux : l'orbe se
            // déplace sur son orbite, sa position par rapport à l'ennemi touché n'a aucun rapport fixe avec la
            // direction réellement voulue (pouvait pousser de travers, voire vers le joueur). WeaponOrbital parente
            // chaque orbe directement au joueur (orbital.transform.SetParent(transform) dans SpawnOrbitals) - le
            // centre voulu est donc transform.parent.position, pas transform.position.
            Vector3 center = transform.parent != null ? transform.parent.position : transform.position;
            Vector3 away = other.transform.position - center;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) enemy.ApplyKnockback(away.normalized * _knockbackForce);
            ImpactSparkVFX.Spawn(transform.position, DamageNumberSpawner.ColorOrbital);
            _lastHitTimestamps[enemyId] = currentTime;
            return;
        }

        // Si ce n'est pas un ennemi basique, on check le Boss
        other.TryGetComponent(out BossBase boss);
        if (boss != null)
        {
            boss.TakeDamage(dmg, DamageNumberSpawner.ColorOrbital);
            ImpactSparkVFX.Spawn(transform.position, DamageNumberSpawner.ColorOrbital);
            _lastHitTimestamps[enemyId] = currentTime;
        }
    }

    // Si le projectile est désactivé ou replacé dans un Object Pool, on nettoie son dictionnaire
    private void OnDisable()
    {
        _lastHitTimestamps.Clear();
        _pendingBosses.Clear();
    }

    // AJOUTÉ (2026-10-05) - sans ça, un orbe recyclé depuis le pool dessinerait une traînée reliant sa position
    // précédente (ailleurs sur la carte) à sa nouvelle position de réapparition - un trait parasite traversant
    // tout l'écran. Vidée aussi à l'activation initiale (Awake peut arriver avant le premier SetActive(true)).
    private void OnEnable()
    {
        if (_trail != null) _trail.Clear();
    }
}