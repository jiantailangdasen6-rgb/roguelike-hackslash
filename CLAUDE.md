# roguelike-hackslash

Top-down 2D survivors-like (Vampire Survivors-style auto attacks) × hack & slash loot × roguelite, online co-op up to 3 players.
Unity 6000.6.2f1 (URP 2D), C#, Netcode for GameObjects 2.13.3. Targets PC and mobile (landscape).
Design docs: `Docs/01_GameDesign.md` (game design, milestones), `Docs/02_TechnicalDesign.md` (architecture, sync model).
Communicate with the user in Japanese. The user directs from their phone; do the implementation and verification end-to-end without asking them to work in the Unity Editor.

## Architecture in one paragraph
All game logic runs on the host in `Game.Core.RunSimulation` (pure C#, no Unity/netcode deps — unit-test it in EditMode).
`Game.Gameplay.RunDirector` (scene NetworkObject) ticks it at 30 Hz, mirrors state into NetworkVariables, sends `SimEvent`s
(reliable named messages, see `EventCodec`) and enemy snapshots (unreliable, `SnapshotCodec`). Every peer, host included,
presents events via `RunDirector.EventReceived`. Player movement is owner-authoritative (`NetworkTransform` Owner mode +
`PlayerMover`). Loot is sent as a seed and rebuilt per client by `ItemGenerator`; each device keeps its own stash (`StashStore`).

## Layout
- `Assets/_Project/Scripts/Core` (`Game.Core`), `Gameplay` (`Game.Gameplay`: Net/View/UI/Session), `UI` (`Game.UI`), `Editor` (`Game.Editor`).
- `Editor/PrototypeBuilder.cs` generates placeholder art, `GameControls.inputactions` (only if missing), `Resources/NetPlayer.prefab`,
  `Resources/NetworkManager.prefab` and `Scenes/Lobby.unity` + `Scenes/Game.unity`. Change scenes/prefabs there and rebuild — don't hand-edit generated YAML.
- UI is built in code with `UIKit` (uGUI + OS font for Japanese).
- Use `GameRandom` (seeded), never `UnityEngine.Random`, for simulation randomness.

## Commands (batch mode — fail if the project is open in the Unity Editor)
```
powershell -File Tools/RunTests.ps1 -Platform EditMode
powershell -File Tools/RunTests.ps1 -Platform PlayMode
powershell -File Tools/RunMethod.ps1 -Method Game.EditorTools.PrototypeBuilder.Build
powershell -File Tools/RunMethod.ps1 -Method Game.EditorTools.PlayerBuild.BuildWindows
powershell -File Tools/NetTest.ps1          # host + 2 clients of the Windows build, bots, screenshots in Logs/net/
```
Player command-line flags (see `LobbyView`, `AutoTestReporter`): `-autosolo`, `-autohost <n>`, `-autojoin <ip>`, `-botmove`,
`-autoquit <s>`, `-screenshot <path> -screenshotat <s>`, `-profile <name>` (separate stash file), `-localonly` (host binds 127.0.0.1).

## Gotchas
- `UnityEngine.UI.SafeArea` exists in ugui 2.6 — don't create a same-named class.
- Engine modules are not listed by default in this template; add `com.unity.modules.*` to the manifest when an API is missing (e.g. ScreenCapture).
- NetworkObject ids (`GlobalObjectIdHash`) are only computed in `OnValidate`; the builder forces it via reflection — keep that when adding network prefabs/scene objects.
- Don't call `InputSystemUIInputModule.AssignDefaultActions()` in edit mode; it assigns defaults itself when enabled.
- Named messages over ~1.2 KB need chunking (Unreliable/ReliableSequenced are not fragmented).
