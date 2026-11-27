---
name: project_economy_targets
description: "Objectifs de durée de la méta-progression (or/éclats) fixés avec l'utilisateur le 2026-10-27 et barème qui les tient"
metadata:
  node_type: memory
  type: project
  originSessionId: f76ab443-7a9d-4afa-9ab2-2c99702e0706
  modified: 2026-11-08T12:33:07.434Z
---

Refonte de l'économie validée le 2026-10-27. Objectifs voulus par l'utilisateur : que les joueurs reviennent sur plusieurs jours, ne jamais pouvoir tout débloquer vite.
- Une branche de personnage maxée : ~30 à 40 parties. Les 3 branches : ~100. Toute la Réputation : ~150 parties (très long terme).
- Premier achat possible dès la 1re partie.
- Paliers de l'arbre (idée de l'utilisateur) : 2 nœuds du bas libres, 2 du milieu = avoir gagné en Modéré, nœud du haut = avoir gagné en Violent (avec le personnage de la branche, un palier plus dur compte aussi).

2026-10-29 : à la demande de l'utilisateur (« un peu plus d'éclats et de gold »), barème relevé d'environ 25 % ; les durées ci-dessus sont donc ~20 % plus courtes (estimation, pas remesurée). Ne pas les « corriger » sans qu'il le demande.

2026-11-07 : l'utilisateur veut que tuer des ennemis rapporte de l'or (côté satisfaisant) et a proposé un x10 des coûts. Or passé à l'échelle x10 (gains, coûts, primes, sauvegardes migrées via SaveData.version 2) + ~20 % ; kills payants avec rendement décroissant selon le rythme de kills (MetaProgressionManager.KillGoldHalfRate). Mesuré : victoire Modéré ~20 700 or, vétéran comme nouveau joueur ; branche ~306 000 or. Éclats +1/minute, 165 par victoire Modéré. Fortune = 12 000 or.

2026-11-07 (plus tard) : l'utilisateur trouvait le dernier palier de Réputation (3 500) quasi impossible (~50 éclats par bonne partie perdue en Apocalyptique, ~70 parties). Coûts Réputation → 150/400/800/1 400/2 200 et éclats 2/minute. Il a précisé « il ne faut quand même pas aller trop loin » : l'objectif ~150 parties pour toute la Réputation est donc abandonné au profit de ~60-80 ; ne pas redescendre davantage sans demande.
2026-11-08 : l'utilisateur trouvait trop d'or en Apocalyptique (30 000 or à 15 min, 11 000 kills). KillGoldHalfRate 150 → 70, kills ~-45 % (estimation calée sur sa partie, non rejouée) ; Fortune = 10 000 or. Victoire Modéré estimée ~17 700 or.
2026-11-08 (plus tard) : l'utilisateur trouvait Fortune trop simple. Objectif désormais par palier : Modéré 10 000 / Violent 15 000 / Apocalyptique 20 000 or (ChallengeManager.FortuneGoldGoal = base x multiplicateur de récompense du palier). 20 000 fixe serait impossible en Modéré.
Prime de première victoire PAR PERSONNAGE et par palier (300/600/1000, 5 700 au total) : confirmée voulue par l'utilisateur le 2026-11-07, ne pas la passer au niveau du compte.

**Why:** l'ancien système (or par ennemi tué) donnait ~40 000 or par partie en Apocalyptique alors que tout l'arbre coûtait 14 000.

**How to apply:** l'or vient surtout des minutes, des boss et de la victoire (barème en tête de MetaProgressionManager, coûts dans SkillTreeData). Toute retouche d'équilibrage doit garder ces durées ; mesurer avec des parties automatiques (voir [[unity-mcp-long-play-measurement]]) plutôt qu'estimer.
