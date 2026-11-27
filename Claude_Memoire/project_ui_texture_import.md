---
name: project_ui_texture_import
description: "Images UI affichées en petit = mipmaps + trilinéaire + compression HQ, sinon flou/grain (curseur, icônes, 2026-10-26)"
metadata:
  node_type: memory
  type: project
  originSessionId: 330f2db8-4600-4ece-b773-321d7a8ff540
  modified: 2026-10-26T17:12:21.342Z
---

Toute image d'UI affichée nettement plus petite que sa source (rapport ≥ 2) doit être importée avec mipmaps activés,
filtrage Trilinear et compression CompressedHQ (BC7) au lieu de la compression normale (DXT5). Sans mipmaps, une icône
d'environ 1 400 px affichée en 75 px paraît granuleuse et floue. L'utilisateur l'a remarqué deux fois : le curseur, puis
les icônes des écrans de fin.

**Why:** le 2026-10-26, 17 icônes de compétence (taille max passée à 1 024 px) et 55 autres images ont été corrigées
(arbre de compétences, logos, cadenas, Éclat, pastilles UI_Circle, portraits, parchemins de cartes, HUD). Les fonds
plein écran (rapport ≈ 1) n'en ont pas besoin.

**How to apply:** pour chaque nouvelle image d'UI ajoutée au projet, vérifier son rapport taille source / taille
affichée, et appliquer ces réglages d'import si elle est réduite.
