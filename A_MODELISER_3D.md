# Aether Storm Survivor : ce qu'il reste à modéliser en 3D (et à habiller)

Audit du 2026-10-15, mis à jour le 2026-10-16 (Vortex de Mana ramené au sol, rayon d'apparition des ennemis corrigé) puis le 2026-10-18 (fusion "Orbes Jumeaux" retirée, remplacée par "Sillage Boueux" — voir §4.1 et §5). Rien de cette liste n'est encore produit. J'ai parcouru tout le jeu (prefabs, scènes, `Resources/`, scripts qui créent des objets en code) et lancé des parties pour voir ce qui s'affiche vraiment à l'écran.
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
| **Sons** (hors 3D mais bloquant pour un jeu pro) | **le jeu n'a aucun bruitage** | 🔴 P1 (voir §9) |

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
| Fusion **Terre Calcinée** | même prefab | Cylindres orange | Sol brûlé : braises, fissures incandescentes, petites flammes. |
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

---

## 8. Objets à ramasser et interface 3D 🟡 P3

| Élément | Aujourd'hui | Option |
|---|---|---|
| Gemmes d'XP | Quads texturés + effet de collecte | ✅ OK. Éventuellement une version 3D (cristal facetté) pour les grosses gemmes. |
| **Or** | Ajouté directement au compteur, rien à ramasser | Pièces/sacs qui tombent des ennemis (plus gratifiant). C'est un choix de design. |
| Coffres | N'existent pas | Coffre lâché par les boss/mini-boss (choix d'amélioration bonus). Choix de design. |
| Soins | N'existent pas (soin seulement à la mort d'un boss) | Potion/fruit à ramasser, rare. Choix de design. |
| Aimant / bombe | N'existent pas | Classiques du genre survivor. Choix de design. |
| **Menu principal** | 100 % 2D (illustrations) | ✅ Cohérent et beau. Option : personnage 3D animé (RT_Aether/Kael/Lyra déjà créées et inutilisées) pour montrer les skins. |

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

## 12. Fichiers à supprimer une fois les remplacements faits
- `Assets/Resources/PulseVisual.prefab` (inutilisé)
- `Assets/Game/Prefabs/Weapons/BouncingOrbProjectile.prefab` (doublon inutilisé de celui de `Resources/`)
- `Assets/Resources/Prefabs/` (29 prefabs en double jamais chargés, **inclus dans le build** car dans `Resources/`)
- `TripoModels/` (doublons de `3D Models/`)
- `MAT_EnemyBulb_Placeholder` et l'enfant `VisualCube_PLACEHOLDER`
- Les `MeshRenderer` cube/capsule désactivés à la racine des prefabs
- `RT_Aether/Kael/Lyra` si on ne fait pas d'aperçu 3D
- Le pack `10 Fantasy Medieval Ambient Tracks` s'il ne sert pas (il n'est pas dans le build mais alourdit le projet)
