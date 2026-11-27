---
name: unity-mcp-ui-screenshots
description: How to capture in-game/menu screenshots and measure text contrast through Unity MCP (used for the 2026-10-25 readability audit)
metadata:
  node_type: memory
  type: reference
  originSessionId: 330f2db8-4600-4ece-b773-321d7a8ff540
  modified: 2026-10-27T12:07:50.861Z
---

- In Play, `ScreenCapture.CaptureScreenshot(path)` from a RunCommand works (Game view is 1920x1080); the PNG is written at the end of a later rendered frame, so wait for the file (bash until-loop) before reading it. The editor in background renders slowly: a capture taken right after a UI change can show an OLD frame — re-capture if the FPS counter text is identical.
- RunCommand types can't be referenced by later commands, but a DontDestroyOnLoad helper MonoBehaviour can be driven later with `GameObject.Find("Name").SendMessage("Method", arg)` (SendMessage also reaches private methods, e.g. SkillNode.HandleClick, SettingsPage.Go).
- Contrast per text: dump every visible TextMeshProUGUI (screen rect, color, alpha incl. CanvasGroups, px size, font, outline) to JSON next to the capture, then a PIL script takes the background as the most frequent colour bin among pixels far from the text colour, inside the rect + an 8 px band. Do NOT exclude outline-coloured pixels (the outline is often the same colour as the background). PIL must be pip-installed into the scratchpad (`pip install --target pylib pillow`).
- Don't call `AssetDatabase.AllowAutoRefresh()` without a matching Disallow: it drives the counter negative and every refresh then asserts `m_DisallowAutoRefresh >= 0` (fixed by one `DisallowAutoRefresh()`).
- Play tests must start from MainMenu (open MainMenu.unity, EnterPlaymode, then `SceneManager.LoadScene("Game")` from the probe). Opening Game.unity directly leaves MetaProgressionManager null: WeaponBase/CrystalSystem throw in Start, the player never shoots and dies within seconds, and the death screen sets timeScale 0, so every capture looks frozen (player lying down). Make the player invincible as soon as the Player tag exists.
- A RunCommand containing `File.Delete` fails with "User interactions are not supported for MCP tool calls"; use new file names instead.
- Findings of the 2026-10-25 audit live in the conversation report; see also [[unity-mcp-long-play-measurement]].
