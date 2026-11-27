---
name: feedback_xp_gems_never_removed
description: "Les gemmes d'XP au sol ne doivent jamais être fusionnées, déplacées ni détruites ; pas de plafond, l'aimant passif reste"
metadata:
  node_type: memory
  type: feedback
  originSessionId: f76ab443-7a9d-4afa-9ab2-2c99702e0706
  modified: 2026-11-25T16:33:18.663Z
---

Aucune limite de gemmes d'XP sur la carte : jamais de fusion des gemmes lointaines, jamais de suppression. L'aimant passif (rayon 4 dès le niveau 3, XPGemSpawner.OnLevelUp) doit rester.

**Why:** le 2026-10-27, l'utilisateur a fait retirer le plafond MaxGemsOnGround (fusion/destruction des gemmes lointaines). Il fera lui-même les tests de performance. J'avais d'abord retiré l'aimant par erreur, croyant que c'était lui qui « déplaçait » les gemmes.

**How to apply:** pour un problème de FPS lié aux gemmes, optimiser le rendu (instancing, LOD…) ou proposer une solution, mais ne jamais réintroduire de plafond ou de fusion sans son accord. Voir [[unity-mcp-long-play-measurement]] pour les mesures FPS du 2026-10-24.

2026-10-28 : les gemmes ne sont plus des GameObjects mais des données dans XPGemField (un seul maillage trié, une boucle, traînées réutilisées, 64 max). Mesuré : 2 500 gemmes au sol = 114 -> 233 images/s (240 sans gemme), aucun GC ni image lente sur 20 s. Comportement et apparence inchangés (captures comparées). Pour les sondes de test : XPGemField.Instance.DebugClear() remplace la désactivation des GameObjects XPGem, XPGemField.Count remplace XPGem.ActiveCount.

3e passe (2026-11-25, partie réelle de l'utilisateur : 9 677 gemmes à 5:50, ~2 ms/image, coût qui grandissait toute la partie) : deux couches, shader Aether/XPGemField (matériau Resources/FX/M_XPGemField) qui anime flottement + respiration sur la carte graphique ; les gemmes posées sont dans un maillage trié reconstruit par lots (1 fois/s max, fusion triée), une gemme ramassée y est « éteinte » (échelle 0) ; les gemmes en mouvement dans un petit maillage refait chaque image. Mesuré : 15 000 gemmes ≈ 0,06 ms/image (Update compris), rebuild ~0,4 ms pour 10 000. Sondes : XPGemField.Instance.DebugValidate() (éditeur) vérifie la cohérence, marqueurs XPGemField.RebuildSettled / RenderMoving dans le profileur.

2e passe (2026-10-28) : seules les gemmes à l'écran sont triées/dessinées, tampon de sommets entrelacé, flottement calculé au rendu. Banc 9 000 gemmes sans plafond d'images : surcoût 2,85 ms -> 0,36 ms par image. Le jeu est plafonné à 240 images/s : pour mesurer un coût réel, mettre vSyncCount=0 et targetFrameRate=-1 dans la sonde (ne modifie pas QualitySettings.asset).
