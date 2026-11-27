---
name: project-render-priority
description: "Ordre d'affichage voulu par l'utilisateur (projectiles ennemis > ennemis > sorts ; dégâts ne gênent pas) et comment il est fait (calques de rendu + Render Objects URP)"
metadata:
  node_type: memory
  type: project
  originSessionId: 8978aab1-77c2-4466-9c43-f560eb03f467
  modified: 2026-11-27T16:46:03.293Z
---

2026-11-27, demande de l'utilisateur : « les projectiles ennemis doivent être la priorité, et après les ennemis ; les sorts en dessous ; l'affichage des dégâts ne doit pas gêner ».

Mise en place : calques de rendu 8 EnemyVisual, 9 EnemyFX, 10 PlayerVisual (objets sans collider uniquement), `RenderPriority.cs`, Render Objects dans les 3 renderers URP (Performant/Balanced/HighFidelity). Le héros caché sous un boss est montré en silhouette. Détails dans NOTES.md (2026-11-27).

**Why:** lisibilité en combat (le boss 3 Lotus fait 12 m et cachait projectiles et joueur).
**How to apply:** tout NOUVEAU type d'ennemi, de boss ou de projectile ennemi doit passer par RenderPriority (ou avoir ses renderers sur le bon calque) ; tout nouveau visuel de projectile ennemi doit rester lisible sur la fleur ET l'herbe ; un shader utilisé en matériau de remplacement doit avoir la variante DOTS_INSTANCING_ON (GPU Resident Drawer). Voir [[project-lot1-feedback-groups]].
