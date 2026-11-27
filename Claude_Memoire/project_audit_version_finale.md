---
name: project-audit-version-finale
description: "Audit \"version finale\" (2026-10-15) — étapes 1-2 faites le 16/10, presque toute l'étape 3 faite le 19/10 ; restent nom de société, IL2CPP, Steam Cloud, puis étape 4"
metadata:
  node_type: memory
  type: project
  originSessionId: 330f2db8-4600-4ece-b773-321d7a8ff540
  modified: 2026-10-23T14:25:12.755Z
---

Audit complet demandé le 2026-10-15 : `AUDIT_VERSION_FINALE.md` (liste de travail, encadrés d'avancement en tête) et `A_MODELISER_3D.md` (21 skins, primitives à remplacer, décor, et AUCUN bruitage en jeu), à la racine du projet.

État au 2026-10-23 (relu après 7 jours / 5 commits de l'utilisateur) :
- Étape 1 (bugs) et étape 2 (équilibrage) faites le 2026-10-16.
- Étape 3 presque entièrement faite le 2026-10-19 (boutons debug masqués via `DevOnly.Show`, sauvegarde atomique + version, Ultime visuel, etc.). Restent, en attente de l'utilisateur : `companyName` (décision de marque, déplace le dossier de sauvegarde, avant la 1re version publique seulement), IL2CPP (à vérifier en compilant un build), Steam Cloud. Le 2026-10-23, l'utilisateur a dit « on s'occupera de l'étape 3 plus tard ».
- Étape 4 entamée (au 2026-10-29) : manette faite, perfs mesurées/corrigées (GC incrémental, gemmes en données), effets dédiés des fusions/Fragmentation/level-up faits. Restent : option secousses/flashs, textures 4K des boss, prefabs en double de Resources, sons.
- Les 5 docs (V16, AUDIT, NOTES, SCRIPTS_INDEX, A_MODELISER_3D) ont été remis à jour le 2026-10-29 (couvrent 24→29/10).
- Beaucoup d'équilibrage a bougé depuis (paliers de difficulté Modéré/Violent/Apocalyptique, coffre de boss, courbe XP `70 × niveau²`, Cerf 63 000 PV, Surpuissance 10 s, Terre Calcinée en météorites) : toujours relire la synthèse `AETHER_STORM_SURVIVOR_PROJECT_COMPLET_V16.md` (le nom du fichier suit le numéro de version) et `NOTES.md` avant de citer un chiffre.

**Why:** l'utilisateur veut un jeu « professionnel » pour la sortie Steam ; l'audit sert de liste de travail partagée.
**How to apply:** avant de reprendre l'audit, relire les encadrés d'avancement en tête d'`AUDIT_VERSION_FINALE.md`. Attention : dans `NOTES.md`, les sessions 25 à 40 (21-23/10) sont tassées dans la ligne 4 « Dernière mise à jour » (~47 000 caractères) au lieu d'avoir leurs propres sections. Ne pas toucher `save.json` ([[feedback-save-is-test-data]]), pas de proposition de commit à chaque petit fix ([[feedback-commit-cadence]]). Voir aussi [[project-steamworks-access]].
