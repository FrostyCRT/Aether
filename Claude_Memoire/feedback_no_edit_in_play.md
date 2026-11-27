---
name: feedback_no_edit_in_play
description: "Ne JAMAIS modifier un script pendant que Unity est en Play : arrêter le Play à la fin de chaque sonde, avant toute retouche"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 330f2db8-4600-4ece-b773-321d7a8ff540
  modified: 2026-11-25T20:35:27.594Z
---

Ne jamais modifier un fichier .cs (Edit, Write, sed, python) pendant que Unity est en mode Play.

**Why:** demande explicite de l'utilisateur. Règle enfreinte 4 fois (2026-10-26, deux fois le 2026-10-27 pendant des
itérations visuelles, puis une 4e fois le 2026-10-27 après un test long : la partie était finie, mais le Play tournait
encore pendant l'analyse des journaux).

2026-11-25 : l'utilisateur peut lancer SA propre partie pendant que je travaille. Un test lancé alors (« Already in play mode ») a injecté ma sonde dans sa partie (invincibilité, armes coupées, boss forcé). Toujours faire GetState JUSTE AVANT d'entrer en Play pour un test ; si Unity est déjà en Play sans que je l'aie lancé, ne rien injecter et ne pas l'arrêter : le signaler.

**How to apply:** le Stop fait partie de la sonde : dès que le fichier de fin apparaît (captures, journal "END"),
appeler Unity_ManageEditor Stop DANS LA MÊME réponse que la lecture des résultats, avant toute analyse. Et TOUJOURS
Unity_ManageEditor GetState juste avant le premier Edit/Write d'un .cs.
