# AETHER STORM SURVIVOR — Documentation projet (V16)

> **Mise à jour : 2026-10-19** (diagnostic complet avant sortie : corrections de l'audit, coffre de boss à la place de l'XP, Choc des titans retiré de la sélection, 3 niveaux de difficulté en Classique - détail dans `NOTES.md`). Cette synthèse décrit le jeu tel qu'il est aujourd'hui et ce qui reste à faire.
> - Détail session par session (décisions, bugs corrigés, valeurs réglées) : **`NOTES.md`** (source de vérité).
> - Rôle de chaque script : **`SCRIPTS_INDEX.md`**.
> - Bugs et chantiers avant la sortie, avec leur avancement : **`AUDIT_VERSION_FINALE.md`**.
> - Tout ce qui reste à modéliser en 3D (skins, projectiles, décor...) et les sons manquants : **`A_MODELISER_3D.md`**.

Jeu horde-survivor 3D vue de dessus, développé en solo sous **Unity 6 (6000.0.83f1)**, pipeline URP. Nom définitif : **Aether Storm Survivor**.

Objectif : rivaliser avec les références du genre (Vampire Survivors, Brotato, Halls of Torment) en restant original. Steam Next Fest visé pour février 2027 (seuil critique : 2000 wishlists). Accès Steamworks complet obtenu le 2026-09-17 (intégration du SDK pas encore commencée).

---

## 0. État d'avancement en un coup d'œil

| Domaine | État |
|---|---|
| Boucle de jeu (run de 15 min, 3 boss, level-up, fusions) | ✅ Complète |
| 3 personnages jouables + déblocages | ✅ Complet |
| 3 modes de jeu commercialisés + 1 retiré | ✅ Classique, Sans fin, Ruée de boss (ces 2 derniers pas encore validés par l'utilisateur) ; Choc des titans retiré de la sélection le 2026-10-19 (code conservé, non supprimé) |
| Niveaux de difficulté (Classique) | ✅ 3 paliers Modéré/Violent/Apocalyptique (2026-10-19), multiplicateurs non playtestés |
| Méta-progression (arbres de compétences, Réputation, défis horaires) | ✅ Complète |
| Boutique de skins | 🟡 Système et interface prêts, **aucun skin produit** (21 à modéliser) |
| Menus, HUD, paramètres complets (affichage, graphismes, audio, jeu, interface, commandes, langue) | ✅ Complets |
| Localisation : 11 langues (FR, EN, DE, ES, PT-BR, IT, PL, RU, ZH, JA, KO) | ✅ Terminée |
| Écran de chargement, transitions, écrans Victoire/Défaite | ✅ Terminés |
| Modèles 3D | 🟡 Personnages, ennemis et boss faits ; projectiles, zones au sol, Bulbe, décor de l'arène encore en primitives (voir `A_MODELISER_3D.md`) |
| **Sons (SFX)** | 🔴 **Aucun bruitage en jeu** (seules les musiques existent) |
| Trailer Steam + version verticale | ✅ Validés (2026-10-14, projet Remotion `../Aether Storm Trailer`) |
| Audit « version finale » | 🟡 Étapes 1 (bugs) et 2 (équilibrage) faites le 2026-10-16 ; étapes 3 (sortie) et 4 (pro) à faire |
| Intégration Steam (SDK, succès, Cloud) | ⏳ Pas commencée |
| Manette / accessibilité | 🟡 Manette partielle (Input Manager legacy), pas d'option secousses/flashs |

---

## 1. Personnages jouables

| Personnage | Branche de l'arbre | Couleur | Arme exclusive | Déblocage |
|---|---|---|---|---|
| Aether | Guerrier | ambre/feu | Boule de feu | Dès le départ |
| Kael | Gardien | vert/nature | Aura de mana | Atteindre le Boss 2 dans une partie, ou 500 Éclats |
| Lyra | Fantôme | bleu-violet/spectral | Salve de couteaux | Battre le Boss 3, ou 1200 Éclats |

- Attaque de base commune (`WeaponBase`, projectile d'eau) équipée au départ.
- Les armes exclusives ne sont **pas** équipées au départ : elles se débloquent par une carte de level-up garantie tôt dans la partie, filtrée par personnage.
- Clone spectral (Lyra uniquement, nœud « Clone Spectral », renommé le 2026-10-19 - ne correspondait plus à l'effet depuis que ce n'est plus un dash) : touche dédiée, attire les ennemis 2 s, invincibilité, boost de fuite, recharge 8 s.

### Arbre de compétences (payé en Or, une branche par personnage)
- **Guerrier (Aether)** : Concentration, Cadence → Fragmentation, Dégâts Cristal → Surpuissance (dégâts ×2 pendant 5 s après l'Ultime, sur toutes les armes depuis le 2026-10-16).
- **Gardien (Kael)** : Vitalité, Récupération → Second Souffle, Armure → Bouclier de Mana.
- **Fantôme (Lyra)** : Impulsion Nova, Dash Amélioré → Maîtrise du Cristal (réduit de 1 le nombre de charges de Nova pour l'Ultime, 6 à 5), Nova Étendue → Clone Spectral.
- Coûts par palier : 100 / 300 / 700 ; nœuds uniques 500 à 1200.

---

## 2. Armes, améliorations et fusions

- **Armes** : tir de base, Boule de feu (Aether), Aura (Kael), Couteaux (Lyra), Orbitaux, Chaîne de foudre, Boue, Orbe rebondissant, Double tir.
- **Améliorations universelles** : Dégâts+ et Cadence+ (+20 % par prise, 5 prises), Soin. Depuis le 2026-10-16, elles sont **additives et identiques sur toutes les armes** (5 × 20 % = ×2) et s'appliquent aussi aux armes obtenues plus tard.
- **6 fusions** (deux armes au palier max → une arme fusionnée plus forte) : Terre Calcinée (Boule de feu + Boue), Vortex de Mana (Aura + Orbitaux, Kael), Lames Ricochet (Couteaux + Orbe, Lyra), Orbes Foudroyants (Orbitaux + Foudre), Marécage Maudit (Boue + Foudre), Sillage Boueux (Orbe rebondissant + Boue). Proposées en Classique et Sans fin uniquement. (Remplace depuis le 2026-10-18 l'ancienne fusion "Orbes Jumeaux", Double tir + Orbe, jugée incohérente et retirée.)
- **Dash / Absorption / Nova / Ultime** : le Dash (invincible) absorbe un projectile → Nova (dégâts + ralentissement) + charge de Cristal. Cristal plein = 1 Ultime stocké (max 2, la 2e charge donne un Ultime renforcé). Dégâts de l'Ultime ×1 → ×4 sur 15 min.
- Bonus de dégâts dynamiques centralisés dans `PlayerBuffs.OutgoingDamageMultiplier` (Concentration × Surpuissance), lu par toutes les armes.

---

## 3. Ennemis, boss et difficulté

- **Ennemis** : Loup, Gobelin à sarbacane (tireur mobile), Golem (costaud), Corbeau, Tisseuse corrompue (araignée qui saute), Bulbe cracheur (tireur fixe, **encore un cube placeholder**). Animation par texture (VAT) pour tenir la foule.
- **Apparition** (`EnemySpawner`, seule référence depuis le 2026-10-16) : 6 phases, plafond d'ennemis simultanés 20 → 45 → 75 → 120 → 105 → **160** (dès 12 min), cadence 2 s → 0,6 s. Mode Sans fin après 15 min : jusqu'à 200 ennemis, cadence 0,3 s. Paliers de composition (types d'ennemis + PV ×1 à ×5) toutes les 2-4 minutes. Réglable dans l'Inspector de l'objet EnemySpawner (scène Game). **À tester en jeu** : la fin de partie est nettement plus dense qu'avant.
- **Boss** (un toutes les 5 min, le chrono s'arrête pendant le combat) :
  1. **Dragon de Mana** : 20 000 PV, charge télégraphiée, tir radial.
  2. **Cerf Ancestral** : 90 000 PV (enfin appliqués depuis le 2026-10-16), saut, spirale de projectiles, rage sous 30 %.
  3. **Source Corrompue** (serpent ancré à une faille) : 180 000 PV, Pulse de cristaux, Frappe, Onde de corruption, Invocation de mini-boss, Implosion.
- Les boss 1 et 2 soignent le joueur de 50 % à leur mort. Les 3 boss utilisent pour l'instant **la même musique**.
- **Récompense de mort de boss** (changé le 2026-10-19) : un vrai boss ne donne plus de burst d'XP, il fait apparaître un **coffre** (`BossChest`) à ramasser qui accorde directement **2 tirages de carte complets** (3 choix à chaque fois), sans jamais donner d'XP - récompense fixe et lisible plutôt qu'une XP qui pouvait être gâchée ou déclencher une cascade illisible. Un mini-boss invoqué (Source Corrompue) garde le burst de gemmes classique.
- **XP** (hors mort de boss) : gemmes 10/20/50, reste reporté sur la mort suivante (aucune XP perdue), courbe `55 × niveau^1,5` (allégée le 2026-10-16). Rayon d'attraction débloqué au niveau 3.
- **Difficulté du Classique** (nouveau, 2026-10-19) : 3 paliers **Modéré / Violent / Apocalyptique**, sélectionnables dans l'onglet Jouer sous le sélecteur de mode (toujours réversible, aucun palier verrouillé). Modéré = comportement actuel inchangé. Violent/Apocalyptique agrandissent la carte jouable (110×110 → 140×140 → 180×180), augmentent les PV/dégâts des ennemis et boss, le nombre d'ennemis simultanés et l'Or/les Éclats de fin de partie, et régénèrent le décor de bordure à la nouvelle taille. Multiplicateurs posés comme premiers jets, non encore playtestés.

---

## 4. Modes de jeu

| Mode | Règle | Déblocage |
|---|---|---|
| Classique | 3 boss en 15 min, victoire au Boss 3 | Dès le départ |
| Sans fin | Les boss reviennent en boucle, de plus en plus forts | Dès le départ |
| Ruée de boss | Les boss s'enchaînent | Avoir battu 1 boss en Classique |

Seul le Classique compte pour la progression (défis, déblocages). Sans fin et Ruée de boss ne sont pas encore validés par l'utilisateur et sont volontairement absents du trailer.

**Choc des titans retiré de la sélection le 2026-10-19** (les 3 boss en même temps, build entièrement maxée dès le départ) : zéro décision de build pendant la partie, à l'opposé du cœur stratégique du genre, et ses constantes d'équilibrage n'étaient que des premiers jets jamais playtestés. Retrait non destructif (`GameModes.IsShippable`) - le code, l'enum et les statistiques de sauvegarde existent toujours, récupérable pour un futur mode post-lancement.

---

## 5. Progression méta

- **Or** : finance les arbres de compétences et les skins Classique.
- **Éclats** : financent la Réputation, les skins Prestige et le déblocage anticipé de Kael/Lyra. Gagnés en fin de partie selon la performance.
- **Réputation** (onglet du menu) : 3 bonus permanents, Dégâts (jusqu'à +42 %), Vitesse (jusqu'à +25 %), Régénération (jusqu'à 70), 5 paliers chacun.
- **Skins** (même onglet) : par personnage, une rangée Classique (Or) et une rangée Prestige (Éclats) de 4 emplacements. Seule la tenue d'origine existe ; les emplacements vides affichent « BIENTÔT ». Ajouter un skin = une ligne dans `Resources/SkinCatalog.asset` (aucun code), mais il faut le modèle 3D, le prefab et le portrait (**21 skins prévus**, voir `A_MODELISER_3D.md`).

---

## 6. Défis horaires

- Un défi tiré **par heure réelle (UTC)**, identique pour toutes les parties de l'heure ; récompense accordée une seule fois par heure. Classique uniquement.
- 12 défis (4 Faciles, 4 Moyens, 4 Difficiles), tirage pondéré 50/35/15, anti-répétition sur les 3 derniers.
- Récompense : multiplicateur de l'or ramassé, **Or ×1,2 / ×1,5 / ×2** selon le palier.
- Les défis « endurance » (Sang-Froid, Sans Cristal, Sans-Faute) ne sont validés qu'au jalon de boss prévu.
- Suivi dans le HUD et détail dans le menu Pause.

---

## 7. Interface

- **Menu principal** : onglets Jouer (avec sélecteur de mode), Personnages, Compétences, Réputation, Paramètres ; décors 2D illustrés animés (vent, feuilles, lumière), curseur gantelet personnalisé.
- **HUD** construit en code (`HudStyler`) : barres vie/XP/boss, chrono, or, kills, défi, Dash, Clone, Ultime, Concentration, légende des touches adaptative.
- **Level-up** : 3 cartes parchemin animées (couleur par personnage, violet pour les fusions), pastilles de palier (jusqu'à 5), bandeau « FUSION DISPONIBLE ! », choix au clavier 1/2/3.
- **Pause** : grille d'armes avec infos, défi en cours, paramètres en jeu, abandon avec confirmation.
- **Victoire / Défaite** : même univers (parchemin doré / rouille), comptage animé de l'Or et des Éclats, défi réussi, records, arsenal de la partie, message contextuel et numéro de tentative pour la défaite, aperçu du prochain déblocage.
- **Paramètres** : affichage, graphismes, audio, jeu, interface, commandes (2 touches par action, préréglages AZERTY/QWERTY, manette), langue.
- **Écran de chargement** : illustration animée (Ken Burns), barre stylée, « Appuyer sur une touche », fondu vers le menu.

---

## 8. DA, musique et marketing

- Logo : texte ornemental doré + cristal bleu. Police des chiffres et titres : Bangers retouchée (cyrillique ajouté) ; polices Noto Sans pour le chinois, le japonais et le coréen.
- Modèles 3D générés avec Tripo3D, squelettes passés en Mixamo (`SkeletonSwapTool`).
- Musiques : menu, chargement, en jeu, boss (Suno + retouches de boucle). **Aucun SFX** pour l'instant (chantier repoussé à la fin, désormais prioritaire, voir `A_MODELISER_3D.md` §9).
- Trailer Steam (≈68 s) + version verticale (40 s) validés le 2026-10-14 : vrai gameplay, DA du jeu, musique CC0.
- Politique Steam : les captures d'écran de la page doivent montrer du vrai gameplay.

---

## 9. Ce qui reste à faire (priorisé)

**Avant la sortie (indispensable)**
1. **Bruitages** de tout le jeu + une musique par boss.
2. Reste de l'étape 3 de l'audit : nom de société ≠ DefaultCompany (décision de marque, en attente de l'utilisateur - déplace le dossier de sauvegarde, à faire **avant** la 1re version publique seulement), IL2CPP (risque de build, à vérifier en compilant), Steam Cloud. Le reste de l'étape 3 (boutons de debug, logs, sauvegarde atomique + version) est fait le 2026-10-19.
3. Remplacer les primitives visibles : projectiles, zones au sol, Bulbe cracheur, Nova/Ultime/Bouclier, et habiller l'arène (sol + décor).
4. Playtests d'équilibrage après les changements du 2026-10-16 (densité 160 ennemis, XP, bonus de dégâts) avec les 3 personnages, **et** des 3 nouveaux paliers de difficulté et du coffre de boss (2026-10-19, multiplicateurs/récompense posés comme premiers jets).
5. Intégration Steamworks (SDK, succès, Cloud, Steam Input).
6. Repasse visuelle "niveau triple A" / comparaison aux meilleurs jeux du genre par captures d'écran, demandée le 2026-10-19 mais pas encore menée (le temps en direct dans Unity a servi à vérifier que le nouveau code fonctionne réellement, pas à un audit esthétique comparatif).

**Niveau « jeu pro »**
- Manette complète (nouvel Input System, navigation des menus, icônes de boutons) et accessibilité (désactiver secousses et flashs).
- 21 skins.
- Effets visuels dédiés (télégraphes, impacts, morts par dissolution).
- Profilage des performances (cible 60 FPS stables, Steam Deck).
- Nettoyage du projet (prefabs en double dans `Resources/`, scripts et assets inutilisés).

**Idées de contenu (non commencées)**
- Plusieurs cartes / biomes (idée « 3 maps »).
- Sanglier (modèle existant, pas intégré).
- Objets à ramasser (coffres, soins, aimant), relance/bannissement de cartes au level-up.
- Écran de crédits.

---

## 10. Principaux bugs corrigés récemment

**2026-10-19** (diagnostic complet) : l'Ultime n'avait aucun retour visuel à l'écran · boss pouvant apparaître sur le joueur près d'un bord de carte · fuite de Material TMP à chaque ouverture de carte/menu · course sur le volume de musique si 2 morceaux s'enchaînaient vite · NullReference en lançant la scène Game directement dans l'éditeur · 2 boutons de debug restaient visibles et cliquables en version release (Réputation donnait 20 000 Éclats gratuits, Personnages déverrouillait Kael/Lyra) · `MapBoundaryUtils` existait en double (classe autonome + copie imbriquée dans WaveManager, 3 sites d'appel oubliés lors d'un premier passage, trouvés par une vraie erreur de compilation) · caractère "→" ajouté à des traductions mais absent de toutes les polices du jeu (Bangers et les 3 polices CJK réduites), trouvé en testant en direct dans Unity.

**2026-10-18** : affichage de la Réputation complètement buggé après avoir quitté et relancé le jeu (pastilles remplies et texte de valeur invisibles - couleur d'accent transparente, piège d'ordre `Awake`/`OnEnable` entre composants parent/enfant) · panneau de défi pas centré en Y quand la description est longue · fusion "Orbes Jumeaux" incohérente retirée et remplacée par "Sillage Boueux".

**2026-10-16** : PV du Cerf ignorés (50 000 forcés au lieu de 90 000) · soin de mort de boss à 100 % au lieu de 50 % · Ultime utilisable en pause et relançant le temps derrière le menu · ennemis tués deux fois (or/XP doublés) · kills comptés après la mort · anneau du Vortex de Mana en l'air · frappe du boss 3 hors de la carte · fuite mémoire du clone de Lyra · compteurs d'effets bloqués après plusieurs parties · armes qui ignoraient des ennemis quand beaucoup de gemmes étaient au sol · pluie de gemmes des boss qui plantait (XP perdue) · phases de difficulté du spawner écrasées · XP perdue à chaque kill · bonus de dégâts incohérents entre armes · Aura/Vortex/Couteaux/Lames qui ignoraient la Concentration.

Détail : `AUDIT_VERSION_FINALE.md` et `NOTES.md`.
