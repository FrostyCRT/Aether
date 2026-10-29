# AETHER STORM SURVIVOR — Documentation projet (V16)

> **Mise à jour : 2026-10-29** (depuis le 2026-10-23 : refonte des 6 fusions avec une vraie mécanique et leurs propres visuels en cristal, refonte de l'économie, typographie Bangers + Fira Sans, performances (gemmes d'XP en données), **manette complète**, animation au survol de tous les boutons, fonds de l'Accueil selon la difficulté, apparition des pages du menu, nouvel effet de level-up - détail dans `NOTES.md`). Cette synthèse décrit le jeu tel qu'il est aujourd'hui et ce qui reste à faire.
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
| Audit « version finale » | 🟡 Étapes 1 (bugs) et 2 (équilibrage) faites le 2026-10-16 ; étape 3 presque faite (restent nom de société, IL2CPP, Steam Cloud, reportés) ; étape 4 entamée (manette, performances) |
| Intégration Steam (SDK, succès, Cloud) | ⏳ Pas commencée |
| Manette | ✅ Complète (2026-10-28/29) : nouvel Input System, boutons modifiables, icônes Xbox/PlayStation/Switch, curseur rond dans les menus, visée au stick droit, vibrations |
| Accessibilité | 🟡 Pause automatique à la perte de focus ; pas encore d'option secousses d'écran / flashs |

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
- **Guerrier (Aether)** : Concentration, Cadence → Fragmentation, Dégâts Cristal → Surpuissance (dégâts ×2 pendant 10 s après l'Ultime - 5 s jusqu'au 2026-10-23, jugé trop court -, sur toutes les armes depuis le 2026-10-16).
- **Gardien (Kael)** : Vitalité, Récupération → Second Souffle, Armure → Bouclier de Mana.
- **Fantôme (Lyra)** : Impulsion Nova, Dash Amélioré → Maîtrise du Cristal (réduit de 1 le nombre de charges de Nova pour l'Ultime, 6 à 5), Nova Étendue → Clone Spectral.
- Coûts par palier : 100 / 300 / 700 ; nœuds uniques 500 à 1200.

---

## 2. Armes, améliorations et fusions

- **Armes** : tir de base, Boule de feu (Aether), Aura (Kael), Couteaux (Lyra), Orbitaux, Chaîne de foudre, Boue, Orbe rebondissant, Double tir.
- **Améliorations universelles** : Dégâts+ et Cadence+ (+20 % par prise, 5 prises), Soin (+15 % de PV max et soin complet, depuis le 2026-10-28). Depuis le 2026-10-16, elles sont **additives et identiques sur toutes les armes** (5 × 20 % = ×2) et s'appliquent aussi aux armes obtenues plus tard.
- **6 fusions** (deux armes au palier max → une arme fusionnée), **toutes refaites le 2026-10-27/28** avec une vraie mécanique nouvelle, leurs propres visuels en cristal et une puissance mesurée à ×2,2-2,9 les deux armes sources au max (validées par l'utilisateur le 2026-10-28) :
  - **Pluie de Météores** (Boule de feu + Boue, ex-Terre Calcinée) : jusqu'à 3 météorites par salve sur des ennemis différents, télégraphe, chute visible, déflagration, cratère.
  - **Tourbière Magnétique** (Boue + Foudre, ex-Marécage Maudit) : seul contrôle de foule du jeu, des tourbières électrisées aspirent les groupes d'ennemis puis déchargent.
  - **Couronne Prismatique** (Aura + Orbitaux, Kael, ex-Vortex de Mana) : cristaux reliés par des rayons qui brûlent et repoussent, rempart polygonal réglé avec A/E (« la meilleure fusion du jeu » pour l'utilisateur).
  - **Constellation Orageuse** (Orbitaux + Foudre, ex-Orbes Foudroyants) : les orbes ionisent les ennemis, la Foudre vise les ionisés, une Surcharge périodique les frappe tous.
  - **Valse des Lames** (Couteaux + Orbe rebondissant, Lyra, ex-Lames Ricochet) : lames de cristal qui rebondissent sur les bords de l'écran puis reviennent ; les lames rattrapées chargent une salve circulaire « Kaléidoscope ».
  - **Sillage Boueux** (Orbe rebondissant + Boue) : orbes qui rebondissent sur les bords de l'écran en semant des flaques ; l'orbe sera remplacé par un modèle Tripo3D de l'utilisateur.
  Les identifiants internes (noms de classe, id de fusion) sont restés les anciens : l'asset de fusion et les sauvegardes y font référence. Proposées en Classique et Sans fin uniquement.
- **Fragmentation** (nœud d'Aether, refait le 2026-10-27) : 1 tir de base sur 4 explose à coup sûr avec tous ses dégâts (rayon 4), explosion de cristal rubis (`ArcaneBurstVFX`).
- **Visée** : automatique par défaut ; en visée manuelle, les tirs partent vers la souris ou dans la direction du stick droit, avec un viseur à l'écran (2026-10-29).
- **Dash / Absorption / Nova / Ultime** : le Dash (invincible) absorbe un projectile → Nova (dégâts + ralentissement) + charge de Cristal. Cristal plein = 1 Ultime stocké (max 2, la 2e charge donne un Ultime renforcé). Dégâts de l'Ultime ×1 → ×4 sur 15 min.
- Bonus de dégâts dynamiques centralisés dans `PlayerBuffs.OutgoingDamageMultiplier` (Concentration × Surpuissance), lu par toutes les armes.

---

## 3. Ennemis, boss et difficulté

- **Ennemis** : Loup, Gobelin à sarbacane (tireur mobile), Golem (costaud), Corbeau, Tisseuse corrompue (araignée qui saute), Bulbe cracheur (tireur fixe, **encore un cube placeholder**). Animation par texture (VAT) pour tenir la foule.
- **Apparition** (`EnemySpawner`, seule référence depuis le 2026-10-16) : 6 phases, plafond d'ennemis simultanés 20 → 45 → 75 → 120 → 105 → **160** (dès 12 min), cadence 2 s → 0,6 s. Mode Sans fin après 15 min : jusqu'à 200 ennemis, cadence 0,3 s. Paliers de composition (types d'ennemis + PV ×1 à ×5) toutes les 2-4 minutes. Réglable dans l'Inspector de l'objet EnemySpawner (scène Game). **À tester en jeu** : la fin de partie est nettement plus dense qu'avant.
- **Boss** (un toutes les 5 min, le chrono s'arrête pendant le combat) :
  1. **Dragon de Mana** : 20 000 PV, charge télégraphiée, tir radial.
  2. **Cerf Ancestral** : 63 000 PV (90 000 jusqu'au 2026-10-23, baissés de 30% sur retour utilisateur "toujours trop de vie" - réduction du PV de BASE, donc appliquée identiquement aux 3 paliers de difficulté), saut, spirale de projectiles, rage sous 30 %.
  3. **Source Corrompue** (serpent ancré à une faille) : 180 000 PV, Pulse de cristaux, Frappe, Onde de corruption, Invocation de mini-boss, Implosion.
- Les boss 1 et 2 soignent le joueur de 50 % à leur mort. Les 3 boss utilisent pour l'instant **la même musique**.
- **Récompense de mort de boss** (changé le 2026-10-19) : un vrai boss ne donne plus de burst d'XP, il fait apparaître un **coffre** (`BossChest`) à ramasser qui accorde directement **2 tirages de carte complets** (3 choix à chaque fois), sans jamais donner d'XP - récompense fixe et lisible plutôt qu'une XP qui pouvait être gâchée ou déclencher une cascade illisible. Un mini-boss invoqué (Source Corrompue) garde le burst de gemmes classique.
- **XP** (hors mort de boss) : gemmes 10/20/50, reste reporté sur la mort suivante (aucune XP perdue), courbe `70 × niveau²` (relevée le 2026-10-21, pèse surtout sur les hauts niveaux). La récompense d'XP par kill suit la pleine croissance des PV des ennemis au fil de la partie (jusqu'à ×5 en fin de partie) - un amortissement à ×3 introduit le 2026-10-11 pour ralentir la montée en XP a été retiré le 2026-10-23 (il empilait mal avec la courbe ci-dessus, plus assez de grosses gemmes dorées en fin de partie). Rayon d'attraction débloqué au niveau 3.
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

- **Économie refondue le 2026-10-27** (une partie Apocalyptique rapportait ~40 000 or pour un arbre complet à 14 000) : l'or vient surtout des minutes de jeu (20/min), des boss et de la victoire (250 en Classique), très peu des ennemis (2 % de leur valeur). Objectifs de l'utilisateur : premier achat dès la 1re partie, une branche maxée en ~30-40 parties, les 3 branches en ~100, toute la Réputation en ~150.
- **Or** : finance les arbres de compétences (paliers 150 / 600 / 1 800, nœuds uniques 1 000 à 12 000 ; les 2 nœuds du bas sont libres, ceux du milieu demandent une victoire en Modéré, celui du haut une victoire en Violent avec le personnage de la branche) et les skins Classique.
- **Éclats** : financent la Réputation, les skins Prestige et le déblocage anticipé de Kael/Lyra. Gagnés aux boss (10 / 20 / 30), à la victoire (+40) et lors de la 1re victoire de chaque personnage dans chaque palier (300 / 600 / 1 000).
- **Réputation** (onglet du menu) : 3 bonus permanents, Dégâts (jusqu'à +60 %), Vitesse (jusqu'à +30 %), Régénération (jusqu'à 70 PV/s), 5 paliers chacun. Un bonus au maximum affiche un bouton « MAX » grisé, comme « Équipé » / « Sélectionné ».
- **Skins** (même onglet) : par personnage, une rangée Classique (Or) et une rangée Prestige (Éclats) de 4 emplacements. Seule la tenue d'origine existe ; les emplacements vides affichent « BIENTÔT ». Ajouter un skin = une ligne dans `Resources/SkinCatalog.asset` (aucun code), mais il faut le modèle 3D, le prefab et le portrait (**21 skins prévus**, voir `A_MODELISER_3D.md`).

---

## 6. Défis horaires

- Un défi tiré **par heure réelle (UTC)**, identique pour toutes les parties de l'heure ; récompense accordée une seule fois par heure. Classique uniquement.
- 12 défis (4 Faciles, 3 Moyens, 5 Difficiles - "Sans Cristal" reclassé Moyen → Difficile le 2026-10-21, jugé trop punitif pour son palier), tirage pondéré 50/35/15, anti-répétition sur les 3 derniers.
- Les défis liés au TEMPS (ex. "Ascension Rapide" : niveau 20 avant la 10e minute) sont réévalués en continu (`ChallengeManager.Update`, 2026-10-23) et pas seulement sur un évènement de jeu - sinon un défi purement temporel ne passait jamais en Échoué.
- Récompense : multiplicateur de l'or ramassé, **Or ×1,2 / ×1,5 / ×2** selon le palier.
- Les défis « endurance » (Sang-Froid, Sans Cristal, Sans-Faute) ne sont validés qu'au jalon de boss prévu.
- Suivi dans le HUD et détail dans le menu Pause.

---

## 7. Interface

- **Menu principal** : onglets Jouer (avec sélecteur de mode et carrousel de difficulté), Personnages, Compétences, Réputation, Paramètres ; décors 2D illustrés animés (vent, feuilles, lumière), curseur gantelet personnalisé.
  - Fond de l'Accueil selon le héros (illustrations V2) **et** la difficulté : Modéré = jour, Violent = crépuscule, Apocalyptique = nuit, fondu par-dessus l'ancien fond (2026-10-29).
  - **Apparition des pages** (2026-10-29) : en ouvrant un onglet, on voit d'abord le décor seul (ce sont de vraies illustrations du lore), puis le contenu apparaît en fondu 0,5 s plus tard. Chaque page a son propre minuteur, qui démarre quand on la quitte : l'animation ne se rejoue que si l'on revient au moins 1 minute plus tard (ou à la 1re ouverture) ; rester sur une page ne la relance jamais.
  - Tous les boutons du jeu grossissent légèrement au survol (`ButtonHoverFX`) ; Alt+F4 ou la croix de la fenêtre demandent confirmation (`QuitConfirm`).
- **HUD** construit en code (`HudStyler`) : barres vie/XP/boss, chrono, or, kills, défi, Dash, Clone, Ultime, Concentration, légende des touches adaptative.
- **Level-up** : temps fort de 0,3 s en jeu avant l'ouverture des cartes, une ascension de cristal doré autour du héros (fragments qui montent en spirale, étoile au-dessus de lui ; remplace l'anneau doré au sol le 2026-10-29). Puis 3 cartes parchemin animées (couleur par personnage, violet pour les fusions, léger grossissement au survol), pastilles de palier (jusqu'à 5), bandeau « FUSION DISPONIBLE ! », choix au clavier 1/2/3 ou au curseur de la manette.
- **Pause** : grille d'armes avec infos, défi en cours, paramètres en jeu, abandon avec confirmation.
- **Victoire / Défaite** : même univers (parchemin doré / rouille), comptage animé de l'Or et des Éclats, défi réussi, records, arsenal de la partie, message contextuel et numéro de tentative pour la défaite, aperçu du prochain déblocage.
- **Paramètres** : affichage, graphismes, audio, jeu (visée automatique ou manuelle, pause automatique), interface, commandes (2 touches clavier/souris + 1 bouton de manette par action, préréglages AZERTY/QWERTY, vibrations), langue.
- **Manette** (2026-10-28/29) : curseur rond au stick dans tous les menus (souris virtuelle : survol, clic, glisser), mode défilement dans les zones déroulantes (A pour entrer, B pour sortir), LB/RB = onglets, LT/RT = catégories des Paramètres, B = retour. Légende du HUD et icônes au style de la manette branchée. Le jeu bascule tout seul entre souris et manette selon le dernier appareil touché.
- Durées affichées partout au format « 03 : 24 » (espace fine autour des deux-points, chrono, records, écrans de fin).
- **Écran de chargement** : illustration animée (Ken Burns), barre stylée, « Appuyer sur une touche », fondu vers le menu.

---

## 8. DA, musique et marketing

- Logo : texte ornemental doré + cristal bleu. **Deux polices** (2026-10-25) : Bangers retouchée (cyrillique ajouté) pour les titres, noms, boutons et chiffres ; **Fira Sans SemiBold** pour toutes les phrases (descriptions, infobulles, sous-titres). Polices Noto Sans pour le chinois, le japonais et le coréen. Les lignes des textes centrés sont équilibrées automatiquement (`TextBalancer`).
- **Grammaire des effets** : les anneaux lisses ou gradués et les disques pleins sont réservés aux télégraphes ennemis (« danger, sors de là ») ; les effets du joueur utilisent des formes de cristal facettées (étoile, couronne brisée, fragments, éclats).
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
- Accessibilité : désactiver les secousses d'écran et réduire les flashs. (La manette est faite.)
- 21 skins.
- Effets visuels dédiés restants (impacts, morts par dissolution) : les fusions, la Fragmentation et le level-up ont déjà les leurs.
- Profilage des performances (cible 60 FPS stables, Steam Deck).
- Nettoyage du projet (prefabs en double dans `Resources/`, scripts et assets inutilisés).

**Idées de contenu (non commencées)**
- Plusieurs cartes / biomes (idée « 3 maps »).
- Sanglier (modèle existant, pas intégré).
- Objets à ramasser (coffres, soins, aimant), relance/bannissement de cartes au level-up.
- Écran de crédits.

---

## 10. Principaux bugs corrigés récemment

**2026-10-24 → 2026-10-29** : gels de 250-360 ms toutes les ~3 min (ramasse-miettes incrémental désactivé) et chute d'images/s avec beaucoup de gemmes au sol (gemmes passées en données) · hitbox des boss sans rapport avec leur modèle (impacts dans le vide sur le Boss 3) · chaîne de foudre qui pouvait viser un ennemi mort et recyclé par le pool · nombres de dégâts à moitié cachés sous les grands ennemis (gobelin, golem) · cartes de Réputation : libellés invisibles après un redémarrage, « Éclats insuffisants » mal placé et trop sombre, bouton qui sortait de la carte au survol · bord de la barre d'XP qui clignotait et tremblait au ramassage · vibrations de manette bloquées après un changement de fenêtre.

**2026-10-23** (grosse liste de retours utilisateur traitée en un lot, capture à l'appui) : gemmes d'XP qui flottaient en l'air pour certains ennemis (atterrissage calé sur la racine de l'ennemi mort au lieu d'être aplati au sol) · défi "Niveau 20 avant la 10e minute" qui ne pouvait jamais passer en Échoué (aucune réévaluation en dehors d'un évènement de jeu pour un défi purement temporel) · PV du boss 2 (Cerf Ancestral) baissés de 30% sur les 3 paliers de difficulté (90 000 → 63 000) · amortissement de l'XP par kill (plafonné à ×3 en fin de partie) retiré, pleine croissance restaurée (jusqu'à ×5) · Surpuissance 5 s → 10 s · fusion "Terre Calcinée" entièrement refondue : d'un simple reskin orange de la Boue à un vrai bombardement de météorites (télégraphe + chute visible + déflagration + cratère résiduel), avec un ciblage qui vise vraiment les ennemis les plus proches (et non plus une zone aléatoire à 6 unités autour du joueur) et jusqu'à 3 météores simultanés sur des cibles différentes contre une horde (1 seul contre un boss, sans rien gaspiller).

**2026-10-19** (diagnostic complet) : l'Ultime n'avait aucun retour visuel à l'écran · boss pouvant apparaître sur le joueur près d'un bord de carte · fuite de Material TMP à chaque ouverture de carte/menu · course sur le volume de musique si 2 morceaux s'enchaînaient vite · NullReference en lançant la scène Game directement dans l'éditeur · 2 boutons de debug restaient visibles et cliquables en version release (Réputation donnait 20 000 Éclats gratuits, Personnages déverrouillait Kael/Lyra) · `MapBoundaryUtils` existait en double (classe autonome + copie imbriquée dans WaveManager, 3 sites d'appel oubliés lors d'un premier passage, trouvés par une vraie erreur de compilation) · caractère "→" ajouté à des traductions mais absent de toutes les polices du jeu (Bangers et les 3 polices CJK réduites), trouvé en testant en direct dans Unity.

**2026-10-18** : affichage de la Réputation complètement buggé après avoir quitté et relancé le jeu (pastilles remplies et texte de valeur invisibles - couleur d'accent transparente, piège d'ordre `Awake`/`OnEnable` entre composants parent/enfant) · panneau de défi pas centré en Y quand la description est longue · fusion "Orbes Jumeaux" incohérente retirée et remplacée par "Sillage Boueux".

**2026-10-16** : PV du Cerf ignorés (50 000 forcés au lieu de 90 000) · soin de mort de boss à 100 % au lieu de 50 % · Ultime utilisable en pause et relançant le temps derrière le menu · ennemis tués deux fois (or/XP doublés) · kills comptés après la mort · anneau du Vortex de Mana en l'air · frappe du boss 3 hors de la carte · fuite mémoire du clone de Lyra · compteurs d'effets bloqués après plusieurs parties · armes qui ignoraient des ennemis quand beaucoup de gemmes étaient au sol · pluie de gemmes des boss qui plantait (XP perdue) · phases de difficulté du spawner écrasées · XP perdue à chaque kill · bonus de dégâts incohérents entre armes · Aura/Vortex/Couteaux/Lames qui ignoraient la Concentration.

Détail : `AUDIT_VERSION_FINALE.md` et `NOTES.md`.
