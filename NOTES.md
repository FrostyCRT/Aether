# Aether Storm Survivor — Notes de travail

Deux listes vivantes tenues au fil des sessions. Mises à jour à chaque passe.
Dernière mise à jour : 2026-09-12 (cadre autour du texte de repli vide + titre "Ce qu'il te reste" ambigu corrigé).

## GameOverPanel/VictoryPanel — cadre + titre ambigu (2026-09-12)

- **`Image` et `TextMeshProUGUI` ne peuvent PAS coexister sur le même GameObject** dans ce projet — `AddComponent` échoue silencieusement dans les deux sens (testé et confirmé directement). À retenir pour la suite : tout texte qui a besoin d'un fond doit être un **enfant** d'un GameObject séparé porteur de l'`Image`, jamais le même objet (déjà le cas pour `ChallengeChipBg`/`ChallengeChip`, maintenant aussi pour `EmptyBuildText`).
- **Cadre ajouté autour du texte de repli "grille vide"** (retour utilisateur : "pour mieux voir le texte") — nouveau `EmptyBuildBg` (Image, sprite `Background`) en parent, `EmptyBuildText` devient son enfant avec une marge interne. `PopulateEmptyBuildState()` bascule désormais le **parent** s'il porte une `Image` (même pattern que le chip de défi), sinon replie sur le texte seul. Teinte Victoire = `#DCCCA3` (même que les cartes Or/Éclats) ; Game Over = `#C4B8A5` (légèrement plus riche que le fond de carte pour se détacher). Vérifié en Play Mode réel sur les deux écrans.
- **Titre `LeftCol` du Game Over ambigu** : "Ce qu'il te reste" pouvait se lire comme "tout ce qu'il te reste dans le jeu" (contredit par le menu, où le joueur a encore plein d'Or/Éclats) plutôt que "ce que CETTE partie t'a donné". Reformulé en **"Ce que cette partie t'a rapporté"** — sans ambiguïté sur le périmètre (cette partie précise, pas le total).

## GameOverPanel — 2ᵉ retour utilisateur, capture précise (2026-09-12)

- **`AttemptText` ("Tentative n°X") flottait au-dessus de la carte**, visuellement détaché — trop proche du bord (20px), à l'intérieur du filet doré décoratif de la texture (`aged_parchment_background`, mesuré à ~26px d'inset). Corrigé : `CenterCard` agrandie de 880→**945px**, `Body` compense exactement (marge -350→-415, hauteur réelle **inchangée à 530px** — aucun effet sur l'équilibre `LeftCol`/`RightCol` déjà réglé). Cascade `Crown` redécalée (Attempt/Title/Message/Separator) avec une vraie marge sous le filet doré. Vérifié par mesure directe : 9,5px d'écart net entre le bas de `Crown` et le haut de `Body`, plus de chevauchement.
- **Vocabulaire** : "arme" → "compétence" dans les 2 pools de repli vide (`_victoryEmptyBuildLines`/`_gameOverEmptyBuildLines`) — la grille contient aussi des upgrades passives (Dégâts+, Cadence+...), pas que des armes.
- **Contraste de `GameOverNextUnlockText` toujours insuffisant** après le 1ᵉʳ correctif (éclaircir le fond ne suffisait pas) : passé sur l'encre foncée `#33271A` (celle du `StatStrip`, déjà prouvée lisible sur cette carte) au lieu de l'ink-soft `#5A4834` — plus la même nuance "discrète" que sur le fond plus clair de la Victoire, il fallait compenser le fond plus sombre par plus de contraste, pas l'inverse. Même couleur appliquée à `EmptyBuildText` par cohérence.
- **Question de ton, tranchée** : les messages d'humour noir de `GameOverMessagePool` ("T'es mort. Voilà, c'est dit.", "GG. Enfin non, pas GG."...) ne "faisaient pas jeu sérieux" selon l'utilisateur — rejoint le point déjà identifié dans "Grands chantiers" (#5) mais jamais réglé. 3 directions proposées (sobre/factuel, épique/sincère, dur-mais-motivant) — **"Dur mais motivant" choisi**. `GameOverMessagePool.cs` entièrement réécrit dans ce ton : zéro blague/deadpan, parle au joueur comme un adversaire respecté, pousse à rejouer. Les 3 phrases d'exemple validées par l'utilisateur sont reprises telles quelles dans le nouveau pool générique. **Le point #5 des "Grands chantiers" peut être considéré réglé pour le Game Over** (reste à vérifier si `_victoryClosers`/les autres pools de `GameUI.cs` ont besoin du même traitement — pas demandé, pas touché cette passe).

## GameOverPanel — retour utilisateur avec capture (2026-09-12)

Question posée directement : est-ce que refaire la même structure que la Victoire était voulu, et est-ce que le résultat rivalise avec les meilleurs du genre. Réponse honnête : la structure identique était une décision assumée (V13 §7 : "même univers, différenciés par la teinte, pas par deux esthétiques" — et c'est aussi ce que fait Vampire Survivors entre écran de victoire et de défaite), mais **l'exécution avait deux vrais défauts**, les deux visibles sur la capture fournie (mort très précoce : 00:18, 4 kills, niveau 1, aucune upgrade) :

1. **Grille d'upgrades vide → grand trou à côté du portrait** quand le joueur meurt/gagne sans avoir pris une seule upgrade (mort avant le 1er level-up, cas réel pas juste théorique). Corrigé : `PopulateBuildGrid()` renvoie désormais le nombre de tuiles posées ; nouveau `PopulateEmptyBuildState()` affiche une phrase de repli (`EmptyBuildText`, nouveau GameObject dans `ArsenalRow`, même zone que la grille) quand ce nombre est 0 — pool dédié par écran (`_victoryEmptyBuildLines` / `_gameOverEmptyBuildLines`, ton cohérent avec le reste). Appliqué aux **deux** écrans par cohérence/robustesse (théoriquement possible aussi sur une Victoire très rapide, pas juste le Game Over). Vérifié en Play Mode réel sur les deux.
2. **`GameOverNextUnlockText` presque illisible** — la teinte du `CenterCard` du Game Over (`#B8AFA0`, choisie pour "désaturer") multipliée à la texture du parchemin donnait un résultat bien plus sombre que prévu, écrasant le contraste du texte ink-soft (`#5A4834`) utilisé un peu partout dans `LeftCol`. Corrigé : teinte éclaircie à `#D6CDBE` (moins saturée que l'or de la Victoire, mais pas plus sombre que nécessaire — "cendre" plutôt que "boue"). Pas re-vérifié à l'œil (toujours aucune capture possible côté assistant), mais le ratio de contraste calculé passe d'environ 4:1 à 5,5:1 pour ce texte précis.

Comme toujours : sauvegardé, pas commité, aucune capture possible côté assistant — tout vérifié par mesure/Play Mode réel, reste à confirmer visuellement.

## Boss — PV restaurés + vrai bug d'équilibrage trouvé (2026-09-12)

L'utilisateur avait baissé les PV des 3 boss pour tester plus vite (`00:30`/boss quasi à 0 PV lors des tests d'écrans de fin). Restauré, mais **pas au même endroit pour chacun** — utile de le noter puisque "aller dans les scripts" ne suffit pas pour 2 des 3 :

- **Boss 1** (`BossBase.prefab`, générique, pas de sous-classe dédiée) : PV vit **uniquement sur le prefab** (`_maxHealth` serialisé), rien à changer côté script. Était à 20, remis à **20000**.
- **Boss 2** ("Le Cerf Ancestral", `BossDeer.prefab`) : PV **codé en dur dans `BossDeer.Start()`**, écrase silencieusement toute valeur du prefab à chaque lancement — la valeur du prefab (50, puis remise à 50000 par cohérence) n'a jamais d'effet réel. **Vrai bug d'équilibrage trouvé au passage, pas juste un réglage de test** : ce hardcode valait `500f` (commentaire "x10" présent, mais jamais mis à jour depuis) alors qu'un commentaire dans `BossCorruptedSource.Start()` documente explicitement la progression voulue **20000 → 50000 → 125000** entre les 3 boss. Le Cerf tournait donc réellement à 500 PV depuis le rescale ×10 — 100x moins que prévu, et largement en dessous des 2 autres boss. Corrigé dans le script à **50000**.
- **Boss 3** ("La Source Corrompue", `BossCorruptedSource.prefab`) : PV vit sur le **prefab** (le script a un commentaire expliquant qu'un ancien hardcode a été retiré exprès). Était à 125, remis à **125000**.
- Un 2ᵉ jeu de ces 3 prefabs existe sous `Assets/Resources/Prefabs/Enemies/` avec des valeurs différentes et tout aussi incohérentes (5000/1/2000) — non référencé par `WaveManager` (vérifié), donc sans effet en jeu ; probablement des doublons obsolètes, pas touchés cette passe.


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

- **GameOverPanel — construit de zéro en parité avec VictoryPanel (2026-09-12)** ✅ scène et code faits, testés en Play Mode réel. Le VictoryPanel ci-dessous est **considéré terminé par l'utilisateur** ("je le trouve vraiment bien et complet") — chantier suivant naturel, cf. V13 §7 ("même richesse de contenu que la Victoire, différenciés par la teinte").
  - **Titre tranché** : "DÉFAITE" (l'option proposée dans V13 §7, jamais actée) — cohérent avec "VICTOIRE !", même registre linguistique.
  - **Différenciation visuelle** (voulue explicitement par l'utilisateur — "il faut qu'il soit différenciable") : teinte rouille/ash (`#B5533E` titre+séparateur, contour noir identique à Victoire) au lieu de l'or, `CenterCard` teintée grise/désaturée (`#B8AFA0F0` au lieu de `#FFFFFFF0`), `DimBackground` plus sombre (`#00000058` contre `#00000037` côté Victoire — une défaite pèse plus lourd). Or/Éclats gardent leurs couleurs fonctionnelles habituelles (`#FFD700`/`#00ECE3`) — ce sont des devises, pas un accent de mood, les changer casserait le code-couleur appris ailleurs dans tout le jeu (HUD, Réputation).
  - **Structure identique à la Victoire** (2 colonnes / 3 temps), reconstruite en clonant les réglages exacts de `VictoryPanel` (sprites, polices, tailles de `LayoutElement`/`HorizontalLayoutGroup`/`VerticalLayoutGroup`) plutôt que de redeviner — zéro nouvel asset :
    - **Crown** : `AttemptText` ("Tentative n°X", nouveau, discret au-dessus du titre) → `TitleText` "DÉFAITE" → `Message` (remplace RecapText, contenu dynamique déjà codé par `PopulateGameOverMessage` — jamais câblé jusqu'ici) → `Separator1`.
    - **LeftCol "Ce qu'il te reste"** (reformulé exprès, pas "Ce que tu emportes" — même registre direct/un peu noir que `GameOverMessagePool`) : cartes Or/Éclats identiques à la Victoire (mêmes tailles, mêmes couleurs, centrage adaptatif déjà hérité) → `ChallengeChipBg` (même mécanique) → `StatStrip` → `GameOverNextUnlockText` (remplace RecordHighlight/QuietLine, voir plus bas).
    - **RightCol "L'arsenal que tu avais"** (passé au passé, différenciation légère) : portrait désaturé (`PopulateGameOverPortrait()`, nouveau, teinte `#8A8580` multipliée — pas une vraie désaturation sans shader dédié, mais assez pour lire "défaite" sans nouvel asset ni shader) + grille d'upgrades identique à la Victoire (`PopulateBuildGrid`, même prefab `UpgradeGridSlot`, même `GridLayoutGroup` 125px/3 colonnes).
    - **ButtonsRow** : "Rejouer" / "Retour au menu", mêmes sprites/police, liés à `GameManager.RestartGame()`/`GoToMainMenu()` (vérifié via les mêmes listeners persistants que les boutons de Victoire).
  - **3 vrais bugs/lacunes trouvés et corrigés en construisant cet écran** (le genre de choses qu'on ne voit qu'en assemblant réellement l'écran, pas en lisant le code) :
    1. **`ShowGameOver()` n'a jamais reçu le nombre de boss vaincus** — `GameManager.BossKillCount` existait et était suivi depuis le début, mais jamais transmis à l'UI. Le `StatStrip` ne pouvait donc PAS afficher une vraie progression ("Boss X/3" comme la Victoire, qui elle peut se permettre un "3/3" fixe puisqu'elle ne se déclenche qu'à ce moment). Corrigé : nouveau paramètre `bossKillCount` sur `ShowGameOver()`, passé depuis `GameManager`. Vérifié en Play Mode : "Boss 1/3" puis "Boss 2/3" affichés correctement selon l'appel.
    2. **`GetNextUnlockPreview()` comparait un manque d'Or et un manque d'Éclats sur la même échelle absolue** (déjà repéré dans NOTES.md, jamais corrigé) — un nœud "à 50 Éclats près" gagnait toujours contre un nœud "à 2000 Or près" alors que les Éclats sont bien plus rares (~200-500/run contre des milliers d'Or). Corrigé : comparaison par **ratio** (part déjà payée du coût, 0 à 1) au lieu d'un écart absolu, indépendant de la devise. Vérifié en conditions réelles (vrai save utilisateur) : fonctionne correctement sur la branche Guerrier (trouve "Concentration", 1987/100 Or → déjà payable) ; **découverte au passage** que la branche Gardien ET les 3 nœuds de Réputation du vrai save sont désormais TOUS maxés (`vitalityLevel=3, armorLevel=3, secondWindUnlocked=true, manaShieldUnlocked=true, recuperationLevel=3, reputationDamage/Speed/RegenLevel=5` partout) — un cas limite réel, pas théorique.
    3. **Ce cas limite ("tout est maxé sur la branche active") laissait `GameOverNextUnlockText` complètement vide** — exactement le même genre de trou que `QuietLine` avait comblé côté Victoire, jamais anticipé pour le Game Over. Corrigé : nouveau pool `_gameOverMaxedOutLines` (5 phrases, ton fier/positif — *"Tu as tout donné à cette branche. Littéralement."*) affiché en repli. Ce texte est désormais présent dans la quasi-totalité des cas (déblocage perso > aperçu palier > repli "tout maxé"), comble naturellement l'espace que `QuietLine` devait combler manuellement côté Victoire.
  - **Nouveau, non prévu dans le sketch V13 §7** : `PlayNextUnlockPreview()` vérifie désormais `ConsumePendingUnlockNotification()` EN PREMIER (prévu depuis le début dans NOTES.md/V13 — "à montrer sur le Game Over si le joueur meurt ensuite [après avoir débloqué Kael]" — jamais câblé jusqu'ici). Si un perso vient d'être débloqué pendant la run, l'annonce ("Nouveau personnage débloqué : Kael !", surlignée en or) prend le pas sur l'aperçu du prochain palier — anticlimatique sinon.
  - **Nettoyage** : les anciennes méthodes `PopulateRecordsText`/`PopulateBuildList` (bloc 5 lignes + liste texte, jamais utilisées par la Victoire depuis sa refonte, gardées mortes dans le code) supprimées avec leurs 5 champs `[SerializeField]` associés (`_gameOverRecordsText`, `_gameOverBuildListText`/`2`, `_victoryRecordsText`, `_victoryBuildListText`/`2`, `_buildListMaxLinesPerColumn`) — plus aucune trace de l'ancien design "liste de texte" dans `GameUI.cs`.
  - **Vérifié en Play Mode réel** (bootstrap `MainMenu`→`Game`, jamais de `SaveSystem.Save` appelé, save réel confirmé intact après coup) : mort boss (Message pool correct, StatStrip dynamique correct, portrait teinté et bon sprite selon perso sélectionné) ; mort horde avec record de kills presque battu (message + couleur or corrects) ; `GetNextUnlockPreview()` sur les 2 branches (Guerrier trouvé correctement, Gardien correctement vide car tout maxé) ; repli "tout maxé" vérifié visuellement (texte + activation) bien qu'à la main plutôt que via la coroutine (voir note technique ci-dessous).
  - **Non testé en live, par prudence** : le déclenchement RÉEL de la notification "nouveau personnage débloqué" (`ConsumePendingUnlockNotification`) — le seul moyen légitime de le déclencher (`UnlockCharacter()`) appelle `SaveSystem.Save()` et le save réel a déjà Kael/Lyra débloqués, donc tout appel aurait été un no-op silencieux ou aurait nécessité de d'abord dé-débloquer un perso (`DebugResetCharacterUnlocks()`, qui sauvegarde aussi) — refusé pour ne pas toucher à la vraie progression de l'utilisateur. Logique relue attentivement à la place (calque exact du pattern déjà éprouvé de `ConsumePendingUnlockNotification` existant).
  - **Note technique (nouvelle observation cette passe)** : dans ce Play Mode piloté par MCP (pas de focus humain sur la fenêtre Editor), `Time.frameCount` reste bloqué à 1 entre mes appels malgré plusieurs secondes réelles écoulées (`Time.unscaledTime` avance, les frames non) — Unity ne semble tourner qu'au moment précis où un appel MCP le sollicite. Les coroutines à base de `CountUpNumber`/délais semblent malgré tout se terminer (un unique frame avec un `deltaTime` énorme suffit à dépasser leur durée), mais je n'ai pas pu observer `PlayNextUnlockPreview()` passer par son fondu de 0,4s dans cet environnement précis - vérifié à la main à la place (voir ci-dessus). Sans incidence en jeu réel (le joueur a un vrai framerate constant) - juste une limite de cet environnement de test, à garder en tête pour de futurs tests de coroutines à délai.
  - Sauvegardé dans `Game.unity`. Comme toujours, **pas de vraie capture d'écran possible côté assistant** — tout vérifié par mesure numérique et par les valeurs réellement affichées en Play Mode, jamais par l'œil. Reste à confirmer visuellement par l'utilisateur.

- **Refonte VictoryPanel — structure 3 temps** ✅ **TERMINÉ, validé par l'utilisateur (2026-09-12)** — scène ET code faits (maquette validée : artifact "Refonte écran Victoire"). Colonne unique de 10 blocs → 2 colonnes / 3 temps, construit directement dans `Game.unity` via MCP (CenterCard 1720×880, plein écran ~100 px de marge) :
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
  - **Retour utilisateur avec les vrais portraits perso (debout, sans fond, 2026-09-11 soir)** — nouvelles illustrations en place, testées manuellement dans la scène. Deux points relevés :
    1. **Bug réel trouvé — pastilles de palier de la grille d'upgrades mal positionnées** : sur `UpgradeGridSlot.prefab`, le `HorizontalLayoutGroup` de `TierDotsRow` était **désactivé** (`enabled=false`), donc les 3 dots gardaient leurs `anchoredPosition` figées (x=52/88/124) — des valeurs héritées d'un contexte bien plus large qu'un slot de 108px, ce qui les faisait déborder par-dessus les tuiles voisines (bandeau de points qui traverse plusieurs cases à l'écran). Corrigé : HLG réactivé + positions recalculées et validées pour la vraie taille runtime (cellule 108×108 imposée par le `GridLayoutGroup`, jamais les 68×68 "bac à sable" du mode édition de prefab) → dots centrés, contenus dans les 102px de `TierDotsRow`, vérifié par instanciation live dans `BuildGridContent` (les 3 dots tiennent dans `[36,66]` sur 102px de large).
    2. **Trop de vide dans "Ce que tu emportes" quand record/défi sont absents** — confirmé, déjà pressenti. Cause identifiée : Or et Éclats étaient 2 lignes pleine largeur (723px) empilées avec l'icône+nombre collés à gauche, laissant une grande bande vide à droite de chaque valeur — la maquette validée prévoyait au contraire 2 **cartes encadrées côte à côte** (`.loot { grid-template-columns: 1fr 1fr }`, fond `#dccca3`), jamais appliqué jusqu'ici. Corrigé : `KeepSection` passé de `VerticalLayoutGroup` à `HorizontalLayoutGroup` (2 cartes de ~354px égales), fond ajouté sur `GoldRow`/`EclatsRow` (sprite `Background` déjà utilisé ailleurs dans la scène, teinte exacte `#DCCCA3` de la maquette), icônes agrandies 66→80px, hauteur de la section 180→210px. Résultat mesuré : vide vertical du pire cas (ni record ni défi) passe de 196px (37%) à 166px (31%) — modeste sur le papier, mais le vrai gain est la disparition de la bande vide horizontale à droite de chaque nombre, plus visible à l'œil que le pourcentage ne le suggère. **Le vide résiduel (~31%) est en partie voulu par la maquette elle-même** (pas de records à vie affichés ici, volontairement — "ça dégonflerait le moment") ; à voir si ça suffit après un nouveau test utilisateur.
  - **Centrage icône+nombre dépendant du nombre de chiffres (2026-09-11)** : `LayoutElement.preferredWidth` figé à 200px sur `GoldText`/`EclatsText` → un nombre court laissait du vide dans sa propre boîte (alignée à gauche), décalant visuellement la paire icône+nombre. Corrigé : `preferredWidth=-1` (TMP fournit sa propre largeur, recalculée à chaque frame de l'animation de comptage) + `childControlWidth=true` sur le HLG + alignement texte `Midline`. Vérifié analytiquement de 1 à 6 chiffres : marges gauche/droite identiques dans tous les cas (écart nul à l'arrondi flottant près).
  - **Vrai playtest avec les nouveaux portraits (2026-09-11, boss à 0 PV pour tester vite)** — capture d'écran fournie, tout fonctionne en conditions réelles. Deux retours :
    1. **Cadres de tuiles d'upgrade trop petits par rapport aux icônes** — cause réelle : `parchment_ember_aura.png` (fond de la tuile) est une des 4 illustrations de carte de level-up par branche (grande, avec une aura/lueur décorative qui occupe la majorité du canevas ; le parchemin "lisible" ne fait que ~60% de la largeur). Écrasée dans une tuile de grille compacte, elle rend le cadre visuellement minuscule à côté d'une icône qui, elle, remplit presque toute la tuile — pas un problème de taille de `RectTransform` (déjà pile la taille de la cellule), un problème de mauvais asset pour cet usage. Corrigé : fond remplacé par le sprite `Background` (déjà utilisé sur les cartes Or/Éclats), plat et sans marge gâchée, qui répond bien à la teinte de branche déjà appliquée par code (`GetTileTint`). Dots agrandis 8→12px (retour "un poil petits"), repositionnés.
    2. **Découverte en cours de route** : le `GridLayoutGroup` de `BuildGridContent` est passé de cellSize 108/spacing 10/3 colonnes-non-5 à cellSize **125**/spacing **20**/constraintCount **3** depuis la dernière session (changé manuellement par l'utilisateur en testant, probablement pour la taille d'icône). Repositionnement des dots recalibré sur cette taille réelle (119px de `TierDotsRow`, pas 102).
  - **Retour utilisateur (2026-09-12)** : correction du calcul du pire cas — **Fireball/Aura/Couteaux sont exclusifs à un seul perso chacun** (pas les 3 en même temps dans une run), donc max réel = 11 - 2 = **9 upgrades distincts**, pas 11. À 3 colonnes ça fait exactement 3 rangées pile, jamais 4. Trois demandes traitées :
    1. **`UnlockDot` (losange de déblocage, coin haut-gauche) mal centré** — même famille de bug que les `TierDotsRow` : ne chevauchait pas d'autres tuiles cette fois, mais tombait en partie dans le rayon d'arrondi (~10px, `border` du sprite `Background`) du nouveau fond de tuile (marge de seulement 3px). Corrigé : marge portée à 16px, vérifié par instanciation live (le point le plus proche du coin est à une distance radiale de 14,8px du coin réel, hors du rayon de 10px).
    2. **Tuile encore trop petite** : `cellSize` 125→**140** (le max raisonnable vu la largeur dispo de la grille, 486px sur 3 colonnes à spacing 20). `ArsenalRow` recalculé sur le vrai pire cas (9 upgrades / 3 colonnes = 3 rangées pile) : 3×140+2×20 = **460px** (au lieu de 373, qui datait de l'ancien calcul à 5 colonnes/108px). Dots `TierDotsRow` (12px) et `UnlockDot` recalibrés pour 140px.
    3. ~~**Tentative pour le vide de "Ce que tu emportes"** : label "OR"/"ÉCLATS" + icônes 80→104px + cartes 210→330px + `CenterCard` 880→960px.~~ **Rejetée par l'utilisateur** ("ça me convient pas") — entièrement annulée et revérifiée : `cellSize`=125, `ArsenalRow`=373, `CenterCard`=880, cartes Or/Éclats revenues à la structure simple (`HorizontalLayoutGroup`, icônes 80px, hauteur 210px, sans label). Le centrage adaptatif icône+nombre (point ci-dessus) est resté intact pendant l'annulation.
  - **Point de repère (2026-09-12)** : l'utilisateur a fini ses propres ajustements sur `UpgradeGridSlot.prefab` — **ce prefab est considéré terminé à 100 %**, ne plus y toucher sauf demande explicite.
  - **Vraie solution du vide, acceptée** : `PopulateVictoryQuietLine()` (nouveau, `GameUI.cs`) — une phrase de texture (pool `_victoryQuietLines`, 7 entrées, même famille de ton que `_victoryClosers` mais sujet différent : reconnaît l'absence de record/défi sans plomber l'ambiance, ex. *"Pas de record aujourd'hui, mais la victoire est bien réelle."*) affichée **UNIQUEMENT si ni le highlight de record ni le chip de défi ne sont actifs** (les deux autres suffisent déjà à remplir l'espace quand l'un d'eux est là — confirmé par l'utilisateur). `PopulateVictoryRecordHighlight()` renvoie désormais un `bool` (record affiché ou non) pour piloter cette décision depuis `ShowVictory()`. Nouveau GameObject `QuietLine` dans `LeftCol` (dernier enfant, même style que `RecapText` : italique, `#5A4834`), nouveau champ `_victoryQuietLineText`. Testé en Play Mode réel (3 scénarios : ni l'un ni l'autre → affiché ; record battu → masqué ; défi réussi sans record → masqué), tous corrects. Vide du pire cas mesuré : **166px (31%) → 92px (17%)**, sans toucher à `CenterCard`/`RightCol`.
  - **Note technique pour la suite** : plusieurs `LayoutElement.preferredHeight` posés sur des objets enfants de `RightCol`/`LeftCol` ne se sont PAS répercutés sur le `rect.height` réel malgré des `LayoutRebuilder.ForceRebuildLayoutImmediate` répétés (y compris sur des appels séparés, pas juste dans le même script) — contrairement à des passes précédentes où ça avait fonctionné. Contourné en fixant `sizeDelta` directement sur les RectTransform à ancrage non-stretch concernés, qui n'a aucune dépendance à un recalcul de layout group. Si un futur ajustement de taille sur ce panel ne se répercute pas visuellement, c'est le premier réflexe à avoir plutôt que de re-déboguer le layout group.
  - Toujours **pas de vraie capture d'écran possible** côté assistant (Canvas Screen Space Overlay, hors de portée des outils de capture disponibles) — tout vérifié par mesure numérique (marges, tailles, distances) et par test Play Mode réel, jamais par l'œil. Reste à confirmer visuellement par l'utilisateur.

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

5. ~~**Choisir un ton.** Messages de mort sarcastiques vs lore perso épique-sincère : incohérent. Une seule voie.~~ ✅ **Tranché (2026-09-12)** : "dur mais motivant" (adversaire respecté, zéro blague/meme), `GameOverMessagePool.cs` réécrit en conséquence. Reste à vérifier si les autres pools de texte (`_victoryClosers` et le reste de `GameUI.cs`) sont déjà cohérents avec ce choix ou méritent la même passe.

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
