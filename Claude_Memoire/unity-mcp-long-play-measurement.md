---
name: unity-mcp-long-play-measurement
description: "How to run and measure a long Play session through Unity MCP (frames advancing, logging, profiler drill-down) - used for the 2026-10-24 FPS investigation"
metadata:
  node_type: memory
  type: reference
  originSessionId: 330f2db8-4600-4ece-b773-321d7a8ff540
  modified: 2026-10-24T18:33:19.480Z
---

- Frames only advance reliably if `Application.runInBackground = true` is set from a RunCommand once in Play; focusing the Unity window via AppActivate is not dependable.
- Debug.Log lines from a probe MonoBehaviour defined inside a RunCommand did not show in GetConsoleLogs; write samples to a file in the scratchpad (`System.IO.File.AppendAllText`) and wait with a background bash `until grep ...` loop. Unity formats floats with a comma (`14,04`), so grep `[.,]`.
- `System.IO.File.Delete` inside a RunCommand is refused ("user interactions not supported"); delete files from bash instead.
- Realistic run: MainMenu Play → `SaveSystem.SuppressWrites = true` → `MainMenuManager.PlayGame()` → in Game: `AddExternalInvincibility`, apply all non-fusion upgrades via `LevelUpManager.Instance.AllUpgrades`, probe calls `LevelUpManager.Instance.SelectUpgrade(0)` every frame and moves the player on a circle.
- Profiler drill-down: `ProfilerDriver.profileEditor = true; enabled = true`, then aggregate `ProfilerDriver.GetHierarchyFrameDataView` item paths over ~30 frames - this is what exposed editor-only costs (EditorLoop) vs PlayerLoop.
- The player dies within ~15 s in Apocalyptic if set up after the scene loads: install a DontDestroyOnLoad hook MonoBehaviour from the menu that applies invincibility/build/probe on the first Game frame. `DebugSetRunTimer` jumps can end the run instantly - don't use them for perf runs.
- Spikes: have the probe scan every new profiler frame (`GetRawFrameDataView(i).frameTimeMs`) only when a >50 ms frame occurred; `Time.frameCount` ↔ profiler index mapping is unreliable. Remaining spikes after the fixes were all `EditorLoop` self time (editor-only, often at boss spawn/death), never game code.
- Findings 2026-10-24: incremental GC was OFF in Player Settings (GC freezes 250-360 ms every ~3 game-min) - enabled, needs an editor restart to apply in Play mode; uncapped XP gems (fixed by a 250 cap with merge) and the `com.unity.sequences` package (removed; ~20 ms/frame of editor-only cost growing with scene object count). See [[project_audit_version_finale]].
- Weapon/fusion power A/B (2026-10-27, Prismatic Crown): one Play session per config, same setup — `DebugSetRunTimer(200)` (Apocalyptic, pre-boss), invincible player, `WeaponBase`/`CrystalSystem` disabled, XP gems deactivated so no level-up pause, `GameInput.DebugMoveOverride` slow circle, timeScale 2, measure 60 game-seconds of HP removed (track `EnemyBase.CurrentHealth` drops + last HP of enemies that vanish/respawn). Run-to-run noise ~4% (973 vs 932). Fusions target ~2.2-2.9x the two maxed source weapons.
- RunCommand refuses `System.Reflection`. To tweak a private serialized field in Play (e.g. longer fusion cycle for a visual capture), use `new SerializedObject(component)` + `FindProperty("_field")` + `ApplyModifiedPropertiesWithoutUndo()`. Non-serialized fields stay out of reach: detect phases from public state instead (counts, visual transform scale).

Depuis le 2026-10-28, les gemmes d'XP sont des données (XPGemField) : dans les sondes, remplacer `foreach XPGem ... SetActive(false)` par `XPGemField.Instance.DebugClear()`.
