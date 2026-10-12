# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

`darkforest` is a Unity **2022.3.62f3** (LTS) project created from the 2D template (`com.unity.feature.2d`). It is an early-stage top-down game in the style of The Binding of Isaac. `Assets/Scenes/SampleScene.unity` is the single scene in Build Settings. Update this file as gameplay code and architecture take shape.

## Structure

- `Assets/Scripts/Player/PlayerMovement.cs` (`Darkforest.Player`): WASD 8-direction movement on a Rigidbody2D (gravity 0, rotation frozen, interpolated). Input is read in `Update` and velocity is set in `FixedUpdate` via `Vector2.MoveTowards`, using serialized `moveSpeed`/`acceleration`/`deceleration`. Exposes `MoveDirection`, `FacingDirection`, `Velocity` and `IsMoving` for the future shooting system. Arrow keys are reserved for shooting, which is not implemented yet.
- `Assets/Editor/PlayerSetup.cs` adds the **Darkforest/Create Player** menu, which is idempotent. It generates `Assets/Art/Placeholder/player.png` (32×32 sprite, Point filter, PPU 32), builds `Assets/Prefabs/Player.prefab` (SpriteRenderer, Rigidbody2D, CircleCollider2D, PlayerMovement), places it in SampleScene and sets the camera background dark. To run it from batchmode, use `-executeMethod Darkforest.EditorTools.PlayerSetup.CreatePlayer`.
- `Assets/Scripts/Camera/RoomCamera.cs` (`Darkforest.CameraSystem`), on Main Camera: an Isaac-style fixed view. 1 unit = 1 tile, and the camera always shows `viewWidth`×`viewHeight` = 17×9.5625 units (16:9, an Isaac 15×9 room plus margin). In Play mode it letterboxes non-16:9 windows with a hidden black camera. In Edit mode it only sets `orthographicSize`, so the viewport rect never gets saved into the scene. The **Darkforest/Setup Room Camera** menu (also run by Create Player) adds it.
- `Assets/Editor/RoomSetup.cs` adds the **Darkforest/Create Map** menu, which is idempotent and builds the basic stage map. The `Map` object (Grid + `RoomMap`, shifted by -0.5 so the start room is centered on the origin) holds a 7×7 grid of 7×7-floor rooms. Neighboring rooms share a 1-tile wall, so rooms repeat every 8 cells. The middle tile of every shared wall (the 4th tile of each side) is a 1-tile passage, and the outer walls are closed. There is one `Floor` tilemap (sorting -10) and one `Walls` tilemap (sorting -5) for the whole map. Walls use a TilemapCollider2D merged into a static CompositeCollider2D. The menu also removes the legacy single `Room` object and wires `RoomCameraFollow`. Tile sprites and Tile assets are generated into `Assets/Art/Tiles/` (32 px, PPU 32, Point filter).
- `Assets/Scripts/Room/RoomMap.cs` (`Darkforest.Rooms`) holds the room-grid math: `columns`, `rows`, `roomSize`, `startRoom` (3,3); `Pitch` = roomSize + 1; `RoomCenterCell` / `RoomCenterWorld`; `RoomOfCell`; `TryGetRoomAt` (true only on a room's floor, not in walls or passages); `HasPassage`.
- `Assets/Scripts/Camera/RoomCameraFollow.cs`, on Main Camera: Isaac-style room camera. It snaps to the start room, then slides (`slideDuration` 0.3 s) to a room's center once the player steps onto that room's floor. It raises `RoomChanged(old, new)`. With `maskOutsideRoom` on, four black bars parented to the camera hide everything outside a (roomSize+2)² window, so only the current room is visible.
- `Assets/Scripts/Room/TileHighlighter.cs` (`Darkforest.Rooms`) lives on `Map/Tile Highlight` (sorting -9). It draws a faint white outline (runtime-generated 32 px sprite, alpha 0.35) on the floor cell under the player, using `WorldToCell` and `HasTile`. It finds the player via `PlayerMovement` if `target` is unset, and exposes `CurrentCell`.
- `Assets/Scripts/Player/PlayerHealth.cs`, on the Player prefab, holds HP: `maxHealth` 3 (full, 2, 1, empty, matching the 4 life frames). `TakeDamage(int)` returns false while invincible or dead and gives `invincibleTime` 1 s of i-frames with sprite blinking. `Heal(int)`. Events: `HealthChanged(current, max)` (also raised in `Start`) and `Died`; on death `PlayerMovement` is disabled. Editor-only debug keys: K = damage, H = heal. Future monsters and traps should call `TakeDamage`.
- `Assets/Scripts/UI/HealthUI.cs` (`Darkforest.UI`) sits on `HUD/Life`. It maps the lost-HP fraction onto `stages` (life1 = full … life4 = empty). The user drew the art in `Assets/Art/Ui/life1-4.png` (64 px, imported as Point/PPU 64/uncompressed).
- `Assets/Editor/HudSetup.cs` adds the **Darkforest/Create Health UI** menu, which is idempotent. It sets up the life sprite imports, adds `PlayerHealth` to the prefab, and builds the `HUD` canvas. The canvas is Screen Space - Camera on Main Camera, so it stays inside the letterboxed 16:9 area. It uses sorting order 2000 (above the room mask) and a CanvasScaler with a 1920×1080 reference. The icon is 128 px (2×), anchored top-left with a 40 px margin.
- No `.asmdef` yet, so scripts compile into `Assembly-CSharp` / `Assembly-CSharp-Editor`.

Two editors are installed under `C:\Program Files\Unity\Hub\Editor\` (2022.3.62f3 and 6000.1.11f1). Always use **2022.3.62f3**. Opening the project in Unity 6 would upgrade it irreversibly.

## Key settings and packages

- Input: `activeInputHandler: 0`, meaning the legacy Input Manager (`UnityEngine.Input`). The new Input System package is not installed.
- Rendering: built-in render pipeline. URP is not installed.
- UI: uGUI + TextMeshPro. Timeline and Visual Scripting are also installed.
- Asset serialization is Force Text, so `.unity`, `.prefab` and `.asset` files are YAML and can be diffed. Avoid hand-editing scene/prefab YAML unless there's no alternative. Prefer changing C# code and letting the Editor handle scene wiring.
- Tests: `com.unity.test-framework` 1.1.33 (NUnit). No test assemblies exist yet.
- IDE: Visual Studio (`.vsconfig` → ManagedGame workload) and Rider integrations. `darkforest.sln` / `*.csproj` are generated by Unity, so don't edit them by hand.

## Working with Unity files

- Every file/folder under `Assets/` needs a matching `.meta` file holding its GUID. When creating assets from the CLI, let Unity generate the `.meta` on next import. When moving or renaming, move the `.meta` along with the asset, or references will break.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` and generated `.sln`/`.csproj` files are local/regenerated. Never edit or commit them. The project is a git repo (branch `main`). It has a Unity `.gitignore` and a `.gitattributes` that sends binary assets (png, psd, wav, mp3, fbx, …) through Git LFS. Keep `.meta` files tracked.
- Scripts compile only inside the Unity Editor (or batchmode). There is no standalone `dotnet build` workflow.

## Git workflow

After each completed change, commit locally with a clear English message: `Assets/` with its `.meta` files, plus `Packages/`, `ProjectSettings/` and docs. **Don't push after every change.** Run `git push origin main` only when the user sends `!작업종료` ("end of work"). Then report what was pushed. The remote is `https://github.com/yeonwoo9010-ship-it/darkforest` (private), and `gh` is logged in as `yeonwoo9010-ship-it`.

## Commands (batchmode, from the project root)

Close the Editor first, because batchmode can't open a project that's already open.

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe"

# Compile check (import + compile scripts, then quit). Inspect the log for "error CS".
"$UNITY" -batchmode -nographics -quit -projectPath . -logFile Logs/batch.log

# Run EditMode / PlayMode tests
"$UNITY" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml -logFile Logs/tests.log
"$UNITY" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml -logFile Logs/tests.log

# Run a single test or fixture (full name, regex supported)
"$UNITY" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter "Namespace.FixtureName.TestName" -testResults Logs/results.xml
```

Test assemblies need an `.asmdef` that references `UnityEngine.TestRunner` / `UnityEditor.TestRunner` and `nunit.framework.dll`. Use `Editor`-only include platforms for EditMode tests.
