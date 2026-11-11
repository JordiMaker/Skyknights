# Skyknights

A First World War battle game made in Unity. You fly a plane over the front line while the infantry of both sides advances on its own. Strafe and bomb the enemy, fight its planes, and land on your airfield's refill icon for fuel and ammunition. It plays on a computer keyboard and on Android phones.

In the code and the Unity folders the game is still called **Redbaron**, its working title.

Current version: **0.1** (prototype). See [CHANGELOG.md](CHANGELOG.md).

## What is in this repository

Only our own work:

| Path | Contents |
|---|---|
| `Assets/Redbaron/` | The game: scripts, the playable scene, prefabs, materials, shaders and the aircraft models (`Naves/`). |
| `Assets/QuickRTS-Fixes/` | A stand-in for a shader Quick RTS expects, plus its team materials. |
| `ProjectSettings/`, `Packages/` | Unity settings and the list of Unity packages. |
| `docs/LEEME.md` | The full game guide: rules, controls, settings and how each feature works. |

The Asset Store packages the game builds on are **not** included, because their licences do not allow redistribution. [THIRD_PARTY.md](THIRD_PARTY.md) lists them, says how to import them, and lists the small fixes to apply afterwards.

## Getting it running

1. Install **Unity 6000.6.5f1** with Android Build Support (for the phone build). The project uses the Built-in render pipeline in Linear colour space.
2. Clone the repository and open the folder in Unity Hub.
3. Unity reports compile errors because the third-party packages are missing. Choose **Ignore** (or enter Safe Mode and leave it) to reach the Editor.
4. Import the packages listed in [THIRD_PARTY.md](THIRD_PARTY.md) from Window > Package Manager > My Assets, following its import notes. **Untick `ProjectSettings` and `Packages`** when a package offers them, or it will overwrite this project's settings.
5. Apply the fixes listed in THIRD_PARTY.md.
6. Open `Assets/Redbaron/Scenes/Redbaron_QuickRTS.unity` and press Play.

## Controls

| Action | Keyboard | Phone |
|---|---|---|
| Roll left / right (the plane turns by banking) | Left / Right arrows | Joystick left / right |
| Speed up / slow down | Up / Down arrows | Joystick up / down |
| Fire | Z or Space | FUEGO |
| Drop a bomb | X | BOMBA |
| Camera: Quick RTS view, behind the plane, cockpit, top view | R, C, F, V | CÁM (cycles through them) |
| Game speed | F5, F6, F7 | none |

Details are in [docs/LEEME.md](docs/LEEME.md).

## Building for Android

The project is set up for Android: package `com.redbaron.frente`, IL2CPP, ARM64, landscape. Build from File > Build Profiles > Android into `Builds/Android/Redbaron.apk`, which git ignores. Install it with `adb install -r Builds/Android/Redbaron.apk`. docs/LEEME.md explains how to enable USB debugging on the phone.

## How we track improvements

- **One issue per improvement or bug.** Write it in any language, from the computer or the GitHub mobile app while playing: say what you saw, what you expected and, for bugs, what you were doing.
- **Labels** say which part of the game is affected (`flight`, `mobile`, `audio`, `AI`, `map`, `UI`, `build`) and where the issue stands:
  - `status: next`: chosen for the next version.
  - `status: in progress`: being worked on.
  - `status: test on phone`: done, waiting for a play test.
- **Milestones** group the issues of each version (`v0.2`, `v0.3`, …).
- **Commits** reference their issue, for example `Closes #12`, so the issue closes when the change lands.
- **Versions** are numbered in Player Settings (`bundleVersion`), tagged in git (`v0.2`) and summarised in CHANGELOG.md.

## Licence

No licence has been chosen yet, so all rights are reserved. Third-party packages keep their own licences.
