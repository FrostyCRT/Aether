---
name: project_vfx_visual_grammar
description: "Formes réservées aux télégraphes ennemis (ThinRing, TickRing, disque plein) ; les effets du joueur ont leur propre grammaire cristal (validé 2026-10-27)"
metadata:
  node_type: memory
  type: project
  originSessionId: f76ab443-7a9d-4afa-9ab2-2c99702e0706
  modified: 2026-10-27T01:38:12.029Z
---

ThinRing, TickRing et le disque plein de FXSprites forment la grammaire des TÉLÉGRAPHES ENNEMIS (GroundTelegraphFX,
ChargeLaneFX : "danger, sors de là"). Aucun effet du joueur ne doit les réutiliser.

**Why:** l'utilisateur a remarqué que l'explosion de Fragmentation ressemblait aux télégraphes ; l'ayant refaite avec des
formes propres (FXSprites.ShatterRing, CrystalFragment, Splinter, dans ArcaneBurstVFX), il a validé : "c'est bien mieux
comme ça".

**How to apply:** pour tout nouvel effet du joueur, créer ou réutiliser des formes cristal facettées (ShatterRing,
CrystalFragment, Splinter, CrystalShard), jamais un anneau lisse ou gradué. Ne pas proposer un "symbole commun" à partir
des télégraphes.
