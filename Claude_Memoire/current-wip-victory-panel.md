---
name: current-wip-victory-panel
description: "End-of-run screens, Mana Shield, Concentration and challenge HUD are all done and user-validated as of 2026-09-17 — current focus moved to CharacterSelectUI visual rework"
metadata: 
  node_type: memory
  type: project
  originSessionId: 40fe5eb3-6995-4ff7-8b66-5c6c1eab823b
  modified: 2026-09-17T17:15:06.428Z
---

As of 2026-09-17, per `NOTES.md` at repo root (read that directly for full detail — it's updated every session and will be more current than this memory):

**All previously-open systems below are now done AND visually validated by the user** — nothing left on any of them unless the user raises something new:
- VictoryPanel + GameOverPanel: both fully built and confirmed ("je le trouve vraiment bien et complet" / "le gameover panel est aussi validé").
- Bouclier de Mana (Kael): capsule verte transparente, flash réactif uniquement à l'absorption, repositionnée à Y=1.5 (le pivot du joueur est déjà à cette hauteur en jeu, pas au sol).
- Concentration (Aether): montée par palier (2/3/5 %/s), HUD avec descente animée du % à 0 sur un coup reçu (plus jamais le mot "Concentration" affiché).
- Challenge HUD/PausePanel: construit de bout en bout, auto-anchoring sur le texte réel.
- Nova (Cristal): dégâts synchronisés frame par frame avec le VFX (plus de dégâts instantanés désynchronisés du visuel), VFX plaqué au sol (Y=0.2, comme `WeaponMudPuddle._groundY`) au lieu de suivre le pivot du joueur.
- 3 petits bugs de longue date corrigés (2026-09-17) : `ObjectPool.ClearPool()` jamais appelé (projectiles ennemis volaient derrière l'écran de fin), texte Fragmentation 20% vs code 15%, `AbandonRun()` qui sautait `ChallengeManager.EvaluateAndApplyReward()` (un défi déjà réussi perdait son bonus d'Or si le joueur abandonnait au lieu de finir la run).

**Current focus (2026-09-17, just started)**: `CharacterSelectUI` (scène MainMenu, onglet "Personnage") jugé pas fini / pas beau par rapport aux autres onglets — voir 4 captures fournies par l'utilisateur. Ce qui est OK et à garder tel quel : le cadre du portrait et le visuel de personnage verrouillé (chaînes + cadenas doré). Ce qui ne va pas : fond en diagonale crème/vert-brun qui a l'air non fini, bouton "Réinitialiser" complètement non stylé (bouton HTML par défaut, aucune cohérence avec le style parchemin/bannières dorées utilisé partout ailleurs comme sur Victory/GameOver), bloc de texte à droite en aplat de couleur uni sans cadre/texture, mise en page globale qui manque de structure comparée aux autres onglets (Compétences/Accueil/Réputation/Paramètres, dont les boutons d'onglets en haut sont eux bien stylés bois+or). Chantier tout juste commencé, pas encore de plan concret posé avec l'utilisateur.

**Grands chantiers de design toujours ouverts** (voir NOTES.md section "Grands chantiers") : différencier les 3 persos dès les 30 premières secondes, casser la monotonie de structure (3 boss fixes), simplifier la méta-progression (Or/Éclats/Réputation qui se recoupent), arbres moins "+X%".
