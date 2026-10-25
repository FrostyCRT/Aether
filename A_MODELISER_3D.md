# Aether Storm Survivor : ce qu'il reste à modéliser en 3D (et à habiller)

Audit du 2026-10-15, mis à jour le 2026-10-16 (Vortex de Mana ramené au sol, rayon d'apparition des ennemis corrigé), le 2026-10-18 (fusion "Orbes Jumeaux" retirée, remplacée par "Sillage Boueux" — voir §4.1 et §5), le 2026-10-19 (§7.1/§7.2 : diagnostic du sol + liste de nouveaux modèles 3D proposés pour Tripo3D, en remplacement des arbres/rochers de bordure actuels que l'utilisateur juge hors DA), puis le 2026-10-20 (§7.2 confirmée comme LA liste active après l'abandon d'une tentative de props de sol procéduraux, jugés trop pauvres visuellement ; §13 ajoutée : diorama du nouveau carrousel de difficulté de la page Accueil du menu ; §8.1 ajoutée : le coffre de boss, système déjà fonctionnel en jeu, oublié de cette liste jusqu'ici). Rien de cette liste n'est encore produit. J'ai parcouru tout le jeu (prefabs, scènes, `Resources/`, scripts qui créent des objets en code) et lancé des parties pour voir ce qui s'affiche vraiment à l'écran.
Le principe : **tout ce que le joueur voit en partie et qui est encore une primitive Unity (cube, sphère, cylindre, capsule) avec un simple matériau de couleur** doit être remplacé par un vrai modèle, un vrai effet visuel ou une vraie texture.

Légende des priorités :
- 🔴 **P1** : visible en permanence ou dans chaque partie. Ça fait « prototype » au premier coup d'œil.
- 🟠 **P2** : visible souvent ou à des moments forts (boss, fusions).
- 🟡 **P3** : finition, contenu en plus, rare à l'écran.

Rappel de la DA (direction artistique) : les modèles actuels viennent de Tripo3D (style fantasy stylisé, textures peintes). Pour garder un ensemble cohérent, tout nouveau modèle doit rester dans ce style : formes lisibles vues de dessus, silhouettes fortes, couleurs d'identité par personnage (Aether = ambre/feu, Kael = vert, Lyra = bleu-violet).

---

## 0. Résumé chiffré

| Catégorie | Nombre d'éléments | Priorité dominante |
|---|---|---|
| Skins joueurs (modèle + portrait) | **21 skins** (7 par personnage) | 🟠 P2 (onglet Réputation affiche « BIENTÔT ») |
| Ennemis encore en placeholder | 1 (Bulbe cracheur) + 1 modèle existant non intégré (Sanglier) | 🔴 P1 |
| Projectiles du joueur en sphères | 5 (tir de base, couteaux, boule de feu, orbitaux, orbe rebondissant) | 🔴 P1 |
| Projectiles ennemis / boss en sphères | 1 prefab partagé par 6 sources + cristaux du boss 3 | 🔴 P1 |
| Zones au sol en cylindres | Boue + 3 fusions | 🔴 P1 |
| Effets en cylindres/capsules | Nova, Ultime, Bouclier de Mana, prefabs du boss 3 | 🟠 P2 |
| Environnement de l'arène | Sol plat unique, aucun décor dans l'aire de jeu | 🔴 P1 |
| Effets procéduraux « génériques » à remplacer par de vrais VFX | ~10 | 🟡 P3 |
| Diorama du carrousel de difficulté (page Accueil, nouveau 2026-10-20) | 4 modèles (terrain + arbre + rocher + loup, style "cartoon" distinct du reste du jeu) | 🟠 P2 (voir §13) |
| Coffre de boss (ajouté 2026-10-20) | Système déjà fonctionnel, 1 modèle manquant (réutilise le mesh des gemmes d'XP aujourd'hui) | 🟠 P2 (voir §8.1) |
| **Sons** (hors 3D mais bloquant pour un jeu pro) | **le jeu n'a aucun bruitage** | 🔴 P1 (voir §9) |

**Total de modèles 3D distincts clairement spécifiés dans ce document (hors options/variantes non chiffrées — voir §0.1) : 53.**

### 0.1 Détail du décompte (53 modèles)

| Section | Modèles comptés | Sous-total |
|---|---|---|
| §1 Skins | 21 skins × 1 modèle de personnage chacun (le bâton n'est compté que s'il change, variable selon le thème retenu — non inclus dans ce total) | 21 |
| §2.1 Bulbe cracheur | 1 | 1 |
| §3 Boss | Cristaux orbitaux du boss 3, taillés (le reste du §3 est de la retexture/VFX, pas un nouveau modèle) | 1 |
| §4.1 Projectiles joueur | Tir de base, couteaux/lame, boule de feu, éclat de mana (orbitaux), orbe de cristal (orbe rebondissant) | 5 |
| §4.2 Projectiles ennemis | Dard de sarbacane, crachat acide, projectile de mana (Dragon), projectile du Cerf, éclat corrompu (boss 3) | 5 |
| §7.2 Arbres de bordure | Chêne noueux, arbre pétrifié à cristaux, bouleau doré | 3 |
| §7.2 Rochers de bordure | Rocher moussu fendu, menhir runique, amas rocheux à cristaux | 3 |
| §7.2 Props d'arène | Touffe d'herbe, bouquet de fleurs, éclat de mana affleurant, caillou, souche, touffe de champignons, patch de mousse | 7 |
| §7.2 Éléments de lore | Colonne brisée, autel fissuré (la "zone corrompue" est un effet de sol, pas un modèle) | 2 |
| §13 Diorama du carrousel | Socle de terrain, arbre miniature, rocher miniature, loup miniature | 4 |
| §8.1 Coffre de boss | Coffre au trésor | 1 |
| Sanglier (§2.2) | **Non compté** : déjà modélisé, juste à intégrer (rig/anim/script) | 0 |
| **Total** | | **53** (hors armes de skin variables, hors "nouveau modèle" alternatif du boss 3, hors version 3D optionnelle des gemmes XP/Bouclier de Mana) |

---

## 1. Skins des personnages (21 au total) 🟠 P2

### Ce qui existe
- `Assets/Resources/SkinCatalog.asset` ne contient que les **3 tenues d'origine** (`aether_default`, `kael_default`, `lyra_default`).
- L'onglet **Réputation** affiche par personnage une rangée **Classique (payée en Or)** de 4 emplacements et une rangée **Prestige (payée en Éclats)** de 4 emplacements. Tout emplacement vide affiche une carte « BIENTÔT ».
- Donc aujourd'hui : 1 skin réel et 7 cartes « BIENTÔT » par personnage.

### Ce qu'il faut produire
**3 skins Classique + 4 skins Prestige par personnage = 21 skins.**

Pour **chaque** skin il faut :
1. **Le modèle 3D du personnage** (même squelette que le modèle d'origine pour réutiliser les animations : idle, course, dash, tir, mort, victoire). Le rig doit être compatible avec le système d'animation actuel. `Assets/Editor/SkeletonSwapTool.cs` existe pour aider à la conversion.
2. **Le bâton/l'arme** du skin s'il change (les 3 bâtons d'origine existent).
3. **Un portrait 2D « buste »** pour la carte et l'aperçu de la page Réputation (champ `preview` de `SkinEntry`, cadré tête + épaules, même style d'illustration que les portraits actuels). Sans portrait, le jeu réutilise celui de la tenue d'origine, et on ne voit pas la différence entre les skins.
4. **Un prefab joueur** (champ `playerPrefab` de `SkinEntry`) qui reprend les composants du prefab d'origine (PlayerController, HealthSystem, armes, etc.) avec le nouveau modèle.
5. Une ligne dans `SkinCatalog.asset` : `id` stable (ne plus jamais le changer, il est sauvegardé), `characterIndex`, `displayName`, `description`, `tier`, `cost`. **Aucun code à écrire.** Penser à ajouter `displayName` et `description` dans les 10 tables de traduction (procédure « ajouter un texte traduit »).

### Proposition de thèmes (à valider ou remplacer)
| Personnage | Classique 1 | Classique 2 | Classique 3 | Prestige 1 | Prestige 2 | Prestige 3 | Prestige 4 |
|---|---|---|---|---|---|---|---|
| **Aether** (feu/ambre) | Apprenti des cendres | Mage des dunes | Garde-forge | Phénix renaissant | Seigneur de l'Éclipse | Avatar de magma | Aether Corrompu (Source) |
| **Kael** (vert/nature) | Druide des sentiers | Gardien de pierre | Chasseur des marais | Sylve ancestrale | Paladin de jade | Esprit-cerf (clin d'œil boss 2) | Kael Corrompu |
| **Lyra** (bleu-violet/ombre) | Éclaireuse | Danseuse des lames | Assassine de nuit | Spectre lunaire | Reine des corbeaux | Lame astrale | Lyra Corrompue |

Conseil : les skins **Prestige** doivent se voir **de loin, vus de dessus** (silhouette, couleur, effet lumineux, traînée de particules), pas seulement par des détails de texture. C'est ce qui justifie leur prix en Éclats.

### Points techniques liés aux skins
- Le **clone fantôme de Lyra** copie le maillage du joueur (`BakeMesh`) : il marchera tout seul avec un skin s'il garde un `SkinnedMeshRenderer`.
- La page **Personnages** affiche toujours le portrait d'origine, jamais le skin équipé. C'est à décider (voir `AUDIT_VERSION_FINALE.md`).
- Les RenderTextures `RT_Aether/Kael/Lyra` (inutilisées aujourd'hui) pourraient servir à afficher un **aperçu 3D qui tourne** dans la page Réputation au lieu d'un portrait 2D. C'est plus « pro », mais aussi plus long à faire.

---

## 2. Ennemis 🔴 P1

### 2.1 Bulbe cracheur : à modéliser entièrement 🔴
- Prefab `Bulb.prefab`, enfant `VisualCube_PLACEHOLDER` = **un cube vert** (`MAT_EnemyBulb_Placeholder`).
- C'est le seul ennemi encore en cube. Il apparaît à partir de 12 minutes, dans chaque partie qui va loin.
- À produire :
  - Modèle : plante/bulbe corrompu enraciné (immobile, il tire à distance). Une silhouette ronde, une « bouche » ou un pistil qui crache.
  - Animations : **sortie de terre** (important : il apparaît à 10 unités du joueur, donc **dans l'écran** ; aujourd'hui il « pop » sans transition), idle (respiration/pulsation), **tir** (gonflement puis crachat), **mort** (flétrissement), réaction aux coups.
  - Texture + émissif (une lueur qui grossit juste avant le tir aide le joueur à lire l'attaque).
  - Penser à la VAT (Vertex Animation Texture) comme pour le Golem si beaucoup d'instances (`Assets/Editor/VATBaker.cs`).

### 2.2 Sanglier : modèle existant mais non intégré 🟡
- `Sanglier.fbx` est modélisé (dans `3D Models` et en double dans `TripoModels/`) mais **il n'existe aucun prefab, aucun script, aucune entrée de spawn**.
- Soit on l'intègre (ennemi « chargeur » rapide en ligne droite, qui varierait le gameplay), soit on le supprime du projet.
- Pour l'intégrer : rig + animations (course, charge, coup, mort), prefab, script (on peut repartir d'`EnemyBase` + la logique de charge du Dragon), poids dans `EnemySpawner`.

### 2.3 Ennemis existants : finitions 🟡
| Ennemi | Modèle | À vérifier / améliorer |
|---|---|---|
| Loup | `exported-model (1).glb` | Renommer le fichier (nom générique de l'export). Animation d'attaque visible ? |
| Corbeau | OK | Animation de vol + plongeon |
| Tisseuse (araignée) | OK | Saut lisible (télégraphe au sol déjà présent) |
| Gobelin + sarbacane | OK | Le **projectile** est une sphère orange (voir §4) |
| Golem | OK (VAT) | Vérifier le flash blanc des dégâts sur les VAT (voir audit) |
| Tous | — | **Effet visuel de brûlure** absent sur les ennemis 3D : la teinte ne s'applique qu'aux `SpriteRenderer`. Il faut une petite flamme/particule attachée, ou une teinte orange qui pulse dans le shader. |
| Bulbe | — | **Effet d'apparition** : les ennemis normaux apparaissent hors champ (rayon 22 réglé dans la scène, corrigé le 2026-10-16 : ce n'était pas 15). Seul le Bulbe apparaît volontairement dans l'écran (10 u) : il lui faut une sortie de terre (voir §2.1). |

### 2.4 Nettoyage des prefabs ennemis
Les racines des prefabs ennemis, boss et joueurs gardent un `MeshRenderer` CUBE/CAPSULE **désactivé** (reste du prototypage). Invisible, mais à supprimer (propreté, et ça évite qu'un jour quelqu'un le réactive par erreur).

---

## 3. Boss 🟠 P2

| Boss | Modèle actuel | Ce qu'il manque |
|---|---|---|
| **Boss 1 : Dragon de Mana** | Drake, 4 sous-maillages | Projectiles radiaux = sphères orange (voir §4). Une « jauge » visuelle de charge avant la ruée serait un plus. |
| **Boss 2 : Cerf Ancestral** | `Deer.glb`, textures **4K** | Spirale de projectiles = sphères orange. Textures 4K inutiles vu de haut, passer en 2K (gain mémoire). Animations de saut/atterrissage à vérifier. |
| **Boss 3 : Source Corrompue** | `White Snake.glb`, 4K, 28,8k triangles | Le serpent est **blanc/argenté**, peu « corrompu » pour le boss final. Retexture (veines violettes émissives, cristaux incrustés) ou nouveau modèle. **Cristaux orbitaux** = sphères bleues (`OrbitalCrystal`, `Mat_CristalCorrompu`) → vrais cristaux taillés. **Pulse** = sphère orange. |
| Prefabs du boss 3 | `RiftPortal` (cylindre), `PFB_ImplosionWarning` (cylindre), `CorruptionWaveRing` (cylindre) | En partie remplacés par des télégraphes procéduraux. Ce qui reste visible doit devenir un vrai VFX : faille/portail, anneau d'onde de corruption, zone d'implosion. |
| Tous | — | **Une musique par boss** : les 3 boss utilisent le même morceau (guid 737e7c…). Ce n'est pas de la 3D, mais c'est un manque pro (voir §9). |

---

## 4. Projectiles 🔴 P1

C'est le placeholder **le plus visible du jeu** : des sphères unies partout, tout le temps.

### 4.1 Projectiles du joueur
| Projectile | Prefab | Aujourd'hui | À produire |
|---|---|---|---|
| **Tir de base** | `Projectile.prefab` | Sphère **grise** avec le matériau Lit par défaut (#808080) : le pire cas, on dirait un objet oublié | Projectile de mana/eau (petit cœur lumineux + traînée). Peut changer de couleur selon le personnage. |
| **Couteaux de Lyra** | `ProjectileShuriken` | Sphères violettes (`MAT_EnemyShooter`, le matériau d'un ennemi !) | Modèle de **lame/couteau de lancer** qui tourne sur lui-même, avec une traînée bleu-violet. Également utilisé par la fusion **Lames Ricochet**. |
| **Boule de feu (Aether)** | `ProjectileFireball` | Sphère orange (`MAT_RougeOrange`) | Boule de feu : cœur émissif + particules de flammes + traînée de fumée. L'explosion à l'impact doit être un vrai VFX (aujourd'hui anneau générique). |
| **Orbitaux** | `Resources/OrbitalProjectile` | Petites sphères cyan (`MAT_Orbital`) | **Éclats de mana / cristaux** qui tournent. Également utilisés par les fusions **Vortex de Mana** et **Orbes Foudroyants**. |
| **Orbe rebondissant** | `Resources/BouncingOrbProjectile` | Grosse sphère jaune unie (`MAT_PulseAOE`) | **Orbe de cristal** avec halo intérieur. La fusion **Sillage Boueux** (remplace "Orbes Jumeaux", retirée le 2026-10-18) réutilise le même orbe tel quel, sans lien visuel entre orbes ; voir plutôt la flaque de boue qu'il laisse derrière lui, ligne "Fusion Sillage Boueux" ci-dessous (§5). |
| **Éclair en chaîne** | `LightningBoltVFX` (LineRenderer procédural) | Fonctionnel | 🟡 Texture d'éclair animée + flash d'impact. |
| **Météorite** (fusion **Pluie de Météores**, Boule de feu + Boue — ajoutée le 2026-10-24 à la demande) | `MeteorFallVFX` (construit en code, pas de prefab) | Sprite **billboard** tourné vers la caméra qui tombe du ciel pendant le signal au sol | **Vrai modèle 3D de météorite** : roche irrégulière à fissures incandescentes (émissif orange), traînée de feu et de fumée derrière elle. L'impact garde `FireExplosionVFX`, déjà validé. |

### 4.2 Projectiles ennemis
| Source | Aujourd'hui | À produire |
|---|---|---|
| `EnemyProjectile` partagé par : Gobelin, Bulbe, Dragon (tir radial), Cerf (spirale), Source Corrompue (pulse) | **La même sphère orange** (`MAT_EnemyProjectile`) pour tout le monde | Au minimum **4 variantes** : **dard de sarbacane** (gobelin), **crachat acide** (bulbe), **projectile de mana du Dragon**, **projectile du Cerf** (feuille/ronce/lumière), **éclat corrompu** (boss 3). Chaque projectile doit garder une couleur « danger » cohérente et lisible (le joueur doit les esquiver ou les absorber au dash). |
| Cristaux orbitaux du boss 3 (`OrbitalCrystal`) | Sphères bleues | Cristaux corrompus taillés, émissifs. |

Note technique : chaque nouveau projectile doit garder le collider et le script actuels. On ne remplace que l'enfant visuel, et on garde l'échelle de la hitbox.

---

## 5. Zones au sol 🔴 P1

| Zone | Prefab | Aujourd'hui | À produire |
|---|---|---|---|
| **Flaques de boue** | `Resources/MudPuddleZone` | Cylindres plats **marron** (`Maron.mat`) | **Décal** ou maillage plat avec texture de boue animée (bulles, reflets), bord irrégulier. |
| Fusion **Pluie de Météores** (ex-« Terre Calcinée », refondue le 2026-10-23, renommée le 2026-10-24) | même prefab, pour le cratère résiduel seulement | Cylindres marron boueux (même teinte que la Boue normale, avant c'était orange) | Sol brûlé : braises, fissures incandescentes, petites flammes. **Le visuel principal de cette fusion n'est plus ce cratère** mais le météore qui tombe du ciel (`MeteorFallVFX`) - entièrement procédural (sprites générés en code, aucun modèle/texture requis), donc rien à produire pour cette partie. |
| Fusion **Marécage Maudit** | même prefab | Cylindres verts | Marécage toxique : eau verte, bulles, brume. |
| Fusion **Sillage Boueux** (nouvelle, 2026-10-18) | même prefab | Cylindres bleu-gris délavé (teinte « boue limoneuse ») | Même traitement que les 2 fusions ci-dessus : sillage continu de petites flaques déposées par les orbes en mouvement plutôt qu'un anneau fixe. |
| **Aura de Kael** | LineRenderer + disque | Fonctionnel mais générique | 🟡 Cercle runique au sol qui tourne, particules vertes. |
| **Vortex de Mana** (fusion) | LineRenderer | Anneau simple, ramené au sol le 2026-10-16 | Tourbillon au sol animé. |

---

## 6. Effets des capacités 🟠 P2

| Effet | Aujourd'hui | À produire |
|---|---|---|
| **Nova de cristal** (`NovaVFX`) | Cylindre bleu plat (`MAT_Crystal`) qui grandit | Onde de choc : anneau lumineux + distorsion + éclats. |
| **Ultime** (`_ultVFXPrefab`) | Cylindre bleu plat qui grandit + `ShowUltEffect` qui ne fait qu'un `Debug.Log` | L'Ultime est **le moment le plus fort du jeu** et il n'a quasiment aucun visuel. À produire : effet plein écran (flash, vignette colorée, ralenti déjà présent), colonne de lumière, onde géante, version différente par personnage si possible. |
| **Bouclier de Mana** (Kael, `ManaShield.BuildBubble`) | **Capsule primitive** verte translucide | Bulle d'énergie avec effet de Fresnel (bords lumineux), hexagones, flash quand elle absorbe un projectile. |
| **Clone fantôme** (Lyra) | Copie du maillage (OK) ; repli = capsule si aucun modèle | Ajouter un shader « spectral » (transparence + bord lumineux) s'il n'existe pas encore. Le repli en capsule ne devrait jamais être vu. |
| **Dash** | ? | Traînée/afterimage visible (aujourd'hui dépend du personnage). |
| **Montée de niveau** | Panneau UI | Petit effet au sol autour du joueur au moment du level-up. |
| `PulseVisual` (cylindre) | **Inutilisé** | À supprimer. |

---

## 7. Environnement / arène 🔴 P1

C'est **le plus gros manque** pour l'impression de « jeu fini ».

### Ce qui existe
- Le sol = **un seul Plane** avec une texture d'herbe (`Tex_Ground_GrassV7`), parfaitement plat.
- La bordure = arbres et rochers (`Tree_1`, `Tree_2`, `Rock 1`, `Rock 2`, ~400 instances placées par `BoundaryDecorationPlacer`).
- **Aucun élément de décor dans toute l'aire de jeu.** Le joueur passe 15 minutes sur une pelouse uniforme.
- Les **coins** de la carte ne sont probablement pas remplis (`BoundaryDecorationPlacer` place le long des bords, pas dans les angles) : à vérifier en allant dans chaque coin.

### À produire
1. **Sol** 🔴 : plusieurs textures mélangées (herbe, terre, pierre, mousse, chemins) avec du *splatting* ou des **décals**. Variation de couleur à grande échelle pour casser la répétition. Normal map.
2. **Props sans collision** (détails visuels) 🔴 : touffes d'herbe, fleurs, cailloux, champignons, petites racines, feuilles mortes, ossements, restes de camp. Peu de triangles, beaucoup d'instances (GPU instancing).
3. **Éléments de « lore »** 🟠 : ruines (colonnes brisées, arches), cristaux de mana qui sortent du sol (rappel du thème « Aether Storm »), autels, statues brisées, zones corrompues violettes près du centre (lien avec la Source Corrompue).
4. **Obstacles avec collision** 🟡 (décision de game design) : quelques rochers ou ruines pour créer des couloirs. Ça change beaucoup le gameplay d'un survivor, à tester avant de généraliser.
5. **Ambiance** 🟠 : particules de poussière ou de mana qui flottent, lucioles, brume au sol, ombres de nuages qui passent, éclairs lointains (le jeu s'appelle « Aether **Storm** »), lumière qui change avec le temps de partie (le ciel s'assombrit vers le boss 3).
6. **Coins de la carte** : compléter la bordure.
7. **Plusieurs biomes / cartes** 🟡 : l'idée « 3 maps » n'est pas commencée. Chaque biome = jeu de textures + props + ambiance + palette d'ennemis éventuellement.
8. **Arène de boss** 🟡 : un changement visuel quand le boss arrive (cercle de runes au sol, brume, lumière).

### 7.1 Sol — diagnostic et recommandation (2026-10-19)
**Constat vérifié** (`Mat_Ground_Grass.mat`, shader URP Lit) : `Tex_Ground_GrassV7.png` est une bonne texture peinte à la main (touffes d'herbe, fleurs blanches/jaunes, trèfles — style cohérent avec les illustrations 2D du menu), mais elle est **tuilée telle quelle à l'échelle 20×20 sur tout le sol**, sans `_BumpMap` (aucun normal map → complètement plat sous la caméra inclinée), sans 2e texture ni variation de teinte à grande échelle → la répétition du motif devient visible, et combiné à l'absence totale de prop DANS la zone jouable (§7 point 2, déjà le plus gros manque identifié), le sol se lit comme un papier peint plutôt qu'un terrain.

**Recommandation : ne pas refaire la texture, l'habiller.** Le dessin de base est bon et déjà dans la DA — le proposer ainsi :
1. **Normal map** généré à partir de `Tex_Ground_GrassV7` (ou repeint) pour casser le côté "plat" sous l'éclairage désormais teinté par palier (voir Bloom/lumière ajoutés le 2026-10-19).
2. **Calque de variation macro** : une 2e texture à faible opacité (taches de terre/mousse/chemin clair), posée en blend par-dessus au shader, pour casser le motif répétitif sans redessiner la texture de base.
3. **Props d'arène** (liste ci-dessous, §7.2) : c'est le levier qui compte le plus pour "sol vivant" — une texture seule ne peut pas remplacer des éléments en vrai relief qui projettent une ombre.

Compromis à avoir en tête : plus de travail qu'un simple remplacement de texture (il faut un passage shader + de nouveaux petits meshs + un script de dispersion/instancing GPU pour rester performant avec beaucoup d'ennemis à l'écran), mais c'est la seule version qui corrige vraiment le "ça fait vide" — un sol plus joli mais toujours sans rien dessus resterait vide.

### 7.2 Nouveaux modèles 3D proposés pour Tripo3D (2026-10-19)
Remplace les arbres/rochers actuels (`Tree_1`, `Tree_2`, `Rock 1`, `Rock 2` dans `TripoModels/`/`Game/3D Models/`, supprimés par l'utilisateur car hors DA) et ajoute des props d'arène qui manquaient totalement (§7 point 2). Même contrainte de style que le reste de la liste (§ Rappel de la DA en tête de document) : silhouettes lisibles vues de dessus, cohérent avec le thème "Fantasy Médiéval avec magie et mana" et les teintes par palier de difficulté déjà en place (ambre Modéré / ambre-orage Violent / violet arcanique Apocalyptique — voir `DifficultyTiers.LightColor`).

**Arbres de bordure** (remplacent `Tree_1`/`Tree_2`) 🔴 :
| Nom | Description (base de prompt Tripo3D) |
|---|---|
| Chêne noueux ancien | Tronc noueux et épais, mousse légèrement phosphorescente dans les creux de l'écorce, feuillage dense vert sombre, silhouette large et lisible vue de dessus. |
| Arbre pétrifié à cristaux | Branches mortes, grises, dénudées, avec de petits cristaux de mana (violet/cyan) qui poussent le long de l'écorce et aux extrémités des branches — écho visuel direct du thème "corruption"/Source Corrompue. |
| Bouleau doré | Écorce claire, feuillage ambré/doré, plus léger visuellement que le chêne pour varier les silhouettes le long de la bordure. |

**Rochers de bordure** (remplacent `Rock 1`/`Rock 2`) 🔴 :
| Nom | Description (base de prompt Tripo3D) |
|---|---|
| Rocher moussu fendu | Bloc de pierre fissuré, mousse et lichen dans les fissures, quelques galets autour à la base. |
| Menhir runique | Pierre dressée, gravée de runes qui émettent une faible lueur, cohérent avec l'identité "magie/mana" du jeu. |
| Amas rocheux à cristaux | Blocs de pierre empilés avec des affleurements de cristaux de mana visibles, même famille visuelle que l'arbre pétrifié ci-dessus. |

**Props d'arène, NOUVEAU — à poser à l'intérieur de la zone jouable, sans collision, bas au sol pour ne jamais masquer le joueur/les ennemis** (répond directement à §7 point 2 et au retour utilisateur "le sol est vide") 🔴 :
| Nom | Description (base de prompt Tripo3D) |
|---|---|
| Touffe d'herbe stylisée | 2-3 variantes de hauteur/teinte, mêmes verts que `Tex_Ground_GrassV7` pour que le sol et les props se répondent visuellement. |
| Bouquet de fleurs sauvages | Reprend les fleurs blanches/jaunes déjà peintes dans la texture de sol, en vrai volume pour quelques points d'accroche. |
| Éclat de mana affleurant | Petit cristal qui sort du sol, lueur douce — cohérent avec les motes de mana ambiantes et la teinte de lumière par palier déjà en place. |
| Caillou/éclat de pierre | Très petit, pur détail de variation du sol. |
| Souche ou tronc couché | Référence ponctuelle de "forêt", casse la monotonie sans gêner la lisibilité. |
| Touffe de champignons | Petite touche de couleur contrastante (rouge/blanc ou bleu luminescent). |
| Patch de mousse/lichen | Mesh plat ou décal, sert aussi à casser visuellement le tuilage répétitif du sol (voir §7.1 point 2). |

**Éléments de lore, plus rares** (§7 point 3, 🟠) :
| Nom | Description (base de prompt Tripo3D) |
|---|---|
| Colonne brisée | Ruine antique, pierre érodée, partiellement envahie de végétation. |
| Autel fissuré | Petit autel de pierre, runes à moitié effacées, cohérent avec le lore "magie ancienne". |
| Zone corrompue | Pas un modèle à proprement parler : sol teinté violet avec fissures légèrement lumineuses, à placer près du centre de la carte en écho à la Source Corrompue (boss 3). |

---

## 8. Objets à ramasser et interface 3D 🟡 P3

| Élément | Aujourd'hui | Option |
|---|---|---|
| Gemmes d'XP | Quads texturés + effet de collecte | ✅ OK. Éventuellement une version 3D (cristal facetté) pour les grosses gemmes. |
| **Or** | Ajouté directement au compteur, rien à ramasser | Pièces/sacs qui tombent des ennemis (plus gratifiant). C'est un choix de design. |
| **Coffre de boss** 🟠 | Existe déjà en jeu (`BossChest.cs`, apparaît à CHAQUE mort de vrai boss en Classique/Sans fin/Ruée de boss, accorde 2 tirages de carte complets) mais réutilise le mesh générique des gemmes d'XP, juste teinté or/ambre (`MAT_Gem` + `MaterialPropertyBlock`) - pas un vrai modèle de coffre | **Vrai modèle de coffre au trésor** (voir description ci-dessous) - contrairement aux autres lignes de ce tableau, ce n'est plus un "choix de design" : le système existe et tourne déjà, il ne manque que le modèle. |
| Soins | N'existent pas (soin seulement à la mort d'un boss) | Potion/fruit à ramasser, rare. Choix de design. |
| Aimant / bombe | N'existent pas | Classiques du genre survivor. Choix de design. |
| **Menu principal** | 100 % 2D (illustrations) | ✅ Cohérent et beau. Option : personnage 3D animé (RT_Aether/Kael/Lyra déjà créées et inutilisées) pour montrer les skins. |

### 8.1 Coffre de boss — à modéliser 🟠 P2 (ajouté 2026-10-20)

Contrairement au reste de ce tableau (§8, 🟡 P3, des idées de design pas encore implémentées), **le coffre de boss est un système déjà fonctionnel et vu à chaque run** : il apparaît à la mort de chaque vrai boss (Classique, Sans fin, Ruée de boss), avec sa propre animation (apparition en "pop", lévitation + rotation lente en attendant le ramassage, hitstop + onde de choc + secousse de caméra à l'ouverture). Seul le modèle manque - `BossChest.cs` affiche aujourd'hui le mesh générique des gemmes d'XP (un simple cristal), juste reteint en or/ambre via `MaterialPropertyBlock` : on reconnaît un gros diamant doré, pas un coffre.

| Nom | Description (base de prompt Tripo3D) |
|---|---|
| Coffre au trésor (boss) | Petit coffre en bois cerclé de métal doré/bronze, couvercle fermé au repos, silhouette lisible vue de dessus en angle 3/4 (même cadrage que le reste des objets ramassables) - cohérent avec l'identité "fantasy médiévale" du jeu. Prévoir idéalement 2 états de maillage ou un morph simple (fermé / entrouvert avec une lueur dorée qui s'échappe) pour accompagner l'animation d'ouverture déjà existante dans le code, mais un seul modèle fermé suffit pour un premier remplacement. |

Point technique : le script attend un `MeshRenderer` (pas un `SpriteRenderer`) sur un enfant du prefab, avec une propriété `_BaseColor` sur son matériau pour la teinte - compatible avec n'importe quel shader URP Lit standard, aucun changement de code nécessaire une fois le modèle importé et branché sur le prefab `BossChest`.

---

## 9. Hors 3D, mais indispensable pour un jeu pro : le son 🔴 P1

Constat vérifié dans le code et les scènes :
- **Le jeu n'a aucun bruitage en partie.** Le seul appel de son est le dash (`PlayerController._dashSound`), et ce champ **n'est assigné dans aucun prefab**. Le son de « palier max » de l'écran de level-up (`_maxTierSfx`/`_sfxSource`) n'est **pas assigné** non plus dans `Game.unity`.
- Le seul fichier SFX du projet (`Game/Audio/SFX/Gentle_water_magic_p_#1-….mp3`) **n'est référencé nulle part**.
- Le réglage **« Effets sonores »** des paramètres ne règle donc rien.
- Les 3 boss partagent la même musique.

Liste minimale de sons à produire/acheter :
| Groupe | Sons |
|---|---|
| Joueur | Tir de base (par personnage), dash, absorption de projectile, dégâts reçus, mort, montée de niveau, ramassage de gemme (petit son qui monte en hauteur avec les gemmes enchaînées), Ultime (gros impact), Nova, Bouclier de Mana (absorption), clone fantôme |
| Armes | Boule de feu (lancer + explosion), couteaux (sifflement), orbitaux (bourdonnement), éclair, boue (splash), orbe (rebond), Aura (bourdonnement continu), chaque fusion |
| Ennemis | Un cri/son d'attaque + mort par type (Loup, Corbeau, Tisseuse, Gobelin, Golem, Bulbe), tir de sarbacane, crachat |
| Boss | Apparition, chaque attaque télégraphiée (le son aide à esquiver), mort, **une musique par boss** |
| Interface | Survol/clic de bouton, ouverture de panneau, choix de carte, achat (compétence, skin), erreur (or insuffisant), défi réussi/échoué, victoire, défaite |
| Ambiance | Vent, orage lointain, nature, qui changent selon la phase de la partie |

Techniquement : brancher tout ça sur un `AudioMixer` avec un groupe SFX relié au réglage « Effets sonores », et utiliser un pool de sources (limiter le nombre de sons identiques simultanés, sinon 150 ennemis qui meurent ensemble = saturation).

---

## 10. Effets procéduraux génériques (à remplacer par de vrais VFX) 🟡 P3

Ils fonctionnent et sont lisibles, mais ce sont des sprites ou des `LineRenderer` génériques. Avec un vrai artiste VFX (ou des packs de l'Asset Store), on passe un cap de qualité :
- `GroundTelegraphFX` (zones d'attaque au sol) : runes/fissures animées.
- `ChargeLaneFX` (couloir de charge du Dragon) : traînée de griffes / énergie.
- `ImpactSparkVFX` (impacts) : variantes par type de dégât (feu, glace, mana, physique).
- `ExpandingRingVFX` (anneaux) : onde avec distorsion.
- `EnemyDeathFX` : dissolution par shader (*dissolve*) au lieu d'un flash + étincelles. C'est le standard du genre.
- `BossFX` (arrivée/mort des boss) : déjà travaillé, à enrichir.
- `SummonPortalFX` (invocation de mini-boss) : portail 3D.
- `XPGemCollectFX` : OK.
- **Chiffres de dégâts** : OK (police Bangers).
- **Contour/surbrillance** des ennemis d'élite ou des mini-boss.

---

## 11. Ordre de production conseillé

1. 🔴 **Sons** (§9) : c'est le plus gros écart avec un jeu pro, et c'est relativement rapide à intégrer.
2. 🔴 **Projectiles** (§4), en commençant par le tir de base gris et les projectiles ennemis.
3. 🔴 **Sol + props de l'arène** (§7, points 1 à 3).
4. 🔴 **Bulbe cracheur** (§2.1) et **zones de boue** (§5).
5. 🟠 **Ultime + Nova + Bouclier** (§6).
6. 🟠 **Boss 3** (retexture + cristaux) et nettoyage de ses prefabs cylindres (§3).
7. 🟠 **Skins** (§1), d'abord 1 Classique par personnage pour valider le pipeline (modèle → rig → prefab → portrait → catalogue), puis le reste.
8. 🟡 Effets génériques, Sanglier, biomes, objets à ramasser.
9. 🟠 **Diorama du carrousel de difficulté** (§13) : indépendant du reste, peut se faire à tout moment — bloque seulement l'habillage final de la page Accueil du menu (le système/carrousel fonctionne déjà avec un dégradé de ciel temporaire).

## 12. Fichiers à supprimer une fois les remplacements faits
- `Assets/Resources/PulseVisual.prefab` (inutilisé)
- `Assets/Game/Prefabs/Weapons/BouncingOrbProjectile.prefab` (doublon inutilisé de celui de `Resources/`)
- `Assets/Resources/Prefabs/` (29 prefabs en double jamais chargés, **inclus dans le build** car dans `Resources/`)
- `TripoModels/` (doublons de `3D Models/`)
- `MAT_EnemyBulb_Placeholder` et l'enfant `VisualCube_PLACEHOLDER`
- Les `MeshRenderer` cube/capsule désactivés à la racine des prefabs
- `RT_Aether/Kael/Lyra` si on ne fait pas d'aperçu 3D
- Le pack `10 Fantasy Medieval Ambient Tracks` s'il ne sert pas (il n'est pas dans le build mais alourdit le projet)

---

## 13. Diorama du carrousel de difficulté (page Accueil du menu, nouveau 2026-10-20) 🟠 P2

Le sélecteur de palier de difficulté de la page Accueil a été repensé en carrousel visuel (`DifficultyCarousel.cs`, remplace l'ancien `DifficultySelector.cs` à 3 cases) : au lieu de boutons plats, chaque palier doit afficher une **mini-scène stylisée façon diorama** (un petit socle de terrain avec 2-3 éléments dessus), avec un **éclairage qui change selon le palier** — jour pour Modéré, crépuscule pour Violent, nuit pour Apocalyptique. Cet éclairage réutilise la VRAIE teinte de lumière déjà utilisée en jeu pour ce palier (`DifficultyTiers.LightColor`/`LightIntensity`), pas une palette inventée à part : le carrousel doit donner un aperçu fidèle de l'ambiance de la vraie partie.

**Important : un seul jeu de modèles suffit pour les 3 paliers** — c'est l'ÉCLAIRAGE qui change, pas le décor. Pas besoin de 3 variantes de terrain/arbre/rocher/loup.

**En attendant ces modèles**, l'emplacement (`DifficultyCarousel._previewImage`) affiche un simple dégradé de ciel procédural teinté par palier (`MenuSprites.SkyGradient`) — purement temporaire, zéro dépendance de code à changer une fois les modèles prêts : il suffira de remplacer le contenu visuel de cet emplacement (sprite, ou une RenderTexture si un rendu 3D en direct est souhaité).

**Style voulu, explicitement différent du reste de la DA du jeu** : "stylisé en cartoon" — plus simple et mignon que les modèles "fantasy peint" du §7.2 (silhouettes rondes, peu de détails, couleurs franches). Ne pas réutiliser les arbres/rochers de bordure du §7.2 tels quels, le contraste de style serait visible dans un si petit cadre.

| Nom | Description (base de prompt Tripo3D) |
|---|---|
| Socle de terrain miniature | Petite galette de terrain (ronde ou hexagonale), vue "diorama/jouet", herbe stylisée sur le dessus, flancs visibles (terre/roche) sur la tranche, à l'échelle pour accueillir 2-3 éléments dessus sans paraître vide ni surchargé. |
| Arbre miniature cartoon | Silhouette simple et ronde, feuillage en boule(s), proportions "jouet" plutôt que réalistes. 2 teintes de feuillage suffisent pour varier (ex. vert été / orangé automne) — pas besoin de 2 modèles séparés si une seule texture peut être retintée. |
| Rocher miniature cartoon | Petit amas arrondi, 1-2 teintes de gris/mousse, même esprit "jouet" que l'arbre. |
| Loup miniature cartoon | Version mignonne/simplifiée du loup du jeu (silhouette ronde, PAS le modèle réaliste du §2.3) — une seule pose fixe (assis ou debout) suffit, le diorama est une image statique, aucune animation nécessaire. |

Repère d'échelle technique (déjà posé dans le code, pour que les modèles arrivent à la bonne taille sans retouche) : le socle de terrain fait environ 3,5 unités de diamètre, l'arbre et le rocher environ 1 à 1,5 unité de haut une fois posés dessus, le loup environ 0,8 à 1 unité de long — proportions pensées pour un cadrage de caméra à ~5 unités de recul, angle 3/4 légèrement plongeant.
