# AETHER — Documentation projet complet (V6)

*Mise à jour du 21 juillet 2026. Ce document remplace la V5 comme référence principale. Il reprend les acquis de la V5 (non détaillés à nouveau ici sauf changement) et documente en détail tout ce qui a été fait, décidé ou cassé/réparé depuis.*

---

## 1. Contexte général

- Jeu : bullet heaven 3D solo (Unity URP), esthétique fantasy médiévale inspirée Ghibli / anime / Mushoku Tensei.
- Développeur : Jules (seul), niveau débutant-intermédiaire Unity/C#, Claude fait office de lead technique + consultant game design.
- **Contrainte majeure actuelle : ~1 mois pour boucler le contenu.** Priorité annoncée en permanence par Jules : le pipeline Tripo3D (assets 3D) passe avant tout chantier de code ou de design secondaire.
- Pipeline de génération d'assets validé : **Tripo3D** pour la génération de modèles, puis rigging via :
  - **Tripo Rigging v2.5** pour les quadrupèdes (marche bien, prouvé sur Loup et Sanglier)
  - **Rig Humanoid natif Unity** pour les bipèdes, avec animations **Mixamo** (marche bien, prouvé sur Aether, Gobelin, Golem)
  - **Mesh2Motion** en dernier recours pour les formes atypiques — résultats pas toujours suffisants (a échoué sur le Sanglier même en mode manuel)

### Règle de design récurrente établie cette session
Toute grosse attaque de boss/tank doit avoir un **tell visuel lisible** avant de faire des dégâts significatifs (glow de mana progressif, ralenti d'animation, cercle de danger au sol). Établie sur le Golem (Slam), reprise sur le Sanglier (charge) et le Cerf (saut). Deux langages de couleur distincts : **bleu mana = pouvoir qui charge**, **rouge/orange = zone de danger imminente au sol**.

---

## 2. Personnage joueur — Aether

Systèmes stables : import Mixamo, Animator Controller, rotation en `FixedUpdate` via `MoveRotation`, projectiles en position orbitale monde, bâton attaché au bone `R_Hand`.

### Système Phantom (clone du joueur) — entièrement reconstruit cette session

**Design final :**
- Déclenché par une **touche dédiée (C)**, totalement indépendante du dash — cooldown propre de 8s, ne dépend d'aucune réduction de cooldown de dash
- Durée de vie du clone : 2s
- Le clone est une **vraie copie du modèle 3D** du joueur (pas une primitive), teintée en bleu via un **Color.Lerp** (pas une multiplication, qui donnait un effet "zombie" sur les textures chaudes)
- Pendant l'activation : le **vrai joueur** devient semi-transparent (matériau dupliqué basculé en mode Transparent au runtime, alpha réduit — pas de clignotement, un fondu statique), **immunisé aux dégâts de contact**, et bénéficie d'un **boost de vitesse temporaire** (~+50%, ~1.2s) pour laisser le temps de fuir
- **Attraction des ennemis** : tout ennemi qui entre dans un rayon (`_phantomAttractRadius`, ~10 unités) pendant que le clone est actif est attiré vers lui — plafonné à **14 ennemis max simultanés** (évite de vider l'écran d'un coup). Vérification faite **en continu à chaque frame** tant que le clone existe (pas juste un burst au moment du spawn)

**Détails techniques importants pour la suite :**
- Les matériaux "fantômes" (transparence joueur + teinte clone) sont **précalculés une seule fois dans `Awake()`**, jamais recréés à chaque activation — évite les micro freezes liés au Garbage Collector
- Le multiplicateur de vitesse du boost d'échappement est un champ **dédié** (`_escapeSpeedMultiplier`), séparé de `_speedMultiplier`, pour ne pas reproduire le bug de conflit qu'on a eu côté ennemi (voir section Bugs)

---

## 3. Ennemis de base — Map 1

Trois matières visuelles distinctes établies pour une bonne lisibilité de swarm :

| Ennemi | Matière | Type | Statut |
|---|---|---|---|
| Loup de Mana | Fourrure/chair | Melee, quadrupède | ✅ Terminé (fait avant cette session) |
| Gobelin des Ronces *(remplace Bulbe Cracheur)* | Bois/racines | Shooter à distance, bipède | ✅ Terminé cette session |
| Golem de pierre + mana *(remplace Golem de Tronc)* | Pierre + veines bleues | Tank, bipède | ✅ Terminé cette session |

### Gobelin des Ronces (remplace le Bulbe Cracheur de la doc V5)
Décision de design : l'idée originale de "fleur carnivore qui s'ouvre pour tirer" a été abandonnée (animation trop complexe pour le temps disponible) au profit d'un **petit lutin/farfadet fait d'écorce et de ronces, armé d'une sarbacane**. Réutilise directement le pipeline bipède + système d'attache d'arme au bone main déjà validé sur Aether — zéro risque technique nouveau.

- Rig Humanoid, animations Mixamo (Walk + tir pistolet réutilisé comme base pour Shot)
- `EnemyShooter.cs` : rotation vers la cible ajoutée (absente du script initial fourni par Jules), animation pilotée **par état** (le bool `IsAttacking` reste vrai en continu tant que le Gobelin est dans sa plage de tir, pas de pulse par tir individuel — corrige un bug où l'animation redémarrait à chaque tir si la cadence était lente)
- Sarbacane : objet séparé, actif/inactif recalculé **à chaque frame** (pas seulement au changement d'état) selon la posture du Gobelin — plus robuste après plusieurs tentatives ratées basées sur un changement d'état ponctuel
- Point de spawn des projectiles dédié (`_projectileSpawnPoint`), enfant de la sarbacane, pour que les tirs partent visuellement de l'embouchure plutôt que du centre du personnage

### Golem de pierre + mana (remplace le Golem de Tronc)
Redesigné en pierre (au lieu de bois, pour ne pas doublonner avec le Gobelin) avec veines de mana bleu bien visibles de loin.

- `EnemyTank.cs` : attaque signature **Slam au sol** — temps de charge (`_windupDuration`) pendant lequel les veines de mana s'illuminent progressivement (via `MaterialPropertyBlock`, sans créer de nouveau Material), puis impact sur une zone plus large que le simple contact (`_slamRadius`)
- Paramètre Animator dédié `IsSlamming`, séparé d'`IsAttacking` (qui reste pour le contact standard hérité d'`EnemyBase`) — évite le conflit entre les deux systèmes qui se disputeraient le même bool

---

## 4. Boss

### Boss 1 — Sanglier de Mana
Design : pelage blanc/pastel, crête végétale verte, touches de bleu mana discrètes sur le visage. Rig quadrupède (Tripo Rigging v2.5).

- `BossBase.cs` étendu avec un système de **tell générique réutilisable** : `_isWindingUp`, `UpdateChargeTelegraph()`, `UpdateGlowEffect()` — pensé pour être réutilisé par les futurs boss (Cerf en hérite directement)
- Séquence : marche + tir radial normal → 1s avant la charge, **arrêt net + ralenti de l'animation Walk (`_animator.speed`) + glow progressif des veines** → charge en ligne droite à ×4 vitesse (animation accélérée) avec **rotation verrouillée sur la direction de charge** (pas de poursuite du joueur pendant le dash, pour rester lisible et "honnête")
- Clamp de zone (`MapBoundaryUtils.ClampToZone`) appliqué au mouvement normal ET à la charge — bug corrigé cette session (le boss pouvait sortir de la map pendant sa charge)

### Boss 2 — Cerf Ancestral
Design : cervidé éthéré, pelage clair, **bois entièrement faits de cristal de mana bleu** (pas juste des veines en surface — les bois eux-mêmes sont l'élément magique). Rig quadrupède (Mesh2Motion, après échec des deux autres méthodes sur ce modèle précis).

- Animations : **Idle, Walk, Run, Jump, Bite, Death** (6 au total)
- Combat en **3 phases** basées sur le %PV, décidées par Claude à la demande explicite de Jules ("tranche toi-même") :
  - Normal (100%–50% PV) : Walk
  - Phase 2 (50%–30% PV) : Run, vitesse ×1.4
  - Rage (< 30% PV) : spirale plus dense, cooldowns de saut et régénération réduits
- **Attaque signature : saut-atterrissage.** Cible la **position figée du joueur au moment du décollage** (pas de poursuite en l'air, cohérent avec la philosophie "attaque committed et lisible" du Sanglier). Télégraphe : cercle rouge/orange au sol qui grandit progressivement pendant le windup (couleur volontairement différente du bleu mana — rouge = danger imminent au sol)
- **Spirale de projectiles** : tir **continu** (pas de pause entre salves, contrairement à la première version), **double spirale** (deux flux de projectiles décalés à 180°) — ajustement fait après retour de Jules que la cadence semblait trop faible
- **Bite** : le Cerf s'arrête net à distance de morsure au lieu de continuer à marcher à travers le joueur (bug initial corrigé)
- **Death** : override de `Die()` qui joue l'animation Death (Trigger) et désactive le collider avant de déclencher la vraie séquence de mort (drops, heal joueur, notify WaveManager) après un délai

### Boss 3 — La Source Corrompue
Pas encore commencé. Prochain sur la liste après résolution du bug de saut du Cerf (voir Bugs non résolus).

---

## 5. Progression et arbre de compétences — refonte de design (décidée, pas codée)

### Principe validé cette session
Deux couches de progression séparées :

1. **Arbre de compétences PAR PERSONNAGE**, séparé et persistant. Branches actuelles (Guerrier orange / Gardien vert / Fantôme cyan) **renommées avec le nom des personnages** correspondants, et **gelées/greyed** selon le personnage actuellement sélectionné dans un nouvel onglet de sélection (avec preview 3D + stats). Réutilise presque telle quelle l'UI d'arbre déjà construite — gating + renommage, pas de refonte.
2. **Progression de compte universelle et passive ("Réputation")** : un compteur qui monte automatiquement en fin de run (kills, temps survécu, boss battus), débloquant des paliers de **bonus génériques plafonnés** (ex: +2% PV, +2% Dégâts, +1% Vitesse tous les X paliers), appliqués à **tous** les personnages, y compris ceux jamais joués. Objectif : éviter qu'un personnage jamais touché se sente totalement nu au premier essai.

### Ce qui part dans le tronc commun (Réputation), confirmé cette session
Toute upgrade qui n'est qu'un **% de stat brute interchangeable** part du tronc commun, peu importe sa branche d'origine :
- Branche Guerrier : +dégâts, +cadence de tir
- Branche Gardien : +régén/sec, +PV max
- Branche Fantôme : +vitesse de déplacement

**Règle établie** : une branche de personnage ne doit contenir que des nœuds qui **changent vraiment le playstyle** (le clone Phantom reste une capacité propre à Lyra, jamais partagée). Un simple bonus numérique interchangeable d'un perso à l'autre n'est pas une identité de personnage.

### Dash invincible
L'immunité aux **projectiles** pendant le dash reste une règle de base du kit, disponible **même sans l'upgrade de l'arbre** (nécessaire pour que la barre d'ultime se charge). L'upgrade de l'arbre ajoute probablement une protection supplémentaire par-dessus (contact ennemi ?) — détail encore à définir.

### Autres décisions de progression
- **Cosmétiques** : validés comme 2e sink de Gold (une fois l'arbre maxé). Débloqués par personnage, très chers. Recommandation technique : privilégier les recolors (via retexture Tripo Studio, peu coûteux) plutôt que de nouveaux modèles complets pour chaque skin.
- **Défis aléatoires par run** : récompense = bonus Gold en fin de run (déjà acté dans la doc V5, revalidé — prend d'autant plus de sens que le Gold reste utile toute la partie grâce aux cosmétiques).

### Nouvelles idées d'upgrades (mentionnées, pas encore designées en détail)
1. **Boues/mud posées autour du joueur** qui ralentissent les ennemis (probablement branche Gardien/tronc commun à trancher)
2. **Projectile à ricochet** — question ouverte non tranchée : ricoche sur les bords de la **zone jouable** (simple, réutilise `MapBoundaryUtils`) ou sur les bords de **l'écran caméra** (plus complexe, calcul de projection écran nécessaire, effet plus chaotique/rapproché) ?
3. **Boules de feu automatiques**, délai de 2s entre chaque tir, grosse hitbox, traversent les ennemis sans s'arrêter — archétype "ligne/couloir", différent de la Nova (zone) et de la Chaîne de Foudre (ciblée/rebond).

---

## 6. Bugs Unity transverses — leçons apprises cette session

Section volontairement dédiée : plusieurs bugs rencontrés ne sont pas spécifiques à un seul système et risquent de se reproduire ailleurs si on ne les documente pas.

1. **`ObjectPool.Get()` — ordre `SetActive`/position.** `SetActive(true)` déclenche `OnEnable()` de façon synchrone. Si la position/rotation sont appliquées **après** `SetActive(true)`, tout script qui lit `transform.position` dans son `OnEnable()` voit l'ANCIENNE position (pré-pooling), pas la nouvelle. **Toujours positionner avant d'activer.**
2. **Une classe fille qui déclare son propre `private void Update()`** masque complètement celui de la classe mère — pas de vrai override possible sur une méthode privée non-virtuelle. Solution : la classe mère expose un hook (`protected virtual void OnEnemyUpdate() {}`) appelé en fin de son propre `Update()`, que les filles overrident. **Ne plus jamais déclarer de `Update()` séparé dans une sous-classe d'`EnemyBase`.**
3. **`Physics.OverlapSphereNonAlloc` sans LayerMask** gaspille les slots du buffer sur des colliders non pertinents (joueur, gemmes, pickups) — toujours filtrer avec un LayerMask dédié en plus de la taille du buffer.
4. **Root Motion coché sur l'Animator** peut faire "dériver" le modèle visuel indépendamment de la racine/hitbox contrôlée par script — cause un désalignement entre où l'objet est logiquement et où le mesh apparaît visuellement. Vérifier systématiquement que **Apply Root Motion est décoché** sur tout ennemi/boss dont le mouvement est piloté par script.
5. **Any State dans l'Animator Unity** a causé des bugs de transition non identifiés précisément sur plusieurs Animator Controllers de ce projet (Golem, évité aussi sur le Cerf). Remplacé systématiquement par des connexions directes état-par-état.
6. **Rig Mixamo "Copy From Other Avatar"** échoue si les clips viennent de fichiers séparés avec des noms de bones différents. Utiliser **"Create From This Model"** à la place (retargeting par correspondance sémantique, pas par nom de transform).
7. **Classes utilitaires statiques dupliquées** (`MapBoundaryUtils` était imbriqué en double dans `PlayerController.cs` ET `EnemySpawner.cs`) : toujours extraire dans un fichier indépendant dès qu'un même bout de code utilitaire est nécessaire à plus d'un endroit, pour éviter les divergences silencieuses.
8. **`Vector3.one * valeur`** pour scale un effet visuel (ex: cercle de télégraphe au sol) gonfle les 3 axes y compris la hauteur — utiliser un `Vector3` explicite par axe pour un effet plat (`new Vector3(diametre, hauteurFixe, diametre)`).

---

## 7. Bugs non résolus à ce jour

1. **Tremblement léger et intermittent de la rotation de certains ennemis** (tous types confondus). Nouvelle piste identifiée cette session, non encore codée : dans `EnemyBase.UpdateBehaviour()`, la ligne `if (Vector3.Dot(final, direction) < 0.1f) final = direction;` crée une **bascule binaire** entre deux directions cibles. Confirmé par Jules : le bug dépend de l'**angle** du joueur par rapport aux ennemis (pas de la distance), et deux ennemis spawnés à la même position tremblent ensemble — cohérent avec un dot product qui oscille juste autour du seuil 0.1 à certains angles précis. **Fix pas encore codé** : remplacer la bascule if/else par une transition continue (lerp/smoothstep).
2. **Boss Cerf — partie du modèle invisible pendant le saut**, dépendant de l'angle caméra, ~1 fois sur 4. Un premier bug voisin (cercle de télégraphe trop volumineux, cachant visuellement le corps) a été identifié et corrigé via capture d'écran. Le bug d'invisibilité lié à l'angle caméra n'est **pas confirmé résolu** — hypothèse de culling (`Update When Offscreen`) partiellement vérifiée (coché sur le renderer principal, pas forcément sur d'éventuels renderers séparés comme les bois en cristal). **Test demandé, résultat non encore rapporté** : comparer Game view (bug visible) et Scene view au même instant pendant une pause (la Scene view n'est jamais soumise au frustum culling caméra) pour trancher entre culling et vrai bug de position.

---

## 8. TODO List à jour (21 juillet 2026)

### Priorité immédiate — pipeline Tripo3D
- [ ] Boss 3 — La Source Corrompue (modèle, rig, comportement)
- [ ] Kael (personnage jouable)
- [ ] Lyra (personnage jouable)

### Bugs ouverts
- [ ] Tremblement de rotation ennemi (piste identifiée, voir section 7)
- [ ] Invisibilité partielle du Cerf pendant le saut (test Scene view vs Game view à faire)

### Systèmes de code non commencés
- [ ] Scaling PV/dégâts ennemis par palier de temps (table déjà conçue plus tôt dans le projet, jamais implémentée — cause probable n°1 de la facilité excessive du jeu)
- [ ] Centralisation des dégâts de contact (actuellement 15 codé en dur dans `HealthSystem`, indépendant du type d'ennemi — bloque le scaling de dégâts tant que non réglé)
- [ ] Sélection de personnage + arbre séparé par personnage + tronc commun Réputation (design tranché, aucun code écrit)
- [ ] Cosmétiques (gold sink)
- [ ] 3 défis aléatoires par run (récompense gold)
- [ ] Menaces d'escalade
- [ ] 3 nouvelles idées d'upgrades (boue/ralentissement, ricochet, boules de feu) — design détaillé à faire

### Plus tard / polish
- [ ] Option accessibilité "Sol simplifié" / "Contraste du décor" (texture basse-fidélité alternative, à ranger en section Accessibilité, pas Graphismes)
- [ ] Fond noir transparent sur l'UI in-game (pour la lisibilité contre le sol)
- [ ] Réévaluer l'animation Walk du Sanglier si le temps le permet

---

## 9. Assets terminés — récapitulatif

| Asset | Statut | Notes |
|---|---|---|
| Aether (joueur) | ✅ | Rig, animations, tir, dash, clone Phantom (touche C) |
| Loup de Mana | ✅ | Quadrupède, Tripo Rigging v2.5 + Mesh2Motion |
| Gobelin des Ronces | ✅ | Bipède Humanoid, sarbacane, tir à distance |
| Golem de pierre + mana | ✅ | Bipède Humanoid, Slam avec tell |
| Boss 1 — Sanglier de Mana | ✅ | Quadrupède, charge avec tell, clamp de zone |
| Boss 2 — Cerf Ancestral | ⚠️ Fonctionnel, 1 bug visuel ouvert | Saut télégraphié, 3 phases, spirale double |
| Boss 3 — La Source Corrompue | ❌ Non commencé | |
| Kael | ❌ Non commencé | |
| Lyra | ❌ Non commencé | |
| Texture de sol | ✅ | V7 désaturée, validée en jeu |
