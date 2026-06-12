# AETHER — Documentation Complète du Projet V2
## Jeu Unity 2021.3.45f2 (URP) — Bullet Heaven Roguelite
## Dernière mise à jour : Juin 2026

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
- **IA assistante** : Claude — répond de façon structurée, donne son avis honnête même si ça déplaît, pose des questions pour se mettre d'accord, ne dit pas oui à tout

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
4. **Les orbitaux avec contrôle de range** — touches A/E pour réduire/augmenter la range en temps réel
5. **Nova de Cristal** — explosion visible à chaque absorption de projectile
6. **Jeu 100% PC** — complexité intentionnelle pour se démarquer des versions mobiles du genre
7. **3D exploitée mécaniquement** — relief de map, pas juste visuel (à venir)

---

# 3. DIRECTION ARTISTIQUE — AETHER

## Style visuel
- **Anime Toon-Shading** type Ghibli / Mushoku Tensei
- **Low-Poly** (~2000 polygones par modèle ennemi, 2500 pour le joueur)
- **Caméra** : Vue top-down inclinée à 65°, projection Orthographique
- **Shading** : URP Toon Shader (Cel-shading)
- **Post-Processing** : Bloom, Color Grading saturé, éclairage chaleureux

## Pipeline Assets validé (100% gratuit)
- **Modèles 3D** : IA générative locale (TRELLIS / Hunyuan3D) → Blender (.fbx) → Mixamo (rigging automatique)
- **Concepts 2D** : Leonardo.ai / Microsoft Designer (150 crédits/jour gratuit)
- **Son et musique style Ghibli** : ElevenLabs + Suno + Audacity
- **Retouche images** : Photopea / remove.bg
- **Assets 3D gratuits map** : Kenney.nl
- **Polices** : Google Fonts (Bangers, Oswald recommandées)
- **Meshy AI (10€/mois)** : À prendre dans ~1 mois quand le gameplay est finalisé — alternative payante pour les modèles 3D

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

### Loup de Mana (Ennemi Basic — corps à corps)
- Pelage gris/bleu foncé avec marques naturelles plus claires
- Crête de poils rigides le long du dos bleutée
- Yeux bleu électrique intenses (seul élément lumineux)
- Muscles des pattes avant surdéveloppés, griffes bleu/gris métallique
- 1000-1200 polygones

### Golem de Tronc (Tank — corps à corps lent)
- Corps humanoïde massif fait de troncs d'arbres torsadés
- Bras qui traînent presque jusqu'au sol
- Pas de visage — deux cavités sombres avec yeux orangés
- Racines sortant des pieds et épaules
- 1200-1500 polygones

### Bulbe Cracheur (Shooter — à distance)
- Corps central : bulbe floral géant fermé, vert/violet
- S'ouvre comme fleur carnivore quand il tire
- 4-6 racines épaisses comme pattes (légèrement translucides)
- Épines sur le bord du bulbe
- 1000-1200 polygones
- **NOTE** : Les ennemis shooters doivent rester minoritaires — la majorité doit être corps à corps

## Boss — Map 1 (Plaine)

### Boss 1 — Le Sanglier de Mana (5min) ✅ IMPLÉMENTÉ
- HP : 500
- Recouvert de mousses et lignes de mana lumineuses
- Défenses en cristal de mana bleu
- **Attaques** :
  - Charge en ligne droite vers le joueur
  - Tir en éventail de 8 projectiles de mana
  - Piétinement — zone AOE autour de lui

### Boss 2 — Le Cerf Ancestral (10min) ✅ IMPLÉMENTÉ
- HP : 800
- Ramure dorée/bleue avec lignes de mana lumineuses
- **Attaques validées** :
  - **Téléportation** derrière le joueur (toutes les 8s) — freeze de 0.5s après téléportation pour laisser une fenêtre de réaction
  - **Spirale** de 24 projectiles tirés en rafale progressive
  - **Régénération** — récupère 100HP toutes les 30 secondes (force le joueur à être agressif)
  - **Rage à 30% HP** — fire rate x1.5, vitesse x1.5, cooldown téléportation /2

### Boss 3 — La Source Corrompue (15min) ✅ IMPLÉMENTÉ
- HP : 1200
- Masse cristalline bleue/noire flottante — **FIXE en Phase 1**
- **Attaques** :
  - **Cristaux orbitaux** — 6 cristaux tournent et tirent des rafales vers le joueur (fire rate : 0.5)
  - **Vague de ralentissement** — ralentit le joueur à 40% de sa vitesse pendant 3s (toutes les 10s)
  - **Invocation** — toutes les 25s, invoque 1 mini-boss aléatoire (Sanglier OU Cerf, alternés strictement) à 30% HP et 60% de taille. Le mini-boss n'affiche pas de barre de vie, ne déclenche pas OnBossDied, ne régénère pas le joueur à sa mort
  - **Implosion** — toutes les 20s, aspire le joueur pendant 1.5s puis AOE 40 dégâts. Le dash annule l'aspiration. Distance minimale de 3 unités pour éviter le spawn kill
  - **Phase 2 à 50% HP** — se met à bouger aléatoirement (vitesse 3, change direction toutes les 2s), cristaux tirent 2x plus vite, vague de ralentissement 2x plus fréquente

## Progression des Maps
1. **Plaine** — Zone de départ, lumineuse et verdoyante ← ON EST ICI
2. **Forêt mystique** — Arbres géants, champignons lumineux
3. **Désert magique**
4. **Montagne enneigée**
5. **Jungle**
6. **Volcan**

**STRATÉGIE** : Finir une zone parfaite et addictive avant d'en faire d'autres. Vampire Survivors a explosé avec une seule map.

---

# 4. CONFIGURATION UNITY

## Paramètres importants
- **Caméra** : Orthographique, Rotation X: 65, Y: 0, Z: 0
- **Cinemachine** : PlayerFollowCam, Body: Transposer
- **Physics Layer** : Layer "Player" et "Enemy" créés
- **Collision Matrix** : Player/Enemy — Trigger OK, physique séparée
- **Scenes** : 0 = MainMenu, 1 = Game (dans Build Settings)

## Hierarchy de la scène Game
```
Main Camera
Directional Light
PlayerFollowCam (Cinemachine Virtual Camera)
Player
  └── PulseVisual (désactivé par défaut)
Ground (Plane, Scale: 3,1,3)
EnemySpawner
ObjectPool
GameManager
XPSystem
LevelUpManager
WaveManager
MetaProgressionManager (DontDestroyOnLoad)
DamageNumberSpawner
Canvas
  ├── LevelUpPanel (désactivé par défaut — DOIT ÊTRE EN BAS de la Hierarchy pour recevoir les clics)
  │   ├── TitleText "LEVEL UP !"
  │   ├── UpgradeCard1
  │   │   ├── UpgradeName (TMP)
  │   │   ├── UpgradeDescription (TMP)
  │   │   └── ChooseButton
  │   ├── UpgradeCard2
  │   └── UpgradeCard3
  ├── HUD
  │   ├── XPBar (Slider, bottom center)
  │   ├── LevelText (TMP, bottom left)
  │   ├── HPBar (Slider, top left)
  │   ├── HPText (TMP, top left)
  │   ├── TimerText (TMP, top center)
  │   ├── GoldText (TMP, top left)
  │   ├── KillCountText (TMP, top — affiche "Kills : X")
  │   ├── DashCooldownBar (Slider, bottom center)
  │   ├── CrystalBar (Panel avec 6 Image icons — WAS 15, now 6)
  │   ├── BossHPBar (Slider vertical, right center, désactivé)
  │   ├── BossNameText (TMP, désactivé)
  │   ├── BossIcon (Image, désactivé)
  │   └── UltOverlay (Image plein écran bleu, désactivé)
  ├── GameOverPanel (désactivé par défaut)
  │   ├── GameOverTitle "GAME OVER"
  │   ├── StatsText (TMP) — Temps | Kills | Gold (plus de "Vague atteinte")
  │   ├── RetryButton → GameManager.RestartGame()
  │   └── MainMenuButton → GameManager.GoToMainMenu()
  └── PausePanel (désactivé par défaut)
      ├── TitleText "PAUSE"
      ├── StatsText (TMP) — Temps | Kills | Gold
      ├── UpgradesText (TMP) — liste des upgrades de la run
      ├── ResumeButton → GameManager.ResumePause()
      ├── AbandonButton → GameUI.ShowAbandonConfirm(true)
      └── AbandonConfirmPanel (désactivé par défaut)
          ├── ConfirmText "Abandonner la run ?"
          ├── OuiButton → GameManager.AbandonRun()
          └── NonButton → GameUI.ShowAbandonConfirm(false)
EventSystem
```

**RÈGLE IMPORTANTE** : Dans le Canvas, l'ordre de la Hierarchy détermine l'ordre de rendu ET la priorité des clics. Ce qui est en BAS = au-dessus visuellement et reçoit les clics en premier. Le LevelUpPanel doit être en bas pour que ses boutons reçoivent les clics.

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

# 5. SCRIPTS C# — VERSION ACTUELLE ET À JOUR

## PlayerController.cs
```csharp
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
    private float        _speedMultiplier = 1f;

    // Dash
    private bool    _isDashing         = false;
    private bool    _isInvincible      = false;
    private float   _dashTimer         = 0f;
    private float   _dashCooldownTimer = 0f;
    private bool    _canAbsorb         = false;
    private float   _absorptionTimer   = 0f;
    private Vector3 _dashDirection;

    private CrystalSystem _crystalSystem;

    public bool  IsDashing           => _isDashing;
    public bool  IsInvincible        => _isInvincible;
    public bool  CanAbsorb           => _canAbsorb;
    public float DashCooldownPercent => _dashCooldownTimer / _dashCooldown;

    private void Awake()
    {
        _rb            = GetComponent<Rigidbody>();
        _healthSystem  = GetComponent<HealthSystem>();
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
        if (Input.GetKeyDown(KeyCode.LeftShift) && !_isDashing && _dashCooldownTimer <= 0f)
        {
            Vector3 direction = _moveDirection != Vector3.zero ? _moveDirection : transform.forward;
            StartDash(direction);
        }

        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
                StopDash();
        }
    }

    private void StartDash(Vector3 direction)
    {
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
            _rb.MovePosition(_rb.position + _dashDirection * _dashSpeed * Time.fixedDeltaTime);
        }
        else
        {
            // _speedMultiplier permet le ralentissement du Boss 3
            _rb.MovePosition(_rb.position + _moveDirection * _moveSpeed * _speedMultiplier * Time.fixedDeltaTime);
        }
    }

    public void AddMoveSpeed(float value) => _moveSpeed += _moveSpeed * value;

    public void SetSpeedMultiplier(float multiplier) => _speedMultiplier = multiplier;

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
        _currentHealth  = Mathf.Max(_currentHealth, 0f);

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

## WeaponBase.cs
```csharp
using UnityEngine;

public class WeaponBase : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject _projectilePrefab;

    [Header("Stats")]
    [SerializeField] private float _fireRate       = 1f;
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private float _damage         = 10f;

    [Header("Double tir")]
    [SerializeField] private bool  _doubleShot      = false;
    [SerializeField] private float _doubleShotDelay = 0.1f;

    private float _cooldownTimer = 0f;

    private void Start()
    {
        float bonusDamage = MetaProgressionManager.Instance.GetBonusDamage();
        _damage += _damage * bonusDamage;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;

        _cooldownTimer += Time.deltaTime;
        if (_cooldownTimer >= 1f / _fireRate)
        {
            Transform nearest = FindNearestEnemy();
            if (nearest != null)
            {
                Vector3 direction = (nearest.position - transform.position).normalized;
                direction.y = 0f;
                Shoot(direction);
                _cooldownTimer = 0f;
            }
        }
    }

    private Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest    = null;
        float     minDist    = _detectionRange;

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

    private void Shoot(Vector3 direction)
    {
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

    public void UnlockDoubleShot()       => _doubleShot  = true;
    public bool IsDoubleShotUnlocked()   => _doubleShot;
    public void AddDamage(float value)   => _damage   += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;
}
```

## CrystalSystem.cs
```csharp
using UnityEngine;
using System.Collections;

public class CrystalSystem : MonoBehaviour
{
    [Header("Jauge")]
    [SerializeField] private int _maxCharges = 6;

    [Header("Ulti — touche F")]
    [SerializeField] private float _ultDamage    = 50f;
    [SerializeField] private float _ultRange     = 10f;
    [SerializeField] private float _slowFactor   = 0.3f;
    [SerializeField] private float _slowDuration = 3f;

    [Header("Nova — à chaque absorption")]
    [SerializeField] private float      _novaDamage    = 10f;
    [SerializeField] private float      _novaRadius    = 3f;
    [SerializeField] private GameObject _novaVFXPrefab; // Cylinder plat cyan

    private int  _currentCharges = 0;
    private bool _isReady        = false;

    public int  CurrentCharges => _currentCharges;
    public int  MaxCharges     => _maxCharges;
    public bool IsReady        => _isReady;

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (_isReady && Input.GetKeyDown(KeyCode.F))
            TriggerUlt();
    }

    public void AbsorbProjectile()
    {
        if (_currentCharges >= _maxCharges) return;
        _currentCharges++;
        GameUI.Instance.UpdateCrystalCharge(_currentCharges, _maxCharges);

        // Nova à chaque absorption
        TriggerNova();

        if (_currentCharges >= _maxCharges)
        {
            _isReady = true;
            GameUI.Instance.SetCrystalReady(true);
        }
    }

    private void TriggerNova()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _novaRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase eb = hit.GetComponent<EnemyBase>();
                if (eb != null) eb.TakeDamage(_novaDamage, DamageNumberSpawner.ColorCritical);

                BossBase boss = hit.GetComponent<BossBase>();
                if (boss != null) boss.TakeDamage(_novaDamage);
            }
        }

        if (_novaVFXPrefab != null)
            StartCoroutine(ShowNovaVFX());
    }

    private IEnumerator ShowNovaVFX()
    {
        GameObject vfx = Instantiate(_novaVFXPrefab, transform.position, Quaternion.identity);
        float duration = 0.3f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.Lerp(0f, _novaRadius * 2f, elapsed / duration);
            vfx.transform.localScale = new Vector3(scale, 0.05f, scale);
            yield return null;
        }

        Destroy(vfx);
    }

    private void TriggerUlt()
    {
        _isReady        = false;
        _currentCharges = 0;
        GameUI.Instance.SetCrystalReady(false);
        GameUI.Instance.UpdateCrystalCharge(0, _maxCharges);
        DamageAllEnemies();
        StartCoroutine(SlowAllEnemies());
    }

    private void DamageAllEnemies()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase eb = hit.GetComponent<EnemyBase>();
                if (eb != null) eb.TakeDamage(_ultDamage, DamageNumberSpawner.ColorCritical);

                BossBase boss = hit.GetComponent<BossBase>();
                if (boss != null) boss.TakeDamage(_ultDamage);
            }
        }
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _novaRadius);
    }
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

    [Header("Contrôle Range (A = réduire / E = augmenter)")]
    [SerializeField] private float _minOrbitRadius   = 1f;
    [SerializeField] private float _maxOrbitRadius   = 6f;
    [SerializeField] private float _rangeChangeSpeed = 2f;

    [Header("Références")]
    [SerializeField] private GameObject _orbitalPrefab;

    [Header("Limites")]
    [SerializeField] private int _maxOrbitalCount = 3; // Max 3 orbitaux (était 5, trop cheat)

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

        // A = réduire range, E = augmenter range
        if (Input.GetKey(KeyCode.A))
            _orbitRadius = Mathf.Max(_minOrbitRadius, _orbitRadius - _rangeChangeSpeed * Time.deltaTime);
        if (Input.GetKey(KeyCode.E))
            _orbitRadius = Mathf.Min(_maxOrbitRadius, _orbitRadius + _rangeChangeSpeed * Time.deltaTime);

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

    public void AddDamage(float value) => _damage += _damage * value;
}
```

## OrbitalProjectile.cs
```csharp
using UnityEngine;
using System.Collections.Generic;

public class OrbitalProjectile : MonoBehaviour
{
    [SerializeField] private float _damage = 15f;
    private float _hitCooldown = 0.5f;

    // Cooldowns par GameObject pour couvrir ennemis ET boss
    private Dictionary<GameObject, float> _hitCooldowns = new Dictionary<GameObject, float>();

    private void Update()
    {
        var keys = new List<GameObject>(_hitCooldowns.Keys);
        foreach (GameObject go in keys)
        {
            _hitCooldowns[go] -= Time.deltaTime;
            if (_hitCooldowns[go] <= 0f)
                _hitCooldowns.Remove(go);
        }
    }

    public void SetDamage(float damage) => _damage = damage;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;
        if (_hitCooldowns.ContainsKey(other.gameObject)) return;

        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeDamage(_damage, DamageNumberSpawner.ColorOrbital);
            _hitCooldowns[other.gameObject] = _hitCooldown;
            return;
        }

        BossBase boss = other.GetComponent<BossBase>();
        if (boss != null)
        {
            boss.TakeDamage(_damage, DamageNumberSpawner.ColorOrbital);
            _hitCooldowns[other.gameObject] = _hitCooldown;
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
    [SerializeField] private float _fireRate = 0.5f;

    [Header("Visuel")]
    [SerializeField] private GameObject _pulseVisual;

    private float _cooldownTimer  = 0f;
    private float _animationTimer = -1f;
    private float _animDuration   = 0.3f;

    [Header("Limites")]
    [SerializeField] private float _maxRadius = 8f;

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

    private void Pulse()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _radius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                // Ennemis normaux
                EnemyBase enemy = hit.GetComponent<EnemyBase>();
                if (enemy != null)
                {
                    enemy.TakeDamage(_damage, DamageNumberSpawner.ColorAOE);
                    continue;
                }

                // Boss
                BossBase boss = hit.GetComponent<BossBase>();
                if (boss != null)
                    boss.TakeDamage(_damage, DamageNumberSpawner.ColorAOE);
            }
        }

        if (_pulseVisual != null)
        {
            _pulseVisual.SetActive(true);
            _pulseVisual.transform.localScale = Vector3.zero;
            _animationTimer = 0f;
        }
    }

    public void AddDamage(float value)   => _damage   += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;

    public void Init(GameObject pulseVisualPrefab)
    {
        GameObject visual = Instantiate(pulseVisualPrefab, transform.position, Quaternion.identity);
        visual.transform.SetParent(transform);
        _pulseVisual = visual;
        _pulseVisual.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
}
```

## EnemyBase.cs
```csharp
using UnityEngine;

public class EnemyBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _maxHealth = 30f;

    [Header("Drops")]
    [SerializeField] private float _xpValue   = 10f;
    [SerializeField] private int   _goldValue = 2;

    [Header("Pool")]
    [SerializeField] private string _poolTag = "Enemy";

    protected float MoveSpeed => _moveSpeed;

    private float     _currentHealth;
    private Transform _playerTransform;

    // OnEnable (pas Awake) pour reset les HP à chaque réactivation depuis le pool
    private void OnEnable()
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
        return force.normalized * 0.5f;
    }

    public void TakeDamage(float damage, Color color = default)
    {
        _currentHealth -= damage;

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

## EnemyProjectile.cs
```csharp
using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float _speed    = 6f;
    [SerializeField] private float _maxRange = 20f;

    private Vector3 _direction;
    private Vector3 _startPosition;
    private bool    _hasHit = false;

    public void Init(Vector3 direction)
    {
        _direction     = direction.normalized;
        _startPosition = transform.position;
        _hasHit        = false;
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
        if (_hasHit) return;

        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null && player.IsInvincible)
            {
                if (player.CanAbsorb)
                {
                    CrystalSystem crystal = other.GetComponent<CrystalSystem>();
                    if (crystal != null) crystal.AbsorbProjectile();
                    _hasHit = true;
                    Destroy(gameObject);
                }
                return;
            }

            // Dégâts gérés par HealthSystem.OnTriggerEnter
            _hasHit = true;
            Destroy(gameObject);
        }
    }
}
```

## BossBase.cs
```csharp
using UnityEngine;
using System.Collections;

public class BossBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] protected float _maxHealth = 500f;
    [SerializeField] protected float _moveSpeed = 3f;
    [SerializeField] protected float _xpValue   = 200f;
    [SerializeField] protected int   _goldValue = 50;

    [Header("Attaque")]
    [SerializeField] protected GameObject _projectilePrefab;
    [SerializeField] protected float      _fireRate        = 1f;
    [SerializeField] protected int        _projectileCount = 8;
    [SerializeField] protected float      _chargeCooldown  = 5f;

    [Header("Identité")]
    [SerializeField] protected string _bossName = "BOSS";

    protected float     _currentHealth;
    protected Transform _playerTransform;
    protected float     _fireTimer   = 0f;
    protected float     _chargeTimer = 0f;
    protected bool      _isCharging  = false;
    protected Vector3   _chargeDirection;

    public float MaxHealth  => _maxHealth;
    public bool  IsSummoned { get; set; } = false;

    protected virtual void Start()
    {
        _currentHealth = _maxHealth;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;

        // Attend une frame pour que IsSummoned soit correctement assigné
        StartCoroutine(InitUI());
    }

    private IEnumerator InitUI()
    {
        yield return null;
        if (!IsSummoned)
            GameUI.Instance.ShowBossHP(_bossName);
    }

    protected virtual void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance.IsGameOver) return;

        HandleMovement();
        HandleShooting();
        HandleCharge();
    }

    protected virtual void HandleMovement()
    {
        if (_isCharging) return;
        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        transform.position += direction * _moveSpeed * Time.deltaTime;
    }

    protected virtual void HandleShooting()
    {
        _fireTimer += Time.deltaTime;
        if (_fireTimer >= 1f / _fireRate)
        {
            ShootRadial();
            _fireTimer = 0f;
        }
    }

    protected virtual void ShootRadial()
    {
        if (_projectilePrefab == null) return;

        float angleStep = 360f / _projectileCount;
        for (int i = 0; i < _projectileCount; i++)
        {
            float   angle     = angleStep * i;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            GameObject projectileGO = Instantiate(_projectilePrefab, transform.position, Quaternion.identity);
            EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
            if (projectile != null)
                projectile.Init(direction);
        }
    }

    protected virtual void HandleCharge()
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
            transform.position += _chargeDirection * _moveSpeed * 4f * Time.deltaTime;
    }

    protected virtual void StopCharge()
    {
        _isCharging = false;
    }

    public virtual void TakeDamage(float damage, Color color = default)
    {
        _currentHealth -= damage;

        if (DamageNumberSpawner.Instance != null)
        {
            Color c = color == default ? DamageNumberSpawner.ColorCritical : color;
            DamageNumberSpawner.Instance.Spawn(transform.position, damage, c, true);
        }

        if (!IsSummoned)
            GameUI.Instance.UpdateBossHP(_currentHealth, _maxHealth);

        if (_currentHealth <= 0)
            Die();
    }

    protected virtual void Die()
    {
        XPSystem.Instance.AddXP(_xpValue);
        GameManager.Instance.AddKill();
        MetaProgressionManager.Instance.AddRunGold(_goldValue);

        if (!IsSummoned)
        {
            GameUI.Instance.HideBossHP();
            WaveManager.Instance.OnBossDied();
            HealthSystem playerHP = GameObject.FindWithTag("Player")?.GetComponent<HealthSystem>();
            if (playerHP != null) playerHP.Heal(0.5f);
        }

        Destroy(gameObject);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HealthSystem health = other.GetComponent<HealthSystem>();
            if (health != null)
                health.TakeDamage(30f);
        }
    }

    public void InitWithReducedHP(float percent)
    {
        _currentHealth = _maxHealth * percent;
    }

    public void SetXPValue(float value) => _xpValue = value;
}
```

## BossDeer.cs (Cerf Ancestral)
```csharp
using UnityEngine;

public class BossDeer : BossBase
{
    [Header("Cerf — Téléportation")]
    [SerializeField] private float _teleportCooldown = 8f;
    [SerializeField] private float _teleportDistance = 3f;

    [Header("Cerf — Spirale")]
    [SerializeField] private float _spiralFireRate   = 0.15f;
    [SerializeField] private int   _spiralBurstCount = 24;

    [Header("Cerf — Régénération")]
    [SerializeField] private float _regenAmount   = 100f;
    [SerializeField] private float _regenCooldown = 30f;

    [Header("Cerf — Rage")]
    [SerializeField] private float _rageThreshold = 0.3f;
    private bool _isRaging = false;

    private float _teleportTimer = 0f;
    private float _regenTimer    = 0f;
    private float _spiralAngle   = 0f;
    private float _spiralTimer   = 0f;
    private bool  _isShooting    = false;
    private int   _spiralCount   = 0;
    private bool  _isTeleporting = false;

    protected override void Start()
    {
        base.Start();
        _bossName  = "Le Cerf Ancestral";
        _maxHealth = 800f;
        _moveSpeed = 4f;
    }

    protected override void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance.IsGameOver) return;

        HandleMovement();
        HandleTeleport();
        HandleSpiral();
        HandleRegen();
        CheckRage();
    }

    protected override void HandleMovement()
    {
        if (_isTeleporting) return;
        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        transform.position += direction * _moveSpeed * Time.deltaTime;
    }

    private void HandleTeleport()
    {
        _teleportTimer += Time.deltaTime;
        if (_teleportTimer >= _teleportCooldown)
        {
            _teleportTimer = 0f;
            TeleportBehindPlayer();
        }
    }

    private void TeleportBehindPlayer()
    {
        if (_playerTransform == null) return;

        Vector3 directionTowardsBoss = (transform.position - _playerTransform.position).normalized;
        Vector3 behindPlayer = _playerTransform.position + directionTowardsBoss * _teleportDistance;

        transform.position = behindPlayer;

        _isTeleporting = true;
        Invoke(nameof(StopTeleport), 0.5f);
    }

    private void StopTeleport() => _isTeleporting = false;

    private void HandleSpiral()
    {
        if (!_isShooting)
        {
            _spiralTimer += Time.deltaTime;
            if (_spiralTimer >= 1f / _fireRate)
            {
                _isShooting  = true;
                _spiralCount = 0;
                _spiralTimer = 0f;
            }
            return;
        }

        _spiralTimer += Time.deltaTime;
        if (_spiralTimer >= _spiralFireRate)
        {
            _spiralTimer = 0f;
            ShootSpiralProjectile();
            _spiralCount++;
            if (_spiralCount >= _spiralBurstCount)
                _isShooting = false;
        }
    }

    private void ShootSpiralProjectile()
    {
        if (_projectilePrefab == null) return;

        float   angleStep = 360f / _spiralBurstCount;
        Vector3 direction = Quaternion.Euler(0, _spiralAngle, 0) * Vector3.forward;

        GameObject projectileGO = Instantiate(_projectilePrefab, transform.position, Quaternion.identity);
        EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
        if (projectile != null)
            projectile.Init(direction);

        _spiralAngle += angleStep;
    }

    private void HandleRegen()
    {
        _regenTimer += Time.deltaTime;
        if (_regenTimer >= _regenCooldown)
        {
            _regenTimer    = 0f;
            _currentHealth = Mathf.Min(_currentHealth + _regenAmount, _maxHealth);
            GameUI.Instance.UpdateBossHP(_currentHealth, _maxHealth);
        }
    }

    private void CheckRage()
    {
        if (_isRaging) return;
        if (_currentHealth / _maxHealth > _rageThreshold) return;

        _isRaging         = true;
        _fireRate         *= 1.5f;
        _moveSpeed        *= 1.5f;
        _teleportCooldown *= 0.5f;
    }
}
```

## BossCorruptedSource.cs (La Source Corrompue)
```csharp
using UnityEngine;
using System.Collections;

public class BossCorruptedSource : BossBase
{
    [Header("Source — Cristaux Orbitaux")]
    [SerializeField] private GameObject _crystalPrefab;
    [SerializeField] private int        _crystalCount       = 6;
    [SerializeField] private float      _crystalOrbitRadius = 4f;
    [SerializeField] private float      _crystalOrbitSpeed  = 90f;
    [SerializeField] private float      _crystalFireRate    = 0.5f;

    [Header("Source — Vague de Ralentissement")]
    [SerializeField] private float _slowWaveCooldown = 10f;
    [SerializeField] private float _slowDuration     = 3f;
    [SerializeField] private float _slowMultiplier   = 0.4f;
    [SerializeField] private float _slowWaveRadius   = 8f;

    [Header("Source — Invocation")]
    [SerializeField] private GameObject _miniBoss1Prefab;
    [SerializeField] private GameObject _miniBoss2Prefab;
    [SerializeField] private float      _summonCooldown = 25f;

    [Header("Source — Implosion")]
    [SerializeField] private float _implosionCooldown  = 20f;
    [SerializeField] private float _implosionPullForce = 15f;
    [SerializeField] private float _implosionRadius    = 10f;
    [SerializeField] private float _implosionDamage    = 40f;

    [Header("Phase 2")]
    [SerializeField] private float _phase2Threshold = 0.5f;

    private float _slowWaveTimer    = 0f;
    private float _summonTimer      = 0f;
    private float _implosionTimer   = 0f;
    private float _crystalAngle     = 0f;
    private float _crystalFireTimer = 0f;

    private bool _isPhase2          = false;
    private bool _isImplosionActive = false;
    private bool _lastSummonWasBoss1 = false;

    private Vector3 _wanderDirection      = Vector3.zero;
    private float   _wanderTimer          = 0f;
    private float   _wanderChangeCooldown = 2f;

    private GameObject[] _crystals;

    protected override void Start()
    {
        base.Start();
        _bossName      = "La Source Corrompue";
        _maxHealth     = 1200f;
        _moveSpeed     = 0f;
        _currentHealth = _maxHealth;
        SpawnCrystals();
    }

    protected override void Update()
    {
        if (_playerTransform == null) return;
        if (GameManager.Instance.IsGameOver) return;

        HandleMovement();
        UpdateWander();
        HandleCrystalOrbit();
        HandleCrystalShooting();
        HandleSlowWave();
        HandleSummon();
        HandleImplosion();
        CheckPhase2();
    }

    protected override void HandleMovement()
    {
        if (!_isPhase2) return;
        transform.position += _wanderDirection * _moveSpeed * Time.deltaTime;
    }

    private void SpawnCrystals()
    {
        if (_crystalPrefab == null) return;
        _crystals = new GameObject[_crystalCount];
        for (int i = 0; i < _crystalCount; i++)
        {
            float angle = (360f / _crystalCount) * i * Mathf.Deg2Rad;
            Vector3 pos = transform.position + new Vector3(
                Mathf.Cos(angle) * _crystalOrbitRadius, 0f,
                Mathf.Sin(angle) * _crystalOrbitRadius);
            _crystals[i] = Instantiate(_crystalPrefab, pos, Quaternion.identity);
            _crystals[i].transform.SetParent(transform);
        }
    }

    private void HandleCrystalOrbit()
    {
        if (_crystals == null) return;
        _crystalAngle += _crystalOrbitSpeed * Time.deltaTime;
        float angleStep = 360f / _crystalCount;
        for (int i = 0; i < _crystals.Length; i++)
        {
            if (_crystals[i] == null) continue;
            float angle = (_crystalAngle + angleStep * i) * Mathf.Deg2Rad;
            _crystals[i].transform.localPosition = new Vector3(
                Mathf.Cos(angle) * _crystalOrbitRadius, 0f,
                Mathf.Sin(angle) * _crystalOrbitRadius);
        }
    }

    private void HandleCrystalShooting()
    {
        if (_crystals == null) return;
        _crystalFireTimer += Time.deltaTime;
        if (_crystalFireTimer < 1f / _crystalFireRate) return;
        _crystalFireTimer = 0f;
        if (_projectilePrefab == null) return;

        Vector3 dirToPlayer = (_playerTransform.position - transform.position).normalized;
        foreach (GameObject crystal in _crystals)
        {
            if (crystal == null) continue;
            GameObject projectileGO = Instantiate(_projectilePrefab, crystal.transform.position, Quaternion.identity);
            EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
            if (projectile != null) projectile.Init(dirToPlayer);
        }
    }

    private void HandleSlowWave()
    {
        _slowWaveTimer += Time.deltaTime;
        if (_slowWaveTimer < _slowWaveCooldown) return;
        _slowWaveTimer = 0f;

        Collider[] hits = Physics.OverlapSphere(transform.position, _slowWaveRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerController player = hit.GetComponent<PlayerController>();
                if (player != null) StartCoroutine(SlowPlayer(player));
            }
        }
    }

    private IEnumerator SlowPlayer(PlayerController player)
    {
        player.SetSpeedMultiplier(_slowMultiplier);
        yield return new WaitForSeconds(_slowDuration);
        player.SetSpeedMultiplier(1f);
    }

    private void HandleSummon()
    {
        _summonTimer += Time.deltaTime;
        if (_summonTimer < _summonCooldown) return;
        _summonTimer = 0f;

        // Alternance stricte Boss1 → Boss2 → Boss1 → Boss2
        bool spawnBoss1 = !_lastSummonWasBoss1;
        _lastSummonWasBoss1 = spawnBoss1;

        GameObject prefabToSpawn = spawnBoss1 ? _miniBoss1Prefab : _miniBoss2Prefab;
        if (prefabToSpawn == null) return;

        // Spawn loin du joueur
        Vector3 playerPos      = _playerTransform.position;
        Vector3 awayFromPlayer = (transform.position - playerPos).normalized;
        Vector3 spawnPos       = playerPos + awayFromPlayer * 10f;

        GameObject mini = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        BossBase boss = mini.GetComponent<BossBase>();
        if (boss != null)
        {
            boss.IsSummoned = true;
            StartCoroutine(InitSummonedBoss(boss, 0.3f));
        }
    }

    private IEnumerator InitSummonedBoss(BossBase boss, float percent)
    {
        yield return null;
        if (boss != null)
        {
            boss.InitWithReducedHP(percent);
            boss.transform.localScale = Vector3.one * 0.6f;
            boss.SetXPValue(boss.MaxHealth * 0.3f);
        }
    }

    private void HandleImplosion()
    {
        if (_isImplosionActive) return;
        _implosionTimer += Time.deltaTime;
        if (_implosionTimer < _implosionCooldown) return;
        _implosionTimer = 0f;
        StartCoroutine(ImplosionSequence());
    }

    private IEnumerator ImplosionSequence()
    {
        _isImplosionActive = true;
        float pullDuration = 1.5f;
        float elapsed      = 0f;
        float minDistance  = 3f;

        while (elapsed < pullDuration)
        {
            elapsed += Time.deltaTime;
            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(_playerTransform.position, transform.position);
                if (dist > minDistance)
                {
                    PlayerController player = _playerTransform.GetComponent<PlayerController>();
                    if (player == null || !player.IsDashing)
                    {
                        Vector3 pullDir = (transform.position - _playerTransform.position).normalized;
                        _playerTransform.GetComponent<Rigidbody>().MovePosition(
                            _playerTransform.position + pullDir * _implosionPullForce * Time.deltaTime);
                    }
                }
            }
            yield return null;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, _implosionRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                HealthSystem health = hit.GetComponent<HealthSystem>();
                if (health != null) health.TakeDamage(_implosionDamage);
            }
        }

        _isImplosionActive = false;
    }

    private void CheckPhase2()
    {
        if (_isPhase2) return;
        if (_currentHealth / _maxHealth > _phase2Threshold) return;

        _isPhase2         = true;
        _moveSpeed        = 3f;
        _crystalFireRate  *= 2f;
        _slowWaveCooldown *= 0.5f;
    }

    private void UpdateWander()
    {
        if (!_isPhase2) return;
        _wanderTimer += Time.deltaTime;
        if (_wanderTimer >= _wanderChangeCooldown)
        {
            _wanderTimer = 0f;
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            _wanderDirection  = new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle));
        }
    }

    protected override void Die()
    {
        if (_crystals != null)
            foreach (GameObject crystal in _crystals)
                if (crystal != null) Destroy(crystal);
        base.Die();
    }
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
    public bool IsPaused   { get; private set; } = false;

    private float _runTimer  = 0f;
    private int   _killCount = 0;
    public int   KillCount => _killCount;
    public float RunTimer  => _runTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (_isGameOver) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (LevelUpManager.Instance != null && LevelUpManager.Instance.IsWaitingForChoice) return;
            TogglePause();
        }

        if (IsPaused) return;
        if (WaveManager.Instance != null && WaveManager.Instance.BossAlive) return; // Timer pausé pendant boss

        _runTimer += Time.deltaTime;
    }

    public void TogglePause()
    {
        IsPaused       = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
        GameUI.Instance.ShowPausePanel(IsPaused);
    }

    public void ResumePause()
    {
        IsPaused       = false;
        Time.timeScale = 1f;
        GameUI.Instance.ShowPausePanel(false);
    }

    public void AbandonRun()
    {
        IsPaused       = false;
        Time.timeScale = 1f;
        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount);
        SceneManager.LoadScene(0);
    }

    public void AddKill()
    {
        _killCount++;
        if (GameUI.Instance != null)
            GameUI.Instance.UpdateKillCount(_killCount);
    }

    public void TriggerGameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        Invoke(nameof(ShowGameOver), 1.5f);
    }

    private void ShowGameOver()
    {
        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount);
        GameUI.Instance.ShowGameOver(_runTimer, _killCount, MetaProgressionManager.Instance.RunGold);
    }

    public void RestartGame()  { Time.timeScale = 1f; SceneManager.LoadScene(1); }
    public void GoToMainMenu() { Time.timeScale = 1f; SceneManager.LoadScene(0); }

    public void TriggerVictory()
    {
        if (_isGameOver) return;
        _isGameOver = true;
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
    public float BossSpawnInterval = 300f; // 5 minutes — mettre à 5 pour les tests

    [Header("Boss")]
    [SerializeField] private GameObject _bossPrefab1;
    [SerializeField] private GameObject _bossPrefab2;
    [SerializeField] private GameObject _bossPrefab3;

    [Header("Limite ennemis")]
    [SerializeField] private int _maxEnemiesOnScreen = 15;

    private int  _bossCount = 0;
    private bool _bossAlive = false;

    public int  CurrentWave => _bossCount + 1;
    public bool BossAlive   => _bossAlive;

    private EnemySpawner _enemySpawner;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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
        if (GameManager.Instance.IsPaused) return;
        if (_bossAlive) return;

        float runTimer = GameManager.Instance.RunTimer;

        ApplyDifficulty();

        if (_bossCount == 0 && runTimer >= BossSpawnInterval) SpawnBoss(1);
        if (_bossCount == 1 && runTimer >= BossSpawnInterval * 2f) SpawnBoss(2);
        if (_bossCount == 2 && runTimer >= BossSpawnInterval * 3f) SpawnBoss(3);

        GameUI.Instance.UpdateTimer(runTimer);
    }

    private void ApplyDifficulty()
    {
        if (_enemySpawner == null) return;
        float minutes = GameManager.Instance.RunTimer / 60f;

        if      (minutes < 3f)  { _enemySpawner.SetSpawnInterval(3f);   _maxEnemiesOnScreen = 15; }
        else if (minutes < 5f)  { _enemySpawner.SetSpawnInterval(2f);   _maxEnemiesOnScreen = 25; }
        else if (minutes < 8f)  { _enemySpawner.SetSpawnInterval(1.5f); _maxEnemiesOnScreen = 30; }
        else if (minutes < 10f) { _enemySpawner.SetSpawnInterval(1f);   _maxEnemiesOnScreen = 40; }
        else if (minutes < 13f) { _enemySpawner.SetSpawnInterval(0.8f); _maxEnemiesOnScreen = 50; }
        else                    { _enemySpawner.SetSpawnInterval(0.6f); _maxEnemiesOnScreen = 60; }

        _enemySpawner.SetMaxEnemies(_maxEnemiesOnScreen);
    }

    private void SpawnBoss(int bossNumber)
    {
        _bossCount++;
        _bossAlive = true;

        ClearAllEnemies();
        _enemySpawner.gameObject.SetActive(false);

        GameObject player   = GameObject.FindWithTag("Player");
        Vector3    spawnPos = player.transform.position + new Vector3(10f, 0f, 0f);

        GameObject bossPrefab = bossNumber == 1 ? _bossPrefab1 :
                                bossNumber == 2 ? _bossPrefab2 : _bossPrefab3;

        if (bossPrefab != null)
            Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        else
            Debug.LogWarning($"Boss {bossNumber} prefab non assigné !");
    }

    private void ClearAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            EnemyBase eb = enemy.GetComponent<EnemyBase>();
            if (eb != null) ObjectPool.Instance.ReturnToPool(GetPoolTag(enemy), enemy);
            else Destroy(enemy);
        }
    }

    private string GetPoolTag(GameObject enemy)
    {
        if (enemy.GetComponent<EnemyShooter>() != null) return "EnemyShooter";
        if (enemy.GetComponent<EnemyTank>()    != null) return "EnemyTank";
        return "Enemy";
    }

    public void OnBossDied()
    {
        _bossAlive = false;
        _enemySpawner.gameObject.SetActive(true);

        if (_bossCount >= 3)
            GameManager.Instance.TriggerVictory();
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
    private List<string>      _chosenUpgrades  = new List<string>();
    private int               _pendingLevelUps = 0;
    private bool              _waitingForChoice = false;
    private float             _delayTimer       = 0f;
    private bool              _showingDelay     = false;

    public bool IsWaitingForChoice => _waitingForChoice;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (_showingDelay)
        {
            _delayTimer -= Time.unscaledDeltaTime;
            if (_delayTimer <= 0f) { _showingDelay = false; DisplayLevelUp(); }
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
        if (_waitingForChoice || _showingDelay) return;
        DisplayLevelUp();
    }

    private void DisplayLevelUp()
    {
        if (_pendingLevelUps <= 0) return;
        _pendingLevelUps--;
        _waitingForChoice = true;
        Time.timeScale    = 0f;
        _currentChoices   = GetRandomUpgrades(3);
        if (_levelUpPanel != null) _levelUpPanel.SetActive(true);
        _upgradeUI.DisplayUpgrades(_currentChoices);
    }

    public void SelectUpgrade(int index)
    {
        if (!_waitingForChoice) return;
        if (index < 0 || index >= _currentChoices.Count) return;

        _waitingForChoice = false;
        UpgradeData chosen = _currentChoices[index];
        chosen.Apply();
        _chosenUpgrades.Add(chosen.upgradeName);

        if (_levelUpPanel != null) _levelUpPanel.SetActive(false);

        HealthSystem health = FindObjectOfType<HealthSystem>();
        if (health != null) health.SetInvincible();

        if (_pendingLevelUps > 0) { _showingDelay = true; _delayTimer = 0.4f; }
        else Time.timeScale = 1f;
    }

    public string GetUpgradesSummary()
    {
        if (_chosenUpgrades.Count == 0) return "Aucune upgrade pour l'instant.";

        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (string name in _chosenUpgrades)
        {
            if (counts.ContainsKey(name)) counts[name]++;
            else counts[name] = 1;
        }

        string summary = "";
        foreach (var kvp in counts)
            summary += kvp.Value > 1 ? $"• {kvp.Key} x{kvp.Value}\n" : $"• {kvp.Key}\n";

        return summary.TrimEnd();
    }

    private List<UpgradeData> GetRandomUpgrades(int count)
    {
        List<UpgradeData> pool = new List<UpgradeData>();
        foreach (UpgradeData upgrade in _allUpgrades)
            if (upgrade.IsAvailable()) pool.Add(upgrade);

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

## MetaProgressionManager.cs
```csharp
using UnityEngine;

public class MetaProgressionManager : MonoBehaviour
{
    public static MetaProgressionManager Instance { get; private set; }

    public SaveData Data    { get; private set; }
    public int      RunGold { get; private set; } = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Data = SaveSystem.Load();
    }

    public void AddRunGold(int amount)
    {
        RunGold += amount;
        if (GameUI.Instance != null) GameUI.Instance.UpdateGold(RunGold);
    }

    public void SaveRunResults(float runTime, int kills)
    {
        Data.totalRuns++;
        Data.totalGold += RunGold;
        if (runTime > Data.bestTime)  Data.bestTime  = runTime;
        if (kills   > Data.bestKills) Data.bestKills = kills;
        SaveSystem.Save(Data);
    }

    public float GetBonusMaxHP()  => Data.hpUpgradeLevel    * 0.1f;
    public float GetBonusDamage() => Data.damageUpgradeLevel * 0.05f;
    public float GetBonusXP()     => Data.xpUpgradeLevel     * 0.1f;

    public bool BuyUpgrade(string upgradeType, int cost)
    {
        if (Data.totalGold < cost) return false;
        Data.totalGold -= cost;
        switch (upgradeType)
        {
            case "hp":     Data.hpUpgradeLevel++;     break;
            case "damage": Data.damageUpgradeLevel++;  break;
            case "xp":     Data.xpUpgradeLevel++;      break;
        }
        SaveSystem.Save(Data);
        return true;
    }
}
```

---

# 6. TOUCHES ET CONTRÔLES

| Touche | Action |
|---|---|
| ZQSD / Flèches | Déplacement |
| Shift gauche | Dash (absorbe projectiles pendant 0.3s) |
| F | Déclencher l'ultime cristal (quand jauge pleine) |
| A | Réduire la range des orbitaux |
| E | Augmenter la range des orbitaux |
| ESC | Pause / Dépause |
| 1 / 2 / 3 | Sélectionner upgrade au level up |
| Clic souris gauche | Sélectionner upgrade au level up (via boutons UI) |

---

# 7. DÉCISIONS TECHNIQUES IMPORTANTES

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
| HP ennemis reset | OnEnable() pas Awake() | Awake ne se rappelle pas à la réactivation depuis le pool |
| Timer de run | GameManager._runTimer uniquement | WaveManager utilisait son propre timer — source de bugs |
| Timer pausé boss | WaveManager.BossAlive → pause GameManager | Le temps ne compte pas pendant les boss |
| Dégâts projectiles ennemis | Gérés par HealthSystem.OnTriggerEnter | Évite le double déclenchement |
| Boss héritage | protected/virtual dans BossBase | Permet aux enfants d'override sans tout réécrire |
| IsSummoned | bool public dans BossBase | Empêche les mini-boss d'affecter l'UI et le compteur de boss |
| Ordre Hierarchy Canvas | LevelUpPanel en bas = reçoit les clics | L'élément le plus bas dans le Canvas est au-dessus visuellement |

---

# 8. GAME DESIGN — VISION COMPLÈTE

## Philosophie
- **Finir une zone parfaite** avant d'en faire d'autres
- **Skill gap** via le dash absorbeur — c'est l'identité du jeu, unique dans le genre
- **Addiction** via meta-progression + records + écran de fin satisfaisant
- **Jeu PC assumé** — complexité intentionnelle, pas un port mobile
- **3D exploitée mécaniquement** — pas juste visuelle (relief de map à venir)
- **Ennemis majoritairement corps à corps** — les shooters restent minoritaires

## Ce qui crée l'addiction (à implémenter par priorité)
1. **Meta-progression + shop** — sans ça le joueur n'a pas de raison de relancer
2. **Nouvelles armes** — plus de builds différents = plus de rejouabilité
3. **Écran de fin de run satisfaisant** — score, records, build final affiché
4. **Drop d'XP au sol** — gemmes à ramasser, crée tension de positionnement + satisfaction visuelle
5. **Records personnels** — "Ton meilleur temps : 08:42" — envie de battre son record

## Nouvelles armes validées (à implémenter)
1. **Nova de Cristal** ✅ FAIT — explosion visible à chaque absorption
2. **Chaîne de Foudre** — touche un ennemi et se propage aux voisins (parfait contre vagues)
3. **Bouclier de Mana** — absorbe automatiquement un projectile toutes les X secondes (passif)
4. **Tir dirigé à la souris** — option dans les Settings ON/OFF, avec curseur visible en jeu

## Équilibre ennemis
- Majorité : corps à corps (Loup de Mana, Golem)
- Minorité : shooters (Bulbe Cracheur)
- Plus d'ennemis en même temps = côté satisfaisant "bordel organisé" comme Vampire Survivors
- À faire : augmenter spawns corps à corps, réduire proportion shooters dans EnemySpawner

---

# 9. CAMÉRA — AMÉLIORATIONS VALIDÉES (À IMPLÉMENTER)

- **Cinemachine avec damping** — effet élastique fluide, lisse les micro-mouvements
- **Look Ahead** — caméra se décale légèrement vers la direction de déplacement du joueur pour anticiper les ennemis
- **Zoom arrière progressif** — voir plus d'ennemis à la fois (attention : ne pas trop zoomer, sinon perte de lisibilité)
- **Spawn ennemis plus loin** — augmenter _spawnRadius dans EnemySpawner en conséquence

---

# 10. MENU PRINCIPAL — SCÈNE MAINMENU

## Design validé
- **Fond** : Image de la Plaine (style anime, générée avec Leonardo.ai)
- **Logo** : AETHER avec cristal bleu (fond transparent via remove.bg/Photopea)
- **Boutons** : Style parchemin avec bordures dorées
- **Icônes** : Pièce d'or et gemme bleue en haut à gauche
- **Onglets** : UPGRADES | MENU | PARAMÈTRES en haut

## Settings — à implémenter
- Tir automatique ON/OFF (par défaut ON)
- Volume musique (slider)
- Volume SFX (slider)
- Plein écran ON/OFF

---

# 11. TODO LIST — ORDRE DE PRIORITÉ

## Priorité immédiate (gameplay)
- [ ] Caméra Cinemachine avec damping + Look Ahead
- [ ] Spawn ennemis plus loin (augmenter _spawnRadius)
- [ ] Plus d'ennemis corps à corps (modifier proportions dans EnemySpawner)
- [ ] Murs invisibles circulaires (colliders en bordure de map)
- [ ] Settings (tir auto ON/OFF, volume, plein écran)

## Nouvelles armes
- [ ] Chaîne de Foudre
- [ ] Bouclier de Mana
- [ ] Tir à la souris (option Settings avec curseur visible)

## Système de progression
- [ ] Drop d'XP au sol à ramasser (gemmes)
- [ ] Écran de victoire complet (15min, 3 boss vaincus)
- [ ] Écran de fin de run satisfaisant (score, records, build final)
- [ ] Records personnels affichés à chaque mort
- [ ] Meta-progression : shop avec upgrades permanentes (HP Max, Dégâts, XP)
- [ ] Système de coffres (Simple/Rare/Légendaire)
- [ ] Déblocage de personnages à la fin de chaque niveau

## Polish
- [ ] Animations VFX (ulti cristal, mort ennemis, particules)
- [ ] Screen shake (Cinemachine Impulse)
- [ ] Musique de fond + SFX (ElevenLabs + Suno)
- [ ] Map 3D avec relief (Terrain Unity + assets Kenney.nl)
- [ ] Modèles 3D (TRELLIS/Hunyuan3D → Blender → Mixamo)
- [ ] Paramètres avancés (touches rebindables, résolution)

---

# 12. RÈGLES DE COLLABORATION IA

- Réponses structurées et honnêtes — dire non si une idée n'est pas bonne, avec arguments
- Poser des questions avant de coder pour éviter les malentendus
- Ne pas dire qu'une idée est bonne juste pour faire plaisir
- Guider au moindre clic dans Unity car l'interface est complexe pour un débutant
- Le jeu doit avoir du sens et être cohérent — pas d'idées bizarres qui casseraient le concept
- Garder en tête l'originalité et la démarquabilité vis-à-vis des grands du genre
- Toujours penser addiction, satisfaction et skill gap

---

*Document mis à jour — Projet Aether — Juin 2026*
*Cette version remplace AETHER_PROJECT_COMPLET.md*
