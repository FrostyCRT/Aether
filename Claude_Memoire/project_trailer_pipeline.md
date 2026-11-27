---
name: project-trailer-pipeline
description: "Trailer Steam + version verticale en cours (2026-10-14) — projet Remotion à côté du jeu, plans capturés par un outil Unity éditeur-only"
metadata:
  node_type: memory
  type: project
  originSessionId: 330f2db8-4600-4ece-b773-321d7a8ff540
  modified: 2026-10-14T16:57:50.345Z
---

✅ VALIDÉ par l'utilisateur le 2026-10-14 (v3 : trailer Steam 68,6 s + short vertical 40 s, dans `out/`). Trailer publicitaire (démarré 2026-10-14) : projet Remotion `C:\Users\jules\My project\Aether Storm Trailer` (frère du projet Unity, pas dedans). Compositions `Trailer` (16:9, ~48 s, 60 i/s) et `TrailerVertical` (9:16, ~26 s).

Décisions de l'utilisateur : vraie DA du jeu uniquement (fonds/logos/illustrations du jeu, police Bangers patchée), vrai gameplay capturé (pas de faux monstres cartoon — la v1 cartoon a été rejetée), visite du menu avec changement de page fluide, musique + SFX libres de droits, rendu "pro pour Steam", les deux formats (Steam + court vertical).

Retours sur la v2 (2026-10-14) : plans longs (3,5-5 s, le temps de lire le texte), caméra de jeu quasi normale (zoom ~0.85, pas de gros plan), déplacements naturels (jamais de cercles), boss bien cadrés (le boss 3 est immobile : aller à lui), montrer le déblocage d'un héros et la fiche d'achat de l'arbre, textes "arguments survivor". Ne PAS montrer les modes de jeu (Sans fin / Ruée de boss / Titans pas encore validés pour le dev — passage retiré le 2026-10-14). AUCUNE fusion et le moins possible d'armes d'amélioration à l'image (pas encore leurs vrais modèles 3D) : arme de départ + arme exclusive + bonus de stats.

**Why:** la v1 (personnages cartoon sur fonds inventés, sans SFX ni gameplay) a été jugée hors DA ; l'utilisateur veut discuter la composition avant de produire.
**How to apply:** proposer/valider la composition avant de gros changements ; garder musique "Determined Pursuit" (Emma_MA, CC0) et SFX Kenney CC0 + synthés (`tools/synth-sfx.js`), crédits dans `public/CREDITS.txt`.

Capture : `Assets/TrailerCapture/` (éditeur-only, supprimable) — `TrailerCapture.RunBatch("plan1,plan2")` via RunCommand ; capture image par image (Time.captureFramerate=60) + encodage ffmpeg. Unity Recorder inutilisable ici (~1 i/s quand Unity n'a pas le focus). Voir [[unity-mcp-editor-step-for-timing]]. Ne jamais modifier un script pendant le Play (recompilation = plan perdu). La file d'attente peut caler entre deux plans (relancer RunBatch avec le reste).
