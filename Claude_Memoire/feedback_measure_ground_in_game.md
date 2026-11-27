---
name: feedback-measure-ground-in-game
description: "Avant de dire qu'un modèle animé ne traverse pas le sol, le MESURER en jeu (BakeMesh sur chaque état), pas en Blender ni sur captures"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 8978aab1-77c2-4466-9c43-f560eb03f467
  modified: 2026-11-26T20:53:04.395Z
---

2026-11-26 : « je crois que tu n'as pas fait de test en jeu, certains éléments passent à travers le sol ». J'avais vérifié des têtes d'os dans Blender et des captures vues du dessus ; en jeu, le boss 3 était enterré de ~1,9 m (décalage de prefab hérité, pivot du boss au sol).

**Why:** la caméra à 65° cache ce qui passe sous le sol ; les mesures Blender ignorent le placement dans le prefab.
**How to apply:** pour tout modèle animé intégré, en Play : raycast du sol sous le pivot, puis pour chaque état Animator.Play(état, 0, t) + Update(0) + SkinnedMeshRenderer.BakeMesh -> point le plus bas en monde ; rapporter les chiffres. Méthode détaillée dans NOTES.md (Lotus, 2026-11-26). Voir [[project-lot1-feedback-groups]].
