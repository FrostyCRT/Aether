using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float _speed = 6f;
    // AJOUTÉ (2026-10-21) - garde la valeur sérialisée d'origine (prefab partagé, UNE SEULE instance de pool pour
    // tous les tireurs) pour pouvoir la restaurer en Init() : sans ça, un projectile dont la vitesse a été modifiée
    // par un override (voir Init plus bas) garderait cette vitesse modifiée à vie une fois recyclé par le pool, même
    // pour un tireur différent qui n'a jamais demandé d'override.
    private float _baseSpeed = -1f;
    [SerializeField] private float _maxRange = 40f;
    [SerializeField] private float _damage = 10f; // Ajout de la stat de dégâts manquante

    private Vector3 _direction;
    private Vector3 _startPosition;
    private bool _hasHit = false;

    // AJOUTE (2026-10-16, audit) - cause de mort transmise au HealthSystem : "boss" pour les tirs de boss (sinon le
    // message de défaite parlait toujours de la horde, même tué par un projectile de boss). Projectile poolé : remis
    // à chaque Init.
    private string _damageSource = "horde";

    // AJOUTÉ (2026-10-21, retour utilisateur : "pour la cadence des bulbes [...] c'est pas la cadence mais la
    // vitesse du projectile lui-même pour se déplacer que je voulais ralentir" - correction du point 4 de la suite
    // 17, mal compris la 1ère fois : la cadence de tir avait été ralentie (3s -> 4,5s, voir EnemyBulb._fireInterval)
    // alors que la vraie demande portait sur la VITESSE DE DÉPLACEMENT du projectile. Cadence redescendue à 3s.
    // EnemyProjectile est PARTAGÉ (Bulb/EnemyShooter/BossDeer/BossCorruptedSource/BossBase) : un override optionnel
    // ici, plutôt que de changer _speed sur le prefab, pour ne ralentir QUE les tirs du Bulbe sans toucher aux
    // autres tireurs. speedOverride <= 0 = garde _speed par défaut (comportement inchangé pour tous les appelants
    // existants qui n'utilisent pas ce paramètre).
    public void Init(Vector3 direction, string damageSource = "horde", float speedOverride = -1f)
    {
        _damageSource = damageSource;
        if (_baseSpeed < 0f) _baseSpeed = _speed; // capturée une seule fois, à la toute première utilisation réelle
        _speed = speedOverride > 0f ? speedOverride : _baseSpeed;
        direction.y = 0f; // AJOUTÉ — sécurité, empêche toute trajectoire qui dériverait en hauteur
        _direction = direction.normalized;

        Vector3 spawnPos = transform.position;
        spawnPos.y = 1.5f;
        transform.position = spawnPos;

        _startPosition = transform.position;
        _hasHit = false;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // CORRECTION PAUSE
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        transform.position += _direction * _speed * Time.deltaTime;

        // OPTIMISATION : SqrMagnitude est plus performant que Vector3.Distance pour les vérifications de portée
        if (Vector3.SqrMagnitude(transform.position - _startPosition) >= _maxRange * _maxRange)
        {
            Despawn();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;

        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            HealthSystem playerHealthCheck = other.GetComponent<HealthSystem>();
            // CORRIGÉ (2026-10-07, retour utilisateur : "quand Lyra met son clone et qu'elle est invisible/invincible,
            // elle peut toujours se prendre des projectiles, même si elle ne se prend pas les dégâts") - ce bloc lisait
            // player.IsInvincible (PlayerController), qui ne vaut vrai QUE pendant le Dash (StartDash) ; l'invincibilité
            // du Clone Fantôme passe par HealthSystem.AddExternalInvincibility() et ne touche jamais ce flag. Résultat :
            // pendant le clone, on tombait dans la branche "dégâts normaux" plus bas - TakeDamage() ne faisait rien
            // (HealthSystem bloque bien les PV), mais le VFX d'impact rouge jouait quand même et le projectile était
            // détruit. Lit maintenant HealthSystem.IsInvincible (la vraie source de vérité, qui inclut le clone).
            if (playerHealthCheck != null && playerHealthCheck.IsInvincible)
            {
                if (player != null && player.CanAbsorb)
                {
                    CrystalSystem crystal = other.GetComponent<CrystalSystem>();
                    if (crystal != null) crystal.AbsorbProjectile();

                    // AJOUTÉ (2026-10-03) - absorption par le Dash/Cristal : jusqu'ici muette, comme tout impact de ce
                    // projectile. Même bleu que le chiffre de dégâts "Projectile" (DamageNumberSpawner.ColorProjectile).
                    ExpandingRingVFX.Spawn(transform.position, 0.9f, new Color(0f, 0.8f, 1f, 0.85f), 0.3f);

                    _hasHit = true;
                    Despawn();
                }
                return;
            }

            // AJOUTE - nœud capstone Gardien "Bouclier de Mana" : si le joueur n'est
            // pas déjà invincible et qu'une charge de bouclier est disponible, le
            // projectile est absorbé (0 dégât) et alimente le Cristal.
            ManaShield shield = other.GetComponent<ManaShield>();
            if (shield != null && shield.TryAbsorb())
            {
                // AJOUTÉ (2026-10-03) - absorption par le Bouclier de Mana : même vert que la bulle (#36D936).
                ExpandingRingVFX.Spawn(transform.position, 0.9f, new Color(0.212f, 0.851f, 0.212f, 0.85f), 0.3f);

                _hasHit = true;
                Despawn();
                return;
            }

            // CORRECTION LOGIQUE : Application des dégâts si le joueur n'est pas invincible
            // CORRIGÉ (2026-10-11, retour utilisateur : "je me fais one shot [...] si je suis dans lui, et qu'il
            // lance ses projectiles, je les prends tous en même temps") - TakeDamage() direct n'avait aucune
            // protection entre plusieurs projectiles distincts touchant la même frame (voir
            // HealthSystem.TakeDamageFromProjectile, qui a maintenant sa propre fenêtre anti-cumul).
            if (playerHealthCheck != null)
            {
                playerHealthCheck.TakeDamageFromProjectile(_damage * GameModes.EnemyDamageScale, _damageSource);

                // AJOUTÉ (2026-10-03) - jusqu'ici le projectile disparaissait sans rien au moment même où il touche le
                // joueur (seul le chiffre de dégâts, un peu après, signalait le coup). Même rouge que
                // DamageNumberSpawner.ColorPlayer, pour que l'œil associe tout de suite éclat ↔ dégât subi.
                ExpandingRingVFX.Spawn(transform.position, 0.7f, new Color(1f, 0.2f, 0.2f, 0.85f), 0.25f);
            }

            _hasHit = true;
            Despawn();
        }
    }

    // CORRECTION POOLING : Centralisation de la désactivation pour éviter de détruire l'entité
    private void Despawn()
    {
        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.ReturnToPool("EnemyProjectile", gameObject);
        }
        else
        {
            Destroy(gameObject); // Sécurité de secours
        }
    }
}