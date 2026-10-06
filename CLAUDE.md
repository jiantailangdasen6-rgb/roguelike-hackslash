# roguelike-hackslash

Top-down 2D hack & slash × roguelike. Unity 6000.6.2f1 (URP 2D), C#. Targets PC and mobile (landscape).
Design docs: `Docs/01_GameDesign.md` (game design, milestones M0–M6), `Docs/02_TechnicalDesign.md` (architecture).
Communicate with the user in Japanese. The user directs from their phone; do the implementation and verification end-to-end without asking them to work in the Unity Editor.

## Layout
- `Assets/_Project/Scripts/<Area>/` — one asmdef per area (`Game.Core`, `Game.Player`, `Game.UI`, `Game.Editor`). Namespaces `Game.<Area>`.
- `Assets/_Project/Scripts/Editor/M0SandboxBuilder.cs` — generates placeholder art, tiles, `GameControls.inputactions`, Player prefab and `Scenes/Dungeon.unity` from code. Scene/prefab changes go here, then rebuild — don't hand-edit the generated YAML.
- `Assets/Tests/EditMode`, `Assets/Tests/PlayMode` — PlayMode tests extend `InputTestFixture` and drive virtual devices.
- Touch controls are Input System On-Screen controls that emulate a Gamepad, so gameplay only reads `PlayerInputReader`.
- Use `Game.Core.GameRandom` (seeded), never `UnityEngine.Random`, for gameplay randomness.

## Commands (batch mode — fails if the project is open in the Unity Editor)
```
powershell -File Tools/RunTests.ps1 -Platform EditMode
powershell -File Tools/RunTests.ps1 -Platform PlayMode
```
Rebuild generated scene/assets, or capture `Logs/screenshot.png`:
```
"C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod Game.EditorTools.M0SandboxBuilder.Build -logFile Logs/batch-build.log
"C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod Game.EditorTools.SceneScreenshot.Capture -logFile Logs/batch-shot.log
```
Check the log for `error CS` / `Exception` after each run.

## Gotchas
- `UnityEngine.UI.SafeArea` exists in ugui 2.6 — don't create a same-named class.
- CompositeCollider2D paths are serialized; when building tilemaps in batch mode call `TilemapCollider2D.ProcessTilemapChanges()` + `CompositeCollider2D.GenerateGeometry()` before saving or walls have no collision.
