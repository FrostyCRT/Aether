using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float _speed = 6f;
    [SerializeField] private float _maxRange = 40f;
    [SerializeField] private float _damage = 10f; // Ajout de la stat de dégâts manquante

    private Vector3 _direction;
    private Vector3 _startPosition;
    private bool _hasHit = false;

    public void Init(Vector3 direction)
    {
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
            if (playerHealthCheck != null)
            {
                playerHealthCheck.TakeDamage(_damage * GameModes.EnemyDamageScale);

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