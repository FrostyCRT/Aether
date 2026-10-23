# Aether Storm Survivor : audit complet avant version finale

Audit du 2026-10-15. J'ai lu **tous les scripts du jeu** : Systems, Enemies, Player, Weapons (+ 6 fusions), Animation, Settings, Data, UI (toutes les pages), Music & VFX, Challenge, Localization, WaitingScreen, Editor, TrailerCapture. J'ai aussi regardé les prefabs, les scènes et les réglages du projet.
Ce document ne change rien au code : c'est une liste de travail. Chaque point donne le fichier et la ligne, ce qui se passe, et la correction proposée.

> ### ✅ Avancement au 2026-10-16 : étape 1 du plan faite (§12)
> Corrigés **et vérifiés en Play** : 1.1 PV du Cerf (90 000 mesurés) · 1.2 soin des boss (600 → 1600 sur 2000, soit +50 %) · 1.3 Ultime en pause + hitstop (la pause reste à `timeScale` 0, et sans pause le temps revient bien à 1) · 1.6 double mort (+1 kill / +1 or seulement, y compris pour un ennemi ressorti du pool) · 1.7 kills après la fin de partie (plus comptés) · 1.8 anneau du Vortex (y = 0,05, rayon 8).
> Corrigés (code relu, scénario difficile à reproduire en test) : 1.9 frappe du boss 3 bornée à la carte + multiplicateur du mode sans fin sur Frappe/Implosion/Ressurgissement · 2.1 fuite du maillage du clone de Lyra · 2.2 compteurs statiques des éclairs **et** des éclats d'impact (`ImpactSparkVFX` avait le même défaut) remis à zéro à chaque scène · 2.3 masque de couche « Enemy » ajouté à **toutes** les recherches d'ennemis des armes (16 requêtes, nouveau `Weapons/EnemyLayers.cs`) et à celles de l'Ultime/Nova · 2.12 cause « boss » pour les projectiles de boss et le saut du Cerf.
> **Nouveau bug trouvé pendant les tests et corrigé** : `XPGemSpawner.BossGemBurstRoutine` parcourait sur plusieurs frames la liste partagée de `SpawnGems`. Un ennemi tué pendant la pluie de gemmes d'un boss la vidait : `ArgumentOutOfRangeException`, gerbe interrompue et **XP du boss perdue**. Le scénario a été reproduit, puis revérifié sans erreur.
> ### ✅ Étape 2 faite (décisions validées par l'utilisateur, vérifiée en Play)
> - **1.4 Spawner** : les phases de l'`EnemySpawner` sont désormais la seule référence, et leur progression suit `RunTimer`. `WaveManager.ApplyDifficulty` ne garde que la montée du mode sans fin après 15 min (cadence jusqu'à 0,3 s, plafond 160 → 200). Le comptage passe par `EnemySpatialGrid.Count` au lieu de `FindGameObjectsWithTag`. Mesuré à 12,5 min : **160 ennemis** actifs (contre 60 avant), cadence 0,6 s, ~100 FPS dans l'éditeur.
> - **1.5 XP** : le reste < 10 est reporté sur la mort suivante (XP totale exacte, aucune gemme en plus). Courbe allégée : `XPSystem._baseXP` passe de 50 à 55 (scène + code). ⚠️ Correction de mon estimation : la perte réelle était de **0 à 15 % selon la phase** (environ 12 % entre 2 et 8 min, presque rien ensuite), pas « jusqu'à 33 % ».
> - **1.10 Bonus de dégâts** : Dégâts+ et Cadence+ sont additifs et identiques sur toutes les armes (mesuré : ×1,4 après 2 prises, ×1,6 après 3, contre ×1,44 / ×1,73 avant sur les armes multiplicatives). Une arme obtenue plus tard rattrape le total. Les Lames Ricochet rattrapent la Cadence+. **Surpuissance** passe dans `PlayerBuffs.OutgoingDamageMultiplier` (×2 sur toutes les armes). L'**Aura**, le **Vortex de Mana**, les **Couteaux** et les **Lames Ricochet** lisent maintenant ce multiplicateur. Ils ignoraient aussi la **Concentration** : c'était un bug de plus.
> - ⚠️ Correction du point 2.4 : dans la scène, le rayon d'apparition est déjà à **22** (le 15 n'était que la valeur par défaut du code). Pas de problème de « pop » à l'écran pour les ennemis normaux. Seuls les Bulbes (10 u, volontairement) apparaissent dans le champ.
>
> ### ✅ Diagnostic complet du 2026-10-19 (partie de l'étape 3, vérifié en Play/compilation réelle)
> Corrigés et compilés (plus, pour 1.11, testé visuellement en direct) : **1.11** l'Ultime a maintenant un vrai retour visuel plein écran (`GameUI.ShowUltEffect`) · **2.5** boss spawné vers le centre de la zone au lieu d'un décalage fixe (+10 en X) qui pouvait le placer sur le joueur près d'un bord · **2.8** fuite de Material TMP (`ApplyReadableOutline`) corrigée par un matériau partagé par (police, largeur) · **2.9** course sur le volume de musique corrigée (`_targetVolume` fixé une fois dans `Awake()`) · **2.14** 4 sites de NullReference potentiel (`PlayerController`, `HealthSystem`) protégés par des gardes `!= null` · **2.15** `DashCircleUI.cs` supprimé · **§3** nouveau `DevOnly.Show()` : 2 boutons de debug restaient VISIBLES EN RELEASE (Réputation donnait 20 000 Éclats gratuits, Personnages déverrouillait Kael/Lyra) - corrigé, méthodes `Debug*` entourées de `#if UNITY_EDITOR || DEVELOPMENT_BUILD` · **§6** sauvegarde atomique (`File.Replace`), champ `version`, fichier `save.corrupt` conservé si save+backup illisibles · **§9** "Gold :" → "Or :" dans la clé française, phrase de confirmation de sortie reformulée, nouveau `Loc.Int()` avec séparateur de milliers appliqué à tous les compteurs d'Or/Éclats · **§10** 3 fichiers confirmés inutilisés supprimés (`PulseVisual.prefab`, le doublon de `BouncingOrbProjectile.prefab`, `DashCircleUI.cs`), `MetaProgressionManager.GetBonusXP()` mort retiré, 3 commentaires périmés corrigés, **`MapBoundaryUtils` dédoublé corrigé** (classe imbriquée dans `WaveManager` supprimée, 4 sites d'appel au total - 3 trouvés seulement après coup, via une vraie erreur de compilation Unity à la reconnexion du pont MCP).
> **2.7 était déjà résolu** avant cette passe (le skip de l'annonce de défi au clic/Échap/Espace était déjà implémenté - `GameUI.ChallengeIntroSkipRequested`).
> **Reporté, en attente de l'utilisateur** : nom de société (`companyName`, décision de marque qui déplace le dossier de sauvegarde - jamais après la 1re version publique) et IL2CPP (changement de build non vérifiable sans compiler un vrai build).
> **Non traité dans cette passe** : 2.4 (déjà confirmé non-bug), 2.6 (en réalité déjà corrigé le 2026-10-18 via `LevelUpManager.CancelForGameEnd`, voir `NOTES.md`), 2.13 (fusion des gemmes XP lointaines), §5/§8 (manette, profilage - "étape 4 niveau pro", hors périmètre de cette passe).
>
> Reste à faire : fin de l'étape 3 (nom de société, IL2CPP, Steam Cloud) et étape 4.
>
> ### Note du 2026-10-23 : valeurs de cet audit depuis changées par des retours utilisateur ultérieurs (détail `NOTES.md`)
> Les chiffres ci-dessous datent de cet audit (2026-10-15/19) et ne sont plus les valeurs actuelles pour ces points précis :
> - **1.1** : les 90 000 PV du Cerf (mesurés après correction de ce point) ont depuis été baissés à **63 000** (-30%, retour utilisateur "le boss 2 a toujours trop de vie") - réduction du PV de BASE dans `BossDeer.prefab`, donc identique sur les 3 paliers de difficulté.
> - **1.5** : `XPSystem._baseXP` est passé de 55 à **70**, et l'exposant de la courbe de 1,5 à **2** (2026-10-21, `70 × niveau²`). La récompense d'XP par kill, qui avait été amortie à ×3 maximum en fin de partie (2026-10-11) pour compenser, a ensuite été ramenée à sa pleine croissance (×5) le 2026-10-23 - l'amortissement empilait mal avec la nouvelle courbe et affaiblissait trop les gemmes de fin de partie.
> - **1.10** : Surpuissance dure maintenant **10 s** (était 5 s, jugé trop court par l'utilisateur, 2026-10-23) ; le multiplicateur reste ×2 sur toutes les armes.
> - Les 12 défis horaires sont maintenant répartis **4 Faciles / 3 Moyens / 5 Difficiles** ("Sans Cristal" reclassé Moyen → Difficile le 2026-10-21) et le défi temporel "Ascension Rapide" (niveau 20 avant 10 min) peut enfin échouer réellement (`ChallengeManager.Update`, manquait toute réévaluation hors évènement de jeu, 2026-10-23).
> - La fusion **Terre Calcinée** (citée dans plusieurs sections comme simple zone de Boue reteintée) a été entièrement refondue le 2026-10-23 en bombardement de météorites multi-cibles (voir `AETHER_STORM_SURVIVOR_PROJECT_COMPLET_V16.md` §2 et `SCRIPTS_INDEX.md`).

Légende :
- 🔴 **Bug confirmé** : vérifié dans le code (et en jeu quand c'était possible). Effet visible pour le joueur.
- 🟠 **Probable / à confirmer en jeu** : le code le permet, l'effet dépend du contexte.
- 🟡 **Qualité / équilibrage / perf** : pas un plantage, mais pas au niveau d'une version finale.
- ⚪ **Nettoyage** : invisible pour le joueur, utile pour la maintenance et la taille du build.

---

## 1. Bugs de gameplay 🔴

### 1.1 Le Cerf (boss 2) a 50 000 PV au lieu de 90 000
- `Enemies/BossDeer.cs:67` : `Start()` force `_maxHealth = 50000f`.
- Le prefab `BossDeer.prefab` a été remonté à **90 000** (NOTES, suite 42, « tué en 3 sec »), mais cette ligne **écrase la valeur du prefab** à chaque apparition.
- Résultat : la correction d'équilibrage de la suite 42 n'a jamais pris effet en mode Classique.
- **Correction** : supprimer la ligne 67 (laisser le prefab décider), comme pour `BossCorruptedSource`.

### 1.2 La mort des boss 1 et 2 soigne 100 % au lieu de 50 %
- `Enemies/BossBase.cs:638-639` : `healAmount = playerHP.MaxHealth * 0.5f; playerHP.Heal(healAmount);`
- Or `Player/HealthSystem.cs:218` `Heal(float percent)` attend un **pourcentage** (0 à 1) et fait `_currentHealth += _maxHealth * percent`.
- Avec 300 PV max, on passe `150`, donc +45 000 PV, plafonnés au maximum : **soin complet**, alors que le chiffre affiché dit 50 %.
- **Correction** : `playerHP.Heal(0.5f);`

### 1.3 L'Ultime (F) se déclenche pendant la pause et relance le temps derrière le menu
- `Player/CrystalSystem.cs:81` : `Update()` ne vérifie que `IsGameOver`, **pas `IsPaused`**. `GameInput.Down` lit le clavier même quand `timeScale = 0`.
- Donc la touche F pendant la **pause**, l'**écran de level-up** ou l'**annonce de défi** déclenche l'Ultime (ennemis tués, charges consommées).
- Pire : `HitstopRoutine` (`CrystalSystem.cs:297-299`) met `Time.timeScale = 0.05` puis **`1`** en temps réel. **Le jeu repart derrière le menu de pause ou l'écran de cartes**, et le joueur se fait toucher sans pouvoir jouer.
- Même risque si on appuie sur Échap pendant les quelques centièmes de seconde du hitstop : à la fin du hitstop, la pause est annulée.
- **Correction** : `if (GameManager.Instance.IsPaused) return;` dans `Update()`. Dans `HitstopRoutine`, ne remettre `timeScale` à 1 **que si le jeu n'est pas en pause** (même logique que `BossFX.UpdateSlowMo`).

### 1.4 Les phases de difficulté de l'`EnemySpawner` sont écrasées chaque frame
- `Systems/WaveManager.cs:232` `ApplyDifficulty()` (appelé **chaque frame**, l.207) impose l'intervalle de spawn et le maximum d'ennemis : 15/25/30/40/50/60, et 110 en mode sans fin.
- `EnemySpawner._phasesDeJeu` définit d'autres valeurs (max 20/45/75/120/105/160, cadences de 2 s à 0,6 s), appliquées **seulement au changement de phase**, puis écrasées à la frame suivante.
- Résultat : les maximums de 75 à 160 et les cadences des phases **ne s'appliquent jamais**. Seul `ennemisParVague` compte. Les commentaires du code (et la suite 42 des NOTES, « plafond qui grimpe à 160 ») décrivent un comportement qui n'existe pas.
- **Correction** : choisir **une seule source de vérité**. Le plus simple : garder les phases de l'`EnemySpawner` et retirer l'écriture de `max`/`interval` dans `ApplyDifficulty`. Ensuite, refaire une partie complète pour réévaluer l'équilibrage, qui va changer.

### 1.5 Une partie de l'XP est perdue à chaque ennemi tué
- `Systems/XPGemSpawner.cs:72` `CalculateGems` découpe l'XP en gemmes de 10, et **le reste inférieur à 10 est jeté**.
- Exemples : Loup 10 × 1,5 = 15, donc **10** (−33 %) ; Corbeau 25, donc **20** ; Bulbe 12, donc **10**.
- **Correction** : ajouter une petite gemme avec le reste, ou arrondir au supérieur, ou reporter le reste dans un accumulateur. Attention : l'XP réelle va augmenter de 20 à 33 %, donc revoir la courbe d'XP en même temps.

### 1.6 Un ennemi peut mourir deux fois (kill, or et XP comptés en double)
- `Enemies/EnemyBase.cs:486` `TakeDamage` n'a **aucune garde « déjà mort »**. Deux coups dans la même frame (boule de feu + aura, éclair en chaîne, Nova) peuvent appeler `Die()` deux fois : double kill, double or, double gemmes, et retour au pool deux fois (risque d'objet partagé).
- Les boss ont cette garde, pas les ennemis normaux.
- **Correction** : `if (_isDead) return;` en tête de `TakeDamage`, et `_isDead = true` au début de `Die()` (remis à faux à la sortie du pool).

### 1.7 Kills et or comptés après la mort du joueur
- Pendant les ~3 s entre la mort du joueur et l'écran de défaite, les projectiles et zones continuent de tuer. Ces kills et cet or sont comptés dans le résultat de la partie.
- **Correction** : dans `EnemyBase.TakeDamage` (ou `Die`), ignorer si `GameManager.Instance.IsGameOver`. Ou geler les armes à la mort.

### 1.8 Fusion Vortex de Mana : anneau à hauteur de torse
- `Weapons/WeaponFusionManaVortex.cs:87` : `new Vector3(cos, 0.05f, sin) * _radius`. La hauteur est **multipliée par le rayon**, donc y = 0,4 au lieu de 0,05. Et le parent est au niveau du torse.
- C'est le même bug que l'Aura (corrigé en suite 40), mais pas reporté ici.
- **Correction** : `new Vector3(cos * _radius, 0f, sin * _radius)`, avec la hauteur gérée par un parent posé au sol (comme l'Aura).

### 1.9 Boss 3 (Source Corrompue) : frappe hors de la carte et dégâts non mis à l'échelle
- `Enemies/BossCorruptedSource.cs:341` : `endPos = startPos + dir * _strikeLungeDistance`, **non borné à la zone de jeu**. Le boss peut sortir de la carte pendant sa frappe.
- Les dégâts de la Frappe, de l'Implosion et de la ré-émergence **ne sont pas multipliés par `EnemyDamageScale`** (mode sans fin), contrairement aux autres attaques.
- **Correction** : `MapBoundaryUtils.Clamp(endPos)` ; appliquer le multiplicateur.

### 1.10 Surpuissance et Dégâts+ incohérents entre les armes
- La compétence **Surpuissance** (« tes dégâts sont doublés pendant 5 s ») ne double que le **tir de base** (`WeaponBase`), pas les autres armes ni les fusions.
- L'amélioration **Dégâts+** est **additive** sur le tir de base (1 + somme : 5 × 20 % = ×2,0) mais **multiplicative composée** sur toutes les autres armes (×1,2 à chaque prise : ×2,49).
- Une arme obtenue **après** avoir pris Dégâts+/Cadence+ **ne reçoit pas** les bonus déjà pris.
- **Correction** : un multiplicateur global de dégâts/cadence porté par le joueur (ou un `StatsSystem`), lu par **toutes** les armes au moment du tir. Ça règle les 3 problèmes d'un coup.

### 1.11 Ultime sans aucun retour visuel à l'écran
- `CrystalSystem.cs:314` appelle `GameUI.ShowUltEffect(true)`, qui ne fait qu'un `Debug.Log("ULT ACTIF")`. Il n'y a pas d'effet plein écran (voir `A_MODELISER_3D.md` §6).

---

## 2. Bugs secondaires / à confirmer 🟠

| # | Fichier | Problème | Correction proposée |
|---|---|---|---|
| 2.1 | `Player/PlayerController.cs:444` | Le clone de Lyra fait `BakeMesh` dans un `Mesh` neuf **jamais détruit** (`Destroy(clone)` ne libère pas le mesh) : environ 110 maillages qui fuient par partie de 15 min | `Destroy(baked)` à la destruction du clone, ou réutiliser un seul mesh |
| 2.2 | `Weapons/LightningBoltVFX.cs:30` | `_activeCount` **statique**, jamais remis à zéro au changement de scène. Les éclairs détruits en plein fondu ne le décrémentent pas, et le plafond (120) peut finir par bloquer tous les éclairs après plusieurs parties | Remise à zéro dans un `[RuntimeInitializeOnLoadMethod]` + sur `sceneLoaded`, décrément dans `OnDestroy` |
| 2.3 | Fireball / Couteaux / Foudre (ciblage) | `OverlapSphereNonAlloc` **sans masque de couche**, contrairement au tir de base. Le buffer (50/64) se remplit aussi avec le sol et le décor. Avec beaucoup de décor (cf. le plan 3D), des ennemis ne seront plus vus | Passer le `LayerMask` Enemy |
| 2.4 | `Enemies/EnemySpawner.cs:23` | Rayon d'apparition 15 u, alors que la caméra voit environ 18 u en largeur (21 dans les coins). Les ennemis **apparaissent à l'écran sans transition** | Rayon ≥ 22, ou effet d'apparition |
| 2.5 | `WaveManager` (spawn du boss) | Boss placé à joueur + (10, 0, 0) puis ramené dans la carte : **près du bord est, le boss apparaît sur le joueur** | Choisir la direction opposée au bord le plus proche |
| 2.6 | `GameManager` | `Invoke(ShowGameOver)` en temps « scalé » : si `timeScale == 0` au moment de la mort (level-up pile à ce moment), l'écran de défaite n'apparaît pas tant que la pause n'est pas levée | `Invoke` → coroutine `WaitForSecondsRealtime` |
| 2.7 | Annonce du défi au lancement | Environ 3,8 s de gel **à chaque partie**, sans possibilité de passer | Passer au clic/à une touche, ou ne l'afficher en grand qu'à la 1re partie de l'heure |
| 2.8 | `UI/GameUI.cs:1789` `ApplyReadableOutline` | `text.fontMaterial` **crée une copie du matériau** à chaque appel (à chaque ouverture de pause/cartes) : petite fuite mémoire | Préparer un matériau partagé une fois, ou utiliser `fontSharedMaterial` avec un preset |
| 2.9 | `Music & VFX/MusicStarter.cs` `SwitchRoutine` | `startVolume` est lu au moment du changement. Si deux changements se chevauchent (boss tué moins de 1,5 s après son apparition, fondu en cours), **la musique reste moins forte jusqu'à la fin de la scène** | Mémoriser le volume cible une fois (Awake) |
| 2.10 | `EnemyBase` (flash de dégâts) | Sur les ennemis animés en VAT (Golem), vérifier quel renderer reçoit le flash blanc et si le `MaterialPropertyBlock` du flash n'écrase pas celui du VAT (animation qui saute pendant le flash) | Test en jeu |
| 2.11 | `EnemyBase` (brûlure) | La teinte de brûlure ne s'applique qu'aux `SpriteRenderer` : **aucun visuel de brûlure** sur les ennemis 3D | Voir le plan 3D |
| 2.12 | Projectiles ennemis | La cause de mort est toujours « horde », même pour un projectile tiré par un boss. Le message de défaite « boss » n'apparaît donc pas dans ce cas | Passer la source au projectile |
| 2.13 | `XPGem` | Les gemmes jamais ramassées s'accumulent sans limite (le pool grandit) en partie longue / sans fin | Fusionner les gemmes lointaines en une grosse gemme au-delà de N |
| 2.14 | `Systems/MetaProgressionManager` | `MetaProgressionManager.Instance` utilisé sans vérification dans `Awake` (PlayerController/HealthSystem) : lancer directement la scène Game dans l'éditeur = NullReference (dev uniquement) | Garde `null` ou bootstrap automatique |
| 2.15 | `DashCircleUI.cs` | Remplissage inversé (`DashCooldownPercent` = temps **restant**), mais le script n'est **utilisé nulle part** (vérifié : aucune scène ni prefab) | Supprimer le fichier |
| 2.16 | Raccourcis de fin de partie (Entrée/Espace/Échap) | En Input Manager legacy, la manette ne peut pas les utiliser | Voir §5 Manette |

---

## 3. À retirer absolument avant la sortie (debug) 🔴

| Élément | Où | État en build release |
|---|---|---|
| Bouton **« DEBUG · RÉINITIALISER »** de l'arbre de compétences | `SkillTreeUI.cs:115` | ✅ Caché en build release (`Debug.isDebugBuild`), mais `ResetSkillTree()` met encore **`totalGold = 50000`** (`MetaProgressionManager.cs:852`) |
| Bouton **« DEBUG · RÉINITIALISER »** de la page Réputation | `ReputationUI._debugResetButton` | ❌ **Visible en build release** : remet `totalEclats = 20000` (`MetaProgressionManager.cs:863`). N'importe quel joueur peut se donner 20 000 Éclats |
| Bouton blanc **« RÉINITIALISER »** de la page Personnages | `CharacterSelectUI._debugResetUnlocksButton` | ❌ **Visible en build release** : reverrouille Kael et Lyra |
| `DebugUnlockAllCharacters`, `DebugResetCharacterUnlocks`, `DebugResetReputation`, `ResetSkillTree` | `MetaProgressionManager` | Code mort en release : à supprimer ou entourer de `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` |
| `DebugCheats` (F1 à F4…) | `Systems/DebugCheats.cs:59` | ✅ Inerte en release (garde présente) |
| `Debug.Log` divers | `ShowVictory`, `ShowUltEffect`, phases du spawner, chaque sauvegarde (`SaveSystem`), boss spawné, `ChallengeManager.cs:491` | Remplissent `Player.log` pour rien : à retirer ou à entourer d'un `[Conditional("UNITY_EDITOR")]` |
| `TrailerCapture/TrailerShotDriver.cs` | Hors dossier `Editor/` | ✅ Entièrement sous `#if UNITY_EDITOR` : pas compilé en build |

**Solution propre** : un seul helper `DevOnly.Show(gameObject)` qui fait `SetActive(Application.isEditor || Debug.isDebugBuild)`, appelé sur les 3 boutons.

---

## 4. Réglages du projet (Player Settings) 🔴

| Réglage | Valeur actuelle | À faire |
|---|---|---|
| `companyName` | **DefaultCompany** | À changer (nom du studio). ⚠️ Ça **déplace le dossier de sauvegarde** (`AppData/LocalLow/<company>/<product>`). C'est sans conséquence aujourd'hui (save = données de test), mais **à faire avant la 1re version publique**, jamais après |
| `bundleVersion` | 0.1.0 | Mettre la vraie version (1.0.0) et l'afficher discrètement dans le menu (utile pour les rapports de bug) |
| Scripting backend (Standalone) | Mono (défaut) | **IL2CPP** recommandé : meilleures performances et code plus difficile à modifier (triche sur la sauvegarde/l'or) |
| `defaultScreenWidth` | 1024 (fenêtré) | Sans importance en plein écran fenêtré, mais vérifier le 1er lancement sur un écran 1440p/4K et en 16:10 / ultra-wide |
| `runInBackground` | 0 | OK (le jeu se met en pause quand on change de fenêtre). Vérifier que la **pause s'ouvre automatiquement** (`OnApplicationFocus(false)`), sinon le joueur revient en pleine horde |
| Always Included Shaders | Sprites/Default, UI/Default… présents | `Shader.Find("Aether/UI/Additive")` (CharacterNameLight, ReputationHallFX) et `"Universal Render Pipeline/Unlit"` ne sont pas garantis en build. Les ajouter à la liste, ou mieux : **référencer un matériau** au lieu de `Shader.Find` |
| `activeInputHandler` | 0 = ancien Input Manager | Voir §5 |
| Icône de l'application, splash screen | À vérifier | Icône à toutes les tailles ; splash Unity selon licence |
| Curseur | `Cursor.lockState = Confined` **en permanence** (`CustomCursor.cs:91`, `GameManager.cs:165/215`) | En **mode fenêtré**, le joueur ne peut plus sortir la souris de la fenêtre, même au menu. Confiner seulement en plein écran, ou seulement en partie |

---

## 5. Manette, clavier, accessibilité 🟠

Un jeu Steam de ce genre sera joué à la manette (et sur Steam Deck). C'est le chantier « pro » le plus important après le son.
- **Input Manager legacy** (`activeInputHandler: 0`) : les gâchettes ne sont pas assignables, la détection de manette est limitée, il n'y a pas d'icônes de boutons, et les libellés affichent « Manette N ». Le **nouveau Input System** règle tout ça (et Steam Input aussi).
- Pas de **bouton manette par défaut** pour les cartes de level-up 1-3, ni pour les orbites.
- Pas de **visée au stick droit** (si le jeu en a besoin : les armes visent toutes seules, donc c'est peut-être inutile).
- **Navigation des menus à la manette** : à tester sur toutes les pages (menu, Compétences, Réputation, Paramètres, pause, level-up, fin de partie). Il faut un `Selectable` sélectionné par défaut sur chaque panneau, et des `Navigation` explicites.
- Le **clavier AZERTY** est détecté d'après la **langue de Windows**, pas d'après la disposition du clavier. Un Belge ou un Suisse en Windows anglais aura du ZQSD affiché en WASD. Le nouveau Input System lit la vraie disposition.
- **Accessibilité manquante** : option pour **désactiver les secousses d'écran** (`CameraShakeFX` n'a aucun réglage) et **réduire les flashs** (mort des boss, Ultime, `EnemyDeathFX`). C'est demandé par beaucoup de joueurs, et c'est un point de vigilance pour la photosensibilité.
- Option **« mettre en pause quand la fenêtre perd le focus »**.

---

## 6. Sauvegarde 🟠

| Point | Détail | Correction |
|---|---|---|
| Écriture non atomique | `SaveSystem.cs:21` `File.WriteAllText` direct. Une coupure de courant pendant l'écriture laisse un fichier tronqué (le backup existe, donc c'est récupérable) | Écrire dans `save.tmp` puis `File.Replace(tmp, save, bak)` |
| Save + backup tous les deux corrompus | Le jeu repart sur une partie vierge et **écrase** le fichier au prochain enregistrement | Renommer le fichier illisible en `save.corrupt` avant de repartir |
| Pas de numéro de version | `SaveData` n'a pas de champ `version` : impossible de migrer proprement après une mise à jour | Ajouter `int version = 1` maintenant |
| Steam Cloud | À configurer (Auto-Cloud sur `persistentDataPath` : `save.json` + `save.bak`) | ⚠️ Les **réglages** sont dans les `PlayerPrefs` = **registre Windows**, donc **non synchronisés**. Soit c'est voulu (réglages par machine), soit il faut les déplacer dans un fichier |
| Triche | Sauvegarde en JSON lisible : on peut se donner de l'or en éditant le fichier | Acceptable pour un jeu solo. Sinon, une somme de contrôle simple |

---

## 7. Équilibrage et game design (à décider, pas des bugs) 🟡

- **Corriger 1.4 (spawner) et 1.5 (XP) va changer tout l'équilibrage** : refaire plusieurs parties complètes avec chaque personnage après ces deux correctifs.
- Pas d'option **relancer / bannir / passer** au level-up : c'est devenu un standard du genre (Vampire Survivors, Brotato…). C'est une bonne utilisation des Éclats ou d'une compétence.
- Quitter le jeu (Alt+F4) en pleine partie = **or de la partie perdu**. Choix à assumer, ou sauvegarder l'or au fil de la partie.
- XP perdue tant que le rayon de ramassage n'est pas débloqué (rayon 0 avant le niveau 3) : vérifier que c'est voulu.
- Défis : l'heure du défi est basée sur l'**horloge UTC du PC**. Changer l'heure de Windows donne un nouveau défi : sans conséquence pour un jeu solo.
- Compétence **« Dash Fantôme »** : le nom ne correspond pas à l'effet (« Clone spectral activable via une touche dédiée »). Renommer en « Clone Spectral » pour la clarté.
- **Maîtrise du Cristal** : la description (« réduit le nombre de charges ») ne donne pas de chiffre, contrairement aux autres compétences.
- **Les 3 boss partagent la même musique.**

---

## 8. Performance 🟡

| Point | Détail |
|---|---|
| `EnemyDeathFX.Play` et `XPGemCollectFX.Play` | **`new GameObject` + `Destroy` à chaque kill / chaque gemme**, sans pool. Avec 100+ kills par minute en fin de partie : allocations et ramasse-miettes (GC) réguliers. `ImpactSparkVFX` est déjà poolé : faire pareil |
| `EnemySpawner` | `FindGameObjectsWithTag("Enemy")` à chaque vague (le commentaire dit le contraire) : utiliser le compteur/la grille spatiale existante |
| `HudKeyLegend.cs:162` | `GameObject.FindWithTag("Player")` + `GetComponent<WeaponOrbital>()` en sondage : OK car limité par un minuteur, mais à mettre en cache |
| `LanguageFonts.OnLanguageChanged` | `FindObjectsByType<TMP_Text>(Include inactive)` : OK (rare) |
| Textures 4K des boss | Cerf et Serpent : 3 textures 4K chacun pour des objets vus de loin et de haut. **Passer en 2K** (÷4 de mémoire vidéo) |
| `Resources/Prefabs/` | 29 prefabs en double **inclus dans le build** (tout ce qui est dans `Resources/` est embarqué) |
| À mesurer | Faire une partie de 15 min + 10 min de sans fin avec le **Profiler** (GC Alloc par frame, temps CPU des ennemis, nombre de draw calls). La cible est 60 FPS stables sur une config modeste et sur Steam Deck |

---

## 9. Textes et traduction 🟡

- Le système de traduction est solide (11 langues, pluriels PL/RU corrects, séparateur décimal par langue, signalement des clés manquantes en debug seulement).
- FR à revoir :
  - « La partie en cours de préparation sera fermée » (quand on quitte depuis le menu) : formulation maladroite.
  - La pause affiche « Gold : » dans une clé française (anglicisme).
- Les grands nombres (or, Éclats) s'affichent sans séparateur de milliers (`12500` au lieu de `12 500` / `12,500`). Formater avec `Loc.Num` selon la langue.
- Tout nouveau texte (skins, sons…) doit passer par la procédure de traduction (mémoire « Jeu traduit en 11 langues »).

---

## 10. Nettoyage du projet ⚪

| Élément | Action |
|---|---|
| `Assets/Resources/Prefabs/` (29 prefabs) | Supprimer (doublons jamais chargés, embarqués dans le build). ⚠️ **Garder** `Resources/MudPuddleZone`, `Resources/OrbitalProjectile`, `Resources/BouncingOrbProjectile` à la racine de `Resources/`, qui sont chargés par leur nom |
| `Resources/PulseVisual.prefab`, `Game/Prefabs/Weapons/BouncingOrbProjectile.prefab` | Supprimer (inutilisés) |
| `RT_Aether`, `RT_Kael`, `RT_Lyra` | Supprimer, sauf si on fait des aperçus 3D de skins |
| `TripoModels/` | Doublons de `3D Models/`. Supprimer après vérification des GUID |
| `Sanglier.fbx` | Intégrer ou supprimer (voir plan 3D) |
| `Player/DashCircleUI.cs` | Supprimer (utilisé nulle part) |
| `MapBoundaryUtils` | Existe **en double** (classe imbriquée dans `WaveManager` + fichier séparé) : garder un seul |
| `MetaProgressionManager.GetBonusXP()` | Code mort |
| `FindFreeSpawnPosition` | Renvoie `Vector3.zero` en cas d'échec : un vrai point de la carte. Utiliser un `bool TryFind…(out Vector3)` |
| Encodage | 3 fichiers ne sont **pas en UTF-8** (`EndScreenIntro.cs`, `GroundTelegraphFX.cs`, `AbandonConfirmReturnToPause.cs`) et 4 ont des accents détruits (« � ») dans les commentaires (`BossFX.cs`, `ChargeLaneFX.cs`, `FXSprites.cs`, `SummonPortalFX.cs`). Ça ne touche que les commentaires et les infobulles de l'Inspector. Réenregistrer en UTF-8 |
| Commentaires périmés | `SetBossZoom « n'existe plus »` (alors qu'il est appelé), `XP x3` (c'est ×1,5), `F6` pour GrantFullBuild (c'est F4), en-tête de `WeaponBouncingOrb`, commentaires du spawner qui parlent du plafond 160 |
| `SCRIPTS_INDEX.md` | ✅ **Mis à jour le 2026-10-16** (tous les scripts du jeu y figurent, `WeaponAOE`/`EnemyKaiju` retirés). Constat d'origine : **périmé** : cite `WeaponAOE.cs` et `EnemyKaiju.cs` (supprimés), et il manque 19 scripts : BossFX, CameraShakeFX, ChargeLaneFX, EndScreenIntro, EnemyDeathFX, EnemySpatialGrid, FXSprites, GroundTelegraphFX, ImpactSparkVFX, SummonPortalFX, TrailerCapture, TrailerShotDriver, VAT*, XPGemCollectFX, AbandonConfirmReturnToPause… |
| `AETHER_STORM_SURVIVOR_PROJECT_COMPLET_V16.md` | ✅ Remis à jour le 2026-10-19 (contenu V16 : état d'avancement, systèmes actuels, reste à faire priorisé) - le nom du fichier suit maintenant le numéro de version |
| `MeshRenderer` cube/capsule désactivés à la racine des prefabs | Supprimer |

---

## 11. Ce qui est déjà au niveau « version finale » ✅

Pour être juste, beaucoup de choses sont solides :
- Architecture claire (singletons cohérents, pooling d'objets, grille spatiale des ennemis, VAT pour les foules).
- Menus très travaillés (animations d'entrée, transitions, effets d'ambiance, retour visuel au survol).
- Écran de level-up soigné (cascade, pastilles de palier, cartes de fusion distinctes, anti-double-clic, clavier 1/2/3 sur le même chemin que la souris).
- Défis horaires bien pensés (anti-répétition, défis « endurance » validés seulement au jalon de boss, récompense une seule fois par heure).
- Localisation 11 langues avec polices CJK, pluriels slaves, signalement des clés manquantes limité au debug.
- Écran de chargement avec chargement asynchrone, fondu de scène, déchargement des assets inutilisés.
- Ralenti de mort des boss qui ne se bat pas avec la pause (`BossFX.UpdateSlowMo`). C'est le bon modèle à reprendre pour l'Ultime (§1.3).
- `TrailerShotDriver` correctement exclu du build.

---

## 12. Plan d'action conseillé

**Étape 1 : bugs (½ à 1 journée)**
1.1 PV du Cerf · 1.2 soin des boss · 1.3 Ultime en pause · 1.6 double mort · 1.7 kills après la mort · 1.8 Vortex · 1.9 boss 3 · 2.1 fuite du clone · 2.2 compteur des éclairs · 2.3 masques de couche.

**Étape 2 : à décider puis rééquilibrer (1 à 2 jours de tests)**
1.4 spawner · 1.5 XP · 1.10 multiplicateurs globaux · 2.4 rayon de spawn · 2.7 annonce du défi.

**Étape 3 : sortie (indispensable)**
§3 debug · §4 Player Settings · §6 sauvegarde atomique + version · Steam Cloud · **sons** (voir `A_MODELISER_3D.md` §9).

**Étape 4 : niveau pro**
§5 manette + accessibilité · §8 profilage · contenu 3D (`A_MODELISER_3D.md`) · §10 nettoyage · mise à jour de `SCRIPTS_INDEX.md`.
