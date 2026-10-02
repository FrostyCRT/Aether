# Index des scripts — Aether Storm Survivor

Ce fichier est un **index de repérage rapide** : pour chaque script C# du projet, une ou deux lignes qui disent ce qu'il EST (type de composant) et ce qu'il FAIT. Ce n'est pas une documentation exhaustive.

- Pour l'historique détaillé session par session (pourquoi tel choix, quels bugs ont été corrigés, quelles valeurs ont été réglées à la main) : **`NOTES.md`**, la source la plus à jour.
- Pour la vue d'ensemble du design du jeu (personnages, armes, progression méta, HUD...) : **`AETHER_STORM_SURVIVOR_PROJECT_COMPLET_V13.md`**. Attention : ce document a pris du retard sur certains points désormais bien réels en jeu — le système de **Fusion d'armes** (`WeaponFusion*.cs`) et l'ennemi **Bulbe cracheur** (`EnemyBulb.cs`) notamment. En cas de doute entre les deux, `NOTES.md` fait foi.

## Comment naviguer

- **UI en général** : commence par `GameUI.cs` (le plus gros fichier, le plus central : HUD, grille d'armes du menu Pause ET de Victoire/Défaite, écrans de fin). `UpgradeSlotRefs.cs` référence les éléments internes des 2 prefabs de carte d'arme (`UpgradeSlot` pour le menu Pause, `UpgradeGridSlot` pour Victoire/Défaite) que `GameUI.ConfigureBuildGridSlot` remplit.
- **Armes** : commence par `WeaponBase.cs` (attaque de base, héritée par la plupart des mécaniques d'upgrade). `UpgradeData.cs` décrit chaque carte d'amélioration (ScriptableObject) et sait comment l'appliquer.
- **Système de Fusion** : `WeaponFusion*.cs` (les 6 armes fusionnées) + les champs `FusionSource1`/`FusionSource2`/`ApplyFusionResult` de `UpgradeData.cs` + la disposition visuelle dédiée dans `UpgradeSlotRefs.cs` (icône de fusion, 2 icônes sources, séparateur, "+").
- **Ennemis / Boss** : `EnemyBase.cs` (stats communes, brûlure, ralentissement) et `BossBase.cs` (charge télégraphiée, musique dédiée) sont les classes de base ; les autres n'en héritent que pour leur pattern spécifique.
- **Paramètres** : `GameSettings.cs` (valeurs) + `InputBindings.cs`/`GameInput.cs` (touches) sont la donnée ; `SettingsApplier.cs` l'applique en jeu, `SettingsPage.cs`/`SettingsRow.cs`/`SettingsSchema.cs` construisent la page.
- **Menu principal / méta-progression** : `MainMenuManager.cs` (onglets), `MetaProgressionManager.cs` (or, éclats, déblocages, sauvegarde — via `SaveSystem.cs`), `LevelUpManager.cs` (choix de cartes en run).

---

## Systems (`Assets/Game/Scripts/Systems`)

| Script | Rôle |
|---|---|
| `BossCameraZoom.cs` | Singleton qui dézoome/rezoome la caméra virtuelle Cinemachine pendant un combat de boss (transition douce vers une taille cible). |
| `BoundaryDecorationPlacer.cs` | Outil de scène (`[ContextMenu]`) qui parsème des arbres/rochers en bordure de map, avec échelle et espacement minimum aléatoires mais déterministes par catégorie. |
| `DamageNumberSpawner.cs` | Singleton qui spawne les nombres de dégâts flottants via l'`ObjectPool`, fusionne les coups rapprochés sur une même cible et respecte les réglages Paramètres (dégâts infligés/subis affichés ou non). |
| `DebugCheats.cs` | Raccourcis clavier F1-F7 DEBUG uniquement (inactif hors éditeur/build dev) : sauter à un boss, donner tout le build sauf fusions, forcer un level-up, invincibilité joueur/ennemis. |
| `GameManager.cs` | Singleton central d'une run : état Game Over/Pause, spawn du personnage sélectionné (avec son skin), chrono, compteur de kills/boss. |
| `GameModes.cs` | Classe statique : définit les 4 modes de jeu (Classique/Sans fin/Ruée de boss/Titans), leurs règles d'échelle et si les fusions sont activées pour ce mode. |
| `GameSceneSettingsApplier.cs` | Applique le réglage Ombres (Paramètres) à la lumière directionnelle de la scène Game au démarrage. |
| `LevelUpManager.cs` | Singleton qui gère les niveaux d'upgrade obtenus, présente les 3 cartes de choix (`UpgradeUI`) à chaque montée de niveau, garantit l'apparition de l'arme exclusive du personnage tôt dans la run. |
| `MapBoundaryUtils.cs` | Classe statique utilitaire : borne une position à la zone de jeu carrée. |
| `MetaProgressionManager.cs` | Singleton persistant (`DontDestroyOnLoad`) : progression méta complète — Or/Éclats, sauvegarde (`SaveSystem`), déblocage de personnages, bonus de Réputation, aperçu du prochain palier pour le Game Over. |
| `ObjectPool.cs` | Singleton générique de pooling d'objets par tag (ennemis, projectiles, VFX...), pré-instanciés à taille fixe. |
| `SaveSystem.cs` | Classe statique : lecture/écriture de `save.json` (+ backup automatique), avec `SuppressWrites` pour tester sans toucher à la vraie sauvegarde. |
| `SkinCatalog.cs` | ScriptableObject : catalogue des skins de personnage (Classique=Or / Prestige=Éclats), un skin = une ligne éditable dans l'Inspector, aucun code à toucher pour en ajouter un. |
| `WaveManager.cs` | Singleton pilotant les vagues et l'apparition des 3 boss classiques (ou la logique Sans fin/Ruée de boss/Titans selon le mode courant). |
| `XPGem.cs` | Gemme d'XP ramassable poolée (3 tailles), attirée vers le joueur une fois dans son rayon d'attraction. |
| `XPGemSpawner.cs` | Singleton : calcule combien/quelles gemmes spawner à la mort d'un ennemi selon l'XP à distribuer, et gère le rayon d'attraction global (débloqué niveau 3). |
| `XPSystem.cs` | Singleton : XP courante, formule de niveau (`baseXP × niveau^1.5`), déclenche les montées de niveau. |

## Player (`Assets/Game/Scripts/Player`)

| Script | Rôle |
|---|---|
| `CharacterIdentity.cs` | Composant minimal qui tague un prefab joueur avec son `CharacterType` (Aether/Kael/Lyra). |
| `CrystalSystem.cs` | Charges d'Ultime, absorption au Dash, Nova (dégâts + ralentissement de zone), scaling de l'Ulti dans le temps, soin de "Récupération" à l'absorption. |
| `DashCircleUI.cs` | Remplit une image radiale HUD avec le cooldown du Dash du joueur (pulse quand prêt). |
| `HealthSystem.cs` | PV du joueur, invincibilité (dash + externe), régénération/armure méta, retient la cause de mort pour le message de Game Over. |
| `ManaShield.cs` | Nœud capstone Gardien : bouclier à charges qui annule totalement un projectile ennemi et alimente le Cristal comme une absorption ; verrouillage temporaire si les charges tombent à 0. |
| `PlayerBuffs.cs` | Multiplicateur STATIQUE de dégâts sortants du joueur (`OutgoingDamageMultiplier`), alimenté aujourd'hui par le nœud "Concentration" (Guerrier) ; conçu pour accueillir d'autres buffs dynamiques. |
| `PlayerController.cs` | Le plus gros script Player : déplacement, rotation, Dash (avec tout son VFX : silhouettes fantômes, squash & stretch), Clone spectral de Lyra, curseur, invincibilité temporaire externe. |
| `StartingUpgradeGranter.cs` | Composant vidé de son contenu — les armes exclusives ne s'équipent plus au spawn (passent par les cartes de level-up), laissé en place pour ne pas casser les prefabs. |

## Enemies (`Assets/Game/Scripts/Enemies`)

| Script | Rôle |
|---|---|
| `BossBase.cs` | Classe de base de tous les boss : PV/dégâts, charge télégraphiée (zone rouge au sol), musique dédiée, visuel de charge de mana, rotation vers la cible (suit le Clone de Lyra s'il existe). |
| `BossCorruptedSource.cs` | Boss 3 (hérite `BossBase`) : serpent ancré à son rift, machine à séquence d'attaques (Pulse de cristaux, Frappe, Onde de corruption, Invocation, Implosion), une seule attaque active à la fois. |
| `BossDeer.cs` | Boss 2 (hérite `BossBase`) : Cerf — saut d'attaque, spirale de projectiles, régénération, 2 phases (rage sous 30% PV). |
| `EnemyBase.cs` | Classe de base de tous les ennemis normaux : PV, dégâts de contact, drops (XP/or), scaling de difficulté, système de Brûlure (palier 3 Fireball), flag "vole" (immunité Boue). |
| `EnemyBulb.cs` | Ennemi tireur FIXE (hérite `EnemyBase`) : ne bouge jamais, crache un éventail de projectiles à cadence lente une fois la cible à portée. |
| `EnemyCorruptedWeaver.cs` | Ennemi additionnel palier 8 min (hérite `EnemyBase`) : saut vers une position figée avec ralentissement à l'atterrissage. |
| `EnemyKaiju.cs` | Ennemi costaud (hérite `EnemyBase`) : attaque frontale en cône + combo de balayage de queue à 360° si le joueur reste au contact. |
| `EnemyProjectile.cs` | Projectile tiré par un ennemi/boss shooter : vol en ligne droite, portée max, dégâts au joueur. |
| `EnemyShooter.cs` | Ennemi tireur mobile (hérite `EnemyBase`) : garde ses distances (fuit si trop proche, se positionne à portée préférée) et tire. |
| `EnemySpawner.cs` | Fait apparaître les ennemis normaux par phases temporisées (cadence, nombre par vague, plafond simultané évolutifs). |
| `EnemyTank.cs` | Ennemi "costaud" basique — hérite entièrement de `EnemyBase` sans rien ajouter (juste des stats différentes sur son prefab). |

## Weapons (`Assets/Game/Scripts/Weapons`)

| Script | Rôle |
|---|---|
| `BouncingOrbProjectile.cs` | Instance poolée de l'Orbe Rebondissant : rebondit indéfiniment sur les bords de l'écran visible, traverse les ennemis sans se détruire. |
| `LightningBoltVFX.cs` | Utilitaire statique : dessine les éclairs visibles (ricochet + flash d'impact) de la Foudre et de ses fusions, avec un pool interne de `LineRenderer` pour éviter le churn d'objets à haute fréquence. |
| `LightningChainUtility.cs` | Algorithme statique de chaîne de foudre (saut d'ennemi en ennemi, dégâts dégressifs), extrait de `WeaponLightningChain` pour être réutilisé tel quel par les fusions qui ajoutent la foudre. |
| `MudPuddleZone.cs` | Instance poolée d'une flaque de Boue : ralentissement + DPS léger aux ennemis (et boss) dans son rayon pendant sa durée de vie. |
| `OrbitalProjectile.cs` | Un éclat en orbite autour du joueur (Orbitaux) : dégâts de contact avec cooldown par ennemi. |
| `ProjectileBasic.cs` | Projectile générique de l'attaque de base et de plusieurs armes : portée, fragmentation à l'impact, perforation, Brûlure, Ricochet (fusion Lames Ricochet). |
| `WeaponAOE.cs` | Arme à zone qui pulse à intervalle régulier (dégâts à tout ce qui est dans le rayon au moment de la pulsation). |
| `WeaponAura.cs` | Champ de dégâts/ralentissement continu autour du joueur (anneau visuel procédural en `LineRenderer`). |
| `WeaponBase.cs` | Attaque de base commune à tous les personnages : tir automatique vers l'ennemi le plus proche détecté, gère Double Tir et les modificateurs de dégâts/cadence des upgrades. |
| `WeaponBouncingOrb.cs` | Orbe Rebondissant : crée/gère les instances de `BouncingOrbProjectile`. |
| `WeaponFireball.cs` | Boule de feu (Aether) : explosion garantie à l'impact, palier 2 = dégâts, palier 3 = Brûlure. |
| `WeaponFusionCursedSwamp.cs` | Fusion "Marécage Maudit" (Boue + Foudre, universelle) : flaques de Boue qui déclenchent aussi une décharge de foudre en chaîne. |
| `WeaponFusionManaVortex.cs` | Fusion "Vortex de Mana" (Aura + Orbitaux, Kael) : champ de l'Aura + éclats en orbite des Orbitaux réunis, plus forts que la somme des deux. |
| `WeaponFusionRicochetBlades.cs` | Fusion "Lames Ricochet" (Couteaux + Orbe rebondissant, Lyra) : salve en éventail dont les lames ricochent vers l'ennemi non touché le plus proche une fois leur perforation épuisée. |
| `WeaponFusionScorchedEarth.cs` | Première fusion créée : "Terre Calcinée" (Boule de feu + Boue, universelle) : zones au sol façon Boue avec dégâts hérités des deux armes sources. |
| `WeaponFusionThunderingOrbs.cs` | Fusion "Orbes Foudroyants" (Orbitaux + Foudre, universelle) : orbes en orbite qui déclenchent une chaîne de foudre à chaque impact. |
| `WeaponFusionTwinOrbs.cs` | Fusion "Orbes Jumeaux" (Double Tir + Orbe rebondissant, universelle) : deux orbes rebondissants toujours lancés ensemble en directions opposées. |
| `WeaponLightningChain.cs` | Chaîne de Foudre : tir qui saute d'ennemi en ennemi (dégâts dégressifs par saut), utilise `LightningChainUtility`/`LightningBoltVFX` pour le rendu. |
| `WeaponMudPuddle.cs` | Boue : fait apparaître des flaques (`MudPuddleZone`) en anneau autour du joueur à intervalle régulier. |
| `WeaponOrbital.cs` | Orbitaux : éclats en orbite autour du joueur, rayon ajustable en jeu (touches A/E). |
| `WeaponShurikenBarrage.cs` | Salve de Couteaux (Lyra) : plusieurs couteaux tirés simultanément en éventail, perforation partagée entre eux. |

## Data (`Assets/Game/Scripts/Data`)

| Script | Rôle |
|---|---|
| `UpgradeData.cs` | **Le cœur du système d'amélioration.** ScriptableObject décrivant une carte d'upgrade : type, valeurs par palier, branche/personnage, et — pour une Fusion — ses 2 armes sources + quel composant créer (`ApplyFusionResult`). Contient aussi `Apply()` (applique l'effet en jeu) et la logique de disponibilité (`IsAvailable`). |

## Challenge (`Assets/Game/Scripts/Challenge`)

| Script | Rôle |
|---|---|
| `ChallengeDatabase.cs` | Classe statique : liste des 12 défis (4 par palier Facile/Moyen/Difficile), chacun avec id, nom, description, difficulté. |
| `ChallengeManager.cs` | Singleton (scène Game) : tire le défi de l'heure (UTC, anti-répétition), suit sa progression/échec en temps réel, calcule et applique la récompense en or. |

## Settings (`Assets/Game/Scripts/Settings`)

| Script | Rôle |
|---|---|
| `GameInput.cs` | Classe statique : point de lecture UNIQUE des commandes du joueur (`Held`/`Down`/`Move`) à travers `InputBindings`, clavier + manette. |
| `GameSettings.cs` | Classe statique : table de toutes les clés de réglages (audio, affichage, graphismes, jeu, interface) avec valeurs par défaut, événement `Changed`, écriture PlayerPrefs différée. |
| `InputBindings.cs` | Classe statique : configuration des touches (2 emplacements par action), défauts AZERTY, préréglages, détection de conflits (échange automatique). |
| `SettingsAmbientFX.cs` | Ambiance procédurale du fond de la page Paramètres (lueurs de bougies/cristaux, poussière), respecte le réglage "Effets d'ambiance des menus". |
| `SettingsApplier.cs` | Singleton persistant auto-créé au boot : applique EN DIRECT tous les réglages (`GameSettings`) dans toutes les scènes — audio, affichage, résolution, V-Sync, FPS, qualité, ombres, pause au perte de focus. |
| `SettingsDropdown.cs` | Liste déroulante générique utilisée par les lignes "Choix" de la page Paramètres (défilement à la molette au-delà de 8 valeurs). |
| `SettingsPage.cs` | Contrôleur de la page Paramètres : carrousel de catégories, construction des lignes depuis `SettingsSchema`, gère les fenêtres de confirmation (affichage, touche, reset). |
| `SettingsRow.cs` | Une ligne de la page Paramètres (curseur/interrupteur/choix/touche/préréglages) : un seul composant, n'active que les éléments de son type. |
| `SettingsSchema.cs` | Classe statique : définition des catégories et lignes de la page Paramètres (données pures, lues par `SettingsPage`). Contient aussi `SettingsStyle` (palette de couleurs de la page). |

## UI (`Assets/Game/Scripts/UI`)

| Script | Rôle |
|---|---|
| `CharacterAmbientFX.cs` | Ambiance magique procédurale de l'onglet Personnage (brume, bokeh, poussières, éclats), teintée à la couleur du personnage actif. |
| `CharacterInfoPanel.cs` | Remplit la fiche personnage de l'onglet Personnage (épithète, arme exclusive, progression réelle de l'arbre, parcours) — ne construit pas la hiérarchie. |
| `CharacterLockOverlay.cs` | Habillage visuel d'un personnage verrouillé (voile, chaînes, cadenas dans un médaillon), teinté à sa couleur. |
| `CharacterNameLight.cs` | Génère la "lumière" additive (halo, reflets, étincelles) derrière/sur le logo-nom d'un personnage, calculée depuis le logo affiché. |
| `CharacterSelectUI.cs` | Contrôleur de l'onglet Sélection de personnage : fond dédié par personnage, portrait, logo, verrouillage. |
| `DamageNumber.cs` | Instance poolée d'un nombre de dégâts flottant (texte 3D, contour, montée + fondu). |
| `GameModeSelector.cs` | Sélecteur de mode de jeu du menu principal (flèches, nom, description, record), verrouille JOUER si le mode choisi n'est pas débloqué. |
| `GameOverMessagePool.cs` | Classe statique : pool de messages contextuels de Game Over selon la cause de mort et le temps de survie (ton "dur mais motivant"). |
| `GameUI.cs` | **Le plus gros script UI.** HUD complet (vie/XP/boss/dash/ultime), grille d'armes 3×3 partagée par le menu Pause ET les écrans Victoire/Défaite (`ConfigureBuildGridSlot`, `PopulatePauseGrid`, `PopulateBuildGrid`), messages de fin de run, records. |
| `HudBar.cs` | Une barre du HUD habillée (vie/boss/XP/esquive) : cadre, remplissage animé, traînée de dégâts, flash au coup reçu, pulsation vie basse. |
| `HudKeyLegend.cs` | Légende des touches à gauche du HUD (Dash/Ultime/Clone/Orbites/Pause), adaptative selon les compétences du personnage et les touches réellement assignées. |
| `HudSprites.cs` | Classe statique : génère en code tous les sprites du HUD (cadres biseautés, capsules, cœur, éclair, gemme, losange...) — zéro asset à importer. |
| `HudStyler.cs` | Pose sur l'objet `HUD` de la scène Game : construit et positionne tout le HUD au lancement selon ses champs Inspector (marges, tailles, palette de couleurs). |
| `MainMenuManager.cs` | Contrôleur des onglets du menu principal (Jouer/Personnages/Compétences/Réputation/Paramètres). |
| `MenuBackgroundController.cs` | Fond du menu principal selon le personnage sélectionné, avec effet de vent procédural (shader `Aether/UI/WindSway`) sur des zones de l'illustration. |
| `MenuLeavesFX.cs` | Feuilles/oiseaux/poussière du décor du menu principal, poussés par le vent de `MenuBackgroundController`. |
| `MenuSprites.cs` | Classe statique : sprites générés en code pour le décor du menu (feuilles, ailes d'oiseau, poussière). |
| `MenuTabAnimator.cs` | Grossissement doux au survol/actif d'un onglet de la barre de navigation du menu principal. |
| `MenuTabBarFX.cs` | Fond "planche de bois + laiton" de la barre d'onglets du menu principal, ajoute automatiquement un `MenuTabAnimator` à chaque onglet. |
| `PauseMenuUI.cs` | Contrôleur du menu Pause : grille d'armes (via `GameUI.PopulatePauseGrid`), infos du défi en cours, tooltip de description au clic sur une carte. |
| `ReputationHallFX.cs` | Ambiance procédurale de l'onglet Réputation (flammes de torches, braises, poussières). |
| `ReputationStatCardUI.cs` | Une carte de bonus permanent (Dégâts/Vitesse/Régénération) de l'onglet Réputation : palier, coût en Éclats, achat. |
| `ReputationStyle.cs` | Classe statique : palette de couleurs de l'onglet Réputation. |
| `ReputationUI.cs` | Contrôleur de l'onglet Réputation : 3 cartes de bonus + boutique de skins par personnage (lit `SkinCatalog`). |
| `SkillDetailPanel.cs` | Fiche de détail d'un nœud de l'arbre de compétences (description, 3 paliers, prérequis, achat) — ne construit pas la hiérarchie. |
| `SkillNode.cs` | Un médaillon de l'arbre de compétences : 4 états visuels (verrouillé/disponible/en cours/maîtrisé), halo qui respire si achetable. |
| `SkillTreeData.cs` | Classe statique : données de tous les nœuds de l'arbre de compétences (nom, description, coûts par palier, prérequis, branche). |
| `SkillTreeLinks.cs` | Dessine les liaisons de prérequis entre nœuds de l'arbre (lues directement dans `SkillTreeData`, aucune duplication). |
| `SkillTreeStyle.cs` | Classe statique : palette de couleurs de l'onglet Compétences (par branche de personnage). |
| `SkillTreeUI.cs` | Contrôleur de l'onglet Compétences : sélection d'un nœud, ouverture de la fiche, achat, en-tête et progression par personnage. |
| `SkinCardUI.cs` | Une carte de skin dans les rangées Classique/Prestige de l'onglet Réputation (aperçu, prix, état Possédé/Équipé/À venir). |
| `TreeAmbientFX.cs` | Ambiance lumineuse procédurale de l'onglet Compétences (halos, rayons de soleil, lucioles, feuilles, braises). |
| `TreeSprites.cs` | Classe statique : sprites de lumière générés en code pour l'onglet Compétences (halo, couronne, rayon). |
| `UpgradeCardHover.cs` | Transmet les événements de survol souris d'une carte d'arme vers `PauseMenuUI` (fermeture en fondu du tooltip de description). |
| `UpgradeSlotRefs.cs` | **Références du prefab de carte d'arme**, partagées par `UpgradeSlot` (menu Pause, style parchemin) et `UpgradeGridSlot` (Victoire/Défaite, tuile teintée) : icône, nom, pastilles de palier, losange de déblocage, et le visuel dédié de Fusion (icône + 2 sources + séparateur + "+", Pause uniquement). |
| `UpgradeUI.cs` | Contrôleur des 3 cartes de choix de level-up : remplit nom/description/icône/pastilles, anime le pick, et gère la disposition spéciale + le bandeau "FUSION DISPONIBLE !" quand une fusion fait partie du choix. |
| `WheelToHorizontal.cs` | Convertit le défilement molette (vertical) en défilement horizontal, posé sur les rangées de skins de l'onglet Réputation. |

## Animation (`Assets/Game/Scripts/Animation`)

| Script | Rôle |
|---|---|
| `EnemyAnimatorController.cs` | Petit pont vers l'Animator d'un ennemi (`IsAttacking`, gel de l'animation en fin de partie). |
| `PlayerAnimatorController.cs` | Petit pont vers l'Animator du joueur (marche, mort, pose Idle forcée pour le bake du Clone spectral). |

## WaitingScreen (`Assets/Game/Scripts/WaitingScreen`)

| Script | Rôle |
|---|---|
| `AmbientDustUI.cs` | Particules de poussière ambiante flottantes, confinées à une bande verticale de l'écran de chargement. |
| `LoadingBarShimmerFX.cs` | Reflet lumineux qui balaie la barre de chargement. |
| `LoadingScreenController.cs` | Contrôleur de la scène de chargement — réservée au tout premier démarrage du jeu (scène de boot), pas aux transitions en cours de jeu. |
| `ProceduralGlowUI.cs` | Halo procédural réutilisable (pulsation + dérive optionnelle) — utilisé sur l'écran de chargement et les portraits de sélection de personnage. |
| `SceneLoader.cs` | Classe statique : point d'entrée unique pour changer de scène en cours de jeu, délègue à `SceneTransitionFader`. |
| `SceneTransitionFader.cs` | Singleton persistant : fondu noir rapide entre les scènes en cours de jeu (chargement async caché derrière le fondu). |
| `SlowKenBurnsEffect.cs` | Effet Ken Burns très lent (zoom/pan discret) sur une image de fond, toujours à l'intérieur d'une marge de sécurité. |

## Music & VFX (`Assets/Game/Scripts/Music & VFX`)

| Script | Rôle |
|---|---|
| `ExpandingRingVFX.cs` | Fonction statique : anneau procédural (`LineRenderer`) qui s'agrandit puis s'estompe — utilisé par la Nova, le souffle de départ du Dash, etc. |
| `MusicStarter.cs` | Singleton : lance/change la musique avec fondus courts, attend que les volumes sauvegardés soient appliqués avant de démarrer. |

---

## Outils Éditeur (`Assets/Editor`)

Scripts `[MenuItem]` sous le menu **Aether >**, jamais exécutés en jeu — ils (re)construisent des hiérarchies de scène idempotentes (supprimer ce qu'ils ont créé avant, refaire à neuf) ou préparent des assets.

| Script | Rôle |
|---|---|
| `BangersDigitsSetup.cs` | **Aether > Rebuild Bangers Digits** : corrige les glyphes 1/7 ambigus de la police Bangers (via une police source dédiée) sans toucher au reste de l'atlas TMP. |
| `InputAxesSetup.cs` | **Aether > Setup Input Axes** : crée les axes manette `PadMoveX`/`PadMoveY` dans les Input Settings du projet (déjà exécuté une fois). |
| `MenuBackgroundBuilder.cs` | **Aether > Rebuild Menu Background** : construit le fond vivant du menu principal (3 illustrations + effet de vent + feuilles/oiseaux). |
| `ReputationPageBuilder.cs` | **Aether > Rebuild Reputation Page** : construit l'onglet Réputation complet (bonus + boutique de skins), crée `SkinCatalog.asset` s'il n'existe pas. |
| `SettingsPageBuilder.cs` | **Aether > Rebuild Settings Page** : construit la page Paramètres complète (carrousel de catégories, lignes). |
| `SkeletonSwapTool.cs` | Fenêtre d'éditeur (`EditorWindow`) : remplace le squelette d'un modèle Tripo3D par un squelette Mixamo en conservant mesh/texture/poids. |
| `SkillTreePageBuilder.cs` | **Aether > Rebuild Skill Tree Page** (+ variante Nodes) : construit l'onglet Compétences complet (nœuds, liaisons, fiche, ambiance). |
