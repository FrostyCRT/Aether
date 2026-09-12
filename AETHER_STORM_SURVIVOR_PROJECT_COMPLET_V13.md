# AETHER STORM SURVIVOR — Documentation projet (V13)

Jeu horde-survivor développé en solo sous Unity 2021.3.45f2. Nom du jeu **confirmé définitif** : Aether Storm Survivor (l'idée de changement de nom évoquée en V12 est abandonnée).

Objectif affiché : viser une qualité capable de rivaliser avec les vrais survivor-like du marché (Vampire Survivors, Brotato, Halls of Torment), en restant original sans s'éloigner du genre. Steam Next Fest visé pour février 2027, seuil critique 2000 wishlists.

---

## 1. Personnages jouables

3 personnages, chacun avec sa branche exclusive dans l'arbre de compétences méta (`SkillTreeData.CharacterBranch`) :

| Personnage | Branche skill tree | Couleur/thème | Arme exclusive |
|---|---|---|---|
| Aether | Guerrier | orange/feu | Fireball |
| Kael | Gardien | vert/nature | Aura |
| Lyra | Fantôme | cyan-violet/spectral | Knives (Salve de Couteaux) |

- Attaque de base commune à tous (`WeaponBase`, projectile d'eau), équipée au spawn.
- Les armes exclusives ne sont **plus** équipées au spawn (trop fort de démarrer avec 2 armes actives) — elles passent en upgrade à déblocage séparé (`RequiresUnlockPick`), garanties de sortir parmi les 3 choix du premier level-up où elles sont proposables, une fois par run.
- Filtrage strict par personnage dans `UpgradeData.IsAvailable()` via `IsForCharacter()`.
- Chaque branche du skill tree n'est accessible qu'à son personnage (filtrage fait en dehors des fichiers vus par l'assistant, confirmé par l'utilisateur).

### Capstones de branche (arbre de compétences méta, payé en Or)
- **Guerrier (Aether)** : Concentration → Fragmentation / Dégâts Cristal → Surpuissance
- **Gardien (Kael)** : Vitalité / Récupération → Second Souffle / Armure → Bouclier de Mana
- **Fantôme (Lyra)** : Impulsion Nova → Maîtrise du Cristal / Nova Étendue → **Dash Fantôme** (débloque le Clone, coût 1200, exclusif Lyra)

Le nœud `phantomDash` débloque le **Clone spectral** (voir HUD/gameplay ci-dessous). Description du nœud reformulée pour ne plus mentionner de touche précise (remappable dans de futurs paramètres).

---

## 2. Armes et gameplay

- **Fireball** (Aether) : explosion à l'impact garantie 100%, rayon 2.25 de base (+1.0 palier 1), palier 2 = dégâts, palier 3 = Brûlure (DOT + tint visuel pulsé orange).
- **Knives / Salve de Couteaux** (Lyra) : éventail de couteaux simultanés (base 2, +1 palier 1), un couteau toujours au centre garanti, palier 2 = dégâts, palier 3 = perforation.
- **Aura** (Kael) : anneau procédural (rayon 3.5 → 4.2), ralentissement palier 3 porté à 50%.
- **Double Tir** : recentré sur `WeaponBase` uniquement (bug de ciblage sur Fireball/Knives corrigé).
- 11 `UpgradeType` au total, pastilles de palier à 3 couleurs (vide gris-brun `#594C40`@150 / rempli cyan `#2DD4CF` / max doré `#FFC94D`) — palette réutilisée partout dans le jeu (HUD, upgrades, Réputation).
- Repères éco : ennemi standard 30 PV, costaud 70 PV, boss ~2000 PV, scaling ennemis jusqu'à x5 en fin de run. Rescale x10 global des dégâts/PV effectué et vérifié (Inspector) sur tous les prefabs.

### Dash / Absorption / Nova / Ultime
- Dash (Shift) : invincibilité courte, ouvre une fenêtre d'absorption de projectile.
- Absorption → déclenche la Nova (dégâts + ralentissement zone) et charge la barre de Cristal.
- Cristal plein → 1 charge d'Ultime stockée (max 2, `CrystalSystem`). Déclenchement à la touche F.
- Ultime scaling temporel x1 → x4 sur 15 min (évite qu'il devienne inutile en fin de run face aux PV scalés).
- Ultime "empowered" (x2 charges) : formule basée sur `_ultDamage * 5 * scale` (corrigé, ancien 100 fixe déconnecté).

### Clone spectral (Lyra, exclusif)
- Débloqué via le nœud `phantomDash` (arbre méta), pas via les cartes de level-up en run.
- Touche dédiée (C par défaut, jamais nommée dans les textes du jeu — remappable à terme).
- Laisse un clone qui attire les ennemis proches 2s, invincibilité temporaire, boost de vitesse d'échappée.
- Cooldown 8s, indépendant du Dash.

### Boss
- Charge télégraphée (zone rouge au sol qui grandit pendant le windup, `BossBase`), rayon = zone de dégât réelle.
- Ralentis par Boue/Aura comme les ennemis normaux (bug corrigé — ne recevaient que les dégâts avant).
- Musique dédiée par boss (`_bossMusicClip` sur `BossBase`), bascule via `MusicStarter.SwitchTo()` avec délai 0.5s + fondu + loop garanti, retour à la musique normale à la mort du boss.

---

## 3. Progression méta

### Deux monnaies séparées
- **Or** (`Data.totalGold`) : finance les 3 arbres de compétences par personnage.
- **Éclats** (`Data.totalEclats`) : finance uniquement les 3 stats de Réputation (Dégâts/Vitesse/Régénération, 5 paliers, coûts 100/300/700/1500/3000). Gagnés en fin de run selon la performance (niveau × 15 + boss × 60 + bonus victoire 200), indépendamment de l'or ramassé.

### Page Réputation (menu principal, 5ème onglet)
- 3 cartes horizontales (icône + pastilles + bouton d'achat direct, pas de popup).
- Onglets personnage colorés (préparés pour un futur système de skins, pas encore construit).
- Script `ReputationUI.cs`. Titres de cartes confirmés : **Dégâts / Vitesse / Régénération**.

---

## 4. Système de défis (nouveau, complet)

Un défi différent tiré **toutes les heures réelles** (UTC, `SaveData.currentChallengeHourBucket`), pas à chaque run — empêche de relancer une run courte pour retomber sur un défi plus facile. Le bonus n'est accordable **qu'une fois par heure**, même en retentant plusieurs runs.

- **12 défis** répartis en 3 paliers (`ChallengeDatabase.cs`) : 4 Faciles, 4 Moyens, 4 Difficiles.
- Tirage pondéré par palier (50/35/15 par défaut), anti-répétition sur les 3 derniers défis joués (`SaveData.recentChallengeIds`).
- Défi 8 initialement "ne jamais dasher" → remplacé par **"ne jamais utiliser l'Ultime"** (le Dash déclenche l'absorption qui alimente Nova ET Ultime, donc l'interdire aurait bloqué 3 systèmes à la fois sans que ce soit clair pour le joueur).
- Suivi en temps réel dans le HUD (icône **Parchemin**, sous le Chrono), détail complet dans le menu Pause (nom, palier, description, récompense chiffrée, statut).
- Récompense : **pourcentage de l'or ramassé pendant la run**, scalable par palier (Facile +10%, Moyen +25%, Difficile +50%) — choix de l'assistant, validé.
- Fichiers : `ChallengeDatabase.cs`, `ChallengeManager.cs` (vit dans la scène `Game`, sibling de `GameManager`).

---

## 5. HUD (état final, validé)

Structure sous `Canvas > HUD` :
- Vie (haut-gauche), Chrono (haut-centre), Or/Kills (haut-droite).
- Rappel du défi en cours sous le Chrono.
- **ActionCluster** (bas-centre, Horizontal Layout Group + Content Size Fitter, hauteur fixée à 32px pour éviter un bug de saut vertical) : Dash (barre) + Clone (cercle radial, Lyra uniquement) — se recentre automatiquement à 2 ou 3 éléments.
- **UltimateGroup** (indépendant, à droite d'ActionCluster) : pastilles de charge + texte "Ult x1/x2", séparé exprès pour que sa largeur variable ne fasse jamais bouger Dash/Clone.
- XP + Niveau (bas-centre, sous ActionCluster).

### Icône du Clone
Cercle violet, lettre **"C"** en police Bangers blanche (pas une icône illustrée — jugée trop détaillée à petite taille lors d'un essai précédent). Raison du choix : rappelle la touche d'activation (le joueur ne la connaît pas forcément) et coïncide avec "Clone".

### Palette Ultime (dots + texte, unifiée)
Empty `#594C40`@150 / Filled (x1) `#2DD4CF` / Max (x2) `#FFC94D` — `SetCrystalReady` prend un `int readyStacks` (pas un bool) pour que dots et texte affichent toujours exactement la même teinte au même palier.

### Contour (Outline)
Sur les icônes de cristal (`UnityEngine.UI.Outline`, 2 instances superposées pour un contour complet) et sur le texte TMP (via un matériau SDF dupliqué, propriété Outline du shader) — résout un problème de lisibilité du cyan sur fond d'herbe clair.

---

## 6. Écran de chargement (terminé)

- Scène dédiée `LoadingScreen`, chargée en premier dans les Build Settings, charge `MainMenu` en tâche de fond (`SceneManager.LoadSceneAsync`, `allowSceneActivation = false`) avec durée minimale garantie (3.5s) pour ne jamais flasher trop vite.
- Fond : illustration de bataille (3 persos vs meute de loups) conservée et étendue (outpainting via MetaIA) pour créer des zones calmes en haut (logo) et en bas (barre de chargement), texte "Appuyer sur une touche pour jouer" **en bas à droite** (choix volontaire).
- Logo séparé en calques : fond+cristal illustré d'un côté, texte du logo détouré de l'autre — évite que la lueur procédurale du cristal ne déborde sur les lettres.
- Effets vivants : Ken Burns très lent (zoom/pan bornés en sécurité, jamais sous l'échelle "pleine image"), particules ambiantes concentrées dans les bandes calmes haut/bas (pas au milieu, trop chargé visuellement), lueur procédurale pulsée sur le cristal.
- Barre de chargement stylée (fond brun foncé, remplissage cyan `#2DD4CF`, liseré doré, reflet animé qui balaye en boucle).
- Texte "Appuyer sur une touche" : scintillement doux (alpha min ~0.08 pour un contraste fort).
- Musique d'ambiance courte en boucle (via `MusicStarter`, toutes les musiques du jeu sont prêtes : menu, en jeu, boss, chargement).
- Fondu noir à la sortie (`SceneTransitionFader`, réutilisable pour toute future transition de scène).

---

## 7. Écrans de fin de run (Victory / Game Over) — ✅ TERMINÉ (2026-09-12)

Refonte complète des deux écrans (avant : placeholders de tout début de développement, notamment le Game Over qui n'avait ni cadre ni richesse). Même univers visuel élégant (parchemin/doré, cadre + boutons repris du menu Pause) pour les deux écrans, différenciés par la teinte — doré éclatant pour la Victoire, rouille/ash + fond plus sombre pour le Game Over — pas par deux esthétiques différentes. **Le Game Over a la même richesse de contenu que la Victoire** (record de la partie, build obtenue) — décision prise après analyse du genre (Vampire Survivors/Brotato/Halls of Torment traitent mort et victoire de façon quasi symétrique), avec en plus deux ajouts propres au Game Over (numéro de tentative, aperçu du prochain palier de progression) qu'aucun des trois n'a.

**Titre du Game Over tranché : "DÉFAITE"** (cohérent avec "VICTOIRE !", même registre).

### Structure finale (2 colonnes / 3 temps, identique dans l'esprit pour les 2 panels)
```
[Victory/GameOver]Panel
├── DimBackground (plus sombre côté Game Over)
├── CenterCard (parchemin, teinté grisé/désaturé côté Game Over)
│   ├── Crown
│   │   ├── (GameOver only) AttemptText   — "Tentative n°X"
│   │   ├── TitleText                    — "VICTOIRE !" / "DÉFAITE"
│   │   ├── RecapText / Message          — Victory: phrase de clôture ; GameOver: message contextuel dynamique (record/humour noir)
│   │   └── Separator1
│   ├── Body
│   │   ├── LeftCol ("Ce que tu emportes" / "Ce qu'il te reste")
│   │   │   ├── KeepSectionTitle
│   │   │   ├── KeepSection (2 cartes Or/Éclats côte à côte, icône+nombre centrés, comptage animé)
│   │   │   ├── ChallengeChipBg (défi réussi, inactif par défaut)
│   │   │   ├── StatStrip (Survie/Kills/Niveau/Boss X sur 3)
│   │   │   └── Victory: RecordHighlight + QuietLine (repli si ni record ni défi) — GameOver: GameOverNextUnlockText (déblocage perso > aperçu palier > repli "tout maxé")
│   │   └── RightCol ("Ton arsenal" / "L'arsenal que tu avais")
│   │       ├── BuildTitleText
│   │       └── ArsenalRow : PortraitImage (perso sélectionné, désaturé côté Game Over) + BuildGridContent (grille d'icônes teintées par branche, `UpgradeGridSlot`)
│   └── ButtonsRow : RetryButton (verrouillé jusqu'à la fin du reveal) + MainMenuButton
```

### Fonctionnalités communes aux deux écrans
- **Comptage animé satisfaisant** de l'Or et des Éclats (ease-out cubique), centrage icône+nombre qui s'adapte au nombre de chiffres.
- **Séquence défi réussi** : l'Or compte d'abord jusqu'au montant de base, puis 2 secondes après, si le défi est réussi, le message "Défi réussi ! x0.XX" apparaît (couleur dorée) et l'Or reprend son compte jusqu'au montant final.
- **Raccourci clavier de relance instantanée** (Entrée/Espace) + bouton Rejouer, tous deux **verrouillés jusqu'à la fin complète de la séquence de reveal** (système événementiel `GameUI.OnEndScreenRevealComplete`) — évite de sauter par-dessus le défi/record en spammant Entrée.
- **4 records** suivis (temps, kills, niveau, or en une partie) — Victory les met en avant seulement s'ils sont battus CETTE partie (highlight ciblé, pas de bloc de records à vie qui "dégonflerait le moment") ; Game Over les intègre à son message contextuel (record battu/presque battu en priorité, sinon humour noir selon la cause de mort).

### Spécifique au Game Over
- **Message contextuel** (`GameOverMessagePool.cs`) : ton familier, direct, sec, sans ponctuation excessive.
- **Numéro de tentative** ("Tentative n°X", `Data.totalRuns`).
- **Aperçu du prochain palier** (`MetaProgressionManager.GetNextUnlockPreview()`, comparaison par ratio du coût plutôt que par écart absolu depuis 2026-09-12 — corrige un biais qui favorisait toujours les nœuds en Éclats, plus rares) : "Encore X Or/Éclats pour débloquer [Nœud]" ou repli "tout maxé" si la branche + Réputation sont entièrement complétées.
- **Annonce de déblocage de personnage** : si un perso vient d'être débloqué pendant la run qui s'achève (ex. Kael au Boss 2), l'annonce prend le pas sur l'aperçu de palier.
- **Boss X/3 dynamique** dans le StatStrip (`GameManager.BossKillCount`, transmis à `ShowGameOver`) — contrairement à la Victoire qui peut se permettre un "3/3" fixe (elle ne se déclenche qu'à ce moment précis).

### Volontairement repoussé
- SFX sur les moments forts (défi réussi, record) — l'utilisateur gère tous les effets sonores en un seul gros morceau une fois le jeu terminé.
- Extension des causes de mort au-delà de boss/horde — nécessite `EnemyBase.cs` et le script des projectiles ennemis, jamais vus par l'assistant.
Icônes Or/Éclats : réutiliser celles déjà existantes (HUD pour l'Or, page Réputation pour les Éclats), pas de nouvel asset.

---

## 8. Historique des bugs majeurs résolus (V9 → V13)

- Freeze pendant dash/nova en plein level-up (désync `IsPaused`/`Time.timeScale`).
- Balles traversant les ennemis attirés par le Clone (composante Y non neutralisée dans le calcul de direction — corrigé partout, y compris la vérification de distance de contact).
- Mouvement joueur qui continuait après victoire (`FixedUpdate()` ne vérifiait pas `IsGameOver`).
- Ennemis figés en animation de marche en fin de partie (`GameManager.OnGameEnded`).
- Joueur qui disparaissait instantanément à la mort (`TriggerDeath()` jamais appelée).
- Gobelins qui sautaient en Y (composante Y non aplatie dans la force de séparation).
- Araignée figée après un saut (transition Jump→Locomotion manquante dans l'Animator).
- Coûts de Réputation bloqués au palier 3 (table de coûts partagée limitée à 3 entrées).
- Ultime "empowered" x2 utilisait un 100 fixe déconnecté de `_ultDamage`.
- Défi "boss en moins de 30s" aurait été trivialement toujours réussi si mesuré avec `RunTimer` (qui ne progresse pas pendant qu'un boss est vivant) — utilise `Time.time` à la place.

---

## 9. DA & Marketing

- Logo final : texte ornemental doré + cristal bleu lumineux, calques séparés du fond pour l'écran de chargement.
- Steam Direct payé, vérification d'identité Steamworks en cours.
- Politique Steam (2026) : les 5 captures d'écran obligatoires doivent être du gameplay réel, pas de l'art conceptuel — la capsule/le logo peuvent rester illustrés.
- Pipeline vidéo marketing : ComfyUI + Hailuo.
- Génération musicale : Suno (vérifier les droits d'usage commercial selon le plan), retouche de boucle via des outils comme Audjust, séparation voix/instrumental via UVR ou des outils web si besoin un jour.

---

## 10. Tâches en attente (à jour)

- Système de fusion (6 fusions au total).
- Système de skins (Or/Éclats) — aucune donnée/logique/asset construite.
- Vérifier les 3 mécaniques spéciales par personnage (Concentration/Récupération/Impulsion Nova) — nœuds existants, effet en jeu à reconfirmer.
- Nouvel ennemi "bulbe cracheur de projectile" (shooter fixe, tir en éventail, cadence faible) — modélisation 3D prévue lors du mois dédié Tripo3D.
- Ordre d'introduction des types d'ennemis à réfléchir avant d'ajouter le bulbe cracheur (éviter le chaos visuel).
- Paliers de difficulté par taille de map (3 maps, du champ infini à l'enceinte fermée) — idée posée, pas commencée.
- Écran d'accueil adaptatif selon le personnage sélectionné.
- Refonte des pages Personnage de Kael et Lyra (existent mais pas finalisées).
- SFX complets (volontairement repoussés à la fin du développement).
- Passe d'équilibrage/playtest après le rescale x10.
- Options/accessibilité (volume, plein écran, remapping clavier).
- Localisation anglaise pour Steam.
- Écran de crédits.
- Extension des causes de mort au-delà de boss/horde (nécessite `EnemyBase.cs`/projectiles ennemis).
