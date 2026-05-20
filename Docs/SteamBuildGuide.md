# Puzzle Core Ground Zero - Steam Desktop Build Guide

This Unity project now boots a complete desktop puzzle game from `Assets/Scripts/PuzzleCoreGame.cs`.
It uses the puzzle-core README as the product shape: ten procedural puzzle types, five difficulties,
and a deterministic endless run driven by a master seed.

## Build Targets

Use Unity's editor menu:

- `Build > Steam > Build Windows x64`
- `Build > Steam > Build macOS`
- `Build > Steam > Build Linux x64`
- `Build > Steam > Build All Desktop Targets`

The builds are written to `Builds/Steam/<platform>`.

For CI or batch mode, call one of these static methods:

```sh
Unity -batchmode -projectPath . -executeMethod GroundZero.PuzzleCore.Editor.SteamBuildPipeline.BuildAllFromCommandLine -quit
Unity -batchmode -projectPath . -executeMethod GroundZero.PuzzleCore.Editor.SteamBuildPipeline.BuildWindowsFromCommandLine -quit
Unity -batchmode -projectPath . -executeMethod GroundZero.PuzzleCore.Editor.SteamBuildPipeline.BuildMacFromCommandLine -quit
Unity -batchmode -projectPath . -executeMethod GroundZero.PuzzleCore.Editor.SteamBuildPipeline.BuildLinuxFromCommandLine -quit
```

The matching platform build support modules must be installed in Unity Hub.

## Steam Release Checklist

- Replace placeholder naming, capsule art, icons, and store metadata with final assets.
- Add the real Steam app ID before integrating Steamworks features.
- Create one SteamPipe depot per desktop platform.
- Test launched-through-Steam behavior on Windows, macOS, and Linux.
- Verify `PlayerPrefs` save persistence, fullscreen/window behavior, and mouse/keyboard input.
- Add achievements/cloud saves only after the game has a real Steam app ID and SDK wrapper.
