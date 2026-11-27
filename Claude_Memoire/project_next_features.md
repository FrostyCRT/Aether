---
name: project-next-features
description: "Escarmouche, Grimoire, didacticiel et Éclipse (ex-Nuit des Éclats) — tous faits entre le 2026-11-14 et le 18 ; ce qui reste à vérifier"
metadata:
  node_type: memory
  type: project
  originSessionId: f76ab443-7a9d-4afa-9ab2-2c99702e0706
  modified: 2026-11-20T19:03:56.608Z
---

État au 2026-11-20 (dernier commit 9aba757 du 18/11) — tout est FAIT, détail dans « ▶ REPRENDRE ICI » en tête de `NOTES.md` :
- **Escarmouche** (5 min) : codée et réglée (3 boss 1:30 / 3:00 / 5:00, Frénésie 4:00, XP ×1,5, récompenses ÷ 1,75). Seuls Classique et Escarmouche sont proposés au joueur (Sans fin / Ruée masqués, code intact).
- **Grimoire** : bouton livre en bas à gauche de l'Accueil, découvertes cartes / fusions / boss (+5 / +15 / +20 éclats), passif.
- **Didacticiel** : visite du menu + partie d'entraînement scénarisée, **validé par l'utilisateur**.
- **Éclipse** (nom joueur de la « Nuit des Éclats », classe `NightOfShards`) : éclat à ramasser → 45 s de nuit (20 s en Escarmouche). Rythme sur une partie complète pas encore confirmé.
- **Gobelin doré** (2:30, `GoldenGoblinEvent`) et **Tempête de cristaux** (13:30, `CrystalStormEvent`) : faits le 2026-11-22, Classique + Sans fin ; météorite Tripo attendue dans `Resources/CrystalStorm/Meteor`. Rythme voulu par l'utilisateur : un événement entre chaque phase de boss.
- Refusés : Héritage, Ouverture rapide (voir [[feedback-run-start-rules]]), cartes verrouillées, Frénésie en Classique.

**Why:** rendre les premières parties satisfaisantes sans bonus de départ ni toucher au tir de base / à la courbe d'XP.
**How to apply:** ne pas reproposer ces chantiers comme nouveaux ; partir de la liste « À faire / à vérifier » de NOTES.md. Avant une sortie : `SaveSystem.FreshSaveOnNewBuild` → false.
