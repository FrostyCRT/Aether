# Aether Storm Survivor — reprise de travail pour Claude Code

Fichier lu automatiquement par Claude Code à l'ouverture du projet. Écrit le 2026-11-27, au moment où l'utilisateur
change de PC (et donc de conversation). **À la première réponse : prendre connaissance de ce fichier, de
`Claude_Memoire/MEMORY.md` et des fichiers qu'il référence, puis reprendre là où on en était, sans tout redemander.**

## Le projet
Jeu Unity 6 (URP) de type « survivor » vu de dessus (caméra orthographique inclinée à 65°), 3 héros (Aether, Kael,
Lyra), 3 boss (Dragon, Cerf ancestral, boss 3 « La Source corrompue »), traduit en 11 langues. Modèles 3D faits par
code dans Blender (scripts dans `Blender/`, fichier `Blender/Archon.blend`), pilotés via le MCP Blender ; Unity piloté
via le MCP Unity. Journal détaillé de TOUT ce qui a été fait : `NOTES.md` (les entrées les plus récentes sont en haut
de chaque section du jour).

## Façon de travailler avec l'utilisateur (important)
- Répondre en **français**, **court**, sans jargon inutile : l'utilisateur surveille sa consommation de tokens.
- Toutes ses règles et préférences sont dans `Claude_Memoire/` (copie de la mémoire de Claude au 2026-11-27).
  Les plus importantes :
  - **jamais** modifier un script `.cs` pendant le Play ; vérifier l'état de l'éditeur (GetState) avant chaque
    modification de script et avant de lancer un test ; si Unity est déjà en Play, c'est peut-être SA partie : ne rien
    injecter, ne rien arrêter, le signaler ;
  - ne **pas** proposer de commit après chaque petit correctif (attendre un gros morceau ou sa demande) ;
  - la sauvegarde de l'éditeur = données de test (ne pas proposer de la restaurer) ;
  - **tester EN JEU** avant d'affirmer qu'une chose marche (captures, mesures) — il l'a reproché quand ce n'était pas
    fait (voir `feedback_measure_ground_in_game.md`) ;
  - rouge = réservé aux zones au sol à esquiver ; pas de cristaux ajoutés par défaut ; pas d'humanoïde modélisé par code.
- Il apprécie l'honnêteté : dire clairement ce qui n'est pas au niveau et proposer une recommandation.

## Où on en est (2026-11-27)
**Sujet en cours : le boss 3, « Le Lotus de la Source ».**
- Historique rapide : serpent -> Archonte (humanoïde, abandonné : « modèle 3D pas terminé ») -> **Lotus** (fleur sacrée
  rose de ~12 m sur une mare, cœur de cristal bleu). Validé visuellement par l'utilisateur (« c'est bien maintenant »).
- Fichiers : `Blender/lotus_build.py` + `Blender/lotus_rig.py` (modèle, squelette, 6 animations) -> export
  `Assets/Game/3D Models/Boss3/SK_Lotus.fbx` ; prefab `Assets/Game/Prefabs/Enemies/BossCorruptedSource.prefab`
  (enfants LotusMesh et SourcePond) ; shaders `LotusPetal`, `SourcePond`, `ArchonSoft`, `PlayerSilhouette` ;
  scripts `Enemies/LotusSourceFX.cs` (mise en scène), `Enemies/BossCorruptedSource.cs`, `Systems/RenderPriority.cs`.
- **En attente : son test EN COMBAT** des derniers changements (2026-11-27) :
  1. boss moins mobile : Frappe avec 10 s de recharge, cercle = point d'atterrissage (5 m), ruée ≤ 8 m ;
  2. repositionnement seulement au-delà de 18 m, zone d'arrivée affichée 1,4 s, s'arrête à 10 m du joueur, pas de
     dégâts de contact pendant le trajet ;
  3. ordre d'affichage : projectiles ennemis (toujours visibles) > silhouette du héros caché > ennemis > chiffres de
     dégâts > sorts (calques de rendu 8/9/10 + Render Objects dans les 3 renderers URP).
- Restes connus : illustration du Grimoire du boss 3 à refaire (elle montre encore un serpent) ; anciens modèles
  Archonte/serpent inutilisés dans `Assets/Game/3D Models/Boss3/` (ne les supprimer que s'il le demande).
- **Rien n'est commité depuis le commit `84b1a33`** (beaucoup de travail en cours dans le dossier).

## Réinstaller la mémoire de Claude sur un nouveau PC
La mémoire de Claude Code est rangée dans le dossier utilisateur, pas dans le projet. Pour la récupérer :
copier le contenu de `Claude_Memoire/` dans
`C:\Users\<utilisateur>\.claude\projects\<chemin-du-projet-avec-des-tirets>\memory\`
(le nom du dossier dépend de l'emplacement du projet ; Claude Code le crée à la première conversation dans le projet —
on peut aussi demander à Claude de faire la copie). Tant que ce n'est pas fait, ce fichier et `Claude_Memoire/`
suffisent pour reprendre.
