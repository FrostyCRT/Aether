---
name: editor-vs-build-onvalidate
description: "Dans l'éditeur, OnValidate relance HudStyler.Apply au chargement (2 passages) alors qu'une build n'en fait qu'un ; toujours rendre l'habillage idempotent et se méfier des mesures faites seulement dans l'éditeur"
metadata:
  node_type: memory
  type: project
  originSessionId: f76ab443-7a9d-4afa-9ab2-2c99702e0706
  modified: 2026-11-11T10:13:47.224Z
---

Le 2026-11-11, le texte des PV n'était pas au même endroit dans l'éditeur et dans une build (44 px d'écart) : `HudStyler.OnValidate` met `_dirty` à vrai au chargement de la scène en Play, donc `Apply()` tourne deux fois dans l'éditeur et une seule fois dans une build. Au second passage, `Find("Health/HPText")` ne trouvait plus le texte (déjà rangé dans la barre par `HudBar`). Même cause pour des halos créés en double (`HudInfoHalos`).

**Why:** plusieurs allers-retours avec l'utilisateur sur un réglage « sans effet » : je mesurais dans l'éditeur, il regardait sa build (parfois même une build ancienne).

**How to apply:** tout code d'habillage appelé depuis `Apply` doit être idempotent (références gardées, objets réutilisés ou détruits avant recréation). Quand l'utilisateur dit que l'éditeur et la build diffèrent, chercher d'abord un chemin propre à l'éditeur (OnValidate, `#if UNITY_EDITOR`, `Application.isEditor`). Lui rappeler de refaire une build avant de juger un réglage. Voir aussi [[unity-mcp-ui-screenshots]].
