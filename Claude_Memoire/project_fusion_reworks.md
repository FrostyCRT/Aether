---
name: project_fusion_reworks
description: Fusion rework campaign (2026-10-27/28) - all fusions reworked and validated, shared method, Tripo3D models planned
metadata:
  type: project
---

The user asked (2026-10-27) to rework every fusion with the quality of the recent ones: new gameplay mechanic, own crystal-style visuals, measured at x2.2-2.9 the two maxed source weapons, 9 translations.

- Done and validated by the user: Terre Calcinée -> Pluie de Météores (ScorchedEarth), Tourbière Magnétique (CursedSwamp), Couronne Prismatique (ManaVortex, "best fusion in the game").
- Done 2026-10-27: Constellation Orageuse (ThunderingOrbs id kept) - orbs ionize enemies, chain lightning prefers ionized ones, periodic Surge strikes all ionized enemies along a spanning tree. Measured x2.4-2.6. The user moved on to the next fusion without objection.
- Done 2026-10-28, waiting for the user's in-game test: Valse des Lames (RicochetBlades id kept, was "Lames Ricochet") - fan salvo of crystal blades that bounce twice off the screen edges (growing), then fly back to the player; caught blades charge a 360° Kaléidoscope salvo. Measured x2.2-2.8.
- Done 2026-10-28, waiting for the user's in-game test: Sillage Boueux polish pass (MudOrb) - same mechanic, own crystal orbs (no BouncingOrbProjectile, fixed orbs dragged along the screen edge), mud blots with azure shards instead of the prefab disc (MudPuddleZone.HideVisual), first real measurement x2.5.
- 2026-10-28: the user validated ALL reworked fusions. Campaign closed.
- Sillage Boueux visuals judged too weak by the user, but they will model the big elements (bouncing orb etc.) themselves with Tripo3D (AI 3D modelling). When a model arrives, plug it in place of the code-built mesh (MudWakeVFX.cs OrbMesh) rather than polishing the procedural one.
- One fusion at a time; the user tests each in game before the next.

**Why:** the old fusions were "source weapon + small bonus", with no new gameplay.
**How to apply:** keep class names and fusion ids (asset + saves reference them), keep A/E for orbital-based fusions, and use the A/B method in [[unity-mcp-long-play-measurement]]. If either side kills the horde as fast as it spawns (many kills, enemy count below its cap), divide damage by 3 or 6 on both sides: the ratio stays valid. Never push the run timer past 5:00 (boss spawn pauses the horde). Follow [[project_vfx_visual_grammar]].
