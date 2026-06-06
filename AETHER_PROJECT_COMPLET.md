# AETHER — Documentation Complète du Projet
## Jeu Unity 2021.3.45f2 (URP) — Bullet Heaven Roguelite

---

# 1. CONTEXTE DU PROJET

- **Développeur** : Solo (1 personne, débutant complet en Unity/C#)
- **Moteur** : Unity 2021.3.45f2 LTS
- **Pipeline** : Universal Render Pipeline (URP) — Universal 3D
- **Plateforme cible** : PC Windows
- **Distribution** : itch.io (WebGL) → Steam à terme
- **Inspiration** : Vampire Survivors, Brotato, Mushoku Tensei (DA)
- **Repository GitHub** : https://github.com/FrostyCRT/Aether
- **Méthode de travail** : Script par script avec explications détaillées

---

# 2. CONCEPT DU JEU

**Nom : AETHER** (anciennement "NEXUS" — changé car trop sci-fi)

Un Bullet Heaven / Auto-shooter roguelite top-down en 3D, caméra vue isométrique à 65°.

## La boucle de jeu
```
Survive → Tue des ennemis → Gagne XP → Niveau up → Choix d'upgrade → Build explose → Mort → Meta-progression → Retry
```

## Ce qui différencie Aether des autres jeux du genre
1. **Le Cristal + absorption de projectiles pendant le dash** — mécanique signature unique
2. **Le dash offensif** — identité PC, traverser des projectiles pour les absorber
3. **L'univers Anime Fantasy style Ghibli/Mushoku Tensei** — rare dans le genre
4. **Les orbitaux chargés** — ralentissent les ennemis au contact

---

# 3. DIRECTION ARTISTIQUE — AETHER

## Style visuel
- **Anime Toon-Shading** type Ghibli / Mushoku Tensei
- **Low-Poly** (~2000 polygones par modèle ennemi, 2500 pour le joueur)
- **Caméra** : Vue top-down inclinée à 65°, projection Orthographique
- **Shading** : URP Toon Shader (Cel-shading)
- **Post-Processing** : Bloom, Color Grading saturé, éclairage chaleureux

## Le Héros (Joueur)
- Inspiré de **Rudeus Greyrat** (Mushoku Tensei) — style sérieux, un peu chibi
- Cheveux blond et en bataille, yeux verts
- Tunique grise en lin avec gilet de cuir marron par dessus
- Ceinture en cuir simple, pantalon sombre, bottes en cuir usées
- Tient un bâton magique en bois surmonté d'un cristal bleu lumineux
- Build musclé mais élancé, proportions normales

**Prompt Meshy AI (800 caractères) :**
```
Young male mage adventurer inspired by Rudeus Greyrat Mushoku Tensei, 
light brown short messy hair, green eyes, serious determined expression, 
wearing layered gray linen robe with brown leather vest over it, 
simple leather belt, dark pants, worn leather boots, 
holding wooden magic staff topped with glowing blue mana crystal, 
muscular but lean build, average height, mature face not childlike, 
low poly game ready 2500 polygons, clean topology, T-pose for rigging, 
optimized for top-down isometric view 65 degrees, 
anime game art style Studio Ghibli inspired, 
detailed on top of head and shoulders for top-down readability, 
no chibi proportions, realistic fantasy adventurer proportions, 
PBR textures Unity URP compatible
```

## Bestiaire — Map 1 (Plaine)

### Loup de Mana (Ennemi Basic)
- Pelage gris/bleu foncé avec marques naturelles plus claires
- Crête de poils rigides le long du dos bleutée
- Yeux bleu électrique intenses (seul élément lumineux)
- Muscles des pattes avant surdéveloppés, griffes bleu/gris métallique
- 1000-1200 polygones

**Prompt Meshy :**
```
Fierce magical wolf enemy, low poly game ready, 1000-1200 polygons,
dark blue-grey fur with lighter natural stripe markings suggesting 
mana corruption, rigid blue-tinted spine crest along back,
intense glowing electric blue eyes, muscular oversized front legs,
large metallic blue-grey claws, aggressive forward-leaning posture,
top-down game view optimized silhouette, fantasy creature,
no particle effects, clean topology, T-pose for rigging,
Studio Ghibli inspired but mature and threatening design,
anime game art style
```

### Golem de Tronc (Tank)
- Corps humanoïde massif fait de troncs d'arbres torsadés
- Bras qui traînent presque jusqu'au sol
- Pas de visage — deux cavités sombres avec yeux orangés
- Racines sortant des pieds et épaules
- 1200-1500 polygones

**Prompt Meshy :**
```
Massive tree stump golem humanoid enemy, low poly game ready,
1200-1500 polygons, body made of twisted dead tree trunks,
extremely wide shoulders and chest, arms nearly dragging on ground,
no visible face except two glowing orange eye cavities,
roots extending from feet and shoulders, rough cracked bark texture,
moss in bark crevices, heavy imposing silhouette,
top-down game view optimized, T-pose for rigging,
mature threatening design, anime game art style,
Studio Ghibli inspired monster
```

### Bulbe Cracheur (Shooter)
- Corps central : bulbe floral géant fermé, vert/violet
- S'ouvre comme fleur carnivore quand il tire
- 4-6 racines épaisses comme pattes (légèrement translucides)
- Épines sur le bord du bulbe
- 1000-1200 polygones

**Prompt Meshy :**
```
Carnivorous plant shooter enemy, low poly game ready, 1000-1200 polygons,
large closed flower bulb body green and purple, opens to reveal
carnivorous mouth when attacking, 4-6 thick root tentacle legs
slightly translucent with mana veins inside, sharp thorns on bulb edges,
elevated posture for shooting over other enemies,
no visible eyes, organic threatening design,
top-down game view optimized silhouette, fantasy creature,
T-pose equivalent rest pose, mature anime game art style,
Studio Ghibli inspired but dangerous
```

## Boss — Map 1 (Plaine)

### Boss 1 — Le Sanglier de Mana (5min)
- Recouvert de mousses et lignes de mana lumineuses
- Défenses en cristal de mana bleu
- 3500-4000 polygones

**Prompt Meshy :**
```
Giant divine corrupted boar boss enemy, low poly game ready,
3500-4000 polygons, massive imposing silhouette,
tusks made of blue mana crystal growing from jaw,
dark grey and green moss-covered body, mana crystal growths 
along spine and shoulders, small glowing blue eyes filled with rage,
heavy muscular build, battle-scarred hide,
top-down game view optimized, T-pose for rigging,
mature threatening boss design, anime fantasy game art style,
no particle effects, clean topology
```

**Attaques :**
- Charge en ligne droite vers le joueur
- Tir en éventail de 8 projectiles de mana
- Piétinement — zone AOE autour de lui

### Boss 2 — Le Cerf Ancestral (10min)
- Cerf immense et élancé
- Ramure dorée/bleue avec lignes de mana lumineuses
- Yeux orange intenses, particules de mana corrompu aux sabots
- 3500-4000 polygones

**Prompt Meshy :**
```
Giant ancestral divine corrupted stag boss, low poly game ready,
3500-4000 polygons, massive branching golden antlers with 
blue mana crystal tips, elegant but imposing body build,
white and golden fur with blue mana line markings along spine,
intense glowing orange eyes, long flowing tail made of mana energy,
hooves leaving dark corruption marks, majestic yet threatening posture,
top-down game view optimized, T-pose for rigging,
mature boss design, anime fantasy game art style,
Studio Ghibli Princess Mononoke inspired
```

**Attaques :**
- Charge éclair — plus rapide que le sanglier, change de direction
- Pluie de feuilles tranchantes — tir en spirale de projectiles verts
- Régénération — récupère 100HP toutes les 30 secondes
- Rage — en dessous de 30% HP, attaques 50% plus rapides

### Boss 3 — La Source Corrompue (15min — Boss Final de Zone)
- Masse cristalline bleue/noire flottante
- Tentacules de mana corrompu
- Cristaux noirs orbitaux
- Formes animales vaguement visibles piégées dans les cristaux
- 5000-6000 polygones

**Prompt Meshy :**
```
Corrupted Mana Source final boss, low poly game ready,
5000-6000 polygons, large floating asymmetric crystal mass,
dark blue and black corrupted crystals, organic and cosmic design,
mana tentacles extending outward, orbiting black crystal shards,
vague trapped animal silhouettes visible inside crystals,
glowing dark purple and blue core, imposing and terrifying,
completely unique non-animal design, top-down game view optimized,
no T-pose needed floating entity, mature intimidating boss,
anime fantasy game art style, final zone boss energy
```

**Attaques :**
- Cristaux orbitaux — 6 cristaux noirs tournent et tirent des lasers
- Vague de corruption — onde circulaire qui ralentit le joueur
- Invocation — fait réapparaître versions affaiblies des Boss 1 et 2
- Implosion — aspire le joueur puis explose en AOE massive
- Phase 2 — à 50% HP, couleur rouge/noir, vitesse doublée

## Progression des Maps
1. **Plaine** — Zone de départ, lumineuse et verdoyante
2. **Forêt mystique** — Arbres géants, champignons lumineux
3. **Désert magique**
4. **Montagne enneigée**
5. **Jungle**
6. **Volcan**

---

# 4. CONFIGURATION UNITY

## Paramètres importants
- **Caméra** : Orthographique, Rotation X: 65, Y: 0, Z: 0
- **Cinemachine** : PlayerFollowCam, Body: Transposer, Damping: 0
- **Physics Layer** : Layer "Player" et "Enemy" créés
- **Layer Collision Matrix** : Player ↔ Enemy cochée (Triggers fonctionnent)
- **Rigidbody Player** : Is Kinematic = TRUE, MovePosition utilisé
- **Rigidbody Ennemis** : Is Kinematic = FALSE (collision physique entre eux)

## Tags créés
- `Player`
- `Enemy`
- `EnemyProjectile`

## Structure des dossiers
```
Assets/
└── _Game/
    ├── Scripts/
    │   ├── Player/         ← PlayerController, HealthSystem
    │   ├── Enemies/        ← EnemyBase, EnemyTank, EnemyShooter, EnemyProjectile, BossBase
    │   ├── Weapons/        ← WeaponBase, ProjectileBasic, WeaponAOE, WeaponOrbital, OrbitalProjectile
    │   ├── Upgrades/       ← UpgradeData (ScriptableObject)
    │   ├── UI/             ← GameUI, UpgradeUI, MainMenuManager, DamageNumber, DamageNumberSpawner
    │   ├── Systems/        ← GameManager, XPSystem, LevelUpManager, WaveManager, ObjectPool, SaveSystem, MetaProgressionManager
    │   └── Data/           ← ScriptableObjects
    ├── Prefabs/
    │   ├── Player/
    │   ├── Enemies/        ← PFB_EnemyBasic, PFB_EnemyTank, PFB_EnemyShooter, PFB_EnemyProjectile, PFB_Boss
    │   ├── Weapons/        ← Projectile, OrbitalProjectile
    │   ├── VFX/            ← DamageNumber
    │   └── Pickups/
    ├── ScriptableObjects/
    │   └── Upgrades/       ← SO_UpgradeDamage, SO_UpgradeFireRate, SO_UpgradeMoveSpeed, SO_UpgradeHeal,
    │                          SO_UnlockAOE, SO_UnlockOrbital, SO_AddOrbital, SO_AOERadius, SO_DoubleShot
    ├── Scenes/
    │   ├── MainMenu
    │   └── Game
    ├── Sprites/
    │   └── UI/             ← Logo Aether, fond Plaine, boutons parchemin, icônes gold/gemmes
    ├── Materials/
    │   ├── MAT_EnemyTank
    │   ├── MAT_EnemyShooter
    │   ├── MAT_EnemyProjectile
    │   ├── MAT_Orbital
    │   └── MAT_PulseAOE
    ├── Fonts/
    └── Audio/
        ├── Music/
        └── SFX/
Resources/
    ├── OrbitalProjectile   ← Prefab chargé dynamiquement
    └── PulseVisual         ← Prefab chargé dynamiquement
```

## Build Settings
- Index 0 : MainMenu
- Index 1 : Game

---

# 5. SCRIPTS C# COMPLETS

## PlayerController.cs
```csharp
using UnityEngine;

public class PlayerController : MonoBehaviour
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Déplacement")]
    [SerializeField] private float _moveSpeed = 5f;

    [Header("Dash")]
    [SerializeField] private float _dashSpeed        = 15f;
    [SerializeField] private float _dashDuration     = 0.15f;
    [SerializeField] private float _dashCooldown     = 2f;
    [SerializeField] private float _absorptionWindow = 0.3f;

    private Rigidbody    _rb;
    private HealthSystem _healthSystem;
    private Vector3      _moveDirection;

    // Dash
    private bool    _isDashing         = false;
    private bool    _isInvincible      = false;
    private float   _dashTimer         = 0f;
    private float   _dashCooldownTimer = 0f;
    private bool    _canAbsorb         = false;
    private float   _absorptionTimer   = 0f;
    private Vector3 _dashDirection;  // Direction verrouillée au moment du dash

    private CrystalSystem _crystalSystem;

    public bool  IsDashing           => _isDashing;
    public bool  IsInvincible        => _isInvincible;
    public bool  CanAbsorb           => _canAbsorb;
    public float DashCooldownPercent => _dashCooldownTimer / _dashCooldown;

    private void Awake()
    {
        _rb           = GetComponent<Rigidbody>();
        _healthSystem = GetComponent<HealthSystem>();
        _crystalSystem = GetComponent<CrystalSystem>();
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        HandleMovementInput();
        HandleDash();
        HandleAbsorptionWindow();
        UpdateDashCooldown();
    }

    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical   = Input.GetAxisRaw("Vertical");
        _moveDirection   = new Vector3(horizontal, 0f, vertical).normalized;
    }

    private void HandleDash()
    {
        // Déclenche le dash
        if (Input.GetKeyDown(KeyCode.LeftShift) && !_isDashing && _dashCooldownTimer <= 0f)
        {
            // Si immobile on dash vers l'avant par défaut
            Vector3 direction = _moveDirection != Vector3.zero ? _moveDirection : transform.forward;
            StartDash(direction);
        }

        // Pendant le dash — on décrémente le timer
        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
                StopDash();
        }
    }

    private void StartDash(Vector3 direction)
    {
        // On verrouille la direction au moment du dash
        _dashDirection     = direction.normalized;
        _isDashing         = true;
        _isInvincible      = true;
        _dashTimer         = _dashDuration;
        _dashCooldownTimer = _dashCooldown;
        _canAbsorb         = true;
        _absorptionTimer   = _absorptionWindow;

        if (_healthSystem != null) _healthSystem.SetInvincibleExternal(true);

        GameUI.Instance.UpdateDashCooldown(0f);
    }

    private void StopDash()
    {
        _isDashing    = false;
        _isInvincible = false;
        if (_healthSystem != null) _healthSystem.SetInvincibleExternal(false);
    }

    private void HandleAbsorptionWindow()
    {
        if (!_canAbsorb) return;

        _absorptionTimer -= Time.deltaTime;
        if (_absorptionTimer <= 0f)
            _canAbsorb = false;
    }

    private void UpdateDashCooldown()
    {
        if (_dashCooldownTimer > 0f)
        {
            _dashCooldownTimer -= Time.deltaTime;
            GameUI.Instance.UpdateDashCooldown(1f - (_dashCooldownTimer / _dashCooldown));
        }
    }

    private void FixedUpdate()
    {
        if (_isDashing)
        {
            // On utilise _dashDirection verrouillée — pas _moveDirection
            _rb.MovePosition(_rb.position + _dashDirection * _dashSpeed * Time.fixedDeltaTime);
        }
        else
        {
            _rb.MovePosition(_rb.position + _moveDirection * _moveSpeed * Time.fixedDeltaTime);
        }
    }

    public void AddMoveSpeed(float value)
    {
        _moveSpeed += _moveSpeed * value;
    }

    public void ReduceDashCooldown(float value)
    {
        _dashCooldown = Mathf.Max(_dashCooldown - value, 0.5f);
    }
}
```

## HealthSystem.cs
```csharp
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _maxHealth = 100f;

    [Header("Invincibilité")]
    [SerializeField] private float _invincibilityDuration = 1f;

    private float _currentHealth;
    private bool  _isInvincible         = false;
    private bool  _isInvincibleExternal = false;
    private float _invincibilityTimer   = 0f;
    private float _damageCooldown       = 0.5f;
    private float _damageTimer          = 0f;

    public bool IsInvincible => _isInvincible || _isInvincibleExternal;

    private void Awake()
    {
        float bonusHP  = MetaProgressionManager.Instance.GetBonusMaxHP();
        _maxHealth    += _maxHealth * bonusHP;
        _currentHealth = _maxHealth;
    }

    private void Start()
    {
        if (GameUI.Instance != null)
            GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
    }

    private void Update()
    {
        if (_isInvincible)
        {
            _invincibilityTimer -= Time.deltaTime;
            if (_invincibilityTimer <= 0f)
                _isInvincible = false;
        }

        if (_damageTimer > 0f)
            _damageTimer -= Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsInvincible) return;
        if (_damageTimer > 0f) return;

        if (other.CompareTag("Enemy"))
        {
            TakeDamage(10f);
            _damageTimer = _damageCooldown;
        }

        if (other.CompareTag("EnemyProjectile"))
        {
            TakeDamage(15f);
            _damageTimer = _damageCooldown;
            
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsInvincible) return;
        if (_damageTimer > 0f) return;

        if (other.CompareTag("Enemy"))
        {
            TakeDamage(10f);
            _damageTimer = _damageCooldown;
        }
    }

    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        _currentHealth = Mathf.Max(_currentHealth, 0f);

        // Chiffre rouge au dessus du joueur
        if (DamageNumberSpawner.Instance != null)
            DamageNumberSpawner.Instance.Spawn(
                transform.position,
                damage,
                DamageNumberSpawner.ColorPlayer
            );

        GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
        if (_currentHealth <= 0)
            Die();
    }

    public void TakeDamageFromProjectile(float damage)
    {
        if (IsInvincible) return;
        TakeDamage(damage);
    }

    public void Heal(float percent)
    {
        _currentHealth += _maxHealth * percent;
        _currentHealth  = Mathf.Min(_currentHealth, _maxHealth);
        GameUI.Instance.UpdateHPBar(_currentHealth, _maxHealth);
    }

    public void SetInvincible()
    {
        _isInvincible       = true;
        _invincibilityTimer = _invincibilityDuration;
    }

    public void SetInvincibleExternal(bool value)
    {
        _isInvincibleExternal = value;
    }

    private void Die()
    {
        GameManager.Instance.TriggerGameOver();
        Destroy(gameObject);
    }
}
```

## EnemyBase.cs
```csharp
using UnityEngine;

public class EnemyBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _moveSpeed  = 2f;
    [SerializeField] private float _maxHealth  = 30f;

    [Header("Drops")]
    [SerializeField] private float _xpValue   = 10f;
    [SerializeField] private int   _goldValue = 2;
    // Gold droppé à la mort
    
    [Header("Pool")]
    [SerializeField] private string _poolTag = "Enemy";
    

    // Protected = accessible par les classes enfants
    protected float MoveSpeed => _moveSpeed;

    private float     _currentHealth;
    private Transform _playerTransform;

    private void Awake()
    {
        _currentHealth = _maxHealth;
    }

    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;
    }

    private void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance.IsGameOver) return;

        UpdateBehaviour(_playerTransform);
    }

    // Virtual = peut être overridé par les classes enfants
    protected virtual void UpdateBehaviour(Transform playerTransform)
    {
        Vector3 direction  = (playerTransform.position - transform.position).normalized;
        Vector3 separation = GetBaseSeparation();
        transform.position += (direction + separation).normalized * MoveSpeed * _speedMultiplier * Time.deltaTime;
    }

    private Vector3 GetBaseSeparation()
    {
        Vector3 force = Vector3.zero;
        float separationRadius = 1.5f;

        Collider[] neighbours = Physics.OverlapSphere(transform.position, separationRadius);
        foreach (Collider neighbour in neighbours)
        {
            if (neighbour.gameObject == gameObject) continue;
            if (!neighbour.CompareTag("Enemy")) continue;

            Vector3 pushDirection = transform.position - neighbour.transform.position;
            force += pushDirection.normalized;
        }

        return force.normalized * 0.5f; // Force réduite pour ne pas trop perturber
    }

    public void TakeDamage(float damage, Color color = default)
    {
        _currentHealth -= damage;

        // Spawn du chiffre flottant
        if (DamageNumberSpawner.Instance != null)
        {
            Color c = color == default ? DamageNumberSpawner.ColorProjectile : color;
            DamageNumberSpawner.Instance.Spawn(transform.position, damage, c);
        }

        if (_currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        XPSystem.Instance.AddXP(_xpValue);
        GameManager.Instance.AddKill();
        MetaProgressionManager.Instance.AddRunGold(_goldValue);
        ObjectPool.Instance.ReturnToPool(_poolTag, gameObject);
    }

    [System.NonSerialized] protected float _speedMultiplier = 1f;

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }
}
```

## EnemyTank.cs
```csharp
using UnityEngine;

public class EnemyTank : EnemyBase
{
    // Hérite de tout EnemyBase — stats différentes dans l'Inspector
    // Move Speed: 1.5, Max Health: 60, XP Value: 30, Gold Value: 6, Pool Tag: EnemyTank
}
```

## EnemyShooter.cs
```csharp
using UnityEngine;

public class EnemyShooter : EnemyBase
{
    [Header("Shooter")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _fireRate       = 1.5f; // Tirs par seconde
    [SerializeField] private float _preferredRange = 8f;   // Distance idéale du joueur
    [SerializeField] private float _fleeRange      = 4f;   // Distance minimale avant de fuir

    
    private float _fireTimer = 0f;

    protected override void UpdateBehaviour(Transform playerTransform)
    {
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Séparation entre Shooters
        Vector3 separationForce = GetSeparationForce();

        if (distanceToPlayer < _fleeRange)
        {
            Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
            transform.position += (fleeDirection + separationForce).normalized * MoveSpeed * _speedMultiplier *  Time.deltaTime;
        }
        else if (distanceToPlayer > _preferredRange)
        {
            Vector3 chaseDirection = (playerTransform.position - transform.position).normalized;
            transform.position += (chaseDirection + separationForce).normalized * MoveSpeed * _speedMultiplier *  Time.deltaTime;
        }
        else
        {
            transform.position += separationForce * MoveSpeed * _speedMultiplier * Time.deltaTime;
            HandleShooting(playerTransform);
        }
    }

    private Vector3 GetSeparationForce()
    {
        Vector3 force = Vector3.zero;
        float separationRadius = 3f; // Distance minimum entre Shooters

        Collider[] neighbours = Physics.OverlapSphere(transform.position, separationRadius);
        foreach (Collider neighbour in neighbours)
        {
            if (neighbour.gameObject == gameObject) continue;
            if (!neighbour.CompareTag("Enemy")) continue;
            if (neighbour.GetComponent<EnemyShooter>() == null) continue;

            Vector3 pushDirection = transform.position - neighbour.transform.position;
            force += pushDirection.normalized;
        }

        return force.normalized;
    }

    private void HandleShooting(Transform playerTransform)
    {
        _fireTimer += Time.deltaTime;

        if (_fireTimer >= 1f / _fireRate)
        {
            Shoot(playerTransform);
            _fireTimer = 0f;
        }
    }

    private void Shoot(Transform playerTransform)
    {
        if (_projectilePrefab == null) return;

        Vector3 direction = (playerTransform.position - transform.position).normalized;

        GameObject projectileGO = Instantiate(
            _projectilePrefab,
            transform.position,
            Quaternion.identity
        );

        EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
        if (projectile != null)
            projectile.Init(direction);
    }
}
```

## EnemyProjectile.cs
```csharp
using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float _speed    = 6f;
    [SerializeField] private float _damage   = 15f;
    [SerializeField] private float _maxRange = 20f;

    private Vector3 _direction;
    private Vector3 _startPosition;

    public void Init(Vector3 direction)
    {
        _direction     = direction.normalized;
        _startPosition = transform.position;
    }

    private void Update()
    {
        transform.position += _direction * _speed * Time.deltaTime;

        float distanceTravelled = Vector3.Distance(_startPosition, transform.position);
        if (distanceTravelled >= _maxRange)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();

            if (player != null && player.IsInvincible)
            {
                if (player.CanAbsorb)
                {
                    // Absorption — charge le cristal
                    CrystalSystem crystal = other.GetComponent<CrystalSystem>();
                    if (crystal != null) crystal.AbsorbProjectile();
                    Destroy(gameObject);
                }
                // Pas dans la fenêtre d'absorption → projectile passe à travers
                return;
            }

            // Joueur non invincible — dégâts normaux
            HealthSystem health = other.GetComponent<HealthSystem>();
            if (health != null)
                health.TakeDamageFromProjectile(_damage);

            Destroy(gameObject);
        }
    }
}
```

## BossBase.cs
```csharp
using UnityEngine;

public class BossBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _maxHealth    = 500f;
    [SerializeField] private float _moveSpeed    = 3f;
    [SerializeField] private float _xpValue      = 200f;
    [SerializeField] private int _goldValue = 50;

    [Header("Attaque")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float      _fireRate        = 1f;
    [SerializeField] private int        _projectileCount = 8; // Projectiles en éventail
    [SerializeField] private float      _chargeCooldown  = 5f; // Secondes entre chaque charge

    [Header("Identité")]
    [SerializeField] private string _bossName = "BOSS";

    private float     _currentHealth;
    private Transform _playerTransform;
    private float     _fireTimer   = 0f;
    private float     _chargeTimer = 0f;
    private bool      _isCharging  = false;
    private Vector3   _chargeDirection;

    private void Start()
    {
        GameUI.Instance.ShowBossHP(_bossName);
        
        _currentHealth = _maxHealth;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;
    }

    private void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance.IsGameOver) return;

        HandleMovement();
        HandleShooting();
        HandleCharge();
    }

    private void HandleMovement()
    {
        if (_isCharging) return; // Pendant la charge c'est HandleCharge qui bouge

        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        transform.position += direction * _moveSpeed * Time.deltaTime;
    }

    private void HandleShooting()
    {
        _fireTimer += Time.deltaTime;
        if (_fireTimer >= 1f / _fireRate)
        {
            ShootRadial();
            _fireTimer = 0f;
        }
    }

    private void ShootRadial()
    {
        if (_projectilePrefab == null) return;

        float angleStep = 360f / _projectileCount;

        for (int i = 0; i < _projectileCount; i++)
        {
            float   angle     = angleStep * i;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            GameObject projectileGO = Instantiate(
                _projectilePrefab,
                transform.position,
                Quaternion.identity
            );

            EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
            if (projectile != null)
                projectile.Init(direction);
        }
    }

    private void HandleCharge()
    {
        _chargeTimer += Time.deltaTime;

        if (_chargeTimer >= _chargeCooldown && !_isCharging)
        {
            _isCharging      = true;
            _chargeDirection = (_playerTransform.position - transform.position).normalized;
            _chargeTimer     = 0f;
            Invoke(nameof(StopCharge), 0.8f);
        }

        if (_isCharging)
        {
            // On déplace via transform, pas via physique
            transform.position += _chargeDirection * _moveSpeed * 4f * Time.deltaTime;
        }
    }

    private void StopCharge()
    {
        _isCharging = false;
    }

    public void TakeDamage(float damage, Color color = default)
    {
        _currentHealth -= damage;

        // Chiffre flottant au dessus du boss
        if (DamageNumberSpawner.Instance != null)
        {
            Color c = color == default ? DamageNumberSpawner.ColorCritical : color;
            DamageNumberSpawner.Instance.Spawn(transform.position, damage, c, true); // isCritical = true pour les dégâts boss
        }

        GameUI.Instance.UpdateBossHP(_currentHealth, _maxHealth);

        if (_currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        XPSystem.Instance.AddXP(_xpValue);
        GameManager.Instance.AddKill();
        MetaProgressionManager.Instance.AddRunGold(_goldValue);
        GameUI.Instance.HideBossHP();
        WaveManager.Instance.OnBossDied();
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HealthSystem health = other.GetComponent<HealthSystem>();
            if (health != null)
                health.TakeDamage(30f);
        }
    }
}
```

## WeaponBase.cs
```csharp
using System;
using UnityEngine;

public class WeaponBase : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject _projectilePrefab;

    [Header("Stats")]
    [SerializeField] private float _fireRate       = 1f;
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private float _damage = 10f;

    [Header("Double tir")]
    [SerializeField] private bool _doubleShot = false;
    [SerializeField] private float _doubleShotDelay = 0.1f;

    private void Shoot(Transform target)
    {
        Vector3 direction = (target.position - transform.position).normalized;
        FireProjectile(direction);

        if (_doubleShot)
            StartCoroutine(FireDelayed(direction));
    }

    private void FireProjectile(Vector3 direction)
    {
        GameObject projectileGO = ObjectPool.Instance.Get(
            "Projectile",
            transform.position,
            Quaternion.identity
        );

        if (projectileGO == null) return;

        ProjectileBasic projectile = projectileGO.GetComponent<ProjectileBasic>();
        if (projectile != null)
            projectile.Init(direction, _damage);
    }

    private System.Collections.IEnumerator FireDelayed(Vector3 direction)
    {
        yield return new WaitForSeconds(_doubleShotDelay);
        FireProjectile(direction);
    }

    public void UnlockDoubleShot()
    {
        _doubleShot = true;
    }

    private float _cooldownTimer = 0f;
    public bool IsDoubleShotUnlocked() => _doubleShot;
    private void Update()
    {
        _cooldownTimer += Time.deltaTime;

        if (_cooldownTimer >= 1f / _fireRate)
        {
            Transform nearestEnemy = FindNearestEnemy();

            if (nearestEnemy != null)
            {
                Shoot(nearestEnemy);
                _cooldownTimer = 0f;
            }
        }
    }

    private Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        Transform nearest = null;
        float     minDist = _detectionRange;

        foreach (GameObject enemy in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy.transform;
            }
        }

        return nearest;
    }
    
   public void AddDamage(float value)
   {
        _damage += _damage * value;
   }

   public void AddFireRate(float value)
   {
        _fireRate += _fireRate * value;
   }

   private void Start()
   {
        // Applique le bonus méta de dégâts
        float bonusDamage = MetaProgressionManager.Instance.GetBonusDamage();
        _damage += _damage * bonusDamage;
   }
}
```

## ProjectileBasic.cs
```csharp
using UnityEngine;

public class ProjectileBasic : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _speed    = 10f;
    [SerializeField] private float _maxRange = 15f;

    private float   _damage;
    private Vector3 _startPosition;
    private Vector3 _direction;

    public void Init(Vector3 direction, float damage)
    {
        _direction     = direction.normalized;
        _startPosition = transform.position;
        _damage        = damage;
    }

    private void Update()
    {
        transform.position += _direction * _speed * Time.deltaTime;

        float distanceTravelled = Vector3.Distance(_startPosition, transform.position);
        if (distanceTravelled >= _maxRange)
            ObjectPool.Instance.ReturnToPool("Projectile", gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        
    
        if (other.CompareTag("Enemy"))
        {
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy != null)
                enemy.TakeDamage(_damage, DamageNumberSpawner.ColorProjectile);
            else
            {
                // Essaie avec BossBase
                BossBase boss = other.GetComponent<BossBase>();
                if (boss != null)
                    boss.TakeDamage(_damage);
            }

            ObjectPool.Instance.ReturnToPool("Projectile", gameObject);
        }
    }
}
```

## WeaponAOE.cs
```csharp
using UnityEngine;

public class WeaponAOE : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage   = 15f;
    [SerializeField] private float _radius   = 3f;
    [SerializeField] private float _fireRate = 0.5f; // Réduit à 0.5 pulse/sec

    [Header("Visuel")]
    [SerializeField] private GameObject _pulseVisual;

    private float _cooldownTimer  = 0f;
    private float _animationTimer = -1f; // -1 = pas d'animation en cours
    private float _animDuration   = 0.3f;

    [Header("Limites")]
    [SerializeField] private float _maxRadius = 8f; // Rayon maximum
    public bool IsMaxRadius() => _radius >= _maxRadius;

    public void AddRadius(float value)
    {
        _radius = Mathf.Min(_radius + _radius * value, _maxRadius);
    }

    private void Update()
    {
        if (GameManager.Instance.IsGameOver) return;

        _cooldownTimer += Time.deltaTime;
        if (_cooldownTimer >= 1f / _fireRate)
        {
            Pulse();
            _cooldownTimer = 0f;
        }

        if (_animationTimer >= 0f)
        {
            _animationTimer += Time.deltaTime;
            float scale = Mathf.Lerp(0f, _radius * 2f, _animationTimer / _animDuration);
            _pulseVisual.transform.localScale = new Vector3(scale, 0.1f, scale);

            if (_animationTimer >= _animDuration)
            {
                _pulseVisual.SetActive(false);
                _animationTimer = -1f;
            }
        }
    }

    // Déplace le OverlapSphere dans FixedUpdate — plus adapté à la physique
    private void FixedUpdate()
    {
        // Rien ici, le pulse est géré dans Update
    }

    private void Pulse()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _radius);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase enemy = hit.GetComponent<EnemyBase>();
                if (enemy != null)
                    enemy.TakeDamage(_damage, DamageNumberSpawner.ColorAOE);
            }
        }

        // Lance l'animation
        if (_pulseVisual != null)
        {
            _pulseVisual.SetActive(true);
            _pulseVisual.transform.localScale = Vector3.zero;
            _animationTimer = 0f;
        }
    }

    public void AddDamage(float value) => _damage  += _damage * value;
    

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }

    public void Init(GameObject pulseVisualPrefab)
    {
        GameObject visual = Instantiate(pulseVisualPrefab, transform.position, Quaternion.identity);
        visual.transform.SetParent(transform);
        _pulseVisual = visual;
        _pulseVisual.SetActive(false);
    }

    public void AddFireRate(float value) => _fireRate += _fireRate * value;
}
```

## WeaponOrbital.cs
```csharp
using UnityEngine;
using System.Collections.Generic;

public class WeaponOrbital : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage       = 15f;
    [SerializeField] private float _orbitRadius  = 3f;
    [SerializeField] private float _orbitSpeed   = 180f;
    [SerializeField] private int   _orbitalCount = 2;

    [Header("Références")]
    [SerializeField] private GameObject _orbitalPrefab;

    [Header("Limites")]
    [SerializeField] private int _maxOrbitalCount = 5; // Maximum 5 orbes
    public bool IsMaxOrbital() => _orbitalCount >= _maxOrbitalCount;

    public void AddOrbital()
    {
        if (_orbitalCount >= _maxOrbitalCount)
        {
            Debug.Log("Nombre maximum d'orbitaux atteint !");
            return;
        }
        _orbitalCount++;
        SpawnOrbitals();
    }

    private List<GameObject> _orbitals = new List<GameObject>();
    private float            _currentAngle = 0f;

    private void Start()
    {
        if (_orbitalPrefab != null)
            SpawnOrbitals();
        else
            Debug.LogWarning("WeaponOrbital : Orbital Prefab non assigné !");
    }

    public void Init(GameObject orbitalPrefab)
    {
        _orbitalPrefab = orbitalPrefab;
        SpawnOrbitals();
    }

    private void SpawnOrbitals()
    {
        foreach (GameObject orbital in _orbitals)
            Destroy(orbital);
        _orbitals.Clear();

        for (int i = 0; i < _orbitalCount; i++)
        {
            GameObject orbital = Instantiate(_orbitalPrefab, transform.position, Quaternion.identity);
            orbital.transform.SetParent(transform);
            _orbitals.Add(orbital);
        }
    }

    private void Update()
    {
        if (GameManager.Instance.IsGameOver) return;
        if (_orbitals.Count == 0) return;

        _currentAngle += _orbitSpeed * Time.deltaTime;
        float angleStep = 360f / _orbitalCount;

        for (int i = 0; i < _orbitals.Count; i++)
        {
            float angle = (_currentAngle + angleStep * i) * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * _orbitRadius;
            float z = Mathf.Sin(angle) * _orbitRadius;
            _orbitals[i].transform.localPosition = new Vector3(x, 0f, z);
        }
    }


    public void AddDamage(float value) => _damage      += _damage * value;
    
}
```

## OrbitalProjectile.cs
```csharp
using UnityEngine;

public class OrbitalProjectile : MonoBehaviour
{
    [SerializeField] private float _damage = 15f;

    // Dictionnaire pour éviter de toucher le même ennemi en boucle
    private System.Collections.Generic.Dictionary<EnemyBase, float> _hitCooldowns
        = new System.Collections.Generic.Dictionary<EnemyBase, float>();

    private float _hitCooldown = 0.5f; // Délai entre deux hits sur le même ennemi

    private void Update()
    {
        // On réduit les cooldowns de hit
        var keys = new System.Collections.Generic.List<EnemyBase>(_hitCooldowns.Keys);
        foreach (EnemyBase enemy in keys)
        {
            _hitCooldowns[enemy] -= Time.deltaTime;
            if (_hitCooldowns[enemy] <= 0f)
                _hitCooldowns.Remove(enemy);
        }
    }

    public void SetDamage(float damage)
    {
        _damage = damage;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy == null) return;

            // On vérifie que l'ennemi n'est pas en cooldown
            if (!_hitCooldowns.ContainsKey(enemy))
            {
                enemy.TakeDamage(_damage, DamageNumberSpawner.ColorOrbital);
                _hitCooldowns[enemy] = _hitCooldown;
            }
        }
    }
}
```

## CrystalSystem.cs
```csharp
using UnityEngine;
using System.Collections;

public class CrystalSystem : MonoBehaviour
{
    [Header("Jauge")]
    [SerializeField] private int _maxCharges = 15;

    [Header("Ulti")]
    [SerializeField] private float _ultDamage = 50f;
    [SerializeField] private float _ultRange = 10f;
    [SerializeField] private float _slowFactor = 0.3f;
    [SerializeField] private float _slowDuration = 3f;

    private int _currentCharges = 0;
    private bool _isReady = false;

    public int CurrentCharges => _currentCharges;
    public int MaxCharges => _maxCharges;
    public bool IsReady => _isReady;

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        if (_isReady && Input.GetKeyDown(KeyCode.E))
            TriggerUlt();
    }

    public void AbsorbProjectile()
    {
        if (_currentCharges >= _maxCharges) return;

        _currentCharges++;
        GameUI.Instance.UpdateCrystalCharge(_currentCharges, _maxCharges);

        if (_currentCharges >= _maxCharges)
        {
            _isReady = true;
            GameUI.Instance.SetCrystalReady(true);
        }
    }

    private void TriggerUlt()
    {
        _isReady = false;
        _currentCharges = 0;

        GameUI.Instance.SetCrystalReady(false);
        GameUI.Instance.UpdateCrystalCharge(0, _maxCharges);

        DamageAllEnemies();
        StartCoroutine(SlowAllEnemies());

        Debug.Log("ULTI DÉCLENCHÉ !");
    }

    private void DamageAllEnemies()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange);
        int count = 0;

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase eb = hit.GetComponent<EnemyBase>();
                if (eb != null) eb.TakeDamage(_ultDamage, DamageNumberSpawner.ColorCritical);

                BossBase boss = hit.GetComponent<BossBase>();
                if (boss != null) boss.TakeDamage(_ultDamage);

                count++;
            }
        }
        Debug.Log($"Ulti — {count} ennemis touchés dans un rayon de {_ultRange}");
    }

    private IEnumerator SlowAllEnemies()
    {
        SetEnemySpeedMultiplier(_slowFactor);
        GameUI.Instance.ShowUltEffect(true);

        yield return new WaitForSeconds(_slowDuration);

        SetEnemySpeedMultiplier(1f);
        GameUI.Instance.ShowUltEffect(false);
    }

    private void SetEnemySpeedMultiplier(float multiplier)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase eb = hit.GetComponent<EnemyBase>();
                if (eb != null) eb.SetSpeedMultiplier(multiplier);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _ultRange);
    }
}
```

## XPSystem.cs
```csharp
using UnityEngine;

public class XPSystem : MonoBehaviour
{
    // Singleton
    public static XPSystem Instance { get; private set; }

    [Header("Stats")]
    [SerializeField] private float _baseXP = 50f; // XP nécessaire au niveau 1

    public int   CurrentLevel { get; private set; } = 1;
    public float CurrentXP    { get; private set; } = 0f;
    public float XPToNextLevel => Mathf.Floor(_baseXP * Mathf.Pow(CurrentLevel, 1.5f));
    // Formule : plus on monte en niveau, plus il faut d'XP

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void AddXP(float amount)
    {
        if (GameManager.Instance.IsGameOver) return;

        // Applique le bonus méta d'XP
        float bonusXP = MetaProgressionManager.Instance.GetBonusXP();
        amount += amount * bonusXP;

        CurrentXP += amount;
        GameUI.Instance.UpdateXPBar(CurrentXP, XPToNextLevel, CurrentLevel);

        while (CurrentXP >= XPToNextLevel)
        {
            CurrentXP -= XPToNextLevel;
            LevelUp();
            GameUI.Instance.UpdateXPBar(CurrentXP, XPToNextLevel, CurrentLevel);
        }
    }

    private void LevelUp()
    {
        CurrentLevel++;
        Debug.Log($"LEVEL UP ! Niveau {CurrentLevel}");
        LevelUpManager.Instance.ShowLevelUp();
    }
}
```

## LevelUpManager.cs
```csharp
using UnityEngine;
using System.Collections.Generic;

public class LevelUpManager : MonoBehaviour
{
    public static LevelUpManager Instance { get; private set; }

    [Header("Références")]
    [SerializeField] private UpgradeData[] _allUpgrades;
    [SerializeField] private GameObject    _levelUpPanel;
    [SerializeField] private UpgradeUI     _upgradeUI;

    private List<UpgradeData> _currentChoices  = new List<UpgradeData>();
    private int               _pendingLevelUps  = 0;
    private bool              _waitingForChoice = false;
    private float             _delayTimer       = 0f;
    private bool              _showingDelay     = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        // Délai entre deux level ups géré dans Update
        if (_showingDelay)
        {
            _delayTimer -= Time.unscaledDeltaTime; // Ignore timeScale
            if (_delayTimer <= 0f)
            {
                _showingDelay = false;
                DisplayLevelUp();
            }
            return;
        }

        if (_waitingForChoice)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectUpgrade(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectUpgrade(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectUpgrade(2);
        }
    }

    public void ShowLevelUp()
    {
        _pendingLevelUps++;

        // Si déjà en train d'afficher un level up on attend
        if (_waitingForChoice || _showingDelay) return;

        DisplayLevelUp();
    }

    private void DisplayLevelUp()
    {
        if (_pendingLevelUps <= 0) return;

        _pendingLevelUps--;
        _waitingForChoice = true;
        Time.timeScale    = 0f;

        _currentChoices = GetRandomUpgrades(3);

        if (_levelUpPanel != null)
            _levelUpPanel.SetActive(true);

        _upgradeUI.DisplayUpgrades(_currentChoices);
    }

    public void SelectUpgrade(int index)
    {
        if (!_waitingForChoice) return;
        if (index < 0 || index >= _currentChoices.Count) return;

        _waitingForChoice = false;

        UpgradeData chosen = _currentChoices[index];
        chosen.Apply();

        if (_levelUpPanel != null)
            _levelUpPanel.SetActive(false);

        HealthSystem health = FindObjectOfType<HealthSystem>();
        if (health != null)
            health.SetInvincible();

        if (_pendingLevelUps > 0)
        {
            // Délai de 0.4 secondes avant le prochain level up
            _showingDelay = true;
            _delayTimer   = 0.4f;
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    private List<UpgradeData> GetRandomUpgrades(int count)
    {
        List<UpgradeData> pool = new List<UpgradeData>();
        foreach (UpgradeData upgrade in _allUpgrades)
        {
            if (upgrade.IsAvailable())
                pool.Add(upgrade);
        }

        List<UpgradeData> result = new List<UpgradeData>();
        count = Mathf.Min(count, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, pool.Count);
            result.Add(pool[randomIndex]);
            pool.RemoveAt(randomIndex);
        }

        return result;
    }
}
```

## UpgradeData.cs (ScriptableObject)
```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "SO_Upgrade", menuName = "BulletHeaven/Upgrade")]
public class UpgradeData : ScriptableObject
{
    [Header("Infos")]
    public string upgradeName;
    public string description;

    [Header("Effet")]
    public UpgradeType upgradeType;
    public float       value;

    public bool IsAvailable()
    {
        switch (upgradeType)
        {
            case UpgradeType.DoubleShot:
                // Disponible une seule fois
                WeaponBase wb = FindObjectOfType<WeaponBase>();
                return wb != null && !wb.IsDoubleShotUnlocked();
            
            case UpgradeType.AddOrbital:
                return FindObjectOfType<WeaponOrbital>() != null;

            case UpgradeType.AOERadius:
                return FindObjectOfType<WeaponAOE>() != null;

            case UpgradeType.UnlockAOE:
                return FindObjectOfType<WeaponAOE>() == null;

            case UpgradeType.UnlockOrbital:
                return FindObjectOfType<WeaponOrbital>() == null;

            default:
                return true;
        }
    }

    public void Apply()
    {
        PlayerController player  = FindObjectOfType<PlayerController>();
        WeaponBase        weapon  = FindObjectOfType<WeaponBase>();
        HealthSystem      health  = FindObjectOfType<HealthSystem>();
        WeaponAOE         aoe     = FindObjectOfType<WeaponAOE>();
        WeaponOrbital     orbital = FindObjectOfType<WeaponOrbital>();

        switch (upgradeType)
        {
            case UpgradeType.DoubleShot:
                if (weapon != null) weapon.UnlockDoubleShot();
                break;

            case UpgradeType.MoveSpeed:
                if (player != null) player.AddMoveSpeed(value);
                break;

            case UpgradeType.Damage:
                if (weapon != null)  weapon.AddDamage(value);
                if (aoe != null)     aoe.AddDamage(value);
                if (orbital != null) orbital.AddDamage(value);
                break;

            case UpgradeType.FireRate:
            if (weapon != null) weapon.AddFireRate(value);
            if (aoe != null)    aoe.AddFireRate(value);
            break;

            case UpgradeType.Heal:
                if (health != null) health.Heal(value);
                break;

            case UpgradeType.UnlockAOE:
                if (aoe == null)
                {
                    GameObject player_go = GameObject.FindWithTag("Player");
                    if (player_go != null)
                    {
                        WeaponAOE newAOE = player_go.AddComponent<WeaponAOE>();
                        GameObject prefab = Resources.Load<GameObject>("PulseVisual");
                        if (prefab != null)
                            newAOE.Init(prefab);
                        else
                            Debug.LogWarning("Prefab PulseVisual introuvable dans Resources !");
                    }
                }
                break;

            case UpgradeType.UnlockOrbital:
                if (orbital == null)
                {
                    GameObject player_go = GameObject.FindWithTag("Player");
                    if (player_go != null)
                    {
                        WeaponOrbital newOrbital = player_go.AddComponent<WeaponOrbital>();
                        // On récupère le prefab depuis les ressources
                        GameObject prefab = Resources.Load<GameObject>("OrbitalProjectile");
                        if (prefab != null)
                            newOrbital.Init(prefab);
                        else
                            Debug.LogWarning("Prefab OrbitalProjectile introuvable dans Resources !");
                    }
                }
                break;

            case UpgradeType.AddOrbital:
                if (orbital != null) orbital.AddOrbital();
                break;

            case UpgradeType.AOERadius:
                if (aoe != null) aoe.AddRadius(value);
                break;
        }
    }
}

public enum UpgradeType
{
    MoveSpeed,
    Damage,
    FireRate,
    Heal,
    UnlockAOE,
    UnlockOrbital,
    AddOrbital,
    AOERadius,
    DoubleShot  // ← ajoute ça
}

public enum UpgradeType
{
    MoveSpeed, Damage, FireRate, Heal,
    UnlockAOE, UnlockOrbital, AddOrbital, AOERadius, DoubleShot
}
```

## GameManager.cs
```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("État du jeu")]
    [SerializeField] private bool _isGameOver = false;
    public bool IsGameOver => _isGameOver;

    private float _runTimer  = 0f;
    private int   _killCount = 0;

    public int   KillCount => _killCount;
    public float RunTimer  => _runTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (_isGameOver) return;
        _runTimer += Time.deltaTime;
    }

    public void AddKill()
    {
        _killCount++;
    }

    public void TriggerGameOver()
    {
        if (_isGameOver) return;

        _isGameOver = true;

        // On affiche le Game Over après 1.5 secondes
        Invoke(nameof(ShowGameOver), 1.5f);
    }

    private void ShowGameOver()
    {
        // On sauvegarde les résultats
        MetaProgressionManager.Instance.SaveRunResults(
            _runTimer,
            _killCount,
            WaveManager.Instance.CurrentWave
        );

        GameUI.Instance.ShowGameOver(
            _runTimer,
            _killCount,
            WaveManager.Instance.CurrentWave,
            MetaProgressionManager.Instance.RunGold
        );
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(1);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0); // 0 = MainMenu dans Build Settings
    }

    public void TriggerVictory()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        Debug.Log("VICTOIRE !");
        // On affichera l'écran de victoire plus tard
        Invoke(nameof(ShowGameOver), 1.5f);
    }
}
```

## WaveManager.cs
```csharp
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Paramètres")]
    public float BossSpawnInterval = 300f; // 5 minutes en secondes

    [Header("Boss")]
    [SerializeField] private GameObject _bossPrefab1;
    [SerializeField] private GameObject _bossPrefab2;
    [SerializeField] private GameObject _bossPrefab3;

    [Header("Limite ennemis")]
    [SerializeField] private int _maxEnemiesOnScreen = 15;

    private float _runTimer = 0f;
    private int _bossCount = 0;
    private bool _bossAlive = false;

    public int CurrentWave => _bossCount + 1;
    public float RunTimer => _runTimer;

    private EnemySpawner _enemySpawner;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _enemySpawner = FindObjectOfType<EnemySpawner>();
        ApplyDifficulty();
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (_bossAlive) return;

        _runTimer += Time.deltaTime;

        // Mise à jour de la difficulté
        ApplyDifficulty();

        // Spawn des boss
        if (_bossCount == 0 && _runTimer >= 300f) SpawnBoss(1);
        if (_bossCount == 1 && _runTimer >= 600f) SpawnBoss(2);
        if (_bossCount == 2 && _runTimer >= 900f) SpawnBoss(3);

        GameUI.Instance.UpdateTimer(_runTimer);
    }

    private void ApplyDifficulty()
    {
        if (_enemySpawner == null) return;

        float minutes = _runTimer / 60f;

        if (minutes < 3f)
        {
            _enemySpawner.SetSpawnInterval(3f);
            _maxEnemiesOnScreen = 15;
        }
        else if (minutes < 5f)
        {
            _enemySpawner.SetSpawnInterval(2f);
            _maxEnemiesOnScreen = 25;
        }
        else if (minutes < 8f)
        {
            _enemySpawner.SetSpawnInterval(1.5f);
            _maxEnemiesOnScreen = 30;
        }
        else if (minutes < 10f)
        {
            _enemySpawner.SetSpawnInterval(1f);
            _maxEnemiesOnScreen = 40;
        }
        else if (minutes < 13f)
        {
            _enemySpawner.SetSpawnInterval(0.8f);
            _maxEnemiesOnScreen = 50;
        }
        else
        {
            _enemySpawner.SetSpawnInterval(0.6f);
            _maxEnemiesOnScreen = 60;
        }

        _enemySpawner.SetMaxEnemies(_maxEnemiesOnScreen);
    }

    private void SpawnBoss(int bossNumber)
    {
        _bossCount++;
        _bossAlive = true;

        ClearAllEnemies();
        _enemySpawner.gameObject.SetActive(false);

        GameObject player = GameObject.FindWithTag("Player");
        Vector3 spawnPos = player.transform.position + new Vector3(10f, 0f, 0f);

        GameObject bossPrefab = bossNumber == 1 ? _bossPrefab1 :
                                bossNumber == 2 ? _bossPrefab2 : _bossPrefab3;

        if (bossPrefab != null)
            Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        else
            Debug.LogWarning($"Boss {bossNumber} prefab non assigné !");

        GameUI.Instance.UpdateWave(-1);
        Debug.Log($"Boss {bossNumber} spawné !");
    }

    private void ClearAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            EnemyBase eb = enemy.GetComponent<EnemyBase>();
            if (eb != null)
                ObjectPool.Instance.ReturnToPool(GetPoolTag(enemy), enemy);
            else
                Destroy(enemy);
        }
    }

    private string GetPoolTag(GameObject enemy)
    {
        if (enemy.GetComponent<EnemyShooter>() != null) return "EnemyShooter";
        if (enemy.GetComponent<EnemyTank>() != null) return "EnemyTank";
        return "Enemy";
    }

    public void OnBossDied()
    {
        _bossAlive = false;
        _enemySpawner.gameObject.SetActive(true);
        GameUI.Instance.UpdateWave(CurrentWave);

        // Victoire si c'était le boss 3
        if (_bossCount >= 3)
            GameManager.Instance.TriggerVictory();

        Debug.Log($"Boss vaincu ! Run continue — Vague {CurrentWave}");
    }
}
```

## EnemySpawner.cs
```csharp
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject _enemyPrefab;    // Le prefab ennemi
    [SerializeField] private Transform  _playerTransform; // Le joueur

    [Header("Paramètres de spawn")]
    [SerializeField] private float _spawnInterval    = 2f;  // Secondes entre chaque spawn
    [SerializeField] private float _spawnRadius      = 10f; // Distance du joueur
    [SerializeField] private int   _enemiesPerWave   = 1;   // Ennemis par vague au départ

    [Header("Difficulté croissante")]
    [SerializeField] private float _difficultyInterval = 10f; // Toutes les X secondes
    [SerializeField] private int   _enemiesIncrement   = 1;   // +X ennemis par palier

    [Header("Types d'ennemis")]
    [SerializeField] private string[] _enemyTags = { "Enemy", "EnemyTank" };
 

    private float _spawnTimer      = 0f;
    private float _difficultyTimer = 0f;

   private void Update()
   {
       // On arrête tout si le jeu est terminé
       if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        // Timer de spawn
        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= _spawnInterval)
        {
            SpawnWave();
            _spawnTimer = 0f;
        }

        // Timer de difficulté
        _difficultyTimer += Time.deltaTime;
        if (_difficultyTimer >= _difficultyInterval)
        {
            _enemiesPerWave += _enemiesIncrement;
            _difficultyTimer = 0f;
            Debug.Log($"Difficulté augmentée ! Ennemis par vague : {_enemiesPerWave}");
        }
    }

    private void SpawnWave()
    {
        int currentEnemies = GameObject.FindGameObjectsWithTag("Enemy").Length;
        if (currentEnemies >= _maxEnemies) return;

        for (int i = 0; i < _enemiesPerWave; i++)
        {
            currentEnemies = GameObject.FindGameObjectsWithTag("Enemy").Length;
            if (currentEnemies >= _maxEnemies) break;
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
   {
        Vector2 randomCircle = Random.insideUnitCircle.normalized;
        Vector3 spawnPos = _playerTransform.position + new Vector3(
            randomCircle.x * _spawnRadius,
            0f,
            randomCircle.y * _spawnRadius
        );

        float roll = Random.value;

        string tag;
        if (roll < 0.2f)
            tag = "EnemyTank";
        else if (roll < 0.4f)
            tag = "EnemyShooter";
        else
            tag = "Enemy";

        ObjectPool.Instance.Get(tag, spawnPos, Quaternion.identity);
   }

   public float GetSpawnInterval() => _spawnInterval;
   public void  SetSpawnInterval(float value) => _spawnInterval = value;

    private int _maxEnemies = 15;

    public void SetMaxEnemies(int max)
    {
        _maxEnemies = max;
    }



}
```

## ObjectPool.cs
```csharp
using UnityEngine;
using System.Collections.Generic;

public class ObjectPool : MonoBehaviour
{
    public static ObjectPool Instance { get; private set; }

    [System.Serializable]
    public class Pool
    {
        public string     tag;        // Identifiant du pool ex: "Enemy", "Projectile"
        public GameObject prefab;     // Le prefab à pooler
        public int        size;       // Nombre d'objets pré-créés
    }

    [SerializeField] private List<Pool> _pools;

    // Dictionnaire : tag → file d'objets disponibles
    private Dictionary<string, Queue<GameObject>> _poolDictionary;

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

        foreach (Pool pool in _pools)
        {
            Queue<GameObject> objectQueue = new Queue<GameObject>();

            for (int i = 0; i < pool.size; i++)
            {
                // On crée l'objet, on le désactive et on le met en file d'attente
                GameObject obj = Instantiate(pool.prefab);
                obj.SetActive(false);
                objectQueue.Enqueue(obj);
            }

            _poolDictionary.Add(pool.tag, objectQueue);
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

        // Si le pool est vide on prend le premier objet quand même
        // (il sera actif mais c'est mieux que de lagger)
        GameObject obj = null;
        if (queue.Count > 0)
        {
            obj = queue.Dequeue();
        }
        else
        {
            // Pool épuisé → on en crée un nouveau
            obj = Instantiate(_pools.Find(p => p.tag == tag).prefab);
            Debug.Log($"Pool '{tag}' épuisé, création d'un objet supplémentaire");
        }

        obj.SetActive(true);
        obj.transform.position = position;
        obj.transform.rotation = rotation;

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

        obj.SetActive(false);
        _poolDictionary[tag].Enqueue(obj);
    }
}
```

## SaveSystem.cs
```csharp
using UnityEngine;
using System.IO;

public static class SaveSystem
{
    private static string _savePath => Application.persistentDataPath + "/save.json";

    public static void Save(SaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(_savePath, json);
        Debug.Log($"Sauvegarde : {_savePath}");
    }

    public static SaveData Load()
    {
        if (!File.Exists(_savePath))
            return new SaveData(); // Première fois → données vierges

        string json = File.ReadAllText(_savePath);
        return JsonUtility.FromJson<SaveData>(json);
    }
}

[System.Serializable]
public class SaveData
{
    public int   totalGold       = 0;  // Gold disponible pour le shop
    public int   totalRuns       = 0;  // Nombre de parties jouées
    public float bestTime        = 0f; // Meilleur temps de survie
    public int   bestWave        = 0;  // Meilleure vague atteinte
    public int   bestKills       = 0;  // Meilleur nombre de kills
    public int   totalGems       = 0;  // Gemmes disponible pour le shop
    // Upgrades méta achetées
    public int hpUpgradeLevel      = 0;
    public int damageUpgradeLevel  = 0;
    public int xpUpgradeLevel      = 0;
}
```

## DamageNumber.cs
```csharp
using UnityEngine;
using TMPro;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _lifetime = 1f;

    private TextMeshPro _text;
    private float _timer = 0f;
    private Color _color;

    private void Awake()
    {
        _text = GetComponent<TextMeshPro>();
    }

    public void Init(float damage, Color color, bool isCritical = false)
    {
        _color = color;
        _text.color = color;
        _text.text = isCritical ? $"{Mathf.CeilToInt(damage)}!" : $"{Mathf.CeilToInt(damage)}";
        _text.fontSize = isCritical ? 10f : 8f; // Plus grand
        _text.fontStyle = FontStyles.Bold;

        // Légère position aléatoire pour éviter que les chiffres se superposent
        transform.position += new Vector3(
            Random.Range(-0.3f, 0.3f),
            0f,
            Random.Range(-0.3f, 0.3f)
        );
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        // Monte vers le haut
        transform.position += Vector3.up * _moveSpeed * Time.deltaTime;

        // Fade out
        float alpha = Mathf.Lerp(1f, 0f, _timer / _lifetime);
        _text.color = new Color(_color.r, _color.g, _color.b, alpha);

        if (_timer >= _lifetime)
            Destroy(gameObject);
    }
}
```

## DamageNumberSpawner.cs
```csharp
using UnityEngine;

public class DamageNumberSpawner : MonoBehaviour
{
    public static DamageNumberSpawner Instance { get; private set; }

    [SerializeField] private GameObject _damageNumberPrefab;

    // Couleurs selon l'arme
    public static readonly Color ColorProjectile = new Color(0f,   0.8f, 1f);   // Bleu cyan
    public static readonly Color ColorAOE        = new Color(0.4f, 0.9f, 0.2f); // Vert
    public static readonly Color ColorOrbital    = new Color(0.9f, 0.9f, 0.9f); // Blanc
    public static readonly Color ColorCritical   = new Color(1f,   0.6f, 0f);   // Orange doré
    public static readonly Color ColorPlayer     = new Color(1f,   0.2f, 0.2f); // Rouge

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Spawn(Vector3 position, float damage, Color color, bool isCritical = false)
    {
        if (_damageNumberPrefab == null) return;

        GameObject go = Instantiate(
            _damageNumberPrefab,
            position + Vector3.up * 1.5f,
            Quaternion.identity
        );

        DamageNumber dn = go.GetComponent<DamageNumber>();
        if (dn != null) dn.Init(damage, color, isCritical);
    }
}
```

## GameUI.cs
```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("XP")]
    [SerializeField] private Slider          _xpBar;
    [SerializeField] private TextMeshProUGUI _levelText;

    [Header("HP")]
    [SerializeField] private Slider          _hpBar;
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private Image           _hpFillImage;

    [Header("Vague et Timer")]
    [SerializeField] private TextMeshProUGUI _waveText;
    [SerializeField] private TextMeshProUGUI _timerText;

    [Header("Boss")]
    [SerializeField] private GameObject      _bossHPBar;
    [SerializeField] private Slider          _bossHPSlider;
    [SerializeField] private TextMeshProUGUI _bossNameText;
    [SerializeField] private GameObject      _bossIcon; // ← ajoute ça

    [Header("Game Over")]
    [SerializeField] private GameObject      _gameOverPanel;
    [SerializeField] private TextMeshProUGUI _statsText;


    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI _goldText;

    [Header("Dash")]
    [SerializeField] private Slider _dashCooldownBar;

    [Header("Cristal")]
    [SerializeField] private UnityEngine.UI.Image[] _crystalIcons; // 15 icônes
    [SerializeField] private GameObject             _ultReadyEffect;

    public void UpdateCrystalCharge(int current, int max)
    {
        for (int i = 0; i < _crystalIcons.Length; i++)
        {
            if (i < current)
                _crystalIcons[i].color = new Color(0f, 0.8f, 1f); // Bleu cyan allumé
            else
                _crystalIcons[i].color = new Color(0.2f, 0.2f, 0.2f); // Gris éteint
        }
    }

    public void SetCrystalReady(bool ready)
    {
        if (_ultReadyEffect != null)
            _ultReadyEffect.SetActive(ready);

        if (!ready)
        {
            // Reset complet en gris
            foreach (var icon in _crystalIcons)
                icon.color = new Color(0.2f, 0.2f, 0.2f);
        }
        else
        {
            // Tous en blanc pour signaler que c'est prêt
            foreach (var icon in _crystalIcons)
                icon.color = Color.white;
        }
    }

    public void ShowUltEffect(bool show)
    {
        // On peut ajouter un overlay visuel plus tard
        Debug.Log(show ? "ULT ACTIF — ennemis ralentis !" : "ULT terminé");
    }

    public void UpdateDashCooldown(float percent)
    {
        if (_dashCooldownBar != null)
            _dashCooldownBar.value = percent;
    }

    public void UpdateGold(int amount)
    {
        if (_goldText != null)
            _goldText.text = $"  : {amount}";
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

    }

    public void UpdateXPBar(float currentXP, float xpToNextLevel, int level)
    {
        _xpBar.value    = currentXP / xpToNextLevel;
        _levelText.text = $"Niv. {level}";
    }

    public void UpdateHPBar(float currentHP, float maxHP)
    {
        float percent = currentHP / maxHP;
        _hpBar.value = percent;
        _hpText.text = $"{Mathf.CeilToInt(currentHP)} / {Mathf.CeilToInt(maxHP)}";

        if (percent > 0.6f)
            _hpFillImage.color = new Color(0f, 0.7f, 0f);
        else if (percent > 0.4f)
            _hpFillImage.color = new Color(0.4f, 0.8f, 0f);
        else if (percent > 0.2f)
            _hpFillImage.color = new Color(1f, 0.5f, 0f);
        else
            _hpFillImage.color = new Color(0.85f, 0.1f, 0.1f);
    }

    public void UpdateWave(int wave)
    {
        if (_waveText == null) return;

        if (wave == -1)
        {
            _waveText.text  = "BOSS !";
            _waveText.color = Color.red;
        }
        else
        {
            _waveText.text  = $"Vague {wave}";
            _waveText.color = Color.white;
        }
    }

    public void UpdateTimer(float seconds)
    {
        if (_timerText == null) return;

        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        _timerText.text = $"{mins:00}:{secs:00}";
    }

   public void ShowBossHP(string bossName)
   {
        _bossHPBar.SetActive(true);
        _bossNameText.gameObject.SetActive(true);
        _bossNameText.text  = bossName;
        _bossHPSlider.value = 1f;
        if (_bossIcon != null) _bossIcon.SetActive(true); // ← ajoute ça
   }

   public void UpdateBossHP(float current, float max)
   {
        _bossHPSlider.value = current / max;
   }

   public void HideBossHP()
   {
        _bossHPBar.SetActive(false);
        _bossNameText.gameObject.SetActive(false);
        if (_bossIcon != null) _bossIcon.SetActive(false); // ← ajoute ça
   }

   public void ShowGameOver(float runTimer, int killCount, int wave, int goldEarned)
   {
        _gameOverPanel.SetActive(true);

        int mins = Mathf.FloorToInt(runTimer / 60f);
        int secs = Mathf.FloorToInt(runTimer % 60f);

        _statsText.text = $"Temps de survie : {mins:00}:{secs:00}\n" +
                          $"Ennemis tués : {killCount}\n" +
                          $"Vague atteinte : {wave}\n" +
                          $"Gold gagné : {goldEarned}";
    }

    private void Start()
    {
        if (MetaProgressionManager.Instance != null)
            UpdateGold(MetaProgressionManager.Instance.RunGold);
    }
}
```

## UpgradeUI.cs
```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeUI : MonoBehaviour
{
    [Header("Cartes d'upgrade")]
    [SerializeField] private UpgradeCard[] _cards;

    [System.Serializable]
    public class UpgradeCard
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descriptionText;
        public Button          chooseButton;
    }

    // Appelé par LevelUpManager pour afficher les upgrades
    public void DisplayUpgrades(System.Collections.Generic.List<UpgradeData> upgrades)
    {
        for (int i = 0; i < _cards.Length; i++)
        {
            if (i < upgrades.Count)
            {
                int index = i; // Capture pour le lambda
                _cards[i].nameText.text        = upgrades[i].upgradeName;
                _cards[i].descriptionText.text = upgrades[i].description;

                // On remet le bouton à zéro avant d'ajouter le listener
                _cards[i].chooseButton.onClick.RemoveAllListeners();
                _cards[i].chooseButton.onClick.AddListener(() =>
                    LevelUpManager.Instance.SelectUpgrade(index)
                );

                _cards[i].chooseButton.gameObject.SetActive(true);
            }
            else
            {
                _cards[i].chooseButton.gameObject.SetActive(false);
            }
        }
    }
}
```

## MainMenuManager.cs
```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject _upgradesPanel;
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private GameObject _settingsPanel;

    [Header("TopBar")]
    [SerializeField] private TextMeshProUGUI _goldDisplay;
    [SerializeField] private TextMeshProUGUI _gemsDisplay;

    private void Start()
    {
        // Attend que MetaProgressionManager soit prêt
        if (MetaProgressionManager.Instance != null)
        {
            SaveData data = SaveSystem.Load();
            _goldDisplay.text = $": {data.totalGold}";
            _gemsDisplay.text = $": {data.totalGems}";
        }
        else
        {
            _goldDisplay.text = " : 0";
            _gemsDisplay.text = " : 0";
        }

        ShowPanel(_menuPanel);
    }

    public void ShowPanel(GameObject panel)
    {
        _upgradesPanel.SetActive(false);
        _menuPanel.SetActive(false);
        _settingsPanel.SetActive(false);
        panel.SetActive(true);
    }

    public void ShowUpgrades() => ShowPanel(_upgradesPanel);
    public void ShowMenu() => ShowPanel(_menuPanel);
    public void ShowSettings() => ShowPanel(_settingsPanel);

    public void PlayGame()
    {
        SceneManager.LoadScene(1); // 1 = Game dans Build Settings
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
```

---

# 6. SCENEOBJECTS — CE QUI EST DANS LA SCÈNE GAME

## Hierarchy de la scène Game
```
Main Camera
Directional Light
PlayerFollowCam (Cinemachine Virtual Camera)
Player
  └── PulseVisual (désactivé par défaut)
Ground (Plane, Scale: 100,1,100) (temporaire, les limites de la map n'ont pas encore été fixées)
EnemySpawner
ObjectPool
GameManager
XPSystem
LevelUpManager
WaveManager
MetaProgressionManager (DontDestroyOnLoad)
DamageNumberSpawner
Canvas
  ├── HUD
  │   ├── XPBar (Slider, bottom center)
  │   ├── LevelText (TMP, bottom left)
  │   ├── HPBar (Slider, top left)
  │   ├── HPText (TMP, top left)
  │   ├── WaveText (TMP, top center)
  │   ├── TimerText (TMP, top center)
  │   ├── GoldText (TMP, top left)
  │   ├── DashCooldownBar (Slider, bottom center)
  │   ├── CrystalBar (Panel avec 15 Image icons)
  │   ├── BossHPBar (Slider vertical, right center, désactivé)
  │   ├── BossNameText (TMP, désactivé)
  │   ├── BossIcon (Image, désactivé)
  │   └── UltOverlay (Image plein écran bleu, désactivé)
  ├── LevelUpPanel (désactivé par défaut)
  │   ├── TitleText "LEVEL UP !"
  │   ├── UpgradeCard1
  │   │   ├── UpgradeName (TMP)
  │   │   ├── UpgradeDescription (TMP)
  │   │   └── ChooseButton
  │   ├── UpgradeCard2
  │   └── UpgradeCard3
  └── GameOverPanel (désactivé par défaut)
      ├── GameOverTitle "GAME OVER"
      ├── StatsText (TMP)
      ├── RetryButton → GameManager.RestartGame()
      └── MainMenuButton → GameManager.GoToMainMenu()
EventSystem
```

## Composants sur le Player
- **Capsule Collider** : Is Trigger = TRUE
- **Rigidbody** : Is Kinematic = TRUE, Freeze Position Y, Freeze All Rotations
- **Tag** : Player
- **Layer** : Player
- Scripts : PlayerController, HealthSystem, WeaponBase, CrystalSystem

## ObjectPool — Pools configurés
| Tag | Prefab | Size |
|---|---|---|
| Enemy | PFB_EnemyBasic | 30 |
| Projectile | Projectile | 50 |
| EnemyTank | PFB_EnemyTank | 10 |
| EnemyShooter | PFB_EnemyShooter | 10 |

## ScriptableObjects Upgrades (dans LevelUpManager — Size: 9)
| SO | Type | Value |
|---|---|---|
| SO_UpgradeDamage | Damage | 0.2 |
| SO_UpgradeFireRate | FireRate | 0.2 |
| SO_UpgradeMoveSpeed | MoveSpeed | 0.15 |
| SO_UpgradeHeal | Heal | 0.2 |
| SO_UnlockAOE | UnlockAOE | 0 |
| SO_UnlockOrbital | UnlockOrbital | 0 |
| SO_AddOrbital | AddOrbital | 0 |
| SO_AOERadius | AOERadius | 0.2 |
| SO_DoubleShot | DoubleShot | 0 |

---

# 7. MENU PRINCIPAL — SCÈNE MAINMENU

## Design validé
- **Fond** : Image de la Plaine (style anime, générée avec Leonardo.ai)
- **Logo** : AETHER avec cristal bleu (fond transparent via remove.bg/Photopea)
- **Boutons** : Style parchemin avec bordures dorées
- **Icônes** : Pièce d'or et gemme bleue en haut à gauche
- **Onglets** : UPGRADES | MENU | PARAMÈTRES en haut

## Hierarchy MainMenu
```
Canvas
  ├── TabBar (Panel, top stretch, hauteur 50)
  │   ├── TabUpgrades → MainMenuManager.ShowUpgrades()
  │   ├── TabMenu → MainMenuManager.ShowMenu()
  │   └── TabSettings → MainMenuManager.ShowSettings()
  ├── UpgradesPanel (désactivé par défaut)
  ├── MenuPanel (actif par défaut)
  │   ├── BackgroundImage (fond Plaine)
  │   ├── LogoAether (Image)
  │   └── PlayButton → MainMenuManager.PlayGame()
  ├── SettingsPanel (désactivé par défaut)
  └── TopBar (Panel, top stretch — DOIT ÊTRE EN BAS de la Hierarchy pour être au-dessus visuellement)
      ├── GoldIcon (Image)
      ├── GoldDisplay (TMP, jaune)
      ├── GemsIcon (Image)
      └── GemsDisplay (TMP, cyan)
EventSystem
MainMenuManager (GameObject vide)
MetaProgressionManager (DontDestroyOnLoad)
```

---

# 8. CE QUI RESTE À FAIRE

## Priorité immédiate (gameplay)
- [ ] Régénération 50HP pour le player à la mort du boss (déjà dans BossBase.cs ci-dessus)
- [ ] Contrôle range orbitale au clavier (A = réduire, E = augmenter)
- [ ] Limite min/max de la range orbitale en public dans Inspector

## Système de niveaux
- [ ] Boss 1 (Sanglier style princesse Mononoke) -> déjà fait
- [ ] Boss 2 (Cerf Ancestral) avec ses patterns spécifiques
- [ ] Boss 3 (La Source Corrompue) avec ses patterns spécifiques
- [ ] Écran de victoire (15min survivées, 3 boss vaincus)
- [ ] Déblocage du niveau suivant

## Meta-progression
- [ ] Système de coffres (Simple/Rare/Légendaire)
- [ ] Fragments par type d'upgrade permanentes
- [ ] Shop avec upgrades permanentes (HP Max, Dégâts, % de chance de Dégâts Critiques)
- [ ] Déblocage de personnages à la fin de chaque niveau

## Polish
- [ ] Animations VFX (ulti cristal, mort ennemis, particules)
- [ ] Screen shake (Cinemachine Impulse)
- [ ] Musique de fond + SFX
- [ ] Paramètres (touches rebindables, son, fenêtre)
- [ ] Map 3D (Terrain Unity + assets Kenney.nl)
- [ ] Modèles 3D Meshy AI (attendre fin du gameplay)

---

# 9. DÉCISIONS TECHNIQUES IMPORTANTES

| Décision | Choix | Raison |
|---|---|---|
| Mouvement joueur | MovePosition (Kinematic) | Évite d'être poussé par les ennemis |
| Collision ennemis | Rigidbody non-Kinematic | Physique de séparation naturelle |
| Layer Player/Enemy | Collision Matrix cochée | Triggers OK, physique séparée |
| Invincibilité dash | Variable _isInvincibleExternal séparée | Évite conflit avec timer d'invincibilité level up |
| Absorption projectile | Fenêtre 0.3s après dash | Skill-based, pas automatique |
| ObjectPool | Tag-based | Simple et efficace |
| Sauvegarde | JSON sur disque | Simple, pas de dépendance externe |
| SceneManagement | Par index (0=Menu, 1=Game) | Plus fiable que par nom |

---

# 10. OUTILS ET RESSOURCES

| Outil | Usage |
|---|---|
| Unity 2021.3.45f2 LTS | Moteur de jeu |
| Visual Studio / VS Code | Éditeur de code C# |
| GitHub Desktop | Versioning (repo: FrostyCRT/Aether) |
| Leonardo.ai | Génération d'images 2D (150 crédits/jour gratuit) |
| Microsoft Designer | Alternative gratuite pour images 2D |
| Meshy AI (10€/mois) | Modélisation 3D — à prendre quand gameplay finalisé |
| Photopea | Retouche images (suppression fond) |
| remove.bg | Suppression fond automatique |
| Kenney.nl | Assets 3D/2D gratuits pour la map |
| Google Fonts | Polices (Bangers, Oswald recommandées) |

---

# 11. Réponse IA

Chacune de tes réponses doivent structurées, tu dois donner ton avis personnel, même si ça me déplait. 
Le jeu doit être original pour pouvoir se démarquer des autres gros jeu de ce genre.
N'hésite surtout pas à me poser des questions, c'est mieux pour se mettre d'accord sur l'avancé du projet.
Quand j'ai une idée, rien ne t'oblige à dire qu'elle est bonne, tu peux tout à fait me dire non, en me donnant tes arguments bien sûr.
Je te fais confiance pour me sortir le meilleur contenu possible, en étant original et en me donnant des arguments fiables et honnête pour une avancée stratégique et efficace du projet.
Attention tout de même à ne pas dire n'importe quoi, le jeu doit avoir du sens, il ne faut pas que ce soit le bordel, ça doit être sérieux.
Je n'ai pas encore payer d'abonnement pour Meshi AI, ou Tripo AI, je l'aurai dans 1 mois.

# 12. Conclusion

Arrives-tu a comprendre tout le projet en détails ?

As-tu des questions ? Je vais avoir besoin de ton aide pour l'avancé du projet.

Je ne sais pas coder en C# et je suis débutant sur Unity donc je dois guider a 100%, le moindre clique doit être guidé par toi car l'interface est très complexe quand on débute. J'ai déjà fait tout ça avec une autre conversation Claude, mais la mémoire a saturé car la conversation était beaucoup trop longue.


*Document — Projet Aether — Dernière mise à jour : 5 Juin 2026*
