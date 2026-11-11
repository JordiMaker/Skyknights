# Redbaron para Unity

Kit para montar en Unity el mapa y la batalla del prototipo web Redbaron. El mapa mide 122 × 367 m, tiene 10 franjas y tres líneas, y su mitad sur es el reflejo de la norte. La batalla incluye infantería con coberturas, casamatas, puestos de mando y combate aéreo a ras de suelo.

- **Tu bando:** la Entente (sur), que controlas con **Quick RTS**.
- **IA:** lleva a las Potencias Centrales (norte).
- **Soldados:** Toon Soldiers WW2 demo.
- **Vegetación y rocas:** Low Poly Environment (Sky Den Games).
- **Terreno, pared de roca, agua, trincheras, puentes, aeródromos y bases:** los genera el kit en estilo low poly.
- **Aviones:** biplanos y triplano generados por el kit. Ver la sección Aviones para la física de vuelo de Air Battle 3D.

## Requisitos

- **Unity:** 2021.3 o 2022.3 LTS con **Built-in Render Pipeline**. Quick RTS solo es compatible con Built-in.
- **Input:** *Project Settings > Player > Active Input Handling* en **Input Manager (Old)** o **Both**. Los atajos del kit usan la API clásica `Input`.
- **Assets de la Asset Store**, importados desde el Package Manager (*My Assets*):
  - Quick RTS (GigiStudio) y sus dependencias: DOTween y Quick Outline.
  - Toon Soldiers WW2 demo.
  - Low Poly Environment (Sky Den Games).
  - Air Battle 3D – Free Game Template (opcional, ver Aviones).

## Montaje paso a paso

### 1. Copiar el kit y crear la escena

1. Copia la carpeta `Assets/Redbaron` dentro de la carpeta `Assets` de tu proyecto.
2. Abre una escena vacía y usa el menú **Redbaron > Crear escena de partida**.

El menú crea:
- Un objeto `Redbaron` con el constructor del mapa, las reglas de la partida, las oleadas y los aviones.
- Ajustes de luz, niebla y cámara.
- El material `Redbaron_VertexColor`.

### 2. Vegetación y rocas

Selecciona el objeto `Redbaron` y, en **RedbaronMapBuilder**, arrastra:

- **Tree Prefabs:** varios árboles del pack. Se escalan solos a 9–14 m de alto.
- **Rock Prefabs:** rocas. Se escalan a 1 m de alto para que sirvan de parapeto.
- **Dead Log Prefab:** un tronco tumbado del pack, si tiene. Sustituye a cada árbol que derribe un avión o una bomba. Si lo dejas vacío, se genera uno low poly.

Si dejas alguna lista vacía, el kit pone árboles o rocas low poly propios. Puedes ver el resultado sin darle a Play con **Redbaron > Vista previa del mapa**. La vista previa no se guarda en la escena: el mapa se reconstruye siempre al darle a Play.

### 3. Soldados (Toon Soldiers WW2 demo)

1. **Crea dos variantes del prefab del soldado** (clic derecho > *Create > Prefab Variant*): `Soldado_Centrales` y `Soldado_Entente`.
2. **En las dos variantes**, añade:
   - `NavMeshAgent`.
   - `SoldierAI`. Revisa los nombres de los parámetros del Animator: `Speed`, `Crouch`, `Shoot` y `Die`.
     - Abre el Animator Controller del soldado y pon los nombres que use.
     - Si alguno no existe, déjalo vacío.
     - Si no hay animación de muerte, el soldado cae de espaldas.
3. **Solo en `Soldado_Entente`**, añade el componente de unidad seleccionable de **Quick RTS**, el mismo que llevan sus unidades de ejemplo. Pon el objeto en la capa de unidades que use Quick RTS.
4. **En `WaveSpawner`** (dentro de `Redbaron`), asigna las dos variantes.

`Tint Uniforms` tiñe el uniforme gris para las Centrales y azul horizonte para la Entente, para distinguir los bandos aunque uses el mismo modelo.

`Soldier Height` escala el modelo a 1,8 m, que es la escala que usa todo el mapa.

### 4. Quick RTS

1. **Suelo:** en `RedbaronMapBuilder > Ground Layer`, pon el número de la capa que Quick RTS usa para hacer clic en el suelo. Suele llamarse *Ground*. Así el terreno, las trincheras y los puentes reciben las órdenes de movimiento.
2. **Cámara:** en la cámara, arrastra el script de cámara de Quick RTS a `RedbaronCameras > Rts Camera Scripts`. Así se desactiva mientras sigues al Fokker.
3. **Escena de ejemplo:** si vas a usar la escena de ejemplo de Quick RTS, borra su suelo y sus unidades de demostración.

**Cómo se conectan.** Cuando Quick RTS manda mover a un soldado de la Entente, cambia el destino de su `NavMeshAgent`. `SoldierAI` detecta ese destino nuevo y lo toma como tu orden: el soldado va corriendo, dispara por el camino con menos puntería, y al llegar se queda en esa zona, busca cobertura a menos de 6 m y dispara.

Si Quick RTS mueve las unidades sin `NavMeshAgent`, llama a `GetComponent<Redbaron.SoldierAI>().OrderMove(punto)` desde su orden de mover. Es una línea.

## Cómo se juega

- **Oleadas:** cada 28 s sale una oleada por bando, con 2 a 4 soldados por línea.
  - Las Centrales avanzan solas por sus líneas.
  - Tus soldados se reúnen delante de tu base y esperan órdenes.
- **Combate de infantería:**
  - Al ver al enemigo a menos de 50 m, cada soldado corre a la cobertura más cercana que lo proteja: roca, árbol, tronco caído, trinchera o cráter.
  - Tras unos segundos salta a otra cobertura más adelantada. Si su grupo supera en número al enemigo cercano, carga.
  - La probabilidad de acertarle se multiplica según dónde esté: trinchera ×0,32, roca ×0,38, tronco ×0,45, árbol ×0,55, cráter ×0,6, agachado al descubierto ×0,75, corriendo ×1.
  - Roca, árbol y tronco solo protegen si el tirador está al otro lado.
  - Las montañas y edificios tapan la línea de tiro, y disparar desde 3 m más alto da un 25 % más de acierto.
- **Casamatas:** disparan ráfagas a la infantería enemiga a menos de 42 m y aguantan 20 impactos.
- **Puesto de mando:** aguanta 150 impactos. Si cae, gana el otro bando y la batalla se reinicia a los 12 s.
- **Aviones:** el Fokker y el Albatros (Centrales) contra dos Camel (Entente).
  - Persiguen a los aviones enemigos y les disparan ráfagas lentas.
  - Sueltan una bomba sobre el aeródromo enemigo y la recargan al pasar por el suyo.
  - Una bomba mata a los soldados a menos de 6 m, daña las casamatas y derriba árboles.
  - Pueden estrellarse contra montañas, árboles, edificios o la pared. Reaparecen en su aeródromo.

### Teclas

| Tecla | Acción |
|---|---|
| 1 / 2 / 3 | Velocidad ×1 / ×2 / ×4 |
| G | Todas tus tropas avanzan solas por sus líneas |
| H | Todas tus tropas se quedan donde están y se cubren |
| F1 | Cámara de Quick RTS |
| F2 | Cámara detrás del Fokker |
| F3 | Cámara cenital siguiendo al Fokker (la rueda cambia la altura) |

## Aviones y Air Battle 3D

La ficha de Air Battle 3D en la Asset Store no publica sus scripts ni su documentación, así que el kit no llama directamente a su código.

La IA de los pilotos (`BiplaneAI`) decide en cada momento un **rumbo** y una **velocidad vertical**. Quien mueve el avión es un componente que implementa la interfaz `IFlightBody`. Por defecto se usa `KinematicFlight`: velocidad constante de 16,8 m/s, giro de 1,15 rad/s y ascenso de hasta 9,5 m/s.

Para usar la física de vuelo de Air Battle 3D:

1. Crea un componente que implemente `IFlightBody` en el objeto del piloto.
2. En `Steer(rumbo, velocidadVertical, dt)`, convierte la diferencia de rumbo en alabeo y guiñada, y la velocidad vertical en cabeceo. Pásale esos valores al script de control del avión de la plantilla, en lugar de los que ahora lee del jugador.
3. Devuelve en `Position`, `Yaw`, `VerticalSpeed` y `Speed` el estado real del avión de la plantilla.

Si me pasas el script de control del avión de Air Battle 3D (el que lee los controles del jugador), escribo ese adaptador.

## Ajustes útiles

| Dónde | Campo | Qué cambia |
|---|---|---|
| WaveSpawner | Wave Every, Min/Max Per Lane, Team Cap | Ritmo y tamaño de las oleadas |
| WaveSpawner | Player Auto Advance | Tus soldados avanzan solos como la IA |
| SoldierAI | Rifle Range, Rifle Cooldown, Base Hit | Alcance, cadencia y puntería de la infantería |
| Health (puesto de mando) | Max Hp | Duración de la partida (150 por defecto) |
| BiplaneAI | Clearance, Look Ahead | Lo bajo que vuela cada piloto y lo pronto que reacciona |
| BiplaneAI | Fire Cooldown, Gun Range | Cadencia y alcance de las ametralladoras |

## Archivos

| Carpeta | Contenido |
|---|---|
| `Scripts/Core` | Datos y función de altura del mapa, vida, línea de visión, trazadoras y explosiones |
| `Scripts/Map` | Constructor del mapa, coberturas, casamatas, puesto de mando, aeródromos y árboles destructibles |
| `Scripts/Units` | IA de infantería y oleadas |
| `Scripts/Air` | Biplanos, IA de pilotos, balas, bombas y restos |
| `Scripts/Game` | Reglas de la partida, panel y cámaras |
| `Scripts/Editor` | Menú Redbaron |
| `Shaders` | Shader low poly de color por vértice (Built-in) |

## Quick RTS integration (added 2026-11-11)

Built and tested in the QuickRTS project (Unity 6.6.5, Built-in Render Pipeline, Linear colour space). Scene: `Assets/Redbaron/Scenes/Redbaron_QuickRTS.unity`.

### What the kit now includes

| Where | What |
|---|---|
| `Integrations/QuickRTS/QuickRtsUnitBridge.cs` | Put it on the player's soldier prefab with Quick RTS's `Selectable` and `Mover`. It gives each soldier its own parent object, fills `Selectable`'s hidden fields before its `Awake`, keeps the info panel's health in step and removes fallen soldiers from the selection. |
| RedbaronMapBuilder → Ground Tag | Quick RTS only gives move orders on objects tagged `Terrain`. Set it to `Terrain`, and set Ground Layer to a layer in SelectorRTS's mask (the scene uses `Placeable`). |
| RedbaronGame → Speed Keys | Quick RTS uses 0–9 for control groups (Ctrl+number saves a group), so the scene uses F5 / F6 / F7 for ×1 / ×2 / ×4. |
| `RedbaronMenu.CreateSceneObjects()` | The same as *Redbaron > Crear escena de partida*, without the dialog, so scripts can call it. |
| `LowPolyVertexColor.shader` | Converts vertex colours in Linear colour space; before, the map looked washed out. |
| Unity 6.6 fixes | `GetInstanceID`, `FindObjectOfType` and `FindObjectsSortMode` replaced. |

### How the scene was built

1. A copy of Quick RTS's Example scene, without its floor, resources, demo units, fog of war, minimap, resource and action panels, and without its baked NavMesh. The kit bakes its own NavMesh at Play.
2. Prefabs `Assets/Redbaron/Prefabs/Soldado_Centrales` and `Soldado_Entente`: variants of the Toon Soldiers prefab with NavMeshAgent, CapsuleCollider, SoldierAI and the controller `Assets/Redbaron/Animation/Redbaron_Soldier.controller` (parameters Speed and Shoot). The Entente variant also has Selectable, Mover, Outline and the bridge, with tag and layer `Troop`.
3. Trees and rocks come from *Low Poly Environment – Nature Free* (Polytope Studio), the pack already downloaded, not the Sky Den Games one: PT_Pine_Tree_03_green, PT_Fruit_Tree_01_green, PT_Generic_Rock_01 and PT_Ore_Rock_01. Dead log: PT_Pine_Tree_03_logs.
4. Quick RTS camera: orthographic, 45°, height 110 m, zoom 6–34, limits set to the map.

### Known limits

- The Toon Soldiers demo has no death or crouch animation: soldiers fall on their back and stay standing in cover.
- The demo's material pointed to a shader that is not in the pack; it now uses Standard.
- Air Battle 3D is not connected; the planes use `KinematicFlight`.
- In Quick RTS's `ShortcutsSelectable`, Space (centre on the collection point) is guarded, because this scene has no collection point.

## Look: soft terrain and set dressing (added 2026-11-11)

The prototype coloured every terrain triangle at random, which read as a mosaic. RedbaronMapBuilder now has a **Look** section:

| Field | What it does |
|---|---|
| Smooth Terrain | Colours vary in soft patches (continuous noise and map zones) and gentle ground is smooth-shaded. Slopes steeper than Facet Slope (38°) keep flat facets, so mountains and banks read as rock. Trench berms take the ground's colour. Off = the prototype's look. |
| Dead Tree Prefabs, Dead Trees From Z | Groves beyond design Z 88 (near the river and the front) use shell-shattered trees. |
| Ground Cover Prefabs | Grass tufts on open ground, sparse in no man's land. Decoration only: no colliders, not in the NavMesh. |
| Mountain Rock Prefabs | Large rocks on mountain slopes. They block line of sight and planes, not the NavMesh. |
| Base Prop Prefabs | Barrels and crates around command posts and hangars. |

The battle map does not move: trees, rocks and craters keep their positions, because the decoration uses its own random stream. A plain ground frame is added around the map, so the horizon is not empty.

### Assets used in the QuickRTS scene

| Use | Prefabs |
|---|---|
| Trees | Happy Little Trees: Oak, Ash, Birch, Spruce_008, Poplar |
| Dead trees | Happy Little Trees: OakDead_006, SpruceDead, BirchDead_009, PoplarDead_001 |
| Fallen trunk (tree destroyed) | Happy Little Trees: OakClutter |
| Rocks (cover) and mountain rocks | Happy Little Trees: Rock, Rock_013 (+ Rock_004 on mountains) |
| Grass | Happy Little Trees: Grass_016, Grass_010, Grass_012; Low Poly Atmospheric Locations: grass, grass 2 |
| Base props | Low Poly Atmospheric Locations: barrel, barrel 2, box, box 2 |

Happy Little Trees is made for URP: its 34 materials were switched to Standard, keeping their textures and colours. Its Plant and Mushroom prefabs have no material and are not used.

### Importing Asset Store packages into this project

Air Battle 3D (a complete project) ships 18 ProjectSettings files, and Cartoon City ships a `Packages/manifest.json`. Importing them whole replaces Quick RTS's layers and tags, colour space and input settings. Untick `ProjectSettings` and `Packages` in the import dialog. If it happens again, restore the settings with `git checkout HEAD -- ProjectSettings/` while Unity is closed.

## Flying your plane (added 2026-11-11)

In the QuickRTS scene you fly the first Entente Camel ("Tu Camel") and your infantry advances on its own from the start (WaveSpawner → Player Auto Advance on). The camera starts behind your plane.

| Key | Action |
|---|---|
| ← → or A / D | Roll only. Full 360° rolls are possible. The bank stays when you let go; below 3° the wings level themselves. |
| ↑ ↓ or W / S | Speed up or slow down, between Min Speed (11 m/s) and Max Speed (26 m/s). |
| Z or Space | Fire. Aim assist aims at the enemy plane closest to the nose (within 14° and gun range), on the flight plane, and never at your own side. Set Aim Assist Angle to 0 to fire straight along the nose. |
| R / C / V | Quick RTS camera / behind your plane / top view over your plane (RedbaronCameras → Free Key, Chase Key, Top Key). C and V switch the camera to perspective; R restores the orthographic Quick RTS camera. |

- **Turning:** turn rate = Max Turn Rate × sin(bank), 65°/s by default. With level wings or upside down the plane flies straight; at 90° it turns hardest.
- **Altitude:** fixed. Every aircraft flies on one flight plane (see below) and never climbs or dives.
- **No piloting aids:** only your keys change the bank and the course. Nothing steers you away from the canyon walls: fly into one and you crash. The one automatic touch is the one you asked for: below 3° of bank, the wings level themselves when you let go.
- **Your bullets:** like every air-to-air round, they stay on the flight plane and hit enemy planes only.
- **In the free camera (R),** the arrow keys pan the Quick RTS camera, so only WASD fly the plane there.
- **Obstacle look-ahead** (planes) uses a 1.5 m sphere instead of a thin ray, so thin tree trunks are not missed.

All of this is on the plane's BiplaneAI component (Player control fields), AirWar → Player Flies Camel, and RedbaronCameras → Start Mode and Flight Fov.

## Plane models from other packs (added 2026-11-11)

Any pilot can fly a 3D model instead of the kit's built biplane: AirWar → Plane Models (one per pilot), or BiplaneAI → Visual Prefab. The model is used as a pure visual:

- Its own scripts, colliders, rigidbodies, animators, cameras, audio, trails and particle effects are removed before it wakes up. So is anything named `Sphere` (Air Battle's aim target), `Main Camera` or `Wheels` (landing gear).
- It is scaled to Model Span (7.5 m wingspan), centred on the pilot and given Team Tint (30% of the team colour: blue for the Entente, red for the Centrales).
- A child named like `propeller`, `prop`, `roter` or `rotor` spins as the propeller.
- If a model fails, that pilot falls back to the built biplane and the console says why.

Models in the QuickRTS scene:

| Pilot | Model |
|---|---|
| Tu Camel (you, Entente) | Air Battle 3D: PlayerPlane-1 (propeller fighter) |
| Camel (Entente AI) | Air Battle 3D: PlayerPlane-3 (twin jet) |
| Fokker (Centrales AI) | Zero Fighter (Klareh Games): zero.prefab |
| Albatros (Centrales AI) | Air Battle 3D: PlayerPlane-2 (stealth jet) |

Zero Fighter is made for URP: its Metal, Glass and Rubber materials were switched to Standard with their colour, normal and metallic maps. Leave AirWar's model fields empty to fly the original biplanes again.

## One flight plane and random models (added 2026-11-11)

- **Flight plane:** all aircraft fly at one height, and all air-to-air bullets travel horizontally at that height. Nobody climbs or dives; bullets spread sideways only. AirWar → Flight Altitude: a positive value = that height (the QuickRTS scene uses 10 m, so tree crowns and mountains cross it), 0 = automatic (just above the highest obstacle inside the map, plus Altitude Margin), negative = the prototype's terrain following.
- **Crashes:** tree crowns, mountains and the canyon walls that reach the flight plane. At 10 m, 5.4% of the map is solid at flight height.
- **AI pilots:** they still steer away from the walls, but they cannot climb.
- **Bombs:** these are air-to-ground and still fall. They are released earlier, as far ahead as they travel while falling from the flight plane.
- **Random models:** every spawn and respawn of every pilot picks a random model from AirWar → Plane Models, plus the kit's three built biplanes when Include Built Biplanes is on. QuickRTS scene: PlayerPlane-1, PlayerPlane-2, PlayerPlane-3, Zero, Camel, Fokker triplane, Albatros. The team shows in the tint, blue or red. The HUD shows the model you are flying.

## Combat on the flight plane (added 2026-11-11)

| Rule | Where to change it |
|---|---|
| Your fire rate is one burst every 0.4 s (was 0.16 s). | BiplaneAI → Player Fire Cooldown |
| Bullets are bigger: tracers 0.22 m wide and 3.5 m long, and they hit a plane within 2.4 m (was 1.4 m). | `Projectiles.BulletWidth`, `BulletTrail`, `BulletHitRadius` |
| No friendly fire: bullets only hit enemy planes, and bombs never hurt troops or bunkers of the bomber's own side. | — |
| Planes collide: two planes closer than 4.5 m both go down, whatever their side. | `BiplaneAI.CollisionRadius` |
| Friend or foe: a green triangle over friendly planes and a red one over enemy planes, drawn in your HUD. | — |
| Trees have solid crowns (a capsule named `Copa` over the foliage, narrower on dead trees). Planes crash into them and bullets stop on them; the infantry NavMesh ignores them. When a tree falls, its crown goes with it. | RedbaronMapBuilder → Canopy Colliders |

**AI pilots put not crashing first.** Every 0.1 s each AI pilot tries 24 headings: around its target, plus hard turns from its current heading, up to a U-turn. It flies each one ahead for 3 s at its real turn rate. Obstacles that reach the flight plane, other planes within 11 m and getting within 12 m of the canyon walls cost far more than not reaching the target, so it chases and attacks only along paths that do not crash. Obstacle heights come from a 1 m grid computed at the start and refreshed where a tree falls. In a 100 s test the AI did not crash once; its 5 losses were all shoot-downs. Frame time was about 8 ms.

Crashes at the map edge are now reported as "la pared de roca".

## Hangar ships, refill icon, bombs and energy (added 2026-11-11)

**Ships.** Only the aircraft from `Redbaron_Naves (1).zip` (the Redbaron hangar) fly now, picked at random on every spawn. They live in `Assets/Redbaron/Naves`:

| Group | Ships |
|---|---|
| Fighters | Halcon_de_Laton and _Curva, Vibora_de_Vapor and _Curva, Aguja, Mantis, Lechuza |
| Two-seater and seaplane | Corneja, Garza |
| Bombers | Leviatan and _Curva, Coloso |
| Gyrocopter and ornithopter | Libelula and _Curva, Murcielago |

- **Left out:** the airships and the balloon (Matriarca, Dorada, Bastion, Vigia). They are up to 75 m long and do not work as fighters in a 122 m canyon.
- **Scale:** 0.6, the hangar's game scale (AirWar → Fit Wingspan off, Model Scale 0.6). Collision and hit size follow each ship's size.
- **Changes to the copies in the project** (the originals in the zip are untouched):
  - In the OBJ files, `o` objects became `g` groups, because Unity's OBJ importer otherwise merges every part into one mesh.
  - The MTL `Kd` colours were converted (value^2.2, the original kept in a comment), because Unity reads them as linear and they came out washed out.
- **At runtime:**
  - Each moving part (`Helice_*`, `Rotor*`, `Ala_I`/`Ala_D`) gets a pivot at its centre (a wing at its root) and spins with the hangar's `GiroPiezas.cs`.
  - The `Equipo` / `EquipoOscuro` parts take the side's colours: red #A3271F / #7A1C16 for the Centrales, blue #2F4F8A / #22385F for the Entente.

**Flight plane:** 8 m (8.1% of the map is solid at that height).

**Respawn:** each pilot always comes back on its own side of its own airfield (AirWar assigns slot 0 = west end, 1 = east end), heading along the runway. It waits until that spot is clear.

**Energy, ammunition, bombs:**

| What | Value |
|---|---|
| Energy | 20 hits (5 times the old 4). |
| Ammunition | 80 rounds (2 per burst). |
| Your bombs | 4. X drops one; a yellow cross on screen marks where it would land. |
| Bomb damage | Kills enemy soldiers within 6 m; damages bunkers within 8 m (6 points of 20) and command posts within 9 m (12 points of 150). Never hurts your own side. |

**Refill icon:** a crate with a green cross inside a gold ring floats over the middle of each airfield, on the flight plane. A plane of that side flying through it gets its energy, ammunition and bombs back. The icon then shrinks for 6 s. AI pilots fly home to it when they run out of ammunition or drop below 35% energy.

**Bars:** an energy bar floats over every aircraft, yours included (green, then yellow, then red). The friend or foe triangle sits above it.

## Mobile controls and Android build (added 2026-11-11)

**Chase camera:** 15 m behind and 5.5 m above the plane, looking a little ahead of it (RedbaronCameras → Chase Back, Chase Up).

**Touch controls (Lean Touch).** `Integrations/LeanTouch/MobileFlightControls.cs` sits on the Redbaron object. It needs the free *Lean Touch* asset (Carlos Wilkes), imported into `Assets/Plugins/CW`, and adds a LeanTouch component itself if the scene has none. The look is Air Battle 3D's touch UI: its sprites `button (2)`, `button (5)` and `button (6)`. Without sprites, plain circles are drawn.

| Control | Where | Action |
|---|---|---|
| Joystick | left half of the screen | Left/right rolls; up/down speeds up or slows down. Both are proportional to how far you push, with a 15% dead zone. The base jumps under your thumb wherever you touch. |
| FUEGO | bottom right, large red button | Fire, held |
| BOMBA | next to FUEGO | Drop a bomb, one per tap |
| CÁM | top right | Cycle cameras: behind your plane → cockpit (first person) → top view → Quick RTS view |

- **Fingers:** several work at once (joystick plus fire, for instance).
- **Layout:** it respects the phone's safe area (notches) and scales with the screen (reference 1920 × 1080).
- **When it shows:** Show = Auto shows it on phones and tablets only. Always also shows it on desktop, where the mouse acts as a finger.
- **Quick RTS:** its selector is switched off in the flight cameras, so touches do not draw selection boxes.

**Android settings:**

| Setting | Value |
|---|---|
| Build target | Android |
| Package | com.redbaron.frente |
| Scripting backend | IL2CPP, ARM64 only (many recent phones no longer run 32-bit apps) |
| Orientation | Landscape, auto-rotating between both sides |
| Active Input Handling | Input Manager (Old). Android does not accept "Both", and nothing in the game uses the new Input System. The Editor applies it at its next start. |

The APK is `Builds/Android/Redbaron.apk` (31 MB). Build it again with File > Build Profiles > Android, or with the Pipeline `build run` command.

**Installing on a phone:**

1. Turn on Developer options: Settings > About phone, tap "Build number" seven times.
2. Turn on USB debugging: Settings > System > Developer options > USB debugging.
3. Connect the phone with a USB data cable and allow USB debugging from this computer.
4. Run `/Applications/Unity/Hub/Editor/6000.6.5f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb install -r Builds/Android/Redbaron.apk`.

Without a cable: copy the APK to the phone and open it, allowing installs from unknown sources.

## First-person camera (added 2026-11-11)

New camera mode **Cockpit**, on key F or in the CÁM button's cycle:

- **View:** from the cockpit, turning and banking with the plane, so the horizon tilts as you roll. Field of view is 70° (RedbaronCameras → Cockpit Fov, Cockpit Up).
- **Your own plane** is hidden in this view and shows again when you leave it.
- **Sight:** a small white cross marks where the nose points. The bomb sight, the friend-or-foe triangles and the other planes' bars stay.

Keyboard: R = Quick RTS view, C = behind your plane, F = cockpit, V = top view.

## Joystick, sounds, energy and ammo counter (added 2026-11-11)

**Energy:** each plane now takes 6 hits (BiplaneAI → Max Hp = 6, 30% of the previous 20). The refill icon over each airfield still restores it in full.

**Ultimate Joystick Lite (Format Games):**

- On the phone, the flight joystick is the package's DefaultJoystick prefab, placed bottom left by `Integrations/UltimateJoystick/UltimateJoystickInput.cs`. FUEGO, BOMBA and CÁM stay as they were. Disable that component to go back to the built-in joystick.
- The package's own axis is all-or-nothing: a unit direction, only past 90% of the travel. So the game reads how far the handle is pushed instead (handle position ÷ radius, from 0 to 1).
- Changes to the package's files in this project, so that it compiles in Unity 6.6 and in player builds:
  - `Joystick.cs` reads touches through `UnityEngine.Input`.
  - `Editor/Editor.cs` was removed.
  - `MenuManager.cs` and the `UnityEditor` parts of `JoystickTools.cs` are inside `#if UNITY_EDITOR`.
  - The missing scripts were removed from `DefaultJoystick.prefab`.

**Joystick sensitivity** (MobileFlightControls, applies to both joysticks):

| Field | Value | Effect |
|---|---|---|
| Dead Zone | 0.08 | Ignores the first 8% of travel, so resting a thumb does not roll the plane. |
| Response | 2 | Curve between push and roll: 1 = linear, 2 = gentle near the centre, still full at the edge. |
| Roll Sensitivity | 1 | Roll at full push compared with the arrow keys (1 = the same, 150°/s). |

With these values, half a push rolls at about 31°/s for fine corrections, three quarters at about 80°/s, and a full push at 150°/s for fast turns. Raise Response for finer control near the centre; lower it toward 1 for a more direct feel.

**Engine sounds** (from Vehicle - Essentials, set in AirWar):

| Use | Clip |
|---|---|
| Your plane | Vehicle_Car_Engine_2000_RPM_Front_Exterior_Loop |
| AI planes | Vehicle_Car_Engine_1000_RPM_Front_Exterior_Loop |
| Cockpit view | Vehicle_Car_Engine_Idle_Interior_Loop_02 |

`PlaneEngineAudio.cs` loops the clip while the plane is alive. On your plane the pitch follows your speed. AI planes are 3D sounds, louder as they pass close, each with a slightly different pitch.

**Battle sounds** (`Scripts/Core/Sfx.cs`): synthesised when the game starts, so no audio files are needed.

| Sound | When it plays |
|---|---|
| Aircraft machine gun | every burst from any plane |
| Rifle | every infantry shot |
| Bunker machine gun | every bunker burst |
| Metal hit | a round strikes a plane |
| Thud | a round strikes the ground, a tree or a rock, or damages a soldier, a bunker or a command post |
| Explosion, and big explosion | bombs, artillery, planes shot down, buildings destroyed |

All are 3D sounds from a pool of 32 voices. Each sound has a minimum gap between plays, so a whole battalion firing at once does not saturate the mix. To use recorded sounds, assign `Sfx.PlaneGunClip`, `RifleClip`, `BunkerGunClip`, `PlaneHitClip`, `ThudClip`, `ExplosionClip` or `BigExplosionClip` from any script; `Sfx.Volume` sets the overall level.

**Ammo counter:** on the right of the screen, a third of the way down. It shows your rounds left as a number and a 20-segment gauge (out of 80), and your bombs as dots (out of 4). The number turns orange at a quarter left and blinks red when empty. Fly through your airfield's refill icon to reload.
