---
name: project-save-backups
description: Où sont les vraies sauvegardes mises de côté pendant les tests (à remettre sur demande « remets ma save »)
metadata:
  node_type: memory
  type: project
  originSessionId: f76ab443-7a9d-4afa-9ab2-2c99702e0706
  modified: 2026-11-12T19:25:05.609Z
---

- 2026-11-12 : vraie save de l'utilisateur copiée sur `C:\Users\jules\Desktop\Aether Storm - ma save (2026-11-12)\` (save.json + save.bak + LISEZ-MOI.txt). La save en place a 10 000 000 d'or (`totalGold`) et 100 000 éclats (`totalEclats`) pour ses tests.
- Plus ancien : `...\LocalLow\DefaultCompany\Aether Storm Survivor\_sauvegarde_test_2026-11-10\` et `_sauvegarde_test_2026-10-29\`.

**Why:** l'utilisateur veut tester sans altérer sa save ; build et éditeur partagent le même save.json.
**How to apply:** à « remets ma save », jeu fermé, recopier save.json ET save.bak du dossier du Bureau 2026-11-12 (le plus récent). Voir [[feedback_save_is_test_data]].
- Depuis le 2026-11-16 : `SaveSystem.FreshSaveOnNewBuild = true` → la 1re ouverture de chaque nouvelle BUILD efface save.json/save.bak (dossier partagé avec l'éditeur) pour tester le didacticiel. Au 2026-11-20 la save de l'éditeur est une save de test (~9,7 M d'or, ~86 000 éclats).
