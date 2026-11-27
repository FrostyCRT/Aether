---
name: unity-mcp-editor-step-for-timing
description: "Use UnityEditor.EditorApplication.Step() in RunCommand scripts to advance Play Mode by exactly one frame, deterministically — the reliable way to test timing/coroutines in this MCP-driven environment."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 40fe5eb3-6995-4ff7-8b66-5c6c1eab823b
  modified: 2026-09-17T17:15:16.564Z
---

In this Unity MCP setup (no human focus on the Editor window), `Time.frameCount`/`Time.time` barely advance between separate `RunCommand` tool calls, and how much real/simulated time elapses per call is unpredictable — this makes testing anything with a short duration (VFX timing, HUD animations, coroutine delays) unreliable if you just call something, wait for a separate tool call, and check state.

**Fix**: call `UnityEditor.EditorApplication.Step()` in a loop inside a single `RunCommand` script (Play Mode must already be running). Each call to `Step()` reliably advances the game by one real frame — confirmed via direct measurement: 50 steps advanced `Time.time` by ~1.0000s and a `PlayerBuffs.ConcentrationBonus` ramping at a known rate (0.05/s) advanced by exactly 0.05 over that span.

**How to apply**: whenever testing a timed effect (VFX duration, HUD countdown/animation, buff ramp rate, cooldowns), drive the frames yourself with a `for` loop of `EditorApplication.Step()` calls interleaved with state checks, all inside one script call, instead of relying on wall-clock time passing between separate tool invocations. This is far more precise and reproducible than the old approach of guessing how much time a gap between calls represents.
