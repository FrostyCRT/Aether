# Aether Storm Survivor — Notes de travail

Deux listes vivantes tenues au fil des sessions. Mises à jour à chaque passe.
Dernière mise à jour : 2026-09-11 (passe vérification VictoryPanel en conditions réelles, via MCP sur le 2ᵉ PC).

## Environnement

- **Unity 6000.0.83f1** (migré depuis 2021.3.45f2 le 2026-09-10).
- Cinemachine **2.10.7** conservé (ligne 2.x, supportée sur Unity 6) → `CinemachineVirtualCamera`, `m_Lens`, `LensSettings` toujours valides, aucune migration CM3.
- Legacy Input Manager (pas de package Input System) → `Input.*` OK.
- URP 17.0.4, TMP intégré à `com.unity.ugui` 2.0.0 (`using TMPro;` inchangé).
- Seul correctif code requis par la migration : `FindObjectOfType`/`FindObjectsOfType` (obsolètes, warnings) → `FindFirstObjectByType` / `FindObjectsByType(FindObjectsSortMode.None)` — 6 sites corrigés (CrystalSystem, GameManager, ObjectPool ×2, WaveManager). Scripts Editor (`SkeletonSwapTool`, `BoundaryDecorationPlacer`, template `ReadmeEditor`) et le sample `Sequences` : API stable, rien à changer.

---

## À modéliser (Tripo) — session dédiée 1 mois, à faire d'un coup

> Rien ne se modélise avant la session Tripo. On accumule ici.

| Élément | Type | Notes |
|---|---|---|
| Bulbe cracheur de projectile | Ennemi | Shooter **fixe** (ne bouge pas une fois spawné), tir en éventail à cadence faible dès que le joueur est à portée. À différencier visuellement du Gobelin shooter. Déjà dans les tâches en attente V13. |

_(rien d'autre pour l'instant — le Bouclier de Mana et les correctifs en cours sont 100 % procéduraux / code)_

---

## À faire — design / équilibrage / UX

### En cours

- **Refonte VictoryPanel — structure 3 temps** ✅ **scène ET code faits** (maquette validée : artifact "Refonte écran Victoire"). Colonne unique de 10 blocs → 2 colonnes / 3 temps, construit directement dans `Game.unity` via MCP (CenterCard 1720×880, plein écran ~100 px de marge) :
  - **T1 récompense** (gauche) : Or + Éclats en gros (comptage animé) + pastille bonus défi + `StatStrip` une ligne (`PopulateVictoryStats` inline dans `ShowVictory`) + `RecordHighlight` = record battu **cette partie** (`PopulateVictoryRecordHighlight`, masqué si aucun record).
  - **T2 build** (droite) : `PortraitImage` = illustration perso (`PopulateVictoryPortrait`, lit `MetaProgressionManager.GetSelectedCharacterIndex()` + `_characterPortraits[]`, sprites tirés de `CharacterSelectUI`) + grille d'icônes teintées par branche (`PopulateBuildGrid`).
  - **T3 pied** : `RecapText` dynamique avec du ton (`PopulateVictoryRecap`, pool de 7 phrases de clôture) + boutons verrouillés jusqu'à la fin du reveal.
  - Dégagé de la scène : bloc Records 5 lignes, `RecordsText`/`RecordsTitleText`, `StatsTitle`, `Separator2/3/4`, liste texte de build.
  - Vérifié : compile propre, rendu PNG en mode Edit avec données de test (15:02 / 847 kills / niv. 34) → récap + stat strip corrects, `RecordHighlight` correctement masqué quand `MetaProgressionManager.Instance` est absent.
  - **Retour utilisateur après premier vrai playtest (2026-09-11)** : trop d'espace vide, "loin de la maquette". Deux correctifs appliqués :
    1. **Bug réel trouvé** : le chip de défi (`ChallengeChipBg`, fond `Image`) restait visible en permanence même sans défi réussi — `PlayGoldSequence()` ne désactivait que le texte enfant (`challengeText.gameObject`), pas son fond. Résultat : bandeau orange vide sur chaque victoire sans défi. Corrigé : on cache/affiche désormais le **parent** (le chip complet) quand il porte une `Image`, repli sur le texte seul sinon (cas Game Over, pas encore câblé).
    2. **Colonnes trop vides** : `LeftCol`/`RightCol` avaient un `VerticalLayoutGroup` aligné `UpperLeft` dans une colonne pleine hauteur (530 px) — tout point non utilisé (défi non réussi, pas de record battu) s'accumulait en un seul bloc vide en bas (jusqu'à ~190 px mesurés). Passé en `childAlignment = MiddleLeft` (espace résiduel réparti haut/bas au lieu de s'entasser en bas) + spacing 20→32 + portrait agrandi 192×264 → 260×373 (ratio conservé) + grille 98→108 px/tuile.
  - **Vérifié en conditions réelles le 2026-09-11 (Play Mode réel, via MCP sur le 2ᵉ PC)** — bootstrap `MainMenu` → `Game` pour avoir un vrai singleton `MetaProgressionManager`, puis `GameUI.ShowVictory()` appelé directement avec des stats fictives dépassant volontairement les bests (jamais persisté sur disque, `SaveSystem.Save` jamais invoqué). Résultat : portrait Lyra assigné correctement (`_characterPortraits[GetSelectedCharacterIndex()]`), `RecordHighlight` s'active avec le bon texte ("Nouveau record de survie : 10:05 !"), Recap/StatStrip/Or/Éclats/chip de défi tous corrects. **Le chemin complet fonctionne en conditions réelles, plus aucune inconnue sur ce point.**
  - **2 bugs supplémentaires trouvés et corrigés pendant cette vérification** (oubliés lors de l'agrandissement du portrait 192×264→260×373, correctif 2 ci-dessus n'avait mis à jour que le portrait et la grille, pas les conteneurs qui dépendent de sa taille) :
    1. **Portrait/grille se chevauchaient de 44px** : `BuildGridContent` gardait un inset gauche de 216px (= ancienne largeur 192 + 24px de marge), jamais remis à jour après l'agrandissement à 260px → les ~44 premiers pixels de la 1ʳᵉ colonne de tuiles passaient sous le portrait. Corrigé : inset recalculé à 284px (260+24).
    2. **`ArsenalRow` trop bas pour son propre contenu** : hauteur figée à 290px alors que le portrait agrandi fait 373px de haut (et qu'une build à 11 upgrades distincts sur 5 colonnes = 3 rangées = 344px) — sans mask, tout ce qui dépasse 290px s'affichait quand même mais faussait le calcul d'espace vide de `RightCol` (risque de léger désaquilibre avec `LeftCol`, et prend le risque de frôler `ButtonsRow` sur une grosse build). Corrigé : `ArsenalRow` remonté à 373px (couvre le pire des deux cas).
  - Sauvegardé dans `Game.unity`. Reste ouvert : le ressenti "encore trop vide" en conditions réelles avec du VRAI texte/portrait/grille remplis (pas testable analytiquement, nécessite un œil humain sur un vrai playtest de victoire — mon test Play Mode n'avait aucune build obtenue donc la grille était vide, `GridTileCount=0`).
  - Pas de nouvel highlight "(ancien : X)" — `SaveRunResults()` écrase déjà `data.bestX` avant l'affichage, l'ancienne valeur n'est plus récupérable sans plomberie supplémentaire (décidé : pas pour cette passe).

### Découvertes annexes (pas traitées cette passe, à trancher)

- **HUD — rappel du défi en cours jamais construit** : `GameUI._challengeText` (le champ utilisé par `UpdateChallengeDisplay()`) est `null` dans la scène `Game` — aucun GameObject "rappel de défi" n'existe sous `HUD`, alors que la doc V13 §5 le décrit comme "état final, validé" sous le Chrono. Écart doc/scène réel : soit l'objet a été supprimé par accident, soit il n'a en fait jamais été construit. Pas touché cette passe (création de nouvelle UI = décision de placement/DA à valider avant d'agir), mais le joueur ne voit actuellement AUCUN rappel de son défi en cours pendant la run.
- **`SaveSystem.Load()` a renvoyé un `SaveData` par défaut (tout à 0) une fois**, en plein milieu de cette session de test, alors que le vrai `save.json` sur disque contenait déjà de la vraie progression (totalRuns 6, 5735 Or, etc. — confirmé intact par une lecture directe du fichier après coup, rien perdu). Point isolé, pas reproduit une 2ᵉ fois, cause non identifiée (piste : lock transitoire du fichier juste après un rechargement de scène/domain reload). À surveiller si ça se reproduit, pas creusé plus loin cette passe.
- **Grille d'upgrades** — `GameUI.PopulateBuildGrid()` codé, câblé et vérifié (Victory). Prefab `UpgradeGridSlot` fait (68×68, ancrage stretch, nom masqué). **Reste** : même traitement 3-temps + grille pour le **Game Over** (teinte désaturée, "Tentative n°X", aperçu du prochain déblocage à la place du portrait triomphant) — pas encore commencé.

### Correctifs méta-progression (audit) — statut

| Nœud / point | Statut |
|---|---|
| HP boss final ×10 trop bas (`BossCorruptedSource`) | ✅ corrigé (script) |
| Encodage CP1252 → UTF-8 (18 fichiers, texte joueur cassé) | ✅ corrigé |
| Impulsion Nova : effet gratuit pour tous | ✅ corrigé (gaté sur `HasImpulsionNova()`) |
| Récupération (Kael) : `GetBonusRecuperation()` jamais lu | ✅ implémenté (20/50/80 PV par absorption) |
| Bouclier de Mana (Kael) : `HasManaShield()` jamais lu | ✅ implémenté (barrière 3 charges) — reste câblage Unity (composant + pips HUD) |
| Concentration (Aether) : `GetBonusConcentrationCap()` jamais lu | ✅ implémenté (`PlayerBuffs`, +8%/s, plafonds 15/30/50) — reste câblage Unity (composant + texte HUD) |
| 3 persos = stats prefab identiques | 🔁 **décision revue** : ne PAS ajouter d'étage de stats prefab (3 couches = confus, redondant avec l'arbre). À la place, traits **innés qualitatifs** (une règle / un verbe, pas un %), à trancher avant le Next Fest : Lyra = dash réellement plus court (1,5 s) ; Kael = une règle tanky (ex. immunité au contact en avançant, ou -50 % dégâts 3 s après un gros coup) ; Aether = baseline, rien (choix assumé). Alt légère : rendre le 1ᵉʳ level-up plus rapide (baisser l'XP niv. 2) pour que l'arme exclusive surgisse en <20 s. |

### Système de déblocage des personnages ✅ (code fait, câblage Unity à faire)

- **Aether** : dès le départ. **Kael** : atteindre le Boss 2 en une partie (`WaveManager.SpawnBoss` → `UnlockCharacter(1)`). **Lyra** : gagner une partie / Boss 3 (`MetaProgressionManager.SaveRunResults` si `victory` → `UnlockCharacter(2)`).
- Filet Éclats : Kael 500, Lyra 1200 (`TryUnlockCharacterWithEclats`, réglable dans l'Inspector de `MetaProgressionManager`).
- `SaveData.kaelUnlocked` / `lyraUnlocked` (nouveaux, false par défaut).
- Sécurités : `GetSelectedCharacterIndex()` retombe sur Aether si le perso sauvegardé est verrouillé ; `SetSelectedCharacter` refuse un perso verrouillé.
- **Save existant** : lancer une fois `MetaProgressionManager.Instance.DebugUnlockAllCharacters()` (ou jouer jusqu'au Boss 2 / gagner). Méthode debug à retirer avant release.

**À faire côté Unity** :
1. Laisser compiler.
2. `CharacterSelectUI` (scène MainMenu) — nouveaux champs à câbler (tous facultatifs sauf le texte) :
   - `Locked Overlay` → un GameObject voile/cadenas par-dessus le portrait.
   - `Unlock Condition Text` → un `TextMeshProUGUI` (affiche "Atteins le Boss 2...").
   - `Unlock With Eclats Button` (+ son `Text`) → bouton du filet Éclats.
   - `Locked Portrait Alpha` → 0.35 par défaut.
   - Placer ces éléments dans le panneau de sélection, visibles seulement quand le perso est verrouillé (le script gère l'affichage).
3. Rien à changer sur les prefabs.

**À faire côté écran de fin (dans la refonte Victory/Game Over)** :
- Appeler `MetaProgressionManager.Instance.ConsumePendingUnlockNotification()` → si non-null, afficher en évidence "Nouveau personnage débloqué : {nom} !". C'est LE moment de récompense. Kael se débloque souvent en pleine partie (spawn Boss 2) → à montrer sur le Game Over si le joueur meurt ensuite. Lyra → sur la Victoire.
- Limitation connue v1 : si Kael est débloqué puis Lyra dans la même session sans consommer entre les deux, seul "Lyra" est retenu (peu probable, ~2 déblocages par vie de joueur).
- Idée bonus plus tard : toast en cours de partie "Kael débloqué !" au spawn du Boss 2 (nécessite un système de toast dans `GameUI`, inexistant).

### Dette créée par ces correctifs (à traiter plus tard)

- **`PlayerBuffs.OutgoingDamageMultiplier`** (nouveau, central) est lu par : `WeaponBase`, `WeaponFireball`, `WeaponLightningChain`, `OrbitalProjectile`, `BouncingOrbProjectile`, `MudPuddleZone`. **Pas** par `WeaponAura`, `WeaponShurikenBarrage`, `WeaponAOE` — inutile aujourd'hui (Concentration = Guerrier only), mais à ajouter si un buff dynamique cross-personnage apparaît.
- **Migrer "Surpuissance" dans `PlayerBuffs`** : aujourd'hui `CrystalSystem.OverpowerBuff()` n'appelle que `WeaponBase.SetDamageMultiplier(2f)` → ne double QUE le tir de base, pas Fireball/Orbital/etc. La plomberie centrale existe maintenant, c'est un petit refactor.

### Grands chantiers (validés en principe, à traiter un par un)

1. **Rendre le loop "défense → offense" central et lisible.**
   Dash → absorption → Nova → charge Cristal → Ultime → Surpuissance. C'est le différenciateur vs Vampire Survivors. À mettre au centre : tuto, DA, communication. Vérifier en priorité que **la fenêtre d'absorption (0,3 s) est satisfaisante** — sinon tout ce système s'écroule.

2. **Différencier les 3 persos dès les 30 premières secondes.**
   Stats de base distinctes (déjà spécifiées V13, jamais faites), idéalement feeling de dash / tir différent. Compte plus que n'importe quel nœud d'arbre.

3. **Simplifier la méta.** Or (arbres perso) + Éclats (Réputation) + défis + fusions + skins = potentiellement 1 système de trop. Réputation et les arbres font tous deux "dépense une monnaie pour des % de stats". Pressuriser : Réputation mérite-t-elle d'exister séparément, ou = tronc commun *dans* l'arbre ?

4. **Rendre les arbres moins "+X%".** ~80 % des nœuds sont des multiplicateurs plats. Ajouter des nœuds qui *changent comment le build joue*, pas seulement l'échelle.

5. **Choisir un ton.** Messages de mort sarcastiques vs lore perso épique-sincère : incohérent. Une seule voie.

6. **Casser la monotonie de structure.** Toujours les mêmes 3 boss, même ordre, à 5/10/15 min, fin au boss 3. Réfléchir : mode sans fin après boss 3, ordre de boss randomisé, ou variance. Les 3 maps prévues aident déjà.

7. **Passe équilibrage/playtest complète après le rescale ×10** (déjà dans les tâches V13).

### Plus petit / à confirmer

- `_phasesDeJeu` (EnemySpawner) écrasé chaque frame par `WaveManager.ApplyDifficulty()` → configuration inspecteur morte. Choisir une seule source de vérité pour la courbe de difficulté.
- `MapBoundaryUtils` existe en double (classe autonome + classe imbriquée dans `WaveManager`).
- `WeaponAOE.cs` : arme orpheline (aucun `UpgradeType.AOE`). Supprimer ou brancher.
- `ObjectPool.ClearPool()` jamais appelé → projectiles ennemis volent pendant l'écran de victoire.
- `GetNextUnlockPreview()` compare "manque d'Or" et "manque d'Éclats" sur la même échelle (devises différentes) — utilisé par l'écran Game Over en construction.
- Fragmentation : texte dit "20 %", code = 15 %.
- Restes de debug connus (déjà notés V13) : `ResetSkillTree()` → `totalGold = 10000`, bouton debug reset Réputation, **`MetaProgressionManager.DebugUnlockAllCharacters()` / `DebugResetCharacterUnlocks()`** + le champ `_debugResetUnlocksButton` + `OnDebugResetUnlocksClicked()` sur `CharacterSelectUI` (nouveaux). Nettoyage pré-release.
- `GameManager.GoToMainMenu()` ne sauvegarde pas la partie (vs `AbandonRun()`). Vérifier le câblage des boutons.
- Death causes : étendre au-delà de `boss`/`horde`. Plomberie identifiée — `EnemyBase` ligne ~191 (`TryTakeContactDamage` sans source), `EnemyProjectile.Init()` (pas de param source), attaques Kaiju/Weaver/BossDeer saut.
- Perf : `FindGameObjectsWithTag`/`FindObjectsOfType` par frame ou par mort ; `Physics.OverlapSphere` allouant dans CrystalSystem / bosses ; `GetComponentInChildren` répétés dans `EnemyBase.Update`.

---

## Bouclier de Mana (Kael) — spec de la refonte

**Mécanique** : barrière rechargeable à charges.
- 3 charges. Chaque projectile ennemi qui toucherait le joueur (hors i-frames dash) consomme 1 charge → dégâts **totalement annulés**.
- Recharge 1 charge / 3 s, jusqu'à 3.
- Charges à 0 → verrou de recharge 4 s (se faire submerger casse vraiment le bouclier).
- Chaque projectile absorbé alimente le Cristal comme une absorption au dash (Nova + jauge + soin Récupération éventuel).
- Portée : **projectiles ennemis uniquement** (pas contact, pas charge de boss, pas zones au sol).
- Visuel : bulle sphérique translucide procédurale (cyan `#2DD4CF`), luminosité = charges, flash à chaque absorption, anneau d'éclat à la casse. Aucun asset.

**Fichiers touchés (code, fait)** : `ManaShield.cs` (nouveau), `EnemyProjectile.cs` (hook), `GameUI.cs` (`SetManaShieldAvailable` / `UpdateManaShield` + pips).

**À faire côté Unity (utilisateur)** :
1. Ouvrir Unity pour laisser compiler `ManaShield.cs` (génère le `.meta`).
2. Ajouter le composant **`ManaShield`** sur les 3 prefabs `Player_Aether`, `Player_Kael`, `Player_Lyra`
   (`Assets/Game/Prefabs/Player/`). Le composant s'auto-neutralise si le perso n'est pas Gardien
   ou si le nœud n'est pas acheté — le mettre sur les 3 est volontaire (cohérent avec les autres buffs).
   - Valeurs Inspector par défaut OK. Ajuster `Bubble Radius` (~1.1) et `Bubble Center Offset` (~0,1,0)
     selon la taille de chaque modèle si la bulle est mal centrée.
3. **HUD** — créer un groupe de pips de charge :
   - Dans `Canvas > HUD`, à côté du groupe Cristal / ActionCluster, créer un GameObject vide
     `ManaShieldGroup` (Horizontal Layout Group, hauteur fixe ~32 px, comme ActionCluster).
   - 3 enfants `Image` (`ShieldPip1/2/3`), sprite = pastille (placeholder cercle OK pour tester,
     asset hexagonal plus tard via MetaIA — cf. liste graphismes ci-dessous).
   - Sur le composant `GameUI` de la scène :
     - `Mana Shield Container` → glisser `ManaShieldGroup`.
     - `Mana Shield Pips` → taille 3, glisser `ShieldPip1/2/3` dans l'ordre.
   - Placement suggéré : symétrique du groupe Clone (Clone = Lyra à un endroit, Bouclier = Kael à l'endroit miroir), ou juste sous les pastilles de Cristal.

**Graphismes (MetaIA, pas urgent)** :
- Pip de bouclier HUD : rune hexagonale ~32 px, plate, style identique aux pastilles de Cristal
  (rempli `#2DD4CF`, vide `#594C40`). Un cercle placeholder suffit pour tester la mécanique.

---

## Concentration (Aether) — spec

**Mécanique** : bonus de dégâts qui monte tant qu'Aether n'est pas touché.
- Montée **+8% / seconde**, plafond **+15% / +30% / +50%** selon le palier du nœud.
- **Se réinitialise à 0** à chaque coup réellement encaissé (Second Souffle inclus). Un dash à travers un projectile ne casse PAS le bonus (aucun dégât reçu → streak conservé).
- Grâce de 0,5 s après un coup avant que la montée reprenne.
- S'applique à **toutes les armes qu'Aether peut avoir** : tir de base, Fireball (impact + explosion + Brûlure), Éclair en chaîne, Orbital, Orbe rebondissant, Flaque de boue. Pas l'Ultime/Nova (nœud `crystalDamage` séparé).
- Multiplicateur central `PlayerBuffs.OutgoingDamageMultiplier` (statique, 1f par défaut).

**Fichiers touchés (code, fait)** : `PlayerBuffs.cs` (nouveau), `HealthSystem.cs` (cache + `NotifyDamaged`), `MetaProgressionManager.cs` (plafonds), `SkillTreeData.cs` (textes), 6 fichiers d'armes (multiply), `GameUI.cs` (`SetConcentrationAvailable` / `UpdateConcentration`).

**À faire côté Unity (utilisateur)** :
1. Laisser Unity compiler `PlayerBuffs.cs`.
2. Ajouter le composant **`PlayerBuffs`** sur les 3 prefabs `Player_Aether/Kael/Lyra` (`Assets/Game/Prefabs/Player/`). S'auto-neutralise hors Guerrier / nœud non pris → le mettre sur les 3.
3. **HUD** — indicateur texte de Concentration (essentiel pour une mécanique de momentum) :
   - Dans `Canvas > HUD`, un GameObject `ConcentrationGroup` avec un enfant `TextMeshProUGUI` (`ConcentrationText`). Placement suggéré : sous le rappel de défi, ou près de la barre d'XP.
   - Sur `GameUI` : `Concentration Container` → `ConcentrationGroup` ; `Concentration Text` → `ConcentrationText`.
   - Le texte affiche "Concentration +XX%" et vire du cyan (`#2DD4CF`) au doré (`#FFC94D`) à mesure que le bonus approche du plafond.

**Graphismes** : aucun (texte seul). Polish possible plus tard : aura montante sur Aether / vignette bord d'écran au plafond.
