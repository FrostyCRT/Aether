---
name: feedback-save-is-test-data
description: La sauvegarde locale (save.json) ne contient que des données de test — inutile de proposer de la restaurer
metadata:
  node_type: memory
  type: feedback
  originSessionId: 330f2db8-4600-4ece-b773-321d7a8ff540
  modified: 2026-10-14T18:40:44.027Z
---

L'utilisateur se fiche du contenu de `save.json` / `save.bak` (AppData\LocalLow\DefaultCompany\Aether Storm Survivor) : « c'est que pour les tests » (2026-10-14).

**Why:** ce n'est pas une vraie progression de joueur, juste un état de test dans l'éditeur.

**How to apply:** ne plus proposer de restaurer la sauvegarde après un test, ni en faire un point en suspens. Garder `SaveSystem.SuppressWrites` pendant les tests reste pratique pour des résultats reproductibles, mais une modification accidentelle n'est pas à signaler comme un problème.
