using UnityEngine;
using System.Collections;

public class EnemyBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _maxHealth = 300f; // MODIFIE - x10, cf. rescale global des degats/PV

    // AJOUTE - coche cette case sur les ennemis volants (ex: le Corbeau). Utilise
    // par les effets de zone au sol (Boue) pour accorder une immunite totale -
    // logique puisqu'un volant ne touche jamais le sol. Champ generique plutot
    // qu'un script dedie au Corbeau, pour rester reutilisable si d'autres ennemis
    // volants sont ajoutes plus tard, sans rien casser pour tous les ennemis
    // existants (false par defaut, aucun changement de comportement pour eux).
    [Header("Vol")]
    [SerializeField] private bool _isFlying = false;
    public bool IsFlying => _isFlying;

    [Header("Drops")]
    [SerializeField] private float _xpValue = 10f;
    [SerializeField] private int _goldValue = 2;

    [Header("Pool")]
    [SerializeField] private string _poolTag = "Enemy";
    public string PoolTag => _poolTag;

    [Header("Distance Joueur")]
    [SerializeField] private float _playerContactRadius = 1.2f;

    [Header("Dégâts de contact")]
    [SerializeField] private float _contactDamage = 150f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _contactDamageCooldown = 0.5f;

    [Header("Scaling difficulté")]
    private float _healthMultiplier = 1f;

    // AJOUTE - systeme de Brulure (palier 3 Fireball). Un seul stack actif a la
    // fois : reapplique = redemarre la duree plutot que d'additionner plusieurs
    // brulures en parallele, pour eviter un scaling degats hors de controle si le
    // joueur touche le meme ennemi plusieurs fois pendant qu'il brule deja.
    [Header("Brulure (visuel + degats sur la duree)")]
    [SerializeField] private float _burnTickInterval = 0.5f;
    [Tooltip("Intensite du pulse de teinte orange sur le sprite pendant la brulure (0 = invisible, 1 = teinte plein orange).")]
    [SerializeField] private float _burnTintIntensity = 0.55f;
    private static readonly Color BurnTintColor = new Color(1f, 0.45f, 0.12f);
    private static readonly Color BurnDamageNumberColor = new Color(1f, 0.55f, 0.15f);
    private Coroutine _burnCoroutine;
    private SpriteRenderer _spriteRenderer;
    private Color _spriteBaseColor = Color.white;
    private bool _spriteBaseColorCaptured = false;

    public void SetHealthMultiplier(float multiplier)
    {
        _healthMultiplier = multiplier;
    }

    private Vector3 _smoothedMoveDirection = Vector3.forward;
    protected float MoveSpeed => _moveSpeed;

    private float _currentHealth;
    private Transform _playerTransform;
    private Transform _currentTarget;

    protected float _speedMultiplier = 1f;
    private float _speedMultiplierTarget = 1f;

    protected virtual void OnEnable()
    {
        _currentHealth = _maxHealth * _healthMultiplier;
        _speedMultiplier = 1f;
        _speedMultiplierTarget = 1f;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }

        _currentTarget = _playerTransform;

        PlayerController.OnPhantomDestroyed += HandlePhantomDestroyed;

        // AJOUTE - un ennemi qui revient du pool doit repartir avec son Animator a
        // vitesse normale, au cas ou il aurait ete fige (speed = 0) a la fin de sa
        // vie precedente via HandleGameEnded ci-dessous.
        GameManager.OnGameEnded += HandleGameEnded;
        if (_animatorController == null) _animatorController = GetComponentInChildren<EnemyAnimatorController>();
        if (_animatorController != null) _animatorController.SetAnimatorPaused(false);

        // AJOUTE - un ennemi qui revient du pool avec une brulure encore active de sa
        // vie precedente serait un bug (coroutine orpheline, teinte qui reste collee).
        // On repart toujours propre a chaque reutilisation.
        StopBurnAndResetTint();
    }

    private void OnDisable()
    {
        PlayerController.OnPhantomDestroyed -= HandlePhantomDestroyed;
        GameManager.OnGameEnded -= HandleGameEnded;

        // AJOUTE - coupe la coroutine de brulure si l'ennemi est desactive/retourne
        // au pool en pleine brulure (ex: tue par une autre source de degats pendant
        // qu'il brule), pour eviter une coroutine qui tourne dans le vide.
        StopBurnAndResetTint();
    }

    // AJOUTE - appele UNE SEULE FOIS, exactement quand la partie se termine
    // (victoire ou game over), via GameManager.OnGameEnded. C'est ce qui manquait
    // pour que les ennemis arretent proprement leur animation de marche au lieu de
    // rester figes a mi-boucle : Update() (et donc UpdateBehaviour) s'arrete net a
    // IsGameOver, donc plus rien n'appelait jamais SetAttacking(false) ou
    // n'indiquait a l'Animator qu'il fallait s'arreter.
    private void HandleGameEnded()
    {
        if (_animatorController == null) _animatorController = GetComponentInChildren<EnemyAnimatorController>();
        if (_animatorController != null) _animatorController.SetAnimatorPaused(true);
    }

    private void HandlePhantomDestroyed()
    {
        if (_currentTarget != null && _currentTarget == PlayerController.ActivePhantomClone)
        {
            SetTarget(_playerTransform);
        }
    }

    private void Start()
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }
        _currentTarget = _playerTransform;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        if (_currentTarget == null)
            SetTarget(_playerTransform);

        if (_currentTarget == null) return;

        if (PlayerController.ActivePhantomClone != null && _currentTarget == _playerTransform)
        {
            float sqrDistance = Vector3.SqrMagnitude(transform.position - PlayerController.ActivePhantomClone.position);
            if (sqrDistance <= PlayerController.PhantomAttractRadius * PlayerController.PhantomAttractRadius)
            {
                PlayerController.TryAttractToPhantom(this);
            }
        }

        UpdateBehaviour(_currentTarget);

        OnEnemyUpdate();
    }

    protected virtual void OnEnemyUpdate() { }

    private EnemyAnimatorController _animatorController;

    protected virtual void UpdateBehaviour(Transform target)
    {
        // MODIFIE - meme raisonnement que pour la direction de deplacement plus bas :
        // Vector3.Distance() mesurait la distance 3D COMPLETE, Y inclus. Tant que la
        // cible etait le joueur (meme hauteur), ca ne changeait rien. Mais avec le
        // Clone Fantome a une hauteur Y differente, un ennemi pouvait arriver pile
        // a cote horizontalement sans jamais que cette distance ne descende sous
        // _playerContactRadius (l'ecart vertical restait bloque dans le calcul) :
        // le seuil de contact n'etait donc jamais atteint, l'ennemi ne s'arretait
        // jamais et tournait indefiniment autour du Clone sans attaquer.
        Vector3 toTargetFlat = target.position - transform.position;
        toTargetFlat.y = 0f;
        float distanceToTarget = toTargetFlat.magnitude;
        bool isInContactWithTarget = distanceToTarget <= _playerContactRadius;

        if (_animatorController == null) _animatorController = GetComponentInChildren<EnemyAnimatorController>();

        if (isInContactWithTarget)
        {
            if (_animatorController != null) _animatorController.SetAttacking(true);

            if (target == _playerTransform)
            {
                HealthSystem playerHealth = _playerTransform.GetComponent<HealthSystem>();
                if (playerHealth != null)
                    playerHealth.TryTakeContactDamage(_contactDamage, _contactDamageCooldown);
            }

            return;
        }

        if (_animatorController != null) _animatorController.SetAttacking(false);

        // Reutilise toTargetFlat calcule en haut de la methode (deja aplati en Y),
        // au lieu de recalculer target.position - transform.position ici.
        Vector3 direction = toTargetFlat.normalized;
        Vector3 separation = GetBaseSeparation();
        Vector3 desiredDirection = (direction + separation * 0.3f).normalized;

        if (desiredDirection.sqrMagnitude > 0.01f)
            _smoothedMoveDirection = Vector3.Slerp(_smoothedMoveDirection, desiredDirection, 10f * Time.deltaTime);

        Vector3 final = _smoothedMoveDirection;

        transform.position += final * MoveSpeed * _speedMultiplier * _speedMultiplierTarget * Time.deltaTime;

        if (final.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(final);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.deltaTime);
        }
    }

    public void SetTarget(Transform newTarget, float speedBoost = 1f)
    {
        _currentTarget = newTarget != null ? newTarget : _playerTransform;

        if (_currentTarget == _playerTransform)
        {
            _speedMultiplierTarget = 1f;
        }
        else
        {
            _speedMultiplierTarget = speedBoost;
        }
    }

    private static readonly Collider[] _neighbourBuffer = new Collider[16];
    private static int _enemyLayerMask = -1;

    // MODIFIE - pushDirection n'etait jamais aplati en Y. Meme cause que le bug de
    // sauts aleatoires en Y trouve sur les gobelins (EnemyShooter.GetSeparationForce,
    // qui a une copie separee de cette meme logique) : une composante Y residuelle
    // ici pouvait se propager dans desiredDirection puis final, faisant deriver
    // n'importe quel type d'ennemi verticalement des qu'il a des voisins proches -
    // corrige ici de facon preventive pour tous les types d'ennemis qui utilisent
    // EnemyBase.UpdateBehaviour() directement, pas seulement les gobelins.
    private Vector3 GetBaseSeparation()
    {
        if (_enemyLayerMask == -1)
            _enemyLayerMask = 1 << LayerMask.NameToLayer("Enemy");

        Vector3 force = Vector3.zero;
        float separationRadius = 1.5f;

        int count = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, _neighbourBuffer, _enemyLayerMask);

        for (int i = 0; i < count; i++)
        {
            Collider neighbour = _neighbourBuffer[i];
            if (neighbour.gameObject == gameObject) continue;

            Vector3 pushDirection = transform.position - neighbour.transform.position;
            pushDirection.y = 0f;
            force += pushDirection.normalized;
        }

        return force.normalized * 0.5f;
    }

    public void TakeDamage(float damage, Color color = default, bool fromNova = false)
    {
        _currentHealth -= damage;

        if (DamageNumberSpawner.Instance != null)
        {
            Color c = color == default ? DamageNumberSpawner.ColorProjectile : color;
            DamageNumberSpawner.Instance.Spawn(transform.position, damage, c, transform, false);
        }

        if (_currentHealth <= 0)
            Die(fromNova);
    }

    // AJOUTE - point d'entree public de la Brulure, appele par ProjectileBasic quand
    // une upgrade Fireball avec Brulure debloquee touche cet ennemi (direct ou via
    // l'explosion). Redemarre la coroutine si une brulure est deja en cours plutot
    // que d'en empiler une deuxieme.
    public void ApplyBurn(float damagePerSecond, float duration)
    {
        if (damagePerSecond <= 0f || duration <= 0f) return;

        CacheSpriteRenderer();

        if (_burnCoroutine != null)
            StopCoroutine(_burnCoroutine);

        _burnCoroutine = StartCoroutine(BurnRoutine(damagePerSecond, duration));
    }

    private void CacheSpriteRenderer()
    {
        if (_spriteRenderer != null) return;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null && !_spriteBaseColorCaptured)
        {
            _spriteBaseColor = _spriteRenderer.color;
            _spriteBaseColorCaptured = true;
        }
    }

    private IEnumerator BurnRoutine(float damagePerSecond, float duration)
    {
        float elapsed = 0f;
        float tickTimer = 0f;
        float tickDamage = damagePerSecond * _burnTickInterval;

        while (elapsed < duration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            tickTimer += dt;

            if (_spriteRenderer != null)
            {
                // Pulsation rapide plutot qu'une teinte fixe : plus lisible comme
                // "effet actif en ce moment" qu'une simple couleur statique.
                float pulse = (Mathf.Sin(elapsed * 10f) + 1f) * 0.5f;
                float blend = _burnTintIntensity * (0.6f + 0.4f * pulse);
                _spriteRenderer.color = Color.Lerp(_spriteBaseColor, BurnTintColor, blend);
            }

            if (tickTimer >= _burnTickInterval)
            {
                tickTimer -= _burnTickInterval;
                TakeDamage(tickDamage, BurnDamageNumberColor);
            }

            yield return null;
        }

        if (_spriteRenderer != null)
            _spriteRenderer.color = _spriteBaseColor;

        _burnCoroutine = null;
    }

    private void StopBurnAndResetTint()
    {
        if (_burnCoroutine != null)
        {
            StopCoroutine(_burnCoroutine);
            _burnCoroutine = null;
        }

        if (_spriteRenderer != null && _spriteBaseColorCaptured)
            _spriteRenderer.color = _spriteBaseColor;
    }

    private void Die(bool fromNova = false)
    {
        if (XPGemSpawner.Instance != null)
            XPGemSpawner.Instance.SpawnGems(transform.position, _xpValue);

        GameManager.Instance.AddKill();
        MetaProgressionManager.Instance.AddRunGold(_goldValue);

        if (fromNova)
        {
            if (_playerTransform != null)
            {
                PlayerController pc = _playerTransform.GetComponent<PlayerController>();
                if (pc != null) pc.ResetDashCooldown();
            }
        }

        ObjectPool.Instance.ReturnToPool(_poolTag, gameObject);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }
}