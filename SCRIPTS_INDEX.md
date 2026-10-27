# Index des scripts — Aether Storm Survivor

Ce fichier est un **index de repérage rapide** : pour chaque script C# du projet, une ou deux lignes qui disent ce qu'il EST (type de composant) et ce qu'il FAIT. Ce n'est pas une documentation exhaustive.

- Pour l'historique détaillé session par session (pourquoi tel choix, quels bugs ont été corrigés, quelles valeurs ont été réglées à la main) : **`NOTES.md`**, la source la plus à jour.
- Pour la vue d'ensemble du design du jeu et l'état d'avancement (personnages, armes, modes, progression méta, ce qui reste à faire) : **`AETHER_STORM_SURVIVOR_PROJECT_COMPLET_V16.md`** (remis à jour le 2026-10-19, version V16 - le nom du fichier suit maintenant le numéro de version). En cas de doute, `NOTES.md` fait foi.
- Pour la liste des bugs / points à régler avant la sortie et leur avancement : **`AUDIT_VERSION_FINALE.md`**. Pour tout ce qui reste à modéliser en 3D (skins compris) et les sons manquants : **`A_MODELISER_3D.md`**.

*Index vérifié le 2026-10-19 contre les fichiers réels : tous les scripts du jeu y figurent (diagnostic complet - `DifficultyTiers.cs`, `BossChest.cs`, `DifficultySelector.cs`, `DevOnly.cs` ajoutés).*

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
| `DebugCheats.cs` | Raccourcis clavier DEBUG uniquement (inactifs hors éditeur/build dev) : F1-F3 sauter à un boss, F4 donner tout le build sauf fusions, F5 forcer un level-up, F6/F7 invincibilité joueur/ennemis, F8/F9/F11 faire apparaître une gemme petite/moyenne/grande, F12 vider les ennemis. |
| `GameManager.cs` | Singleton central d'une run : état Game Over/Pause, spawn du personnage sélectionné (avec son skin), chrono, compteur de kills/boss. |
| `DevOnly.cs` | Classe statique (2026-10-19) : point d'appel unique pour cacher un objet hors éditeur/build dev (`Application.isEditor \|\| Debug.isDebugBuild`) - utilisé par les 3 boutons de debug du jeu (Réputation, Personnages, Compétences), 2 d'entre eux restaient visibles en version release avant cette passe. |
| `DifficultyTiers.cs` | Classe statique (2026-10-19) : les 3 paliers de difficulté du mode Classique (Modéré/Violent/Apocalyptique, persisté par `PlayerPrefs`) - taille de carte, PV/dégâts ennemis, plafonds de spawn, Or/Éclats, nombre de décors de bordure. |
| `GameModes.cs` | Classe statique : définit les modes de jeu (Classique/Sans fin/Ruée de boss, + Titans conservé mais retiré de la sélection via `IsShippable` depuis le 2026-10-19), leurs règles d'échelle et si les fusions sont activées pour ce mode. |
| `GameSceneSettingsApplier.cs` | Applique le réglage Ombres (Paramètres) à la lumière directionnelle de la scène Game au démarrage. |
| `LevelUpManager.cs` | Singleton qui gère les niveaux d'upgrade obtenus, présente les 3 cartes de choix (`UpgradeUI`) à chaque montée de niveau, garantit l'apparition de l'arme exclusive du personnage tôt dans la run. Retient aussi le total des bonus « Dégâts+ » / « Cadence+ » pris (`DamageBonusTotal` / `FireRateBonusTotal`), pour qu'ils soient additifs et identiques sur toutes les armes. Depuis le 2026-10-19, `GrantFreePicks(int)` accorde des paliers directement (coffre de boss), sans passer par l'XP. |
| `MapBoundaryUtils.cs` | Classe statique utilitaire : borne une position à la zone de jeu carrée. `ZoneHalfSize` n'est plus une constante depuis le 2026-10-19 (varie selon le palier de difficulté du Classique, fixée une fois par `GameManager.Awake()`) ; l'ancienne copie imbriquée dans `WaveManager` a été supprimée (doublon, voir `AUDIT_VERSION_FINALE.md` §10). |
| `MetaProgressionManager.cs` | Singleton persistant (`DontDestroyOnLoad`) : progression méta complète — Or/Éclats, sauvegarde (`SaveSystem`), déblocage de personnages, bonus de Réputation, aperçu du prochain palier pour le Game Over. |
| `ObjectPool.cs` | Singleton générique de pooling d'objets par tag (ennemis, projectiles, VFX...), pré-instanciés à taille fixe. |
| `SaveSystem.cs` | Classe statique : lecture/écriture de `save.json` (+ backup automatique), avec `SuppressWrites` pour tester sans toucher à la vraie sauvegarde. |
| `SkinCatalog.cs` | ScriptableObject : catalogue des skins de personnage (Classique=Or / Prestige=Éclats), un skin = une ligne éditable dans l'Inspector, aucun code à toucher pour en ajouter un. |
| `WaveManager.cs` | Singleton pilotant l'apparition des 3 boss classiques (ou la logique Sans fin/Ruée de boss/Titans selon le mode courant). La densité d'ennemis est réglée par les phases de l'`EnemySpawner` ; `WaveManager` n'y ajoute que la montée du mode Sans fin après 15 min (cadence jusqu'à 0,3 s, plafond 160 → 200). |
| `XPGem.cs` | Gemme d'XP ramassable poolée (3 tailles), attirée vers le joueur une fois dans son rayon d'attraction. |
| `XPGemSpawner.cs` | Singleton : calcule combien/quelles gemmes (10/20/50) spawner à la mort d'un ennemi, reporte le reste < 10 sur la mort suivante (aucune XP perdue), gère le rayon d'attraction global (débloqué niveau 3). Atterrissage toujours aplati au sol (`GemLandY=0`, 2026-10-23) plutôt que sur la position racine de l'ennemi mort - certains ennemis ont une racine haute (pose VAT), leurs gemmes flottaient sinon. La gerbe de gemmes (`SpawnBossGemBurst`) ne sert plus qu'aux mini-boss invoqués depuis le 2026-10-19 (un vrai boss fait apparaître un `BossChest`, voir Weapons). |
| `XPSystem.cs` | Singleton : XP courante, formule de niveau (`baseXP × niveau²`, baseXP=70 depuis le 2026-10-21 - était `55 × niveau^1,5`), déclenche les montées de niveau. |

## Player (`Assets/Game/Scripts/Player`)

| Script | Rôle |
|---|---|
| `CharacterIdentity.cs` | Composant minimal qui tague un prefab joueur avec son `CharacterType` (Aether/Kael/Lyra). |
| `CrystalSystem.cs` | Charges d'Ultime, absorption au Dash, Nova (dégâts + ralentissement de zone), scaling de l'Ulti dans le temps, soin de "Récupération" à l'absorption. `OverpowerBuff()` : Surpuissance dure **10 s** (5 s jusqu'au 2026-10-23). |
| `HealthSystem.cs` | PV du joueur, invincibilité (dash + externe), régénération/armure méta, retient la cause de mort pour le message de Game Over. |
| `ManaShield.cs` | Nœud capstone Gardien : bouclier à charges qui annule totalement un projectile ennemi et alimente le Cristal comme une absorption ; verrouillage temporaire si les charges tombent à 0. |
| `PlayerBuffs.cs` | Multiplicateur STATIQUE de dégâts sortants du joueur (`OutgoingDamageMultiplier` = Concentration × Surpuissance), lu par TOUTES les armes au moment des dégâts. Concentration est gérée ici ; Surpuissance est activée par `CrystalSystem` via `SetOverpowerActive`. |
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
| `EnemyProjectile.cs` | Projectile tiré par un ennemi/boss shooter : vol en ligne droite, portée max, dégâts au joueur (fenêtre anti-cumul), cause de mort « boss » ou « horde » transmise par `Init`. |
| `EnemyShooter.cs` | Ennemi tireur mobile (hérite `EnemyBase`) : garde ses distances (fuit si trop proche, se positionne à portée préférée) et tire. |
| `EnemySpatialGrid.cs` | Classe statique : grille spatiale des ennemis actifs (séparation entre ennemis sans appel physique par ennemi) ; `Count` = nombre d'ennemis normaux actifs, lu par `EnemySpawner`. |
| `EnemySpawner.cs` | Fait apparaître les ennemis normaux. **Seule référence de la densité** : 6 phases réglables dans l'Inspector (scène Game, liste « Phases De Jeu » : plafond, ennemis par vague, cadence), calées sur `GameManager.RunTimer`, + paliers de composition (types d'ennemis, multiplicateur de PV). |
| `EnemyTank.cs` | Ennemi "costaud" basique — hérite entièrement de `EnemyBase` sans rien ajouter (juste des stats différentes sur son prefab). |

## Weapons (`Assets/Game/Scripts/Weapons`)

| Script | Rôle |
|---|---|
| `EnemyLayers.cs` | Classe statique : masque de couche « Enemy » partagé par toutes les recherches d'ennemis des armes (évite que gemmes/décor remplissent les tampons de détection). |
| `BouncingOrbProjectile.cs` | Instance poolée de l'Orbe Rebondissant : rebondit indéfiniment sur les bords de l'écran visible, traverse les ennemis sans se détruire. |
| `LightningBoltVFX.cs` | Utilitaire statique : dessine les éclairs visibles (ricochet + flash d'impact) de la Foudre et de ses fusions, avec un pool interne de `LineRenderer` pour éviter le churn d'objets à haute fréquence. |
| `LightningChainUtility.cs` | Algorithme statique de chaîne de foudre (saut d'ennemi en ennemi, dégâts dégressifs), extrait de `WeaponLightningChain` pour être réutilisé tel quel par les fusions qui ajoutent la foudre. |
| `MudPuddleZone.cs` | Instance poolée d'une flaque de Boue : ralentissement + DPS léger aux ennemis (et boss) dans son rayon pendant sa durée de vie. |
| `BossChest.cs` | Instance poolée (2026-10-19) : coffre qui apparaît à la mort d'un vrai boss (remplace le burst d'XP) - pop d'apparition, bob/rotation en attendant, ramassage par trigger (hitstop + `ExpandingRingVFX`) qui accorde `LevelUpManager.GrantFreePicks(2)`. |
| `OrbitalProjectile.cs` | Un éclat en orbite autour du joueur (Orbitaux) : dégâts de contact avec cooldown par ennemi. |
| `ProjectileBasic.cs` | Projectile générique de l'attaque de base et de plusieurs armes : portée, fragmentation à l'impact, perforation, Brûlure, Ricochet (fusion Lames Ricochet). |
| `WeaponAura.cs` | Champ de dégâts/ralentissement continu autour du joueur (anneau visuel procédural en `LineRenderer`). |
| `WeaponBase.cs` | Attaque de base commune à tous les personnages : tir automatique vers l'ennemi le plus proche détecté, gère Double Tir et les modificateurs de dégâts/cadence des upgrades. |
| `WeaponBouncingOrb.cs` | Orbe Rebondissant : crée/gère les instances de `BouncingOrbProjectile`. |
| `WeaponFireball.cs` | Boule de feu (Aether) : explosion garantie à l'impact, palier 2 = dégâts, palier 3 = Brûlure. |
| `WeaponFusionCursedSwamp.cs` | Fusion "Marécage Maudit" (Boue + Foudre, universelle) : flaques de Boue qui déclenchent aussi une décharge de foudre en chaîne. |
| `WeaponFusionManaVortex.cs` | Fusion "Vortex de Mana" (Aura + Orbitaux, Kael) : champ de l'Aura + éclats en orbite des Orbitaux réunis, plus forts que la somme des deux. |
| `WeaponFusionMudOrb.cs` | Fusion "Sillage Boueux" (Orbe rebondissant + Boue, universelle, créée le 2026-10-18 en remplacement d'"Orbes Jumeaux") : les orbes rebondissants laissent derrière eux un sillage continu de flaques de Boue (déposées à la position réelle de chaque orbe, jamais à distance fixe du joueur). |
| `WeaponFusionRicochetBlades.cs` | Fusion "Lames Ricochet" (Couteaux + Orbe rebondissant, Lyra) : salve en éventail dont les lames ricochent vers l'ennemi non touché le plus proche une fois leur perforation épuisée. |
| `WeaponFusionScorchedEarth.cs` | Fusion "Terre Calcinée" (Boule de feu + Boue, universelle, refondue le 2026-10-23 — n'est plus un simple reskin de la Boue) : bombardement de météorites, jusqu'à 3 simultanées par salve (nombre ADAPTATIF selon le nombre d'ennemis/boss distincts trouvés sur toute la carte, jamais 2 météores sur le même ennemi, dégât total de la salve réparti entre elles) — télégraphe au sol + chute visible (`GroundTelegraphFX`/`MeteorFallVFX`) puis déflagration (`FireExplosionVFX`) et cratère de lave résiduel (`MudPuddleZone`, teinte marron boueux). |
| `WeaponFusionThunderingOrbs.cs` | Fusion "Orbes Foudroyants" (Orbitaux + Foudre, universelle) : orbes en orbite qui déclenchent une chaîne de foudre à chaque impact. |
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
| `ChallengeDatabase.cs` | Classe statique : liste des 12 défis (4 Facile / 3 Moyen / 5 Difficile - "Sans Cristal" reclassé Moyen → Difficile le 2026-10-21, jugé trop punitif pour son palier), chacun avec id, nom, description, difficulté. |
| `ChallengeManager.cs` | Singleton (scène Game) : tire le défi de l'heure (UTC, anti-répétition), suit sa progression/échec en temps réel, calcule et applique la récompense en or. |

## Settings (`Assets/Game/Scripts/Settings`)

| Script | Rôle |
|---|---|
| `GameInput.cs` | Classe statique : point de lecture UNIQUE des commandes du joueur (`Held`/`Down`/`Move`) à travers `InputBindings`, clavier + manette. |
| `GameSettings.cs` | Classe statique : table de toutes les clés de réglages (audio, affichage, graphismes, jeu, interface) avec valeurs par défaut, événement `Changed`, écriture PlayerPrefs différée. |
| `InputBindings.cs` | Classe statique : configuration des touches (2 emplacements par action), défauts AZERTY (QWERTY/ZQSD→WASD si Windows n'est pas en français), préréglages, détection de conflits (échange automatique). |
| `SettingsAmbientFX.cs` | Ambiance procédurale du fond de la page Paramètres (lueurs de bougies/cristaux, poussière), respecte le réglage "Effets d'ambiance des menus". |
| `SettingsApplier.cs` | Singleton persistant auto-créé au boot : applique EN DIRECT tous les réglages (`GameSettings`) dans toutes les scènes — audio, affichage, résolution, V-Sync, FPS, qualité, ombres, pause au perte de focus. |
| `SettingsDropdown.cs` | Liste déroulante générique utilisée par les lignes "Choix" de la page Paramètres (défilement à la molette au-delà de 8 valeurs). |
| `SettingsPage.cs` | Contrôleur de la page Paramètres : carrousel de catégories, construction des lignes depuis `SettingsSchema`, gère les fenêtres de confirmation (affichage, touche, reset). |
| `SettingsRow.cs` | Une ligne de la page Paramètres (curseur/interrupteur/choix/touche/préréglages) : un seul composant, n'active que les éléments de son type. |
| `SettingsSchema.cs` | Classe statique : définition des catégories et lignes de la page Paramètres (données pures, lues par `SettingsPage`). Contient aussi `SettingsStyle` (palette de couleurs de la page). |

## Localization (`Assets/Game/Scripts/Localization`)

| Script | Rôle |
|---|---|
| `Loc.cs` | Classe statique : langue courante (français, anglais, allemand, espagnol, portugais du Brésil, italien, polonais, russe, chinois simplifié, japonais, coréen ; réglage `GameSettings.Language`, défaut = langue de Windows, anglais si elle n'est pas gérée). Le TEXTE FRANÇAIS EST LA CLÉ : `Loc.T("Reprendre")`, `Loc.F("Niveau {0}", n)`, `Loc.Plural`. Pluriels selon la règle de chaque langue (3ᵉ forme polonaise et russe, singulier russe pour 21, 31…, pas d'accord en chinois, japonais, coréen). `Loc.Num(v, "0.#")` : nombre à virgule avec le séparateur décimal de la langue du jeu. Événement `Loc.Changed` pour le changement en direct. Clé absente → repli anglais puis français + avertissement console `[Loc] Traduction manquante (langue)` (une fois). |
| `LocTableEN.cs` | La table français → anglais : RÉFÉRENCE de la liste des clés (toute nouvelle clé s'ajoute ici d'abord, en fin de table). |
| `LocTableDE.cs` / `LocTableES.cs` / `LocTablePT.cs` / `LocTableIT.cs` / `LocTablePL.cs` / `LocTableRU.cs` / `LocTableZH.cs` / `LocTableJA.cs` / `LocTableKO.cs` | Tables allemande, espagnole, portugaise (Brésil), italienne, polonaise, russe, chinoise (simplifié), japonaise, coréenne. GÉNÉRÉES par `Tools/loc/langs.js build xx` depuis `Tools/loc/tr_xx.txt` (une ligne « numéro\| traduction » par entrée de `skeleton.txt`) : corriger dans le .txt puis régénérer. |
| `LocalizedText.cs` | Composant posé sur un texte FIXE : se retraduit au changement de langue sans écraser un texte réécrit par le code. Posé automatiquement au chargement de chaque scène sur tout texte dont le contenu est une clé de la table (`LocalizeTree`, les scènes ne sont pas modifiées) ; `LocalizedText.Attach(tmp)` pour les libellés fixes construits par code. |
| `LanguageFonts.cs` | Met en tête des polices de repli TMP celle de la langue courante (chinois / japonais / coréen : un même idéogramme n'a pas le même dessin en Chine et au Japon), au démarrage et à chaque changement de langue ; ordre d'origine remis en sortie de Play. |
| `CjkTextStyle.cs` | Pas d'italique en chinois, japonais ni coréen : les textes de scène en italique passent en droit dans ces langues (faux italique TMP illisible sur des idéogrammes), et retrouvent leur italique dans les autres langues. Suit les changements de langue en direct. |

Outils de vérification (Node, dossier `Tools/loc/`) : `check_keys.js` (tout littéral passé à `Loc.T/F/Plural` existe dans la table), `check_data.js` (textes stockés en données : compétences, défis, personnages, skins...), `scene_texts.js` (textes TMP des scènes/prefabs absents de la table ; ceux qui restent sont des textes de remplissage réécrits par le code), `apply.js` (remplacements exacts en lot depuis un JSON), `langs.js` (`skeleton` : liste numérotée des clés ; `build xx` : génère la table d'une langue ; `check` : contrôle toutes les tables — clés manquantes, trous {0}, balises, retours à la ligne), `fix.py` (corrige des lignes d'un tr_xx.txt). `langs.js check` contrôle aussi l'écriture : ponctuation chinoise (pas de — … “ ” + que Bangers dessinerait), mots russes mêlant lettres latines et cyrilliques, guillemets droits en russe.

Polices des langues hors alphabet latin (voir NOTES.md, session 2026-10-15) : `Tools/patch_bangers_cyrillic.py` ajoute le cyrillique à Bangers (lettres de Fira Sans Extra Condensed Black Italic remises aux proportions de Bangers, crénage compris) ; `Tools/build_language_fonts.py` réduit Noto Sans SC / JP / KR Black aux caractères chinois / japonais / coréens du jeu ET vérifie que chaque caractère de chaque langue est dessinable. Sources et licences OFL : `Tools/fonts/`.

## UI (`Assets/Game/Scripts/UI`)

| Script | Rôle |
|---|---|
| `AbandonConfirmReturnToPause.cs` | Posé sur la fenêtre de confirmation d'abandon : « Non » rouvre le menu Pause au lieu de laisser le jeu figé sans menu. |
| `CharacterAmbientFX.cs` | Ambiance magique procédurale de l'onglet Personnage (brume, bokeh, poussières, éclats), teintée à la couleur du personnage actif. |
| `CharacterInfoPanel.cs` | Remplit la fiche personnage de l'onglet Personnage (épithète, arme exclusive, progression réelle de l'arbre, parcours) — ne construit pas la hiérarchie. |
| `CharacterLockOverlay.cs` | Habillage visuel d'un personnage verrouillé (voile, chaînes, cadenas dans un médaillon), teinté à sa couleur. |
| `CharacterNameLight.cs` | Génère la "lumière" additive (halo, reflets, étincelles) derrière/sur le logo-nom d'un personnage, calculée depuis le logo affiché. |
| `CharacterSelectUI.cs` | Contrôleur de l'onglet Sélection de personnage : fond dédié par personnage, portrait, logo, verrouillage. |
| `CustomCursor.cs` | Remplace le curseur système par le gantelet fantasy (`Resources/Cursor_Gauntlet`) via une Image UI qui suit la souris ; objet auto-créé dans toutes les scènes (comme `SettingsApplier`), masqué pendant le jeu actif et affiché sur les panneaux (pause/paramètres/level-up/Game Over) via `GameManager.NeedsCursor`. |
| `DamageNumber.cs` | Instance poolée d'un nombre de dégâts flottant (texte 3D, contour, montée + fondu). |
| `DifficultySelector.cs` | Sélecteur du palier de difficulté du Classique (2026-10-19), sous `GameModeSelector` dans l'onglet Jouer : rangée fixe de 3 boutons toujours cliquables (jamais verrouillé, contrairement au mode de jeu), visible seulement en Classique. |
| `GameModeSelector.cs` | Sélecteur de mode de jeu du menu principal (flèches, nom, description, record), verrouille JOUER si le mode choisi n'est pas débloqué. Saute les modes non commercialisés (`GameModes.IsShippable`) depuis le 2026-10-19. |
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
| `SummonPortalFX.cs` | Effet d'invocation (construit en code) des mini-boss de la Source Corrompue : cercle runique qui grandit, halo violet, éclats. |
| `TreeAmbientFX.cs` | Ambiance lumineuse procédurale de l'onglet Compétences (halos, rayons de soleil, lucioles, feuilles, braises). |
| `TreeSprites.cs` | Classe statique : sprites de lumière générés en code pour l'onglet Compétences (halo, couronne, rayon). |
| `UpgradeCardHover.cs` | Transmet les événements de survol souris d'une carte d'arme vers `PauseMenuUI` (fermeture en fondu du tooltip de description). |
| `CloseButtonHover.cs` | Survol de la croix de fermeture (panneau de description de l'arbre de compétences) : fond rond qui passe au rouge, croix en blanc, bouton qui grossit. |
| `TextBalancer.cs` | Global, sans composant à poser : équilibre la longueur des lignes des textes centrés sur plusieurs lignes, passe en Justifié les paragraphes alignés à gauche (Fira) de 3 lignes ou plus, et rend insécables (balises <nobr>) les espaces dans les parenthèses et avant « : ; ! ? % » ; FitToLines cale la police pour tenir en N lignes (descriptions des cartes classiques : 2 lignes). |
| `UpgradeSlotRefs.cs` | **Références du prefab de carte d'arme**, partagées par `UpgradeSlot` (menu Pause, style parchemin) et `UpgradeGridSlot` (Victoire/Défaite, tuile teintée) : icône, nom, pastilles de palier, losange de déblocage, et le visuel dédié de Fusion (icône + 2 sources + séparateur + "+", Pause uniquement). |
| `UpgradeUI.cs` | Contrôleur des 3 cartes de choix de level-up : remplit nom/description/icône/pastilles, anime le pick, et gère la disposition spéciale + le bandeau "FUSION DISPONIBLE !" quand une fusion fait partie du choix. |
| `WheelToHorizontal.cs` | Convertit le défilement molette (vertical) en défilement horizontal, posé sur les rangées de skins de l'onglet Réputation. |

## Animation (`Assets/Game/Scripts/Animation`)

| Script | Rôle |
|---|---|
| `EnemyAnimatorController.cs` | Petit pont vers l'Animator d'un ennemi (`IsAttacking`, gel de l'animation en fin de partie). |
| `PlayerAnimatorController.cs` | Petit pont vers l'Animator du joueur (marche, mort, pose Idle forcée pour le bake du Clone spectral). |
| `VATAnimatedRenderer.cs` | Animation d'ennemis par texture (VAT, gros gain de performance) : remplace Animator + SkinnedMeshRenderer, avance juste un numéro de frame par instance (lu par `VATLit.shader`), déphasage aléatoire. |
| `VATClipSet.cs` | Plages de frames de chaque clip (« Walk », « Bite »...) dans la texture d'animation cuite d'un ennemi. |
| `VATBoneAttachment.cs` / `VATBoneAttachmentData.cs` | Fait suivre un objet tenu (sarbacane du Gobelin...) au corps animé par VAT, via une petite courbe position/rotation précuite. |

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
| `BossFX.cs` | Effets d'ARRIVÉE et de MORT des boss, construits en code (flash, ondes de choc, pilier de lumière, braises, court ralenti global qui ne se bat jamais avec la pause). |
| `CameraShakeFX.cs` | Tremblement de caméra (« trauma ») réservé aux gros impacts (explosion de Boule de feu, arrivée/mort de boss), figé en pause. Pas encore d'option pour le désactiver (voir audit). |
| `ChargeLaneFX.cs` | Couloir de charge d'un boss construit en code : couloir entier visible, remplissage selon le windup, chevrons qui défilent. |
| `EndScreenIntro.cs` | Animation d'arrivée des panneaux Victoire/Défaite (fondu, carte qui monte, éléments en cascade). |
| `EnemyDeathFX.cs` | Petit éclat (flash + 5 étincelles, ~0,3 s) à la mort d'un ennemi normal. |
| `FireExplosionVFX.cs` | Déflagration enflammée poolée en plusieurs couches (flash, 2 `FlameBurst` superposés, braises qui jaillissent/montent, fumée qui traîne) : impact de la Boule de feu, réutilisée telle quelle pour l'impact de la fusion "Terre Calcinée" (météorites). |
| `FXSprites.cs` | Bibliothèque partagée des effets de combat : sprites générés en code (blancs, teintés par le `SpriteRenderer`), matériau commun, courbes d'animation. |
| `GroundTelegraphFX.cs` | Signal de danger au sol construit en code (zone à rayon fixe qui se remplit, ou onde), utilisé par toutes les attaques télégraphiées des boss et par la fusion "Terre Calcinée". |
| `ImpactSparkVFX.cs` | Éclat d'impact générique poolé (4 traits, 0,18 s) à la couleur de l'arme qui frappe ; compteur remis à zéro à chaque scène. |
| `ExpandingRingVFX.cs` | Fonction statique : anneau procédural (`LineRenderer`) qui s'agrandit puis s'estompe — utilisé par la Nova, le souffle de départ du Dash, etc. |
| `MeteorFallVFX.cs` | Objet qui tombe du ciel pendant la fenêtre de télégraphe (billboard caméra, pool statique de Runner réutilisés), utilisé uniquement par la fusion "Terre Calcinée" — `UpdateLandPosition` permet de recaler la chute en vol tant que l'ennemi ciblé reste vivant. |
| `MusicStarter.cs` | Singleton : lance/change la musique avec fondus courts, attend que les volumes sauvegardés soient appliqués avant de démarrer. |
| `XPGemCollectFX.cs` | Petit éclat (~0,22 s) au ramassage d'une gemme d'XP. |

---

## Outils Éditeur (`Assets/Editor`)

Scripts `[MenuItem]` sous le menu **Aether >**, jamais exécutés en jeu — ils (re)construisent des hiérarchies de scène idempotentes (supprimer ce qu'ils ont créé avant, refaire à neuf) ou préparent des assets.

| Script | Rôle |
|---|---|
| `BangersDigitsSetup.cs` | **Aether > Rebuild Bangers Digits** : branche la police Bangers retouchée (1/7 lisibles, accents réduits, cyrillique ajouté) sur l'atlas TMP, VIDE l'atlas puis le remplit (caractères déjà présents + ASCII, Latin-1, Latin étendu A, alphabet russe, ponctuation), crénage compris. |
| `LanguageFontsSetup.cs` | **Aether > Rebuild Language Fonts** : atlas TMP statiques des polices chinoise, japonaise et coréenne (`Assets/Game/Fonts/Lang/NotoSans<SC|JP|KR>-Black SDF`) depuis les polices réduites et leurs listes de caractères (`.chars.txt`), inscrits en polices de repli globales (TMP Settings). À chaque compilation : erreur console si un caractère de ces langues manque à la fois à Bangers et à l'atlas de sa langue. |
| `InputAxesSetup.cs` | **Aether > Setup Input Axes** : crée les axes manette `PadMoveX`/`PadMoveY` dans les Input Settings du projet (déjà exécuté une fois). |
| `MenuBackgroundBuilder.cs` | **Aether > Rebuild Menu Background** : construit le fond vivant du menu principal (3 illustrations + effet de vent + feuilles/oiseaux). |
| `ReputationPageBuilder.cs` | **Aether > Rebuild Reputation Page** : construit l'onglet Réputation complet (bonus + boutique de skins), crée `SkinCatalog.asset` s'il n'existe pas. |
| `SettingsPageBuilder.cs` | **Aether > Rebuild Settings Page** : construit la page Paramètres complète (carrousel de catégories, lignes). |
| `SkeletonSwapTool.cs` | Fenêtre d'éditeur (`EditorWindow`) : remplace le squelette d'un modèle Tripo3D par un squelette Mixamo en conservant mesh/texture/poids. |
| `VATBaker.cs` | Outil de cuisson VAT : précuit les animations d'un ennemi en textures (position + normale) + mesh statique + matériau `VATLit`. |
| `SkillTreePageBuilder.cs` | **Aether > Rebuild Skill Tree Page** (+ variante Nodes) : construit l'onglet Compétences complet (nœuds, liaisons, fiche, ambiance). |

## Outil trailer (`Assets/TrailerCapture`)

Éditeur UNIQUEMENT (`#if UNITY_EDITOR`, jamais compilé dans un build) ; tout le dossier peut être supprimé sans effet sur le jeu.

| Script | Rôle |
|---|---|
| `TrailerShotDriver.cs` | Met en scène une partie (ou le menu) et capture les plans du trailer Steam image par image (`Time.captureFramerate`, 60 i/s). |
| `Editor/TrailerCapture.cs` | Passe la Game View en 1920×1080, lance un plan puis assemble les images en MP4 (ffmpeg de Remotion) dans `../Aether Storm Trailer/public/shots/`. |

## Hors jeu (exemples Unity, à supprimer avant la sortie)

`Assets/TutorialInfo/Scripts/Readme.cs` + `Editor/ReadmeEditor.cs` (page d'accueil du modèle de projet URP) et `Assets/Samples/Sequences/.../AssignDefaultMaterial.cs` (exemple du paquet Sequences) : ne font pas partie du jeu.
