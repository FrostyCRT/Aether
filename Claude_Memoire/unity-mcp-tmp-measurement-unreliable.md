---
name: unity-mcp-tmp-measurement-unreliable
description: TextMeshPro glyph-level measurement APIs give stale/wrong results when called repeatedly inside one Unity MCP RunCommand script
metadata: 
  node_type: memory
  type: project
  originSessionId: 40fe5eb3-6995-4ff7-8b66-5c6c1eab823b
  modified: 2026-09-14T18:44:48.112Z
---

Inside a single `mcp__unity-mcp__Unity_RunCommand` script, calling `TMP_Text.GetPreferredValues()`, `.textBounds`, or `.textInfo.lineInfo[i].width` more than once in a row (e.g. changing `fontSize` or `.text` and re-measuring in a loop) returns stale/frozen values — even with `ForceMeshUpdate(true, true)`, `SetAllDirty()`, or `LayoutRebuilder.ForceRebuildLayoutImmediate()` in between. Confirmed by getting the exact same width for multiple genuinely-different strings in one script call.

`RectTransform.GetWorldCorners()` / `anchoredPosition` / `sizeDelta`, by contrast, update correctly and immediately within the same script — these are plain Transform data, not TMP's internal mesh cache.

**How to apply:** For layout verification involving TMP text, trust `RectTransform`-based box measurements (`GetWorldCorners`, `anchoredPosition`, `sizeDelta`) done via RunCommand. Don't trust glyph-level TMP measurement (`GetPreferredValues`, `textBounds`, `lineInfo`) for more than one read per RunCommand call — if multiple text/fontSize variants need checking, either split each into its own separate RunCommand call, or better, delegate font-fit decisions to TMP's own built-in `enableAutoSizing` (which works correctly through the normal OnEnable/Canvas pipeline) rather than hand-rolling a bisection loop that re-measures via these APIs.

Also: entering/reloading a scene via `SceneManager.LoadScene` while in Play Mode resets any component field wired only via `SerializedObject` during that same Play session (e.g. re-wiring a `[SerializeField]` reference at runtime) — such wiring must be done in Edit Mode and saved (`EditorSceneManager.SaveScene`) to survive a scene reload, same as any other scene edit. See [[current-wip-victory-panel]] for the broader Play-Mode-revert pattern this project already tracks.
