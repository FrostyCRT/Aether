# AETHER STORM SURVIVOR — Documentation Projet V8

> **Changement de nom** : le projet, précédemment nommé "AETHER", est renommé **AETHER STORM SURVIVOR** (le nom "Aether" seul étant trop répandu sur Steam / dans les jeux existants). Le personnage jouable Aether garde son nom propre — seul le titre du jeu change. Toute référence à "AETHER" dans le code, les assets ou les anciens documents désigne le projet, pas nécessairement le personnage.

---

## 1. Priorité absolue

Pipeline Tripo3D + Meshy (textures) pour boucler le contenu 3D restant. Deadline resserrée, session V8 en plein cœur du développement des personnages Kael et Lyra. Le contenu ennemis/bosses (Corbeau, Tisseuse, Brute/Kaiju, Boss 1/2/3) est désormais considéré **stable et complet**, tous les bugs connus corrigés (cf. section 3).

---

## 2. État des assets 3D

### Personnages jouables
- **Aether** : terminé, modèle non-chibi, pipeline de rig résolu (swap de squelette externe via `SkeletonSwapTool.cs`). Branche d'arbre de compétences assignée : **Guerrier**.
- **Kael** : modèle 3D terminé (tank/brute), rig Mixamo swappé et validé, arbalète lourde attachée en tant qu'objet séparé (non riggé) sur bone de main. Animator créé (Idle/Walk/Death via Mixamo). **Reste à faire** : brancher les scripts (voir section 9 — architecture personnages), test en jeu complet, upgrade de départ (Aura). Branche d'arbre de compétences assignée : **Gardien/Tank**.
- **Lyra** : non commencée. Branche d'arbre de compétences assignée : **Fantôme**.

### Ennemis et bosses — tous stables, aucun bug connu ouvert
- Loup, Gobelin (= EnemyShooter), Golem (= EnemyTank, simplifié en contact pur avec cooldown ajustable, `IsSlamming`/`Idle` retirés de l'Animator)
- Corbeau des Brumes (palier 4 min), Tisseuse Corrompue (palier 8 min), Brute Corrompue / Kaiju (palier 12 min) — tous terminés et scriptés (`EnemyCorruptedWeaver.cs`, `EnemyKaiju.cs`)
- Boss 1 (Sanglier de Mana → renommé **La Guivre de Mana**, modèle dragon chétif/corrompu réutilisant le rig kaiju de Mesh2Motion) : pattern retravaillé (charge distance fixe + recovery + windup, tir radial quasi permanent), animations Idle/Vole plane/Vole flap/Walk câblées via bools `IsCharging`/`IsWindingUp`/`IsRecovering`/`IsCruising`
- Boss 2 (Cerf Ancestral) : bug d'invisibilité résolu (angle de saut restreint via zone interdite + hauteur de saut plafonnée à 0.7), télégraphe/dégâts de saut adaptés en mini-boss (scale 0.75)
- Boss 3 (La Source Corrompue) : pattern enrichi (salves multiples Crystal Pulse, rythme resserré), mini-boss invoqués (Sanglier/Cerf réduits) désormais correctement dimensionnés (scale du `SkinnedMeshRenderer` uniquement, Collider intact)

---

## 3. Bugs résolus cette session (V8) — pour référence, ne pas re-découvrir

Tous les bugs suivants ont été identifiés et corrigés au cours de cette session, certains après plusieurs itérations :

- **Invincibilité Dash + Clone Fantôme partagée** : `HealthSystem` refactoré avec un compteur `_externalInvincibilitySources` (`AddExternalInvincibility()`/`RemoveExternalInvincibility()`) au lieu d'un bool unique `_isInvincibleExternal`.
- **Tremblement de rotation générique** (`EnemyBase.UpdateBehaviour()`) : remplacement du snap binaire par un lissage `Vector3.Slerp` continu sur `_smoothedMoveDirection`.
- **Ennemis qui tournoient autour du Clone Fantôme** : `isInContactWithTarget` calculé sur la vraie cible (`target`, pas toujours `_playerTransform`), dégâts de contact conditionnés à `target == _playerTransform` uniquement.
- **EnemyShooter — glissement pendant le tir** : suppression du mouvement de séparation en position de tir (`inFiringStance`).
- **EnemyShooter — dérive verticale (Y) en fuite/poursuite** : `fleeDirection`/`chaseDirection` aplaties sur Y avant normalisation.
- **PV scaling non appliqué sur nouveaux ennemis** : `ObjectPool.Get()` (surcharge 4 arguments) corrigé pour appliquer `SetHealthMultiplier()` avant le premier `SetActive(true)`, y compris sur instance nouvellement créée (`isNewInstance`).
- **OnEnable() masqué silencieusement** (`EnemyKaiju`, `EnemyCorruptedWeaver`) : `EnemyBase.OnEnable()` passé en `protected virtual`, sous-classes en `override` + `base.OnEnable()`. Cause du bug "PV à 0 / one-shot" sur ces deux ennemis.
- **Slow de la Tisseuse qui ne se retire jamais** : coroutine de slow déplacée côté `PlayerController.ApplyTemporarySlow()` (survit à la mort de l'ennemi qui l'a lancée), au lieu de tourner sur le GameObject de l'ennemi.
- **Golem — un seul coup de contact puis plus rien** : absence de `OnTriggerStay` généralisée à tous les ennemis et boss ; remplacé par un système de dégâts de contact actif par distance (`EnemyBase._contactDamage`/`_contactDamageCooldown`, `HealthSystem.TryTakeContactDamage()`).
- **Boss — corps à corps qui ne tape qu'une fois** : ajout de `OnTriggerStay` dans `BossBase` (absent à l'origine, seul `OnTriggerEnter` existait).
- **Charge du Boss 1 sans dégâts / distance irrégulière** : distance de charge fixe (`_chargeDistance`, `Vector3.Lerp` entre position de départ et destination calculée), dégâts actifs par distance (tunneling évité), condition de distance minimale avant déclenchement (`_minChargeDistance`) pour éviter le bug visuel quand collé au joueur.
- **Boss — dérive verticale progressive (Y ≈ 1.55)** : cause précise non confirmée avec certitude (probablement résolution de collision Rigidbody contre le sol malgré `Freeze Position Y`), filet de sécurité générique ajouté dans `BossBase.Update()`/`BossDeer.Update()` qui force `transform.position.y = 0` hors phase de saut.
- **Cerf — invisibilité partielle pendant le saut** : cause finale identifiée comme un déplacement vertical intégré dans le clip d'animation lui-même (pas Root Motion, pas de bounds/culling), pas seulement la trajectoire du script. Fix appliqué : `_jumpHopHeight` plafonné à 0.7, **et** zone angulaire interdite (`IsInForbiddenJumpZone()`, -150° à -30° par rapport au joueur) empêchant le déclenchement du saut dans la zone où la caméra coupe le corps.
- **Projectiles ennemis (boss) spawnés à mauvaise hauteur (Y=1.55) et non absorbables** : `EnemyProjectile.Init()` force désormais `spawnPos.y = 0f` et `direction.y = 0f` à la source, indépendamment de la hauteur de l'émetteur au moment du tir.
- **Portée des projectiles ennemis trop courte** : `_maxRange` doublé (20 → 40) sur le prefab `EnemyProjectile`.
- **Mini-boss invoqués trop petits pour les tirs du joueur** : cause = scale appliqué sur la racine du prefab (Collider inclus) plutôt que sur le mesh visuel seul. Fix : scale appliqué uniquement sur tous les `SkinnedMeshRenderer` trouvés via `GetComponentsInChildren`, Collider racine intact. Scale remonté de 0.6 à 0.75 (trop petit visuellement à 0.6, plus petit qu'un ennemi de base).
- **Golem — animation Walk en boucle même à l'arrêt** : ajout d'un paramètre `IsWalking`/état Idle (finalement retiré avec la simplification du Golem en contact pur, cf. ci-dessous).
- **Golem — Slam abandonné** : remplacé par un simple contact renforcé (`_contactDamage` monté sur le prefab), `EnemyTank.cs` réduit à une classe quasi vide (conservée comme marqueur de type pour `WaveManager.GetPoolTag()`).
- **WaveManager.GetPoolTag() fragile** (chaîne de `GetComponent<X>()` à rallonge) : remplacé par lecture directe du champ `EnemyBase.PoolTag` (déjà existant, juste exposé publiquement).
- **Caméra Cinemachine — élasticité et recul de boss cassés** : cause = confusion entre `CinemachineTransposer` (ciblé initialement par erreur) et le vrai composant utilisé, `CinemachineFramingTransposer`. Élasticité restaurée via les champs Damping du Framing Transposer (tombés à 0 par erreur). Recul de boss finalement implémenté via `LensSettings.OrthographicSize` en `LateUpdate()` (les tentatives via `m_FollowOffset` et `m_CameraDistance` ont échoué, probablement écrasées par le recalcul interne de Cinemachine — cause non confirmée avec certitude).
- **Clone Fantôme invisible puis mal dimensionné/mal orienté** : cause racine = scale erroné sur le GameObject "PlayerTripo" (mis à 100/100/100 par erreur, alors que seul le squelette Mixamo doit être à l'échelle). Une fois corrigé à 1/1/1, fix technique = `BakeMesh()` sur le `SkinnedMeshRenderer` source, snapshot en mesh statique, parentage temporaire sous `sourceSkinned.transform` puis détachement en conservant la position monde (méthode plus robuste qu'une recopie manuelle de position/rotation/scale).
- **Bâton du Clone manquant** : le bâton (objet séparé, enfant d'un bone de la main, non inclus dans le `SkinnedMeshRenderer`) est désormais cloné séparément au moment du snapshot et rattaché au clone via le même principe de parentage temporaire.

---

## 4. Game design — Kael et Lyra (nouveau, session V8)

### Répartition personnages / branches d'arbre de compétences
Décision actée cette session, **remplace** toute affectation précédente :
- **Aether** (inspiré du design de Rudeus Greyrat, Mushoku Tensei S2) → branche **Guerrier**
- **Kael** (tank/brute) → branche **Gardien**
- **Lyra** (mobilité/esquive) → branche **Fantôme**

Les noms de branches (Guerrier/Gardien/Fantôme) sont **décoratifs et probablement temporaires** — possibilité de les remplacer directement par les noms des personnages dans la version finale pour plus de lisibilité.

Un nouvel onglet de sélection de personnage est prévu dans le menu (non construit à ce stade). Dans l'arbre de compétences, la branche correspondant au personnage sélectionné devra être mise en valeur visuellement (highlight) pour plus d'intuitivité.

### Différenciation mécanique par personnage
Décision : **pas de modification des stats de base** entre personnages (déjà couvert par le tronc commun Réputation et l'arbre de compétences — éviter le doublon). La différenciation passe uniquement par :
1. Un **trait mécanique inné**, déjà défini avant cette session :
   - Aether : équilibré, aucune particularité
   - Kael : plus de PV, moins de dégâts
   - Lyra : recharge de dash très rapide
2. Une **upgrade active de départ**, unique par personnage, débloquée dès le lancement d'une run (indépendante de l'arbre de compétences), pensée pour renforcer le trait mécanique plutôt que le contredire :
   - **Aether — Boule de Feu** : projectile à dégâts directs toutes les X secondes.
   - **Kael — Aura permanente** : dégâts par seconde autour de lui + ralentissement **léger** des ennemis à proximité (pas de knockback retenu, pas de ralentissement fort — le risque de gameplay statique/passif a été discuté et écarté à faible intensité). Distincte du nœud "mud slow aura" déjà prévu dans le backlog de l'arbre (à surveiller pour éviter un doublon si ce nœud est un jour implémenté).
   - **Lyra — Salve de Shuriken** : projectiles perforants (traversent plusieurs ennemis) toutes les X secondes, cohérent avec un thème ninja et son trait de mobilité.

Idée écartée pour Kael : zone AOE ponctuelle type "coup de poing/frappe sismique" — rejetée car elle recoupe une carte d'upgrade déjà existante dans le pool commun (AOE toutes les X secondes), et jugée moins impressionnante visuellement qu'une boule de feu/salve de shuriken.

### Design visuel — Kael
- Arme retenue : **arbalète lourde**, tenue à deux mains. Alternative écartée : arc (trop associé visuellement à la précision/distance/légèreté, contradictoire avec l'identité tank/contact de Kael) ; arme de mêlée pure écartée d'emblée (le jeu est un bullet heaven, toutes les attaques passent par le système de projectile `WeaponBase`/`ProjectileBasic`).
- Silhouette : carrure nettement plus large qu'Aether (épaules, torse, avant-bras), posture légèrement penchée en avant, centre de gravité bas — sans dérive vers un style bodybuilder irréaliste, reste dans les proportions stylisées Ghibli.
- Visage et rendu de texture : cohérence stricte avec Aether/Rudeus S2 (même famille de traits, même palette, même niveau de stylisation).
- Modèle généré via **MetaIA** (conversation en français, image de référence d'Aether fournie pour ancrer le style) puis passé dans **Tripo3D** pour la 3D — nouveau canal de génération d'image utilisé cette session en complément du pipeline Tripo/Meshy habituel.
- Import en T-pose, **mains vides**, arbalète modélisée et importée séparément (non riggée avec le corps), attachée après coup sur bone(s) de main dans Unity — cohérent avec la méthode déjà utilisée pour le bâton d'Aether.
- Attache à deux mains : parentage simple à la main dominante retenu pour commencer (pas de contrainte IK/Two Bone IK à ce stade, jugée disproportionnée par rapport au gain visuel et au temps disponible) ; à revoir seulement si l'écart de la main secondaire est visuellement trop marqué en jeu.

### Design visuel — Lyra
Non commencé à ce stade.

---

## 5. Arbre de compétences

Aucun changement structurel cette session au-delà de la réattribution des branches (section 4). Cf. V6 pour le détail complet des 15 nœuds (arbre par personnage + tronc commun passif "Réputation", règle de tri stat-brute-vs-playstyle, dash toujours invincible aux projectiles de base).

---

## 6. Leçons Unity transverses (mises à jour — nouveaux ajouts en gras)

Rappel V6/V7 : ordre position/SetActive dans l'ObjectPool ; jamais de 2e `Update()` dans une sous-classe d'`EnemyBase`/`BossBase` ; éviter Any State dans les Animator Controllers ; LayerMask systématique sur `OverlapSphereNonAlloc` ; Vector3 explicite par axe pour un scale plat au sol ; ne jamais redéclarer un `[SerializeField]` du même nom qu'un champ parent ; champ `private` parent inaccessible depuis une sous-classe ; deux hooks virtuels (`UpdateBehaviour`/`OnEnemyUpdate`) tournent en parallèle sans se bloquer ; méthode de swap de squelette Tripo → Mixamo via script C# plutôt que Blender en premier réflexe.

**Nouveau cette session :**

- **Une méthode non-`virtual` redéclarée à l'identique dans une sous-classe (même sans `[SerializeField]`) masque silencieusement la version parente**, y compris `OnEnable()`, `Start()`, `Update()`. Contrairement aux champs `[SerializeField]` dupliqués (qui provoquent une erreur de compilation claire), ce cas ne génère **aucun avertissement** si la méthode parente n'est pas `virtual`. Si elle est `virtual` et que la sous-classe la redéclare sans `override`, Unity/le compilateur émet cette fois un warning explicite (`CS0114`) — un vrai progrès de sécurité par rapport au silence total. Réflexe : dès qu'une méthode de cycle de vie Unity est déclarée dans une sous-classe d'`EnemyBase`/`BossBase`, vérifier si le parent la déclare déjà, et utiliser `override` + `base.MaMethode()` si c'est le cas.
- **Le pooling d'objets ne réinitialise pas automatiquement l'état d'un Animator.** Un objet réactivé via `ObjectPool` reprend l'Animator là où il était figé (potentiellement en plein milieu d'une animation d'attaque). Fix : `Animator.Rebind()` + `Animator.Update(0f)` dans `OnEnable()` avant de rejouer un état neutre.
- **Un flag "s'est passé une fois par run" doit être `static` ET réinitialisé explicitement au bon endroit** (typiquement `GameManager.Awake()`, qui est réellement recréé à chaque nouvelle scène de jeu puisque `GameManager` n'a pas de `DontDestroyOnLoad`) — jamais supposer qu'un flag statique se réinitialise tout seul entre les runs.
- **Un bool d'invincibilité externe partagé entre plusieurs sources indépendantes (Dash, Clone Fantôme, etc.) doit être un compteur, pas un bool.** Deux sources qui écrivent `true`/`false` sur le même bool peuvent se marcher dessus si l'une se termine avant l'autre alors que la seconde est encore active.
- **La détection de collision par trigger physique (`OnTriggerEnter` seul, sans `OnTriggerStay`) ne se redéclenche qu'à l'entrée du recouvrement.** Rester immobile en contact continu (ex: joueur collé à un boss) ne déclenche l'événement qu'une seule fois. Pour un contact continu qui doit infliger des dégâts répétés avec cooldown, ajouter systématiquement `OnTriggerStay` en plus de `OnTriggerEnter`, ou passer à une détection par distance active (plus fiable, indépendante de la résolution physique).
- **Une attaque rapide qui déplace `transform.position` directement (sans Rigidbody, sans détection continue) peut "sauter" par-dessus sa cible en une frame sans jamais déclencher de trigger — tunneling classique.** Pour une charge/dash d'ennemi ou de boss, préférer une vérification de dégâts par **distance active** (calculée chaque frame pendant le mouvement) plutôt que de compter uniquement sur un `OnTriggerEnter`.
- **Un scale appliqué sur le GameObject racine d'un ennemi/boss réduit aussi son Collider proportionnellement**, ce qui peut rendre sa hitbox trop petite/trop basse pour être touchée par des tirs à trajectoire fixe. Pour réduire uniquement l'apparence visuelle d'une instance (ex: mini-boss invoqué), scaler directement le(s) `SkinnedMeshRenderer` (recherche via `GetComponentsInChildren`, plus robuste qu'un `transform.Find()` par nom qui peut échouer silencieusement si l'enfant est nommé différemment ou niché plus profondément que prévu), jamais la racine.
- **Un clip d'animation peut déplacer verticalement des os spécifiques (tête, cou, membres) bien au-delà de ce que suggère la position Y du `transform` racine**, indépendamment de Root Motion (qui ne concerne que le transform racine) et indépendamment des bounds/culling de rendu. Un bug d'affichage qui semble lié à la hauteur du transform peut en réalité venir entièrement du clip lui-même — vérifier visuellement la hauteur atteinte par les parties concernées du modèle pendant l'animation, pas seulement la valeur Y du script.
- **`BakeMesh()` sur un `SkinnedMeshRenderer` fige les vertices dans l'espace local du renderer lui-même, pas dans celui d'un parent quelconque.** Pour repositionner un mesh baké (snapshot figé) au bon endroit dans le monde, le plus fiable est de parenter temporairement le nouvel objet sous le `Transform` du renderer source (`SetParent(source, false)`) puis de le détacher en conservant la position monde (`SetParent(null, true)`) — plutôt que de recopier manuellement position/rotation/scale, qui peut se tromper si la hiérarchie a des échelles ou rotations combinées non triviales.
- **Un scale incohérent entre deux objets liés par un swap de squelette externe (ex : mesh visuel à une échelle, squelette Mixamo à une autre) peut faire dériver silencieusement toute logique qui dépend de la transform du mesh** (comme le snapshot du Clone Fantôme). Vérifier que seul le squelette externe porte l'échelle non-unitaire nécessaire, jamais le mesh visuel lui-même une fois le rig en place.
- **Un script qui écrit en continu sur une propriété caméra Cinemachine (ex: `m_FollowOffset`, `m_CameraDistance`) peut être silencieusement écrasé par le recalcul interne de Cinemachine si celui-ci s'exécute après notre script dans le pipeline `LateUpdate`,** notamment si un mode de framing automatique (Group Framing) est actif sur le composant Body. Le paramètre `Lens.OrthographicSize` (une caméra orthographique) est un point de contrôle plus direct et moins sujet à ce type d'interférence pour un effet de zoom/recul global.
- **Le composant "Body" d'une `CinemachineVirtualCamera` peut être l'un de plusieurs types distincts** (`CinemachineTransposer`, `CinemachineFramingTransposer`, et d'autres selon la version de Cinemachine) — `GetCinemachineComponent<T>()` retourne silencieusement `null` si le type demandé ne correspond pas à celui réellement configuré sur la caméra, sans aucune erreur visible. Toujours vérifier le nom du composant affiché dans l'Inspector avant d'écrire un script qui le cible.
- **Toujours vérifier la version installée d'un package tiers (ex: Cinemachine) avant de diagnostiquer un comportement "qui marchait avant et plus maintenant"** — une mise à jour silencieuse (acceptée par mégarde, ou résolue automatiquement par le Package Manager) peut renommer ou restructurer des composants entiers (ex: Cinemachine 2.x → 3.x) sans erreur de compilation si l'ancien type existe encore en rétrocompatibilité partielle.
- **Un joueur qui dashe à travers une rafale dense de projectiles (nombreux projectiles spawnés la même frame, ex: tir en spirale de boss) a statistiquement plus de chances de rencontrer un raté de détection ponctuel** que face à un tir isolé — même mécanisme de collision, mais la fréquence d'occurrence rend le problème plus visible sur les patterns dense-fire. Toujours vérifier en priorité la hauteur/position de spawn des projectiles avant de suspecter un bug de tunneling complexe.

---

## 7. Pipeline rig externe pour assets Tripo3D / MetaIA (méthode standard, réutilisée sur Kael)

Contexte, méthode retenue (`SkeletonSwapTool.cs`), principe technique et étapes : **inchangés depuis la V7**, cf. section 7 de ce document précédent (toujours d'actualité, validés à nouveau sur Kael cette session).

**Nouveau cette session** : ajout de **MetaIA** comme canal de génération d'image 2D en amont de Tripo3D, en complément (pas en remplacement) de Leonardo.ai déjà utilisé pour les textures d'environnement. Avantage identifié : conversation en français possible, et capacité à fournir une image de référence existante (ex: Aether) pour ancrer strictement le style d'un nouveau personnage. Méthode : fournir l'image de référence + un prompt détaillé insistant explicitement sur la fidélité stylistique (palette, texture, proportions du visage) tout en isolant clairement les éléments à faire varier (ici, la morphologie du corps). Itération possible dans la même conversation pour corriger un aspect précis (ex: carrure pas assez différenciée) sans tout regénérer.

---

## 8. Nouveaux ennemis par palier de temps

Statut : **terminé et stable**, cf. section 3 pour la liste des bugs résolus. Table de pool pondéré et scaling PV/dégâts par palier : implémentés et fonctionnels (`EnemySpawner.SpawnEnemy()`, `_paliersEnnemis`, `ObjectPool.Get()` 4-arguments). Paliers de difficulté alignés entre `WaveManager.ApplyDifficulty()` et le pool pondéré (mêmes bornes 4/8/12/15 min des deux côtés, décision actée cette session pour éliminer la dette de double découpage temporel — **à vérifier si l'alignement a bien été appliqué dans le code, l'intention a été confirmée en conversation mais le code final n'a pas été redemandé/revu**).

---

## 9. Architecture personnages jouables (nouveau, session V8 — en cours, non finalisée)

### Constat de départ
`PlayerController`, `HealthSystem`, `WeaponBase` sont des scripts génériques, partagés tels quels par tous les personnages jouables (Aether, Kael, à terme Lyra) sur un même type de prefab. Rien dans le code actuel ne distingue "quel personnage" joue — un point bloquant pour :
- Attribuer la bonne upgrade de départ (Boule de Feu / Aura / Shuriken) au bon moment.
- Construire le futur menu de sélection de personnage.

### Approche recommandée (proposée, pas encore implémentée)
1. Dupliquer le prefab Player en un prefab par personnage (`Player_Aether`, `Player_Kael`, à terme `Player_Lyra`), chacun avec son propre modèle 3D et point d'attache d'arme, mais les mêmes composants de script génériques.
2. Ajouter un petit script d'identité (`CharacterIdentity` ou équivalent, non encore créé) avec un enum `CharacterType { Aether, Kael, Lyra }`, posé sur chaque prefab — permet à n'importe quel autre script de savoir "quel personnage" sans dupliquer la logique de `PlayerController`.
3. Un script dédié (`StartingUpgradeGranter` ou équivalent, non encore créé) donnerait l'upgrade de départ correspondante au lancement d'une run, en s'appuyant sur le système d'upgrade existant (`LevelUpManager` ou équivalent — **jamais vu en détail dans cette conversation, à fournir avant de coder cette partie**).
4. Le menu de sélection de personnage (nouvel onglet, non construit) chargerait dynamiquement le bon prefab selon le choix du joueur.

### Étape immédiate suivante (validée, méthode de test avant le menu)
Pour tester Kael sans attendre la construction du menu de sélection complet : remplacer manuellement le prefab Player par `Player_Kael` dans la scène de jeu (glisser-déposer direct dans le champ concerné), lancer, valider modèle/arbalète/animations/gameplay de base — reporter la construction du menu et de l'upgrade de départ scriptée à après cette validation manuelle.

---

## 10. Fichiers de référence créés/modifiés cette session

| Fichier | Rôle |
|---|---|
| `HealthSystem.cs` | Compteur d'invincibilité externe, `TryTakeContactDamage()` |
| `EnemyBase.cs` | Lissage de rotation, contact par distance, `PoolTag` public, `OnEnable()` virtual |
| `EnemyTank.cs` | Réduit à une classe quasi vide (Golem simplifié) |
| `EnemyKaiju.cs` | Roar au premier spawn de run, `OnEnable()` override |
| `EnemyCorruptedWeaver.cs` | Renommage `_playerTransform` → `_weaverPlayerTransform`, slow déplacé côté Player, attaque conditionnée au contact réel |
| `EnemyShooter.cs` | Suppression du mouvement en position de tir, aplatissement Y fuite/poursuite |
| `EnemyProjectile.cs` | Hauteur et direction forcées à plat, portée doublée |
| `PlayerController.cs` | `ApplyTemporarySlow()`, fix Clone Fantôme (BakeMesh + parentage temporaire), bâton cloné séparément |
| `ObjectPool.cs` | Surcharge `Get()` avec multiplicateur de PV, fix instance neuve |
| `WaveManager.cs` | `GetPoolTag()` simplifié, appels caméra boss |
| `GameManager.cs` | Reset `EnemyKaiju.ResetRunState()` |
| `BossBase.cs` | `OnTriggerStay`, charge à distance fixe avec recovery et distance minimale, filet de sécurité Y, système d'état Animator (`IsCharging`/`IsWindingUp`/`IsRecovering`/`IsCruising`) |
| `BossDeer.cs` | Filet de sécurité Y conditionnel, zone d'angle interdite pour le saut, télégraphe/dégâts adaptés en mini-boss, `override UpdateAnimatorState()` |
| `BossCorruptedSource.cs` | Salves multiples Crystal Pulse, scale mini-boss via `SkinnedMeshRenderer` |
| `BossCameraZoom.cs` | Réécrit plusieurs fois ; version finale sur `LensSettings.OrthographicSize` en `LateUpdate()` |
| `ProjectileBasic.cs` | Analysé, non modifié (le blocage Y venait de `WeaponBase`, pas de ce script) |
| `WeaponBase.cs` | Analysé, non modifié à ce stade (blocage Y volontaire conservé, alternative écartée) |

---

## 11. Prochaines étapes immédiates

1. Finaliser l'architecture personnages (section 9) : créer `CharacterIdentity`, tester Kael manuellement en jeu (swap de prefab direct dans la scène) avant de construire le menu.
2. Une fois Kael validé en jeu : brancher l'upgrade de départ (Aura) — nécessite de fournir le script du système d'upgrade existant (`LevelUpManager` ou équivalent, jamais vu dans cette conversation).
3. Construire le nouvel onglet de sélection de personnage dans le menu, une fois Kael (et idéalement Lyra) individuellement validés.
4. Démarrer Lyra : génération MetaIA/Tripo3D (même méthode que Kael), arme (à définir — cohérent avec le thème ninja/shuriken déjà évoqué), pipeline de rig via `SkeletonSwapTool`.
5. Mettre en valeur visuellement la branche d'arbre de compétences correspondant au personnage sélectionné (highlight, une fois le menu de sélection en place).
6. Vérifier que l'alignement des paliers de difficulté (4/8/12/15 min, `WaveManager.ApplyDifficulty()` vs pool pondéré) a bien été appliqué dans le code — décision actée en conversation mais code non re-vérifié.
7. Reprendre l'Avatar Mask torche du Player (Aether), toujours en attente depuis la V6/V7.
8. Point en observation, non bloquant : clipping visuel entre plusieurs `EnemyShooter` proches (fix simple appliqué, jamais confirmé gênant ou non par l'utilisateur en playtest prolongé).
9. Accessibilité "Sol simplifié" / "Contraste du décor" : toujours prévue en toute fin de contenu (Kael, Lyra, tout bug restant), non commencée.
