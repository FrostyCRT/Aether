# NEXUS — Bullet Heaven — Unity 2021.3.45f2 (URP)

> Jeu solo développé avec Claude. Dernière mise à jour : Mai 2026.

---

## Contexte du projet

- **Développeur** : Solo (1 personne)
- **Moteur** : Unity 2021.3.45f2 LTS
- **Pipeline de rendu** : Universal Render Pipeline (URP) — Universal 3D
- **Plateforme cible** : PC Windows
- **Distribution** : itch.io (WebGL) → Steam à terme
- **Inspiration principale** : Vampire Survivors, Brotato, Ball x Pit, 20 Minutes Till Dawn

---

## Concept du jeu

Un **Bullet Heaven / Auto-shooter roguelite** top-down en 3D (caméra vue de dessus).

Le joueur contrôle un personnage qui attaque automatiquement des vagues d'ennemis de plus en plus nombreuses. À chaque niveau gagné, il choisit parmi 3 upgrades aléatoires pour construire un build de plus en plus puissant. L'objectif est de survivre le plus longtemps possible.

### La boucle addictive

```
Survive → Tue des ennemis → Gagne XP → Niveau up → Choix d'upgrade → Build explose → Mort → Meta-progression → Retry
```

### Ce qui rend le jeu addictif
- Sessions courtes (15–30 min) → "encore une partie"
- Upgrades aléatoires → chaque run est différente
- Les chiffres qui explosent → dopamine garantie
- Meta-progression entre les runs → toujours quelque chose à débloquer
- Builds variés → stratégie + chance

---

## Configuration Unity

### Modules installés
- ✅ Windows Build Support (IL2CPP)
- ✅ WebGL Build Support
- ✅ Documentation

### Pipeline
- **Universal Render Pipeline (URP)**
- Projet créé via le template "Universal 3D"

### Packages Unity à activer (via Package Manager)
- `Cinemachine` — pour la caméra qui suit le joueur + screen shake
- `TextMeshPro` — pour les damage numbers flottants et toute l'UI
- `Input System` — optionnel (on utilisera l'ancien système `Input.GetAxis` pour rester simple)

---

## Architecture du projet

### Structure des dossiers (à créer dans Assets/)
```
Assets/
├── _Game/
│   ├── Scripts/
│   │   ├── Player/
│   │   ├── Enemies/
│   │   ├── Weapons/
│   │   ├── Upgrades/
│   │   ├── UI/
│   │   ├── Systems/        ← XP, Wave, ObjectPool, Save
│   │   └── Data/           ← ScriptableObjects
│   ├── Prefabs/
│   │   ├── Player/
│   │   ├── Enemies/
│   │   ├── Weapons/
│   │   ├── Pickups/        ← orbes XP, gold
│   │   └── VFX/
│   ├── ScriptableObjects/
│   │   ├── Weapons/
│   │   ├── Upgrades/
│   │   ├── Enemies/
│   │   └── Players/
│   ├── Scenes/
│   │   ├── MainMenu
│   │   ├── Game
│   │   └── GameOver
│   ├── Materials/
│   ├── Animations/
│   └── Audio/
│       ├── Music/
│       └── SFX/
└── ThirdParty/             ← assets externes (Kenney, etc.)
```

### Conventions de nommage
- Scripts C# : `PascalCase` → `PlayerController.cs`, `EnemyBase.cs`
- GameObjects dans la scène : `PascalCase` → `PlayerSpawner`, `EnemyPool`
- ScriptableObjects : `SO_NomDuType` → `SO_WeaponOrbit`, `SO_UpgradeDamage`
- Prefabs : `PFB_NomDuPrefab` → `PFB_EnemyBasic`, `PFB_XPOrb`
- Méthodes : `PascalCase` → `TakeDamage()`, `SpawnEnemy()`
- Variables privées : `_camelCase` → `_currentHealth`, `_moveSpeed`

---

## Roadmap de développement

### Phase 1 — Core gameplay (~ 1 semaine)
> Objectif : le jeu tourne, on peut se déplacer et mourir

- [ ] Scène de jeu de base avec sol et bordures
- [ ] Mouvement joueur top-down (`Rigidbody2D` ou `CharacterController` en 3D + caméra orthographique)
- [ ] Caméra qui suit le joueur (`Cinemachine Virtual Camera`)
- [ ] Spawner d'ennemis autour du joueur à intervalles croissants
- [ ] 1 arme automatique (projectile droit, tire vers l'ennemi le plus proche)
- [ ] Système HP joueur + ennemis
- [ ] Écran Game Over simple

**Scripts à créer :**
- `PlayerController.cs`
- `EnemyBase.cs`
- `EnemySpawner.cs`
- `WeaponBase.cs`
- `ProjectileBasic.cs`
- `HealthSystem.cs`
- `GameManager.cs`

---

### Phase 2 — Progression dans la run (~ 1 semaine)
> Objectif : la boucle de level up addictive

- [ ] Les ennemis lâchent des orbes XP
- [ ] Collecte d'XP par trigger
- [ ] Barre XP + calcul du niveau suivant (`xpNeeded = baseXP * level^1.5`)
- [ ] Pause du jeu au level up (`Time.timeScale = 0`)
- [ ] Affichage de 3 upgrades aléatoires parmi une liste
- [ ] Application de l'upgrade choisie

**Upgrades de base :**
| Upgrade | Effet |
|---|---|
| Dégâts + | +20% aux dégâts de toutes les armes |
| Vitesse d'attaque + | -15% au cooldown de toutes les armes |
| Vitesse de déplacement + | +15% à la vitesse du joueur |
| Projectiles + | +1 projectile par tir |
| Zone + | +20% à la taille des zones d'attaque |
| Soin | Restaure 20% des HP max |

**Scripts à créer :**
- `XPSystem.cs`
- `LevelUpManager.cs`
- `UpgradeData.cs` (ScriptableObject)
- `UpgradeUI.cs`

---

### Phase 3 — Contenu et variété (~ 2 semaines)
> Objectif : rendre chaque run différente et intéressante

**Armes (3 minimum) :**
| Arme | Description |
|---|---|
| Projectile droit | Tire vers l'ennemi le plus proche, simple |
| Zone circulaire | Pulse autour du joueur, touche tout ce qui est proche |
| Orbitale | Projectile tourne en orbite autour du joueur |

**Ennemis (3 types) :**
| Ennemi | Comportement |
|---|---|
| Basic | Fonce vers le joueur en ligne droite |
| Shooter | Garde une distance, tire des projectiles |
| Tank | Lent, beaucoup de HP, fait beaucoup de dégâts |

**Système de waves :**
- Timer de run (défaut 20 min)
- Difficulté croissante toutes les 30 secondes
- Boss toutes les 5 minutes (ennemi avec beaucoup de HP + patterns)
- `WaveData.cs` ScriptableObject pour configurer les waves

**ObjectPool :**
- Pool générique réutilisable pour les ennemis et les projectiles
- Obligatoire pour les performances avec beaucoup d'ennemis à l'écran
- `ObjectPool.cs` (générique, utilisable pour n'importe quel type)

**Scripts à créer :**
- `ObjectPool.cs`
- `WaveManager.cs`
- `WaveData.cs` (ScriptableObject)
- `WeaponOrbital.cs`
- `WeaponAOE.cs`
- `EnemyShooter.cs`
- `EnemyTank.cs`
- `BossBase.cs`

---

### Phase 4 — Méta-progression (~ 1 semaine)
> Objectif : donner une raison de revenir après la mort

- [ ] Gold ramassé pendant les runs, persisté entre les parties
- [ ] Écran de méta-shop : acheter des upgrades permanentes
- [ ] 2–3 personnages jouables avec passifs uniques
- [ ] Statistiques de run affichées en Game Over (temps, kills, record)
- [ ] Sauvegarde/chargement via JSON (`Application.persistentDataPath`)

**Upgrades méta (exemples) :**
| Upgrade méta | Coût | Effet |
|---|---|---|
| HP de base +10% | 50 gold | Permanent |
| Dégâts de départ +5% | 75 gold | Permanent |
| Débloquer le personnage 2 | 200 gold | Nouveau perso |
| XP bonus +10% | 100 gold | Niveaux plus vite |

**Personnages (idées) :**
| Perso | Passif |
|---|---|
| Guerrier (défaut) | Commence avec l'arme de base améliorée |
| Mage | Toutes les zones sont 20% plus grandes |
| Chasseur | Projectiles 30% plus rapides |

**Scripts à créer :**
- `MetaProgressionManager.cs`
- `SaveSystem.cs`
- `PlayerData.cs` (ScriptableObject)
- `MetaShopUI.cs`
- `RunStatsUI.cs`

---

### Phase 5 — Polish et publication (~ 1 semaine)
> Objectif : transformer le prototype en vrai jeu

**Juice (ressenti) :**
- [ ] Screen shake à chaque impact (`Cinemachine Impulse Source`)
- [ ] Particules de mort ennemis (`ParticleSystem` simple)
- [ ] Chiffres de dégâts flottants (`TextMeshPro` + animation)
- [ ] Feedback visuel au level up (flash d'écran, son)
- [ ] Musique de fond + SFX pour les coups, morts, level ups

**Scènes :**
- [ ] Menu principal (Play, Options, Quit)
- [ ] Sélection de personnage
- [ ] Scène de jeu principale
- [ ] Écran Game Over avec stats + bouton retry
- [ ] Meta-shop

**Publication :**
- [ ] Build WebGL testé dans le navigateur
- [ ] Page itch.io créée (screenshots, description, prix ou gratuit)
- [ ] Build Windows .zip uploadé aussi

---

## Ressources graphiques gratuites

| Source | URL | Contenu |
|---|---|---|
| Kenney.nl | kenney.nl | Sprites, 3D assets, UI — tout gratuit |
| itch.io assets | itch.io/game-assets/free | Énorme bibliothèque gratuite |
| Unity Asset Store (free) | assetstore.unity.com | Filtrer par "Free" |
| OpenGameArt | opengameart.org | Sprites, sons, musiques |

---

## Ressources d'apprentissage

| Sujet | Ressource recommandée |
|---|---|
| Unity 2021 URP débutant | Brackeys (YouTube) — toujours valable en 2025 |
| Roguelite game design | GDC Talk "Vampire Survivors Post-Mortem" |
| ScriptableObjects | Jason Weimann (YouTube) |
| Object Pooling | Unity Learn officiel |
| Cinemachine | Tutorial de Cinemachine dans Unity Learn |

---

## Notes de design

### Ce qui DOIT être fun dès la Phase 1
- Le mouvement du joueur doit être **fluide et réactif** — c'est la chose la plus jouée du jeu
- Les ennemis doivent mourir de façon **satisfaisante** (particule même simple)
- Le **screen shake** change tout, à ajouter le plus tôt possible

### Pièges à éviter
- ❌ Ne pas travailler trop longtemps sur les graphismes avant que le gameplay soit fun
- ❌ Ne pas coder sans ObjectPool — le jeu laggera avec 200 ennemis
- ❌ Ne pas ajouter de features avant que la boucle de base soit addictive
- ❌ Ne pas hardcoder les stats — tout passer par des ScriptableObjects

### Inspirations directes
- **Vampire Survivors** → La psychologie de slot machine appliquée aux upgrades, chest opening
- **Brotato** → La variété des personnages et des builds
- **Ball x Pit** → Preuve qu'un solo dev peut cartonner en 2025 avec une mécanique simple

---

## Décisions techniques

| Décision | Choix | Raison |
|---|---|---|
| Pipeline | URP | Standard indie actuel, bien documenté, performant |
| Caméra | Orthographique top-down | Genre Bullet Heaven, lisibilité parfaite |
| Physique | Rigidbody3D + colliders | Simple, intégré Unity, pas de lib externe |
| Sauvegarde | JSON sur disque | Simple, lisible, pas de dépendance externe |
| Input | Legacy Input System | Plus simple pour débutant, suffisant pour ce genre |
| UI | TextMeshPro + Canvas | Standard Unity, excellent rendu |
| Pooling | Custom ObjectPool | Léger, adapté à ce projet précis |

---

*Ce fichier est mis à jour au fur et à mesure du projet.*

Pour ton Meta_Shop, je ne veux pas qu'il apparaisse dans le Game_Over, je veux créer un vrai menu où l'on peut changer d'onglets, par exemple l'onglet principal où on choisit le niveau 1, si on slide sur le coté on a acces a la meta-shop, et on créera d'autre slide mais pour l'instant je veux faire ça je trouve ça bien. Ne commence pas les etapes et le code tout de suite. Si tu as des questions pose les moi. Pour le systeme de niveau, j'ai pensé a faire en plusieurs niveau selon plusieurs maps. Pour terminer un niveau, il faut survivre 15min sans mourir, il y a un boss différent toutes les 5 minutes donc 3 boss en tout, sachant que le 3eme boss est le plus fort vu qu'il clos le niveau.
J'ai retravailler sur le Game design, voici ce que j'ai pensé:
# Complément Game Design & Direction Artistique — NEXUS (Style Anime / High Fantasy)

Pour faire suite à la structure technique du projet, voici l'univers graphique et conceptuel validé pour le jeu. Le style retenu est une DA "Anime Toon-Shading" (type Ghibli / Mushoku Tensei), lumineuse et colorée, optimisée pour le Low-Poly (environ 2 000 polygones par modèle pour garantir les performances de l'auto-shooter).

---

## 1. Le Héros (Le Joueur)
- **Concept** : Un jeune apprenti humain classique, inspiré de Rudeus Greyrat au début de son aventure.
- **Visuel** : Style Chibi (proportions adaptées à la vue de dessus à 65°), cheveux blonds courts en bataille, yeux bleus vifs, expression déterminée. Il porte une tunique simple en tissu beige clair, un gilet de cuir marron, des petites bottes et tient un bâton magique en bois brut surmonté d'un petit cristal bleu lumineux.

---

## 2. Le Bestiaire de la Plaine (Map 1)
Le gameplay commence "chill" avec des créatures vivantes et organiques de la faune locale, altérées par le mana :

**L'Ennemi Basique (Loup de Mana)** : Un loup agile au pelage vert/bleu magique. Il fonce en ligne droite sur le joueur. Ses yeux brillent d'une lueur magique et il laisse une traînée de particules d'herbe derrière lui.
**Le Tank (Golem de Tronc)** : Une créature lente, large et massive faite d'une souche d'arbre et de racines. Il possède de gros bras en écorce et encaisse beaucoup de dégâts.
**Le Shooter (Bulbe Cracheur)** : Une plante carnivore mobile qui s'arrête à distance pour mitrailler le joueur avec des projectiles de sève ou de pollen magique.
**Le Boss (Gardien de la Source)** : Un sanglier géant divin (style Princesse Mononoké) recouvert de mousses et de lignes de mana lumineuses sur le dos. Il apparaît à la 5ème minute avec des patterns de charges et d'invocations de racines.
---

## 3. Progression des Environnements (Maps)
Le jeu proposera plusieurs cartes fermées avec des limites claires, marquant un voyage magique :
1. **Plaine** (Zone de départ, lumineuse et verdoyante)
2. **Forêt mystique** (Arbres géants, ambiance plus humide, champignons lumineux)
3. **Désert magique**
4. **Montagne enneigée**
5. **Jungle**
6. **Volcan**

---

## 4. Identité Visuelle des Sorts (Phase 3)
Les effets visuels (VFX) doivent compenser le low-poly en apportant beaucoup de dynamisme ("juice") :

**Projectile Droit (Flèche d'Eau)** : Un sort d'eau claire et naturelle utilisant un shader semi-transparent bleu cyan brillant (Bloom). À l'impact sur un ennemi, elle éclate en dizaines de petites gouttes d'eau physiques via le Particle System.
**Zone Circulaire (Onde de Vent)** : Une déflagration circulaire blanche/transparente qui part du joueur pour repousser les ennemis, accompagnée de feuilles vertes qui tourbillonnent vers l'extérieur.
**Orbitale (Éclats de Roche)** : 3 ou 4 blocs de pierre brute entourés de poussière magique qui lévitent et tournent en orbite autour du joueur. Les blocs ont une rotation sur eux-mêmes pour paraître vivants.
---

## 5. Intentions de Rendu dans Unity (URP)
- **Caméra** : Vue Top-down inclinée à (65, 0, 0) fixe, projection Orthographique pour un style arcade très lisible.
- **Shading** : Utilisation d'un "URP Toon Shader" (Cel-shading) pour obtenir des contours noirs nets et des aplats de couleurs typés dessin animé.
- **Post-Processing** : Éclairage chaleureux, Color Grading saturé pour les verts de la nature, et Bloom activé pour faire briller les yeux des monstres et les effets magiques.

Q: Pour le slide entre les onglets du menu, tu veux quel style de navigation ?


Q: Pour le système de niveaux, les maps sont-elles débloquées progressivement ?
A: Oui, mais on peut rejouer les anciens niveaux

Q: Pour la meta-shop, tu veux quoi comme upgrades permanentes ?
A: je pense qu'on va créer un autre system, avec des coffres qu'il faut ouvrir, et dedans on gagne des fragments d'amélioration. Quand on a tous les fragments, on peut Payer avec nos gold l'amélioration. Si on arrive a terminer un niveau, on débloque un nouveau perso. Et aussi je veux qu'on mette une upgrade de plus, c'est que au lieu de tirer 1 seul projectile, on en tire 2 d'affilé (avec un mini délais pour pas qu'ils soient strictement collés)

Contenu du coffre Simple : Fragments (quantité petite) et Gold
Contenu du coffre Rare :   Fragments (quantité moyenne) pour 2 up permanente différentes, et plus de Gold et Gemme
Contenu du coffre Légendaire : Fragments (quantité élevée) pour 4 up permanentes différentes, et bien plus de Gold Gemme et possibilité d'avoir un skin(on verra ça plus tard)

Pourquoi les gemmes ? les gemmes servent soit à compléter les pièces quand on en a pas assez pour payer une upgrade, soit elles servent pour payer un skin. Bien évidemment, les gemmes sont bien plus rare que les pièces, donc pour sa fonctionnalité du complément d'achat upgrade, il faudra gérer un pourcentage pour que ce soit rentable.
Avec les gemmes on peut également acheter des pièces tout simplement, et des coffres.

Il faut que le jeu ne fasse pas trop comme un jeu mobile, car c'est un jeu sur pc.
Donc, pour changer dans le menu, on va faire un système d'onglet, en haut, avec en haut milieu, le menu principal, a gauche, les upgrade, a droite les paramètre.
Quand on est dans le menu complet, en haut (en dehors des onglets) on doit voir notre nombre de pièces et de gemmes.

Nouvelle idée d'upgrade, c'est d'intégrer un dash (avec la touche Maj) pour une identité plus focalisé sur le jeu PC, et non pas mobile, car il faut quand meme une complexité, sinon c'est nul et on ne reste pas longtemps sur le jeu.

Dans l'onglet paramètre qu'on fera a la fin, on pourra modifier les touches, augmenter / diminuer le son / gérer la fenêtre (plein écran, fenêtré, plein écran fenêtré)


Pour réussir un niveau, il faut tenir 15 min, 1 boss différent toutes les 5 minute. Le 3eme boss doit être plus dure a battre car c'est lui qui clos le niveau.
Est-ce que tu as des questions sur un point à préciser ?
