---
name: project-lot1-feedback-groups
description: "Retours lot 1 (2026-11-25) : groupes 1-5 validés ; boss 3 devenu le LOTUS DE LA SOURCE (modèle, mare, effets, mobilité, ordre d'affichage faits au 2026-11-27, test en combat de l'utilisateur attendu)"
metadata:
  node_type: memory
  type: project
  originSessionId: f76ab443-7a9d-4afa-9ab2-2c99702e0706
  modified: 2026-11-27T16:22:46.916Z
---

Retours du 2026-11-25 sur le lot 1 des modèles Blender. Ils sont traités groupe par groupe, avec un /clear entre deux groupes.

**ÉTAT AU 2026-11-27 (fin de session, changement de PC)** : tous les groupes validés. Boss 3 = Lotus de la Source validé visuellement (« c'est bien maintenant »). Derniers changements EN ATTENTE DE TEST EN COMBAT par l'utilisateur : Frappe moins fréquente (10 s) avec cercle d'atterrissage de 5 m, repositionnement seulement au-delà de 18 m avec zone d'arrivée affichée, ordre d'affichage (projectiles ennemis > silhouette du héros > ennemis > chiffres de dégâts > sorts), voir [[project-render-priority]]. Restes connus : illustration du Grimoire du boss 3 à refaire (montre un serpent), anciens modèles Archonte/serpent inutilisés (suppression possible si l'utilisateur le demande), tout le travail depuis le commit 84b1a33 n'est PAS commité.
L'utilisateur surveille sa consommation de tokens : faire peu de captures et des réponses courtes.

**Groupe 1 : FAIT** (le détail est dans NOTES.md, 2026-11-25)
- Les dagues font un demi-tour avant d'être rattrapées.
- Pierres runiques agrandies.
- Traînée de l'Orbe de cristal à la largeur de la boule.
- Textes de la carte Dagues sans « m ».
- Valse des Lames : taille de base et +20 %.
- Pas encore testé en Play par l'utilisateur.

**Groupe 2 : identité des dagues de Lyra** — VALIDÉ par l'utilisateur le 2026-11-25 (Lame croissant + traînée de lune discrète + portée de détection corrigée, détails dans NOTES.md)
- Retour : le modèle SM_LyraDagger « fait épée de chevalier », on ne se dit pas « ce sont les couteaux de Lyra ».
- Retour : la lueur tourbillonnante bleu-violet (KnifeSwirl) n'est pas belle.
- But : retrouver l'identité de Lyra dans l'arme. Demander d'abord son style ou son illustration.

**Groupe 3 : visuels de la Valse des Lames** — VALIDÉ le 2026-11-25 (« Valse de lune » + bords de traînées fondus par T_TrailSoftEdge, détails dans NOTES.md)
- La lueur des lames est trop simple. Il faut quelque chose de beaucoup plus beau, cohérent avec le tracé de la lame.
- Les cristaux qui se chargent autour de Lyra au retour des lames puis tirent : « violet éclaté moche », à refaire.

**Groupe 4 : boss** — 2026-11-26 (soir) : l'Archonte ABANDONNÉ (« modèle pas terminé ») -> boss 3 = « LE LOTUS DE LA SOURCE » (Blender/lotus_build.py, collection Lotus X=80 : 3 couronnes de pétales blanc crème -> rose poudré, cœur de cristal bleu Cerf, étamines, racines, corruption INDIGO sombre d'un côté) ; forme validée, rig + 6 animations + intégration Unity FAITS le 2026-11-26, palette « Rose 2 » choisie par tests en jeu, projectiles = pétales roses ; puis sol corrigé (mesuré), plus imposant, passe qualité (shader LotusPetal, bassin de la Source + feuilles flottantes, effets vivants, charge/implosion via LotusSourceFX, corruption à encre dans l'eau) le 2026-11-26 ; 2026-11-27 : bassin fixe (ne tourne plus), coupé aux murs, et corruption RETIRÉE de la fleur à la demande de l'utilisateur (« pas besoin de marque de corruption » - ne pas la remettre) ; à tester par l'utilisateur. Avant : boss 3 REMPLACÉ par « L'Archonte de la Source ». v1 (armure porcelaine/or) REJETÉE (« robotique », couleurs). v2 = être de lumière DRAPÉ blanc-bleu (corps organique en métaballes, robe de voiles, mèches de lumière, capuchon, argent, corruption violette ; Blender/archon_v2_*.py) , puis (« trop flashy / hologramme ») palette « Saphir, Or et cristal du Cerf » choisie par tests en jeu + projectile FEUILLE dorée (la plume faisait « missile »), le 2026-11-26, à tester par l'utilisateur ; règle apprise : sur la carte verte, éviter les grandes surfaces vertes et l'auto-éclairage fort ; puis corps BLANC MAT comme le Cerf (pas d'or ni de lumière sur le corps), éléments superflus retirés et modèle basculé face caméra (BossCameraTilt) car un humanoïde debout est illisible sous la caméra à 65° ; modèle était à l'envers (corrigé, 180°) ; style vraie divinité imposante, AUCUNE référence japonaise, voir [[project-future-kitsune-idea]]). Historique : VALIDÉ le 2026-11-25 (taille du boss 3 réduite à ~15 m, échelle 0,11, OK) — boss 1 (dard d'écaille argent, nervure + halo cyan pâle) et boss 2 (épine de cristal blanc, cœur vert mousse, halo blanc) FAITS le 2026-11-25, en attente d'avis ; Cerf repassé en BLEU (amas de cristaux plus détaillé, moins flashy) le 2026-11-25, non validé ; Cerf : couleurs reprises des pixels de ses bois (bleu opaque) le 2026-11-25 ; boss 3 : v1 rejetée (animations, hitbox, longueur) -> v2 à plat, gueule ouverte, très détaillée (Blender/Export/RiftSerpent_Flat.glb) ; site abandonné : j'ai animé la v2 dans Blender (60 i/s, centre de masse toujours à l'origine = exigence de l'utilisateur), intégrée le 2026-11-25 (anciens modèles supprimés, hitbox corrigée, Frappe calée sur la tête), à valider ; projectiles (éclat d'obsidienne, cœur bordeaux sourd) et cristaux (grappe, cœur indigo) du boss 3 faits le 2026-11-25, à valider
- Règle : les projectiles restent de taille projectile, petits et nets. Il y en a beaucoup à l'écran, ça ne doit pas faire brouillon.
- Boss 1 : rejet de la boule de feu recolorée en rouge. Chercher une autre solution (proposé : petite flèche de flamme).
- Boss 2 (cerf ancestral blanc et bleu) : les projectiles orange façon magma n'ont pas de rapport avec lui.
- Boss 3 : rejet de « tout vert et brillant », pour les projectiles comme pour les cristaux.
  - Affiner les cristaux pour qu'ils soient plus graphiques.
  - Garder une nuance de couleur entre cristaux et projectiles : en jeu on les confond.
  - Demander à l'utilisateur les couleurs du boss.
  - 2026-11-25 : le modèle 3D du boss 3 va être REFAIT (l'actuel n'est pas beau), ça reste un SERPENT. Ses projectiles et cristaux se conçoivent d'après le nouveau modèle.
- Illustrations du Grimoire (Assets 2D/UI/Grimoire/Grimoire_Boss1-3.png) : Dragon argent/gris acier à reflets cyan ; Cerf blanc, bois en cristal bleu, mousse verte ; Serpent gris argenté (rendu sans couleur).
- VALIDÉ 2026-11-25 : le Dragon est bien argent + cyan en jeu.
- CORRIGÉ 2026-11-25 : le ROUGE est réservé aux ZONES AU SOL à esquiver (télégraphes), PAS aux projectiles (« trop rouge, les projectiles des ennemis de base ne sont pas rouge flash »). Projectiles de boss = couleurs du boss, discrètes, comme ceux des ennemis de base (halo léger).
- Boss 3 : concept « Serpent de la Faille » VALIDÉ (Aether corrompu : obsidienne noire/bleu nuit, veines rouges incandescentes, yeux rouges, collerette de cristaux brisés en éventail type cobra, crête d'éclats noirs le long de l'échine, queue en lame de cristal ; projectiles/cristaux du serpent : à revoir sans rouge flash, voir la correction ci-dessus). Modélisé par code dans Blender (générateurs désactivés), livré long et droit, topologie propre ; animations : j'essaie de les faire (6 états de l'Animator « Snake » : Idle, Bite, Coiled, Dance, Death, Side winding), sinon le site de l'utilisateur. Ordre : ébauche montrée d'abord. À faire APRÈS les projectiles des boss 1 et 2.
- (historique) Ma recommandation : un code « danger » commun à tous les projectiles ennemis (jamais utilisé par les sorts du joueur), car le cyan du Dragon et le bleu du Cerf se confondent avec Lyra et l'Orbe.

**Groupe 5 : refonte du tir de base** — CODÉ le 2026-11-26 (Double / Triple / Déluge, Cadence sans effet sur le Déluge, détails NOTES.md), Déluge gardé à 0,85 par l'utilisateur (≈ x1,5 Pierres runiques max), en test en partie ; écrans Victoire/Défaite à 3 pastilles pas encore vérifiés
- Problème : il est presque inutile en fin de partie.
- Idée de l'utilisateur : carte sur 3 niveaux. Niveau 1 tir double, niveau 2 tir triple, niveau 3 (max) tir continu avec un micro-délai.
- Points de progression : ma recommandation est 3 ronds sans point bleu de déblocage. Décision de l'utilisateur en attente.
- À adapter : menu pause, écran de Game Over, écran de Victoire.
