# AETHER — Documentation Complète du Projet V3
## Jeu Unity 2021.3.45f2 (URP) — Bullet Heaven Roguelite
## Dernière mise à jour : Juin 2026

---

# 1. CONTEXTE DU PROJET

- **Développeur** : Solo (1 personne, débutant complet en Unity/C#), doit être guidé au moindre clic dans l'interface Unity
- **Moteur** : Unity 2021.3.45f2 LTS
- **Pipeline** : Universal Render Pipeline (URP) — Universal 3D
- **Plateforme cible** : PC Windows
- **Distribution** : itch.io (WebGL) → Steam à terme
- **Inspiration** : Vampire Survivors, Brotato, Mushoku Tensei (DA)
- **Repository GitHub** : https://github.com/FrostyCRT/Aether
- **Méthode de travail** : Script par script avec explications détaillées
- **IA assistante** : Claude — répond de façon structurée, donne son avis honnête même si ça déplaît, pose des questions pour se mettre d'accord, ne dit pas oui à tout, qualité maximale visée peu importe le temps nécessaire

---

# 2. CONCEPT DU JEU

**Nom : AETHER** (anciennement "NEXUS" — changé car trop sci-fi)

Un Bullet Heaven / Auto-shooter roguelite top-down en 3D, caméra vue isométrique à 65°.

## La boucle de jeu
```
Survive → Tue des ennemis → Gemmes XP au sol → Ramassage → Niveau up → Choix d'upgrade → Build explose → Mort/Victoire → Meta-progression → Retry
```

## Ce qui différencie Aether des autres jeux du genre
1. **Le Cristal + absorption de projectiles pendant le dash** — mécanique signature unique
2. **Le dash offensif** — identité PC, traverser des projectiles pour les absorber
3. **L'univers Anime Fantasy style Ghibli/Mushoku Tensei** — rare dans le genre
4. **Les orbitaux avec contrôle de range** — touches A/E pour réduire/augmenter la range en temps réel
5. **Nova de Cristal** — explosion visible à chaque absorption de projectile, reset le dash si elle tue un ennemi
6. **Jeu 100% PC** — complexité intentionnelle pour se démarquer des versions mobiles du genre
7. **Arbre de compétences meta-progression** — rare dans le genre bullet heaven, structure en losange par branche
8. **3D exploitée mécaniquement** — relief de map, pas juste visuel (à venir)
9. **Personnages jouables asymétriques** — prévu, change radicalement le style de jeu (Kael, Lyra)

---

# 3. DIRECTION ARTISTIQUE — AETHER

## Style visuel
- **Anime Toon-Shading** type Ghibli / Mushoku Tensei
- **Low-Poly** (~2000 polygones par modèle ennemi, 2500 pour le joueur)
- **Caméra** : Vue top-down inclinée à 65°, projection Orthographique
- **Shading** : URP Toon Shader (Cel-shading)
- **Post-Processing** : Bloom, Color Grading saturé, éclairage chaleureux
- **Ombres** : désactivées (Directional Light → Shadow Type → No Shadows) suite aux retours d'utilisateurs trouvant les ombres sur joueur/ennemis/projectiles trop nombreuses et créant une impression de "double". Option à terme dans les Settings.

## Pipeline Assets validé (100% gratuit, complété par des outils IA payants ponctuels)
- **Modèles 3D** : IA générative locale (TRELLIS / Hunyuan3D) → Blender (.fbx) → Mixamo (rigging automatique)
- **Concepts 2D et UI** : Leonardo.ai / Pippit AI / Microsoft Designer
- **Son et musique style Ghibli** : ElevenLabs + Suno + Audacity
- **Retouche images** : Photopea / remove.bg / Paint (suppression manuelle de watermark si besoin)
- **Assets 3D gratuits map** : Kenney.nl
- **Polices** : Google Fonts (Bangers, Oswald recommandées)
- **Meshy AI (10€/mois)** : à prendre quand le gameplay est finalisé — alternative payante pour les modèles 3D

## Leçon apprise sur la génération d'assets UI
Les fonds trop détaillés (textures de pierre/runes complexes, paysages avec reflets nets) nuisent à la lisibilité des éléments d'interface qui doivent ressortir par-dessus (nœuds, médaillons, boutons). Un fond atmosphérique/flou, ou un élément central fort qui structure naturellement la composition (comme un grand arbre Ghibli), fonctionne mieux qu'un fond générique même magnifique. Toujours tester avec les vrais éléments UI dessus avant de valider un fond.

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

### Loup de Mana (Ennemi Basic — corps à corps) — 65% des spawns
- Pelage gris/bleu foncé avec marques naturelles plus claires
- Crête de poils rigides le long du dos bleutée
- Yeux bleu électrique intenses (seul élément lumineux)
- Muscles des pattes avant surdéveloppés, griffes bleu/gris métallique
- 1000-1200 polygones
- Gold dropé : 1

### Golem de Tronc (Tank — corps à corps lent) — 15% des spawns
- Corps humanoïde massif fait de troncs d'arbres torsadés
- Bras qui traînent presque jusqu'au sol
- Pas de visage — deux cavités sombres avec yeux orangés
- Racines sortant des pieds et épaules
- 1200-1500 polygones
- Gold dropé : 5

### Bulbe Cracheur (Shooter — à distance) — 20% des spawns
- Corps central : bulbe floral géant fermé, vert/violet
- S'ouvre comme fleur carnivore quand il tire
- 4-6 racines épaisses comme pattes (légèrement translucides)
- Épines sur le bord du bulbe
- 1000-1200 polygones
- Gold dropé : 3

**NOTE IMPORTANTE** : proportions ajustées en cours de développement pour favoriser le combat rapproché façon Vampire Survivors plutôt que le bullet hell pur — sensation de "bordel satisfaisant" avec beaucoup d'ennemis au contact plutôt que de l'esquive pure de projectiles.

## Boss — Map 1 (Plaine)

### Boss 1 — Le Sanglier de Mana (5min) ✅ IMPLÉMENTÉ ET STABLE
- HP : 500, Gold dropé : 40
- Recouvert de mousses et lignes de mana lumineuses
- Défenses en cristal de mana bleu
- **Attaques** :
  - Charge en ligne droite vers le joueur
  - Tir en éventail de 8 projectiles de mana
  - Piétinement — zone AOE autour de lui

### Boss 2 — Le Cerf Ancestral (10min) ✅ IMPLÉMENTÉ ET STABLE
- HP : 800, Gold dropé : 70
- Ramure dorée/bleue avec lignes de mana lumineuses
- **Attaques validées et corrigées** :
  - **Téléportation** derrière le joueur (toutes les 8s, distance 3 unités) — freeze de 0.5s après téléportation pour laisser une fenêtre de réaction (corrigé : avant le fix le boss repartait instantanément vers le joueur, le rendant impossible à fuir même au dash)
  - **Spirale** de 24 projectiles tirés en rafale progressive
  - **Régénération** — récupère 100HP toutes les 30 secondes (force le joueur à être agressif)
  - **Rage à 30% HP** — fire rate x1.5, vitesse x1.5, cooldown téléportation /2
  - **RageDisabled** — propriété ajoutée pour empêcher la rage instantanée quand ce boss est invoqué affaibli par le Boss 3

### Boss 3 — La Source Corrompue (15min) ✅ IMPLÉMENTÉ ET STABLE
- HP : 1200, Gold dropé : 120
- Masse cristalline bleue/noire flottante — **FIXE en Phase 1**
- **Attaques corrigées et finalisées** :
  - **Cristaux orbitaux** — 6 cristaux tournent et tirent des rafales vers le joueur (fire rate : 0.5). **Chaque cristal vise désormais le joueur depuis SA PROPRE position** (et non plus depuis le centre du boss), rendant les tirs beaucoup plus précis et dangereux, empêchant de camper proche du boss sans risque
  - **Vague de ralentissement** — ralentit le joueur à 40% de sa vitesse pendant 3s (toutes les 10s)
  - **Invocation** — toutes les 25s, invoque 1 mini-boss en **alternance stricte** (Sanglier puis Cerf puis Sanglier...) à 30% HP, 60% de taille, spawné à 10 unités du joueur dans la direction opposée au boss (jamais sur le joueur). Le mini-boss : n'affiche pas de barre de vie, ne déclenche pas OnBossDied, ne régénère pas le joueur à sa mort, a sa rage désactivée (RageDisabled), donne 30% de son XP normal
  - **Implosion** — toutes les 20s, aspire le joueur pendant 1.5s puis AOE 40 dégâts. Le dash annule l'aspiration. Distance minimale de 3 unités pour éviter le spawn kill. **Zone d'avertissement visuelle rouge qui grandit progressivement** pendant l'aspiration et suit la position du boss, supprimée si le boss meurt pendant l'attaque. Le boss est **immobile** pendant toute la durée de cette attaque (corrigé : avant il pouvait se déplacer en Phase 2 pendant l'implosion, désynchronisant la zone visuelle de sa vraie position)
  - **Phase 2 à 50% HP** — se met à bouger aléatoirement (vitesse 3, change direction toutes les 2s), cristaux tirent 2x plus vite, vague de ralentissement 2x plus fréquente

## Progression des Maps
1. **Plaine** — Zone de départ, lumineuse et verdoyante ← ON EST ICI, priorité absolue de finalisation
2. **Forêt mystique** — Arbres géants, champignons lumineux
3. **Désert magique**
4. **Montagne enneigée**
5. **Jungle**
6. **Volcan**

**STRATÉGIE CONFIRMÉE** : Finir une zone parfaite et addictive avant d'en faire d'autres. Le code de la Map 1 sera réutilisable à 80% pour les maps suivantes — c'est le CONTENU (nouveaux ennemis, boss, assets, équilibrage) qui prend du temps, pas la technique de base.

---

# 4. CONFIGURATION UNITY

## Paramètres importants
- **Caméra** : Orthographique, Rotation X: 65, Y: 0, Z: 0
- **Cinemachine** : PlayerFollowCam, Body: Framing Transposer
  - X/Z Damping : 0.15
  - Lookahead Time : 0.1, Lookahead Smoothing : 5, Lookahead Ignore Y : coché
  - Screen X/Y : 0.5, Dead Zone : 0, Soft Zone : 0.8
  - Ortho Size : 10-12 (zoom arrière pour voir plus d'ennemis)
- **Physics Layer** : Layer "Player" et "Enemy" créés
- **Collision Matrix** : Player/Enemy — Trigger OK, physique séparée
- **Scenes** : 0 = MainMenu, 1 = Game (dans Build Settings)
- **Directional Light** : Shadow Type = No Shadows (décision DA)
- **Canvas Scaler** (sur tous les Canvas, Menu et Game) : UI Scale Mode = Scale With Screen Size, Reference Resolution 1920x1080, Screen Match Mode = Match Width Or Height, Match = 0.5
- **Fenêtre Game** : réglée en 1920x1080 (16:9) pour correspondre exactement à la vue Scene lors du placement d'éléments UI

## Hierarchy de la scène Game
```
Main Camera
Directional Light (No Shadows)
PlayerFollowCam (Cinemachine Virtual Camera)
Player
  └── PulseVisual (désactivé par défaut)
Ground (Plane, Scale: 3,1,3 — pas de mur invisible, le joueur ne tombe pas car pas de gravité)
EnemySpawner (spawnRadius: 15, proportions 65% Enemy / 20% Shooter / 15% Tank)
XPGemSpawner (gère le drop et l'attraction des gemmes XP)
ObjectPool
GameManager
XPSystem
LevelUpManager
WaveManager
MetaProgressionManager (DontDestroyOnLoad)
DamageNumberSpawner
Canvas
  ├── LevelUpPanel (désactivé par défaut — DOIT ÊTRE TOUT EN BAS de la Hierarchy pour recevoir les clics, sinon le HUD intercepte les clics)
  │   ├── TitleText "LEVEL UP !"
  │   ├── UpgradeCard1/2/3
  │   │   ├── UpgradeName, UpgradeDescription (TMP)
  │   │   └── ChooseButton
  ├── HUD (référence directe assignée dans GameUI._hudPanel, plus de GameObject.Find)
  │   ├── XPBar, LevelText, HPBar, HPText
  │   ├── TimerText (timer unique venant de GameManager, pausé pendant les boss)
  │   ├── GoldText, KillCountText ("Kills : X")
  │   ├── DashCooldownBar
  │   ├── CrystalBar (6 Image icons)
  │   ├── BossHPBar, BossNameText, BossIcon (désactivés)
  │   └── UltOverlay (désactivé)
  ├── GameOverPanel (désactivé par défaut)
  │   ├── StatsText (Temps | Kills | Gold)
  │   ├── RetryButton → GameManager.RestartGame()
  │   └── MainMenuButton → GameManager.GoToMainMenu()
  ├── VictoryPanel (désactivé par défaut, NOUVEAU)
  │   ├── TitleText "VICTOIRE !"
  │   ├── SubtitleText "La Plaine est libérée !"
  │   ├── StatsText (Temps/Kills/Niveau/Gold de la run)
  │   ├── RecordsText (Meilleur temps/kills, runs totales)
  │   ├── BuildTitleText "Build :" (centré) + BuildListText (aligné gauche, liste des upgrades)
  │   ├── RetryButton, MainMenuButton
  └── PausePanel (désactivé par défaut)
      ├── StatsText, UpgradesText (vide si 0 upgrade, pas de message)
      ├── ResumeButton → GameManager.ResumePause()
      ├── AbandonButton → GameUI.ShowAbandonConfirm(true)
      └── AbandonConfirmPanel (désactivé par défaut)
EventSystem
```

**RÈGLE CRITIQUE CONFIRMÉE PLUSIEURS FOIS** : Dans le Canvas, l'ordre de la Hierarchy détermine l'ordre de rendu ET la priorité des clics. Ce qui est en BAS = au-dessus visuellement et reçoit les clics en premier. C'est la cause de plusieurs bugs résolus cette session (LevelUpPanel ne recevant pas les clics).

## Hierarchy de la scène MainMenu (mise à jour)
```
Canvas (Canvas Scaler configuré comme ci-dessus)
  ├── TabBar (Top/Stretch, Height 80) — boutons UPGRADES / MENU / SETTINGS
  ├── MenuPanel
  ├── UpgradesPanel (= l'Arbre de Compétences, voir section dédiée)
  └── SettingsPanel (à construire)
```

## ObjectPool — Pools configurés
| Tag | Prefab | Size |
|---|---|---|
| Enemy | PFB_EnemyBasic | 30 |
| Projectile | Projectile | 50 |
| EnemyTank | PFB_EnemyTank | 10 |
| EnemyShooter | PFB_EnemyShooter | 10 |

## ScriptableObjects Upgrades de RUN (dans LevelUpManager — distinct de la meta-progression)
| SO | Type | Value |
|---|---|---|
| SO_UpgradeDamage | Damage | 0.2 |
| SO_UpgradeFireRate | FireRate | 0.2 |
| SO_UpgradeMoveSpeed | MoveSpeed | 0.15 |
| SO_UpgradeHeal | Heal | 0.2 |
| SO_UnlockAOE | UnlockAOE | 0 |
| SO_UnlockOrbital | UnlockOrbital | 0 |
| SO_AddOrbital | AddOrbital | 0 (max 3, vérifié via IsMaxOrbital()) |
| SO_AOERadius | AOERadius | 0.2 |
| SO_DoubleShot | DoubleShot | 0 |
| SO_UnlockLightning | UnlockLightning | 0 |
| SO_AddLightningChain | AddLightningChain | 0 (max 3 upgrades) |

---

# 5. SCRIPTS C# — ÉTAT ACTUEL (CHANGEMENTS DE CETTE SESSION UNIQUEMENT)

**Note** : pour les scripts qui n'ont pas changé depuis la dernière sauvegarde (PlayerController de base, HealthSystem de base, WeaponBase de base, BossBase, BossDeer, etc.), se référer au document précédent. Voici uniquement ce qui a évolué ou ce qui est nouveau.

## EnemySpawner.cs — Version finale avec anti-chevauchement
```csharp
using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject _enemyPrefab;
    [SerializeField] private Transform  _playerTransform;

    [Header("Paramètres de spawn")]
    [SerializeField] private float _spawnInterval  = 2f;
    [SerializeField] private float _spawnRadius    = 15f;
    [SerializeField] private int   _enemiesPerWave = 1;

    [Header("Difficulté croissante")]
    [SerializeField] private float _difficultyInterval = 10f;
    [SerializeField] private int   _enemiesIncrement   = 1;

    private float _spawnTimer      = 0f;
    private float _difficultyTimer = 0f;
    private int   _maxEnemies      = 15;
    private List<Vector3> _recentSpawnPositions = new List<Vector3>();

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= _spawnInterval)
        {
            SpawnWave();
            _spawnTimer = 0f;
        }

        _difficultyTimer += Time.deltaTime;
        if (_difficultyTimer >= _difficultyInterval)
        {
            _enemiesPerWave += _enemiesIncrement;
            _difficultyTimer = 0f;
        }
    }

    private void SpawnWave()
    {
        _recentSpawnPositions.Clear(); // Reset à chaque vague

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
        // Proportions : 65% corps à corps, 20% shooter, 15% tank
        float roll = Random.value;
        string tag;
        if (roll < 0.15f)      tag = "EnemyTank";
        else if (roll < 0.35f) tag = "EnemyShooter";
        else                   tag = "Enemy";

        Vector3 spawnPos = FindFreeSpawnPosition();
        if (spawnPos == Vector3.zero) return;

        _recentSpawnPositions.Add(spawnPos);
        ObjectPool.Instance.Get(tag, spawnPos, Quaternion.identity);
    }

    private Vector3 FindFreeSpawnPosition()
    {
        float minDistance = 1.5f;
        int   maxAttempts = 10;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            Vector3 candidatePos = _playerTransform.position + new Vector3(
                randomCircle.x * _spawnRadius, 0f,
                randomCircle.y * _spawnRadius);

            // Vérifie contre la physique (ennemis déjà existants)
            Collider[] nearby = Physics.OverlapSphere(candidatePos, minDistance);
            bool isFree = true;
            foreach (Collider col in nearby)
            {
                if (col.CompareTag("Enemy")) { isFree = false; break; }
            }

            // Vérifie aussi contre les spawns de cette même vague (même frame)
            foreach (Vector3 recentPos in _recentSpawnPositions)
            {
                if (Vector3.Distance(candidatePos, recentPos) < minDistance)
                {
                    isFree = false;
                    break;
                }
            }

            if (isFree) return candidatePos;
        }

        return Vector3.zero;
    }

    public float GetSpawnInterval()            => _spawnInterval;
    public void  SetSpawnInterval(float value) => _spawnInterval = value;
    public void  SetMaxEnemies(int max)         => _maxEnemies   = max;
}
```

**Bug résolu** : la vérification précédente avec `Physics.OverlapSphere` seule échouait quand plusieurs ennemis spawnaient dans la même frame, car la physique Unity n'est pas encore mise à jour pour les objets instanciés dans le frame courant. La liste `_recentSpawnPositions` comble cette lacune.

## EnemyBase.cs — Die() avec reset dash conditionnel
```csharp
public void TakeDamage(float damage, Color color = default, bool fromNova = false)
{
    _currentHealth -= damage;

    if (DamageNumberSpawner.Instance != null)
    {
        Color c = color == default ? DamageNumberSpawner.ColorProjectile : color;
        DamageNumberSpawner.Instance.Spawn(transform.position, damage, c);
    }

    if (_currentHealth <= 0)
        Die(fromNova);
}

private void Die(bool fromNova = false)
{
    // Spawn des gemmes XP au lieu d'un AddXP direct
    if (XPGemSpawner.Instance != null)
        XPGemSpawner.Instance.SpawnGems(transform.position, _xpValue);

    GameManager.Instance.AddKill();
    MetaProgressionManager.Instance.AddRunGold(_goldValue);

    // Reset du dash uniquement si tué par la Nova de Cristal
    if (fromNova)
    {
        GameObject playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            PlayerController pc = playerGO.GetComponent<PlayerController>();
            if (pc != null) pc.ResetDashCooldown();
        }
    }

    ObjectPool.Instance.ReturnToPool(_poolTag, gameObject);
}
```

**Décision importante** : `XPSystem.Instance.AddXP()` n'est plus appelé directement à la mort d'un ennemi. C'est maintenant `XPGemSpawner.SpawnGems()` qui fait apparaître des gemmes au sol, et `AddXP()` n'est appelé qu'au moment où le joueur **ramasse** une gemme (dans `XPGem.Collect()`).

## BossBase.cs — Die() adapté pour les mini-boss invoqués
```csharp
protected virtual void Die()
{
    if (XPGemSpawner.Instance != null)
        XPGemSpawner.Instance.SpawnGems(transform.position, _xpValue);

    GameManager.Instance.AddKill();
    MetaProgressionManager.Instance.AddRunGold(_goldValue);

    if (!IsSummoned)
    {
        GameUI.Instance.HideBossHP();
        WaveManager.Instance.OnBossDied();

        HealthSystem playerHP = GameObject.FindWithTag("Player")?.GetComponent<HealthSystem>();
        if (playerHP != null)
        {
            float healAmount = playerHP.MaxHealth * 0.5f;
            playerHP.Heal(0.5f);
            if (DamageNumberSpawner.Instance != null)
                DamageNumberSpawner.Instance.Spawn(playerHP.transform.position, healAmount, Color.green);
        }
    }

    Destroy(gameObject);
}

public bool RageDisabled { get; set; } = false; // Empêche la rage instantanée des mini-boss invoqués à HP réduits
```

## XPGem.cs — NOUVEAU
```csharp
using UnityEngine;

public class XPGem : MonoBehaviour
{
    private float _xpValue = 10f;
    private bool  _attracted = false;
    private float _moveSpeed = 8f;
    private Transform _playerTransform;

    public enum GemType { Small, Medium, Large }
    private GemType _gemType;

    public void Init(GemType type, Transform player)
    {
        _gemType         = type;
        _playerTransform = player;

        switch (type)
        {
            case GemType.Small:
                _xpValue = 10f;
                transform.localScale = Vector3.one * 0.3f;
                ApplyColor(new Color(0.2f, 0.6f, 1f)); // Bleue
                break;
            case GemType.Medium:
                _xpValue = 20f;
                transform.localScale = Vector3.one * 0.4f;
                ApplyColor(new Color(0.7f, 0.2f, 1f)); // Violette
                break;
            case GemType.Large:
                _xpValue = 50f;
                transform.localScale = Vector3.one * 0.6f;
                ApplyColor(new Color(1f, 0.8f, 0.1f)); // Dorée
                break;
        }
    }

    private void ApplyColor(Color c)
    {
        Renderer r = GetComponent<Renderer>();
        r.material.color = c;
        r.material.SetColor("_EmissionColor", c * 2f);
    }

    private void Update()
    {
        if (_playerTransform == null) return;
        transform.Rotate(0f, 90f * Time.deltaTime, 0f);

        // Lit le rayon d'attraction en temps réel — fonctionne même pour les gemmes
        // spawnées avant le déblocage de l'attraction
        float currentRadius = XPGemSpawner.Instance != null ? XPGemSpawner.Instance.AttractionRadius : 0f;
        float dist = Vector3.Distance(transform.position, _playerTransform.position);

        if (dist <= currentRadius) _attracted = true;

        if (_attracted)
        {
            Vector3 dir = (_playerTransform.position - transform.position).normalized;
            transform.position += dir * _moveSpeed * Time.deltaTime;
            if (dist <= 0.5f) Collect();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) Collect();
    }

    private void Collect()
    {
        XPSystem.Instance.AddXP(_xpValue);
        Destroy(gameObject);
    }
}
```

## XPGemSpawner.cs — NOUVEAU
```csharp
using UnityEngine;
using System.Collections.Generic;

public class XPGemSpawner : MonoBehaviour
{
    public static XPGemSpawner Instance { get; private set; }

    [SerializeField] private GameObject _gemPrefab;
    private Transform _playerTransform;

    public float AttractionRadius { get; private set; } = 0f; // 0 = pas d'attraction, débloqué niveau 3

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) _playerTransform = player.transform;
    }

    public void OnLevelUp(int level)
    {
        if (level >= 3) AttractionRadius = 4f;
    }

    public void SpawnGems(Vector3 position, float xpValue)
    {
        List<XPGem.GemType> gems = CalculateGems(xpValue);
        foreach (XPGem.GemType gemType in gems)
        {
            Vector3 offset = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            GameObject gemGO = Instantiate(_gemPrefab, position + offset, Quaternion.identity);
            XPGem gem = gemGO.GetComponent<XPGem>();
            if (gem != null) gem.Init(gemType, _playerTransform);
        }
    }

    // Algorithme "rendre la monnaie" — décompose l'XP total en gemmes
    private List<XPGem.GemType> CalculateGems(float xpValue)
    {
        List<XPGem.GemType> result = new List<XPGem.GemType>();
        int remaining = Mathf.RoundToInt(xpValue);

        while (remaining >= 50) { result.Add(XPGem.GemType.Large);  remaining -= 50; }
        while (remaining >= 20) { result.Add(XPGem.GemType.Medium); remaining -= 20; }
        while (remaining >= 10) { result.Add(XPGem.GemType.Small);  remaining -= 10; }

        return result;
    }
}
```

**Exemples de décomposition** : EnemyBase (10 XP) → 1 gemme bleue. EnemyTank (30 XP) → 1 violette + 1 bleue. Boss Sanglier (200 XP) → 4 dorées. Boss Cerf (350 XP) → 7 dorées. Mini-boss invoqué (30% de 500 = 150 XP) → 3 dorées.

## CrystalSystem.cs — Nova ajoutée
```csharp
public void AbsorbProjectile()
{
    if (_currentCharges >= _maxCharges) return;
    _currentCharges++;
    GameUI.Instance.UpdateCrystalCharge(_currentCharges, _maxCharges);

    TriggerNova(); // Nova à CHAQUE absorption

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
            if (eb != null) eb.TakeDamage(_novaDamage, DamageNumberSpawner.ColorCritical, true); // fromNova = true

            BossBase boss = hit.GetComponent<BossBase>();
            if (boss != null) boss.TakeDamage(_novaDamage);
        }
    }

    if (_novaVFXPrefab != null) StartCoroutine(ShowNovaVFX());
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
```

**Touches** : F = ultime (changé depuis E pour éviter le conflit avec le contrôle orbital A/E).

## WeaponLightningChain.cs — NOUVEAU (Chaîne de Foudre)
```csharp
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WeaponLightningChain : MonoBehaviour
{
    [SerializeField] private float _damage         = 20f;
    [SerializeField] private float _chainRange     = 4f;
    [SerializeField] private int   _maxChains      = 3;
    [SerializeField] private float _fireRate       = 1f;
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private int   _maxChainUpgrades = 3;

    private int   _chainUpgradeCount = 0;
    private float _cooldownTimer     = 0f;

    public bool IsMaxChain() => _chainUpgradeCount >= _maxChainUpgrades;

    public void AddChain()
    {
        if (IsMaxChain()) return;
        _maxChains++;
        _chainUpgradeCount++;
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
                StartCoroutine(ChainLightning(nearest));
                _cooldownTimer = 0f;
            }
        }
    }

    private IEnumerator ChainLightning(Transform firstTarget)
    {
        List<GameObject> hit = new List<GameObject>();
        Transform current    = firstTarget;

        for (int i = 0; i <= _maxChains; i++)
        {
            if (current == null) break;

            float damage = _damage * Mathf.Pow(0.7f, i); // Dégâts dégressifs par rebond

            EnemyBase eb = current.GetComponent<EnemyBase>();
            if (eb != null) eb.TakeDamage(damage, DamageNumberSpawner.ColorCritical);

            BossBase boss = current.GetComponent<BossBase>();
            if (boss != null) boss.TakeDamage(damage, DamageNumberSpawner.ColorCritical);

            hit.Add(current.gameObject);
            current = FindNextChainTarget(current.position, hit);

            yield return new WaitForSeconds(0.05f);
        }
    }

    private Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float minDist = _detectionRange;
        foreach (GameObject enemy in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < minDist) { minDist = dist; nearest = enemy.transform; }
        }
        return nearest;
    }

    private Transform FindNextChainTarget(Vector3 from, List<GameObject> alreadyHit)
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float minDist = _chainRange;
        foreach (GameObject enemy in enemies)
        {
            if (alreadyHit.Contains(enemy)) continue;
            float dist = Vector3.Distance(from, enemy.transform.position);
            if (dist < minDist) { minDist = dist; nearest = enemy.transform; }
        }
        return nearest;
    }

    public void AddDamage(float value)   => _damage   += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;
}
```

**Comportement** : 1 tir/sec vers l'ennemi le plus proche (15u de portée), rebondit jusqu'à 3 fois (extensible à 6 max via upgrades de run) sur les ennemis dans un rayon de 4u, dégâts x0.7 par rebond.

## GameManager.cs — Version finale de cette session
```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

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
        if (WaveManager.Instance != null && WaveManager.Instance.BossAlive) return; // Timer pausé pendant les boss

        _runTimer += Time.deltaTime;
    }

    public void TogglePause()
    {
        IsPaused       = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
        GameUI.Instance.SetHUDVisible(!IsPaused);
        GameUI.Instance.ShowPausePanel(IsPaused);
    }

    public void ResumePause()
    {
        IsPaused       = false;
        Time.timeScale = 1f;
        GameUI.Instance.SetHUDVisible(true);
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
        if (GameUI.Instance != null) GameUI.Instance.UpdateKillCount(_killCount);
    }

    public void TriggerGameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        Invoke(nameof(ShowGameOver), 1.5f);
    }

    private void ShowGameOver()
    {
        GameUI.Instance.SetHUDVisible(false);
        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount);
        GameUI.Instance.ShowGameOver(_runTimer, _killCount, MetaProgressionManager.Instance.RunGold);
    }

    public void RestartGame()  { Time.timeScale = 1f; SceneManager.LoadScene(1); }
    public void GoToMainMenu() { Time.timeScale = 1f; SceneManager.LoadScene(0); }

    public void TriggerVictory()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        Invoke(nameof(ShowVictory), 2f);
    }

    private void ShowVictory()
    {
        GameUI.Instance.SetHUDVisible(false);
        MetaProgressionManager.Instance.SaveRunResults(_runTimer, _killCount);
        GameUI.Instance.ShowVictory(_runTimer, _killCount, MetaProgressionManager.Instance.RunGold, XPSystem.Instance.CurrentLevel);
    }
}
```

## WaveManager.cs — timer unifié
```csharp
// Extrait clé — timer lu depuis GameManager, plus de timer séparé
public bool BossAlive => _bossAlive;

private void Update()
{
    if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
    if (GameManager.Instance.IsPaused) return;
    if (_bossAlive) return;

    float runTimer = GameManager.Instance.RunTimer; // Lecture, pas de timer propre

    ApplyDifficulty();

    if (_bossCount == 0 && runTimer >= BossSpawnInterval) SpawnBoss(1);
    if (_bossCount == 1 && runTimer >= BossSpawnInterval * 2f) SpawnBoss(2);
    if (_bossCount == 2 && runTimer >= BossSpawnInterval * 3f) SpawnBoss(3);

    GameUI.Instance.UpdateTimer(runTimer);
}
```

## GameUI.cs — Ajouts de cette session (Victoire + HUD visibility)
```csharp
[Header("Victoire")]
[SerializeField] private GameObject      _victoryPanel;
[SerializeField] private TextMeshProUGUI _victoryStatsText;
[SerializeField] private TextMeshProUGUI _victoryRecordsText;
[SerializeField] private TextMeshProUGUI _victoryBuildListText;

[Header("HUD")]
[SerializeField] private GameObject _hudPanel;

public void SetHUDVisible(bool visible)
{
    if (_hudPanel != null) _hudPanel.SetActive(visible);
}

public void ShowVictory(float runTimer, int killCount, int goldEarned, int level)
{
    _victoryPanel.SetActive(true);

    int mins = Mathf.FloorToInt(runTimer / 60f);
    int secs = Mathf.FloorToInt(runTimer % 60f);

    _victoryStatsText.text = $"Temps de survie : {mins:00}:{secs:00}\n" +
                             $"Ennemis tués : {killCount}\n" +
                             $"Niveau atteint : {level}\n" +
                             $"Gold gagné : {goldEarned}";

    SaveData data = MetaProgressionManager.Instance.Data;
    int bestMins  = Mathf.FloorToInt(data.bestTime / 60f);
    int bestSecs  = Mathf.FloorToInt(data.bestTime % 60f);

    _victoryRecordsText.text = $"Meilleur temps : {bestMins:00}:{bestSecs:00}\n" +
                               $"Meilleur kills : {data.bestKills}\n" +
                               $"Runs totales : {data.totalRuns}";

    _victoryBuildListText.text = LevelUpManager.Instance.GetUpgradesSummary();
}
```

## LevelUpManager.cs — GetUpgradesSummary corrigé
```csharp
public string GetUpgradesSummary()
{
    if (_chosenUpgrades.Count == 0) return ""; // Plus de message qui dépassait le cadre

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
```

---

# 6. META-PROGRESSION — ARBRE DE COMPÉTENCES (NOUVEAU SYSTÈME MAJEUR)

## Décisions de game design prises cette session

**Ressource unique** : suppression du concept de double monnaie (Gold + Gemmes). Une seule ressource — le **Gold** — pour simplifier l'expérience d'un premier jeu. Les Gemmes pourront devenir une ressource de prestige plus tard.

**Coffres supprimés** du plan pour l'instant — feature qui demande trop de contenu pour être satisfaisante sans assets riches.

**Structure retenue** : un arbre de compétences à 3 branches thématiques (pas un simple shop de stats plates), avec :
- Inspiration directe : Hades (Miroir des Ténèbres) pour la satisfaction de progression permanente
- Niveaux multiples (max 3) pour les nœuds de stats pures
- Nœuds uniques (1 seul niveau) pour les capacités spéciales mémorables, pas juste des %

## Structure finale en LOSANGE (mise à jour majeure vs version précédente)

**Changement important** : la structure n'est plus une simple ligne verticale par branche. Chaque branche a maintenant une forme de **losange 2+2+1** :
- **Ligne du bas** : 2 nœuds côte à côte (les fondations de la branche)
- **Ligne du milieu** : 2 nœuds côte à côte (progression intermédiaire)
- **Ligne du haut** : 1 nœud unique — la capacité ultime de la branche

**Règle de déblocage de la capacité ultime** : nécessite qu'AU MOINS 1 niveau ait été acheté dans **CHACUN** des 2 nœuds de la ligne du milieu (pas besoin de les remplir à 3, juste 1 achat dans chaque sous-branche). Ça crée un vrai choix de répartition pour le joueur tout en gardant un objectif clair.

### Branche Guerrier ⚔️ (rouge/orange)
```
                    [Surpuissance] (unique)
                   ↗                ↖
        [Dégâts Cristal]      [Fragmentation] (unique)
              ↑                      ↑
        [Cadence]              [Dégâts]
```
- **Dégâts** (multi x3) : +10% / +25% / +50%
- **Cadence** (multi x3) : +10% / +20% / +35%
- **Fragmentation** (unique) : 20% chance d'explosion à l'impact, 50% dégâts aux ennemis proches, rayon 2u
- **Dégâts Cristal** (multi x3) : +25% / +50% / +100% (sur Ultime et Nova)
- **Surpuissance** (unique) : après l'ultime, dégâts x2 pendant 5s

### Branche Gardien 🛡️ (vert/émeraude)
```
                    [Bouclier de Mana] (unique)
                   ↗                  ↖
        [Second Souffle] (unique)  [Armure]
              ↑                      ↑
        [Régénération]          [Vitalité]
```
- **Vitalité** (multi x3) : +15% / +30% / +50% HP Max
- **Armure** (multi x3) : -8% / -15% / -25% dégâts reçus
- **Régénération** (multi x3) : +1 / +2 / +4 HP/sec
- **Second Souffle** (unique) : survit une fois par run à un coup fatal avec 1 HP
- **Bouclier de Mana** (unique) : absorbe automatiquement 1 projectile toutes les 8s

### Branche Fantôme 👻 (cyan/bleu)
```
                    [Dash Fantôme] (unique)
                   ↗                ↖
        [Nova Étendue]         [Maîtrise du Cristal] (unique)
              ↑                      ↑
        [Agilité]                [Dash Amélioré]
```
- **Agilité** (multi x3) : +8% / +18% / +30% vitesse
- **Dash Amélioré** (multi x3) : -0.3s / -0.6s / -1.0s cooldown (base **3s**, pas 2s)
- **Nova Étendue** (multi x3) : +30% / +60% / +100% rayon nova
- **Maîtrise du Cristal** (unique) : -1 charge nécessaire pour déclencher l'ultime
- **Dash Fantôme** (unique) : laisse un clone qui attire les ennemis pendant 2s

## Coûts
| Niveau (multi) | Coût |
|---|---|
| Niv 1 | 100 gold |
| Niv 2 | 300 gold |
| Niv 3 | 700 gold |

| Nœud unique | Coût |
|---|---|
| Fragmentation | 500 |
| Overpower | 1000 |
| Second Souffle | 800 |
| Mana Shield | 900 |
| Crystal Mastery | 600 |
| Phantom Dash | 1200 |

## ⚠️ ATTENTION — Code à vérifier/adapter
Le `MetaProgressionManager.cs` documenté dans la version précédente de ce document utilisait une structure **linéaire à 1 branche par catégorie** (5 nœuds empilés). L'utilisateur a depuis retravaillé la disposition visuelle vers la structure en **losange 2+2+1** décrite ci-dessus, dans une conversation séparée. 

**Avant de continuer le développement**, il faut :
1. Vérifier que `IsNodeUnlockable()` dans `MetaProgressionManager.cs` reflète bien la nouvelle règle : le nœud du sommet (capacité unique finale) nécessite un niveau ≥1 dans LES DEUX nœuds de la ligne du milieu, pas un seul
2. Reconfirmer les `nodeId` string utilisés dans le code correspondent exactement à ceux assignés sur chaque `SkillNode` dans Unity
3. Vérifier que tous les bonus (`GetBonusDamage`, `GetBonusArmor`, `HasSecondWind`, etc.) sont bien lus dans `HealthSystem`, `PlayerController`, `WeaponBase`, `CrystalSystem` avec les bons noms

## SaveData.cs — Étendu
```csharp
[System.Serializable]
public class SaveData
{
    public int   totalGold  = 0;
    public int   totalRuns  = 0;
    public float bestTime   = 0f;
    public int   bestKills  = 0;

    // Branche Guerrier
    public int damageLevel        = 0;
    public int cadenceLevel       = 0;
    public int crystalDamageLevel = 0;
    public bool fragmentationUnlocked = false;
    public bool overpowerUnlocked     = false;

    // Branche Gardien
    public int  vitalityLevel     = 0;
    public int  regenLevel        = 0;
    public int  armorLevel        = 0;
    public bool secondWindUnlocked  = false;
    public bool manaShieldUnlocked  = false;

    // Branche Fantôme
    public int  agilityLevel      = 0;
    public int  dashLevel         = 0;
    public int  novaRadiusLevel   = 0;
    public bool crystalMasteryUnlocked = false;
    public bool phantomDashUnlocked    = false;
}
```

## UI de l'Arbre — Réalisation visuelle (TERMINÉE, niveau jugé professionnel)

**Structure technique** :
- `SkillTreeUI.cs` : gère le panel de détail (apparition près du nœud cliqué au clic, pas de suivi de souris, clamp aux bords d'écran 1920x1080), le calcul des coûts/niveaux affichés, l'appel à `MetaProgressionManager.TryBuyNode()`
- `SkillNode.cs` : géré sur chaque médaillon, gère l'état visuel (couleur vive si débloqué/disponible, grisé si verrouillé — **jamais l'inverse**), les dots de niveau (centré si nœud unique)

**Réalisation visuelle finale** : fond représentant un **grand arbre Ghibli central** qui structure naturellement la composition et remplace les lignes de connexion qu'on cherchait à dessiner manuellement. Médaillons avec aura colorée distincte par branche (orange/vert/cyan), tous générés via IA (Leonardo.ai et Pippit AI) et jugés de qualité professionnelle par les deux parties. Bouton "Réinitialiser" ajouté par l'utilisateur. Titres de branches en bas de l'écran (Guerrier/Gardien/Fantôme).

**Itérations de background qui ont précédé la version finale** :
1. Texture pierre/runes sombre → rejetée, trop chargée, nuit à la lisibilité
2. Fond uni `#0D0D1A` → bon pour tester la structure, mais ton trop éloigné du reste du jeu
3. Paysage de lac lumineux (généré avec watermark Pippit AI, supprimé manuellement via Paint) → bien mais sans structure
4. **Grand arbre Ghibli central** → version finale validée, excellent compromis lisibilité/esthétique/cohérence DA

---

# 7. TOUCHES ET CONTRÔLES (mis à jour)

| Touche | Action |
|---|---|
| ZQSD / Flèches | Déplacement |
| Shift gauche | Dash (absorbe projectiles pendant 0.3s) |
| **F** | Déclencher l'ultime cristal (changé depuis E pour éviter conflit) |
| A | Réduire la range des orbitaux |
| E | Augmenter la range des orbitaux |
| ESC | Pause / Dépause (ignoré pendant un choix de level up) |
| 1 / 2 / 3 | Sélectionner upgrade au level up |
| Clic souris gauche | Sélectionner upgrade (boutons UI) / interagir avec l'arbre de compétences |

**Décision actée** : le tir dirigé à la souris façon twin-stick shooter a été **testé puis abandonné** comme comportement par défaut — jugé trop complexe à gérer en simultané avec dash + esquive pour le joueur moyen. Sera proposé comme **option activable dans les Settings** avec un curseur visible à l'écran (pas encore implémenté), le tir automatique restant le comportement par défaut.

---

# 8. GAME DESIGN — VISION ÉTENDUE (mise à jour majeure)

## Philosophie (inchangée, confirmée)
- Finir une zone parfaite avant d'en faire d'autres
- Skill gap via le dash absorbeur — c'est l'identité du jeu
- Jeu PC assumé — complexité intentionnelle
- Ennemis majoritairement corps à corps (65%), shooters minoritaires (20%)
- Qualité maximale visée, le temps n'est pas une contrainte

## Analyse de longévité — pourquoi c'était nécessaire
Constat fait en cours de session : l'arbre de compétences seul représente 15-20 runs pour être entièrement débloqué, soit seulement 2-4h de jeu. Insuffisant pour un jeu complet face à des références comme Vampire Survivors qui proposent 15-25h de contenu via plusieurs couches de longévité empilées :
1. Déblocage de contenu progressif (personnages, armes, maps)
2. Défis et succès
3. Variété de builds par évolution/fusion d'armes
4. Asymétrie forte des personnages jouables

## Plan de longévité retenu (par impact/effort)
| Feature | Impact | Effort | Statut |
|---|---|---|---|
| Arbre meta-progression | Élevé | Élevé | ✅ FAIT |
| Personnages jouables (Kael, Lyra, Aether) | Très élevé | Moyen | À faire |
| 3 défis aléatoires par run | Élevé | Faible | À faire |
| Fusions d'armes (x3) | Moyen | Moyen | Conçu, pas codé |
| Map 2 et suivantes | Très élevé | Très élevé (contenu) | Plus tard |

## Personnages jouables (conçu, pas encore codé)
Nouvel onglet à créer dans `MainMenuManager` pour la sélection.

| Personnage | Rôle | Bonus | Malus/Mécanique |
|---|---|---|---|
| **Aether** | Base | Aucun | Statistiques équilibrées par défaut |
| **Kael** | Guerrier | +20% dégâts | -20% vitesse, dash en ligne droite uniquement |
| **Lyra** | Fantôme | +30% vitesse | -30% HP Max, dash recharge 2x plus vite |

## Système de défis par run (conçu, pas encore codé)
3 défis aléatoires affichés au début de chaque run, récompensés en gold bonus :
- "Tuer 50 ennemis sans prendre de dégâts"
- "Utiliser l'ultime 5 fois"
- "Survivre 3 minutes sans prendre d'upgrade"

## Fusions d'armes (conçu, pas encore codé)
| Fusion | Armes requises | Effet | Couleur carte |
|---|---|---|---|
| Nova Orbitale | Nova (toujours présente) + Orbital | Les orbitaux explosent en nova à chaque rotation complète | Cyan électrique |
| Tempête | Chaîne de Foudre + AOE | L'AOE pulse déclenche des éclairs sur les ennemis touchés | Jaune électrique |
| Vague de Cristal | AOE + Nova | Grande AOE permanente qui **ralentit** (pas repousse) les ennemis dans la zone | Bleu profond |

**Règles** :
- Carte "Fusion disponible !" avec couleur vive spécifique dans le LevelUpPanel
- Couleur réutilisée dans tous les écrans affichant les upgrades (Pause, Victoire, GameOver)
- Une arme déjà engagée dans une fusion ne peut plus servir à une autre — vrai choix stratégique
- **Décision importante** : le repousse a été écarté en faveur du ralentissement pour la Vague de Cristal, car repousser les ennemis les éloigne de la zone de dégâts et des gemmes XP, ce qui est contre-productif en gameplay

---

# 9. CAMÉRA — ÉTAT FINAL DE CETTE SESSION

- **Cinemachine Framing Transposer** configuré avec damping réduit (0.15 au lieu de 0.3 initial) après retour utilisateur sur une fluidité excessive pendant les esquives latérales
- **Lookahead Time** à 0.1 (réduit depuis 0.3) pour un effet d'anticipation subtil sans excès
- **Ortho Size** remonté à 10-12 pour voir plus d'ennemis à la fois
- **Spawn radius** des ennemis augmenté en conséquence à 15 unités

---

# 10. MENU PRINCIPAL — ÉTAT ACTUEL

## Réalisé
- Fond anime fantasy cohérent avec la DA (jugé très bon par les deux parties)
- TabBar avec 3 onglets : UPGRADES (= Arbre de compétences) / MENU / SETTINGS
- Logo AETHER avec cristal bleu
- **Arbre de compétences entièrement fonctionnel et visuellement abouti** (voir section 6)

## À faire
- **SettingsPanel** : actuellement vide/noir, à construire avec :
  - Tir automatique ON/OFF
  - Volume musique (slider)
  - Volume SFX (slider)
  - Plein écran ON/OFF
  - **Ombres ON/OFF** (ajouté suite aux retours sur l'éclairage, contrôle `Directional Light` Shadow Type)
- **Nouvel onglet Personnages** : sélection entre Aether/Kael/Lyra une fois codés

---

# 11. TODO LIST — ORDRE DE PRIORITÉ MIS À JOUR

## Vérification immédiate (avant tout nouveau développement)
- [ ] Confirmer que `MetaProgressionManager.IsNodeUnlockable()` reflète la structure finale en losange 2+2+1 (les deux nœuds de la ligne du milieu doivent être investis, pas un seul, pour débloquer le nœud du sommet)
- [ ] Vérifier que tous les bonus de l'arbre sont bien lus et appliqués dans `HealthSystem` / `PlayerController` / `WeaponBase` / `CrystalSystem`
- [ ] Confirmer les valeurs finales de gold par ennemi/boss réglées dans l'Inspector

## Priorité haute — confort et longévité
- [ ] Settings complet (volume, plein écran, tir auto, ombres ON/OFF)
- [ ] Personnages jouables (Aether/Kael/Lyra) + onglet de sélection dans le menu
- [ ] 3 défis aléatoires par run avec récompense gold

## Priorité moyenne — polish gameplay
- [ ] Fusions d'armes (Nova Orbitale, Tempête, Vague de Cristal)
- [ ] Game Over enrichi avec records (actuellement seul l'écran Victoire les affiche)
- [ ] VFX réel pour la Chaîne de Foudre (actuellement juste un Debug.DrawLine)

## Priorité basse — contenu long terme
- [ ] Map 2 — Forêt mystique (nouveaux ennemis, boss, assets, équilibrage)
- [ ] Modèles 3D définitifs (TRELLIS/Hunyuan3D → Blender → Mixamo)
- [ ] Musique et SFX (ElevenLabs + Suno)
- [ ] Relief de map 3D exploité mécaniquement

---

# 12. RÈGLES DE COLLABORATION IA (confirmées et enrichies cette session)

- Réponses structurées et honnêtes — dire non si une idée n'est pas bonne, avec arguments
- Poser des questions avant de coder pour éviter les malentendus
- Ne jamais dire qu'une idée est bonne juste pour faire plaisir
- Guider au moindre clic dans Unity car l'interface est complexe pour un débutant
- Le jeu doit avoir du sens et être cohérent — pas d'idées bizarres qui casseraient le concept
- Garder en tête l'originalité et la démarquabilité vis-à-vis des grands du genre
- Toujours penser addiction, satisfaction et skill gap
- **Le temps n'est pas une contrainte** — privilégier systématiquement la qualité maximale (ex : arbre de compétences en losange avec assets IA dédiés plutôt qu'un simple shop de stats)
- Quand une fonctionnalité semble complexe à tort, évaluer objectivement l'effort réel (code vs contenu) avant de la déconseiller
- Toujours vérifier les éléments visuels avec captures d'écran réelles avant de valider une direction artistique

---

*Document mis à jour — Projet Aether — Juin 2026*
*Cette version (V3) remplace AETHER_PROJECT_COMPLET_V2.md*
*Accompagné de aether_conversation_save.json pour le détail exhaustif de cette session*
