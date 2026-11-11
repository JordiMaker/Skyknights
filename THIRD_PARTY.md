# Third-party packages

The game uses the packages below. They are not in this repository, because their licences do not allow redistribution. Each person who works on the project imports them from their own Unity Asset Store account. All of them are free.

Unity keeps the same asset IDs when a package is imported, so the scene and prefabs find these assets again by themselves.

## Packages to import

| Package | Publisher | Folder after import | Used for | Import notes |
|---|---|---|---|---|
| Quick RTS | GigiStudio | `Assets/RTS` | Unit selection, orders, RTS camera, team managers | **Untick `ProjectSettings` and `Packages/manifest.json`**: the package ships its own and would overwrite ours. |
| DOTween (HOTween v2) | Demigiant | `Assets/Plugins/Demigiant` | Animations Quick RTS needs | After import: Tools > Demigiant > DOTween Utility Panel > Setup DOTween. |
| Quick Outline | Chris Nolet | `Assets/QuickOutline` | Selection outlines in Quick RTS | none |
| Toon Soldiers WW2 demo | Polygon Blacksmith | `Assets/ToonSoldiers_WW2_demo` | Infantry model and animations | none |
| Low poly trees - free nature pack (Happy Little Trees) | Nebula | `Assets/Happy Little Trees - Free nature pack by Nebula` | Trees, dead trees and grass | Its materials are URP; convert them (see below). |
| Low Poly Atmospheric Locations Pack | Palmov Island | `Assets/Palmov Island` | Barrels, boxes and other base props | none |
| Lean Touch | Carlos Wilkes | `Assets/Plugins/CW` | Reading touches for the phone controls | none |
| Ultimate Joystick Lite | Format Games | `Assets/UltimateJoystickLite` | The phone's flight joystick | Needs four fixes (see below). |
| Vehicle - Essentials | NoxSound | `Assets/Vehicle_Essentials` | Engine sounds | Only three clips are used (see below). |
| Air Battle 3D - Free Game Template | Black Rose Developers | `Assets/AirBattle` | Round button sprites for the phone controls | **A complete project**: import only `Art/Sprites/Input/button (2).png`, `button (5).png` and `button (6).png`. Importing all of it overwrites the project's layers, tags, colour space and input settings. |

The project also contains other packs that the game does not use today: HQ Explosions Pack FREE, Zero Fighter, Polytope Studio's Low Poly Environment, and ithappy's Military and Cartoon City packs. You do not need them.

## Fixes to apply after importing

### Quick RTS

`Assets/RTS/Scripts/RTSController/Managers/ShortcutsSelectable.cs`: holding Space throws an error every frame in scenes without a collection point, and Space is the fire key. In `Update`, change the condition

```csharp
if (Input.GetKey(KeyCode.Space))
```

to

```csharp
if (Input.GetKey(KeyCode.Space) && TeamManager.Instance.CollectionsPointsPerGroup[SelectorRTS.Instance._group].Count > 0)
```

### Ultimate Joystick Lite

Without these fixes it does not compile in Unity 6.6 or in a phone build.

1. `Core/Scripts/Joystick.cs`: the package calls an `InputManager` class it does not ship. Add this line under the other `using` lines:
   ```csharp
   using InputManager = UnityEngine.Input;
   ```
2. Delete `Core/Editor/Editor.cs`.
3. `Core/Scripts/Extensions/MenuManager.cs`: wrap the whole file in `#if UNITY_EDITOR` … `#endif`.
4. `Core/Scripts/Extensions/JoystickTools.cs`: wrap `using UnityEditor;` and the code that uses it in `#if UNITY_EDITOR` … `#endif`.
5. Open `Prefab/DefaultJoystick.prefab` and remove its missing-script components.

### Happy Little Trees

Its 34 materials use URP shaders and show pink in the Built-in pipeline. For each material, switch the shader to **Standard**. Then put the texture that was in `_BaseMap` into Albedo (`_MainTex`), and the colour that was in `_BaseColor` into the Albedo colour (`_Color`).

Read both values before changing the shader: once the shader is switched, the Inspector no longer shows the old properties. They are still in the material file under `m_SavedProperties`.

### Vehicle - Essentials

The scene's `AirWar` component uses these clips from `Vehicle_Car/Vehicle_Car_Engine`:

| Field | Clip |
|---|---|
| Player Engine | `Vehicle_Car_Engine_2000_RPM_Front_Exterior_Loop` |
| Ai Engine | `Vehicle_Car_Engine_1000_RPM_Front_Exterior_Loop` |
| Cockpit Engine | `Vehicle_Car_Engine_Idle_Interior_Loop_02` |

They are linked by asset ID and reconnect on import; this table is for checking.
