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

## Main menu, missions and cards (added 2026-11-12)

The game now starts on a main menu (`Assets/Redbaron/Scenes/Redbaron_Menu.unity`, first in the build). Its title reads **SKYKNIGHTS**.

| Screen | What it shows |
|---|---|
| Main | MISIONES, NAVES, EDIFICIOS and SALIR (SALIR is hidden in the Editor), a hangar ship turning on the right, and the version (bottom right). |
| Misiones | The mission list and the selected mission's place and date, briefing, objective and battle. DESPEGAR loads it. |
| Naves | One card per hangar ship: a 3D model in its side's colours (drag to turn it), role, side, size, combat values and a description. |
| Edificios | Command post, bunker, hangar and refill point: a 3D model, toughness, bomb damage and what each one does. |

The phone's back button (Escape) returns to the main screen.

**Missions** are data assets in `Assets/Redbaron/Missions` (Create > Redbaron > Mission): title, place and date, briefing, objective, battle name and scene. For now the three missions load the same battle with the same rules. When a mission needs to differ (map, sides, waves, objectives), add the field to `MissionDefinition` and read it in the battle through `MissionSession.Current`. The battle panel shows the chosen mission's title.

**Cards** come from `Assets/Redbaron/Data/Redbaron_Catalogo.asset` (Create > Redbaron > Catalog), which holds each ship's model, side, role, size and description, and each building's values. All ships fly the same in the battle for now, so their cards share one set of combat values. The building cards draw the same geometry as the map, from `BuildingModels.cs`.

**Pause:** in the battle, the **II** button at the top of the screen, or Escape, pauses the game. Time and sound stop, and neither keys nor touch controls fly the plane. CONTINUAR resumes; MENÚ PRINCIPAL goes back to the menu.

**Rebuilding:** Redbaron > Crear menú principal writes the default catalog, the three missions and the menu scene again, adds the pause to the battle scene and sets the build's scene list. It overwrites edits made to the catalog and missions.

## Rock wall (added 2026-11-12)

The cliff around the map is now built from complete low-poly rock models instead of the generated strip, which showed holes where its faces turned away from the camera.

- **Rocks:** two overlapping tiers of Palmov Island "stone large" columns, scaled to reach the wall's rim (about 28 m), plus extra rocks at the four corners.
- **At the foot:** Happy Little Trees boulders.
- **On top:** a plateau.
- **Collisions:**
  - The generated wall is still there as an invisible collider, so planes and the NavMesh still stop at it.
  - The rocks have box colliders, which also work in phone builds. The mountain rocks now use box colliders too: their mesh colliders could not be built on the phone.

Settings in RedbaronMapBuilder → Boundary wall:

| Field | Default | Effect |
|---|---|---|
| Wall Rock Prefabs | stone large, stone large 2 | Rock models for the cliff. Empty = the old generated wall is drawn. |
| Wall Boulder Prefabs | Rock_004, Rock_013, Rock_005, Rock_002 | Boulders at the foot (decoration only). |
| Wall Rock Height | 13–17 m | Height of each lower-tier rock; the upper tier fills the rest. |
| Wall Rock Overlap | 0.55 | Distance between rocks as a share of a rock's width (below 1 they overlap). |
| Wall Boulder Count | 70 | |

The rocks use their own random sequence, so trees, craters and trenches stay where they were.

## Side colours (added 2026-11-12)

The hangar ships come from `Redbaron_Color (1).zip`, the Redbaron colour kit. Each ship is a single model, painted at runtime by the kit's `PaletaColor` script, which is in `Scripts/Color` with `TemaColor`.

**Colours by side.** TeamLivery picks the theme for each plane:

| Who | Theme | Insignia |
|---|---|---|
| Entente | Naval: navy blue #2F4F8A, light grey canvas | Roundel |
| Central Powers | Verde: green #3F6B3A, khaki canvas | Cross |
| Your plane | Barón Rojo: red #B32418 | Your side's roundel, in your side's colours |

- **Insignias:** every ship carries both insignias (`Insignia_Escarapela` and `Insignia_Cruz`), and the script switches on the one for its side.
- **Grain:** `PaletaColor` switches the materials to the kit's `Redbaron/LowPolyGrano` shader. Paint, metal, wood, canvas and stone each get their own grain. The shader is in Graphics Settings → Always Included Shaders, so it is also in the phone build.
- **Other places:** the menu cards show each ship in the colours of the side it was designed for. Flags, HUD bars and the side names in the menu use the same green and navy.

**Changes to the copies in the project.** These are the same as for the first hangar zip:
- The files keep their old names, so the scene and the catalog still find them. In the zip, the curved ships are named `X` and the straight ones `X_Recta`; in the project they are `X_Curva` and `X`.
- `o` objects became `g` groups.
- The MTL `Kd` colours were converted to linear (value^2.2, the original kept in a comment).

## Collisions (added 2026-11-12)

A collision no longer destroys a plane outright. This covers a tree crown, a mountain, a building, the rock wall and another plane.

- **Damage:** each collision costs 2 energy points (Collision Damage), and a plane goes down when its energy runs out.
- **Push:** the plane is pushed clear at 16 m/s, fading over 0.6 s (Push Speed, Push Time). The push only moves the plane, so you still have to turn away. While it lasts, the plane cannot move back into what it hit.
- **Immunity:** for 1.5 s after a collision (Collision Grace), further collisions do no damage. The plane is still pushed out of anything it touches, so it never passes through.
- **Mid-air:** in a collision between two planes, both are pushed apart and both lose energy.
- **Trees:** a tree that is hit still falls.
- **Log:** the battle log says "choca contra …" for each collision and "se estrella contra …" when one brings the plane down.

All fields are on BiplaneAI → Collisions.

## Anti-aircraft towers (added 2026-11-12)

Each side has an anti-aircraft tower beside its command post, at design position (64, 8) and its mirror. The model is `Torre_Flak` from the colour kit, in `Assets/Redbaron/Edificios`, painted in its side's colours.

- **Size:** AirWar builds the towers after the flight plane is known, scaled so that their top is 1.6 m below it (Flak Tower Clearance). Planes therefore fly over them, and the towers do not raise the flight plane.
- **Firing:** every 2.5 s the tower fires at the nearest enemy plane within 60 m that it can see. The turret turns at 80°/s and fires only when it points within 5° of the plane.
- **Bursts:** the shell bursts at the plane's distance at the moment of firing, with 2.5 m of scatter, on the flight plane. A plane whose edge is within 5 m of the burst loses one energy point.
- **No lead:** the tower aims at where the plane is, so a fast plane flying straight is usually gone before the burst, and a slow or turning one gets caught.
- **Damage:** a tower has 18 points, and a bomb does 6 within 8 m, so three bombs destroy it. A new battle restores it.

The settings are on AirWar → Anti-aircraft towers (model, spots, clearance) and on each tower's FlakTower component (range, cadence, shell speed, burst radius, scatter).

## Fire rates and no aim assist (added 2026-11-12)

**Fire rates.** Every gun fires 20 % more slowly, and plane rounds fly 20 % more slowly:

| Who | Now | Before | Field |
|---|---|---|---|
| Your plane | 1 burst every 0.5 s | 0.4 s | BiplaneAI → Player Fire Cooldown |
| AI pilots | 1 burst every 2.75 s | 2.2 s | BiplaneAI → Fire Cooldown |
| Plane rounds | 96 m/s plus the plane's speed | 120 m/s | BiplaneAI → Bullet Speed |
| Riflemen | 1 shot every 3.25 s, plus up to 1 s | 2.6 s, plus up to 0.8 s | SoldierAI → Rifle Cooldown (also in both soldier prefabs) |
| Bunkers | 1 burst every 1.75 s | 1.4 s | Bunker → Burst Every |

**No aim assist.** Nothing aims for anyone:

- **Your plane:** the rounds leave along the nose, with no correction towards a target. The yellow target box and the bomb sight are gone. The cockpit view keeps its small fixed sight on the nose.
- **AI pilots:** they point at where the enemy is now, without computing a lead. They drop their bomb when the enemy airfield is 6 m ahead of the nose, without working out the fall, so a bomb may land long.
- **Towers:** they do not lead their targets either.
- **Riflemen and bunkers:** they had no aim assist; they still hit by chance, depending on distance and cover.

One rule stays: everything in the air flies on one flight plane, so rounds and bursts never miss above or below.

Some earlier sections describe the previous rules: the aim assist in "Flying your plane", the yellow bomb cross, planes going down on any collision, and red for the Central Powers. The sections above replace them.

## Big-headed soldiers (added 2026-11-12)

The infantry wears the big-headed soldiers of the colour kit. The models are in `Assets/Redbaron/Soldados`: five troop types, each with the Adrian helmet (Entente) and the steel helmet (Central Powers). The mortar team is left out, because it has no legs to march with.

- **Choice:** each unit type in WaveSpawner → Unit Types has its models for each side (see "Tanks, mechs and unit types" below). A type with no models keeps the Toon Soldiers look.
- **How it is done:** the soldier prefabs are unchanged, so Quick RTS selection, the NavMesh agent and the collider still work. When a soldier spawns, UnitModels (formerly SoldierModels) removes the Toon Soldiers mesh, bones and Animator, and puts the kit model in their place, scaled to 1.8 m. It also gives Quick Outline the new meshes, so selected soldiers are still outlined.
- **Colours and draw calls:** each model is baked once per side into vertex-coloured meshes (body and both legs) in its side's theme. The Entente gets navy blue and the Central Powers green. Every soldier of that model and side shares those meshes and one material, `Materials/Redbaron_Soldados.mat` (LowPolyVertexColor with GPU instancing on). A soldier therefore costs three draw calls instead of one per material. The kit's grain shader is not used on soldiers: at their size it does not show.
- **Animation:** the kit's AndarMech swings the legs at walking pace (28° at the hip, 1.5 steps per second at full speed). With no Animator, a soldier that is killed topples onto its back in 0.45 s. It rises by the depth of its back and backpack, so it lies on the ground instead of sinking into it.
- **Import:** the soldier OBJs have Read/Write on, because their meshes are baked at runtime. They got the same two fixes as the ships: `o` → `g` and MTL `Kd` converted to linear.
- **Kit script change:** `PaletaColor.ColorFor` was added. It returns the colour a material takes in a theme.

## Tanks, mechs and unit types (added 2026-11-12)

The waves now bring eleven unit types: five infantry types, three tanks and three mechs. The vehicle models are in `Assets/Redbaron/Tanques` (Lince, Escarabajo, Mastodonte) and `Assets/Redbaron/Mechs` (Tetera, Zancudo, Caldera). They got the same import fixes as the soldiers.

**The roster (WaveSpawner → Unit Types).** Each type sets its models per side, units per lane, hit points, speed and weapon: range, cooldown, rounds per burst, hit chance, damage and blast radius. The defaults are in `UnitType.Defaults()` (Scripts/Units/UnitTypes.cs).

| Type | Model | Hit points | Weapon |
|---|---|---|---|
| Fusilero | Fusilero | 2 | Rifle, 50 m, one shot every 3.25 s |
| Ametrallador | Ametrallador | 2 | Machine gun, 45 m, bursts of four every 4.5 s |
| Granadero | Granadero | 2 | Grenades, 26 m, 2.5 m blast |
| Lanzallamas | Lanzallamas | 2 | Flamethrower, 12 m; closes in on anything within 25 m |
| Zapador | Zapador | 2 | Demolition charge with a 3 s fuse, 14 damage, 4 m blast; goes for the nearest enemy building within 90 m, then for vehicles |
| Tanque ligero | Lince | 28 | Turret cannon, 45 m, 2.5 m blast |
| Tanqueta lanzallamas | Escarabajo | 20 | Turret flamethrower, 14 m |
| Tanque pesado | Mastodonte | 50 | Hull cannon, 50 m, 3 m blast (it turns its hull to aim) |
| Mech ligero | Tetera | 16 | Grenade launcher, 34 m, 3 m blast; goes for men in cover |
| Mech explorador | Zancudo | 16 | Anti-aircraft gun, 60 m: flak at enemy planes first, a light shell against the ground; fast |
| Mech de asalto | Caldera | 34 | Cannon, 45 m, 3 m blast |

Grenadiers, flamethrower troops and sappers also carry a rifle for targets beyond their own weapon. Machine gunners and riflemen do not need one.

**Each weapon has its job.**
- Rifles and machine guns: infantry in the open. Against armour they are nearly useless, so they go for soldiers.
- Grenades and the grenade-launcher mech: men in cover. Fragments do a quarter of their damage to armour.
- Flamethrowers: cover and bunkers.
- Sappers: buildings. The sapper walks up to the nearest enemy building, sets a charge against the wall, runs about 9 m clear and the charge blows up three seconds later. The men around the charge see it and may run. With no building within 90 m it sticks the charge on an enemy vehicle, and the charge travels with it. Sappers are the only infantry that kills tanks.
- Cannons (tanks, assault mech): vehicles first, then buildings near, then infantry.
- The anti-aircraft mech: enemy planes within 60 m, with the same flak shells as the tower (FlakGun, shared). While a plane is in reach it does not fire at the ground. Its gun lifts up to 75°.

**Waves (WaveSpawner → Rotation, Lane Step).** Each wave, every lane sends the next entry of the rotation. Lane 2 starts 4 entries further along and lane 3 starts 8 entries further along. The default rotation alternates an infantry squad and a vehicle, and the vehicles alternate between tanks and mechs. With an even lane step, all three lanes send infantry in one wave and vehicles in the next. An infantry squad has 2–4 men, and a vehicle comes alone. A vehicle counts as three units towards the cap of 60 per side.

**Damage (Combat.Factor).** Armour is what makes a tank a tank: bullets do a tenth of their damage to vehicles, grenade fragments a quarter, and flames half. Shells, charges and bombs (DamageKind.Blast) do full damage to everything. Flames do half to buildings too. Blasts lose strength towards their edge, down to 30 % at the rim, and never hurt their own side, the same as the bombs. A bomb does 14 damage to a vehicle within 5 m.

**Troop bar (TroopBar, Scripts/Game).** A button per unit type, in the battle (mouse or finger) and in the RTS test screen. A press puts a squad of four infantry or one vehicle at your base, in the next lane, heading for the enemy command post. Each type then waits its own cooldown (UnitType → Recruit Every): 18 s for riflemen, 22–26 s for the other infantry, 36–60 s for vehicles; the button shows the seconds left and a yellow bar filling up. `cooldownScale` on the component speeds everything up or slows it down. The bar sits at the top of the screen, to the right of the game panel, and leaves the phone's camera button free. A new battle clears every cooldown.

**Behaviour.**
- Units pick the nearest enemy. A weapon that barely scratches armour (rifles, machine guns) prefers soldiers.
- With no enemy unit in sight (50 m), units attack bunkers, flak towers and hangars within 42 m, and the command post within 40 m.
- A unit whose weapon does not reach closes in. Vehicles never take cover. A tank turns its turret, and a mech its torso, before it fires. The torso turns up to 100° from the legs.
- The Mastodonte has no turret, so it turns its hull.
- Your Entente vehicles obey Quick RTS orders like your soldiers.

**How the models are built (UnitModels).**
- The kit's OBJ files put every part at the model origin, so the joints (hips, knees, arms, torso, turret, gun) are taken from the kit's GLB files and written into a table in UnitModels.
- Each part is baked once per model and side, like the soldiers.
- Wheels go into the hull, because they do not turn. Each side's insignia goes on the part it sits on.
- A tank costs three draw calls, and a mech six to nine.
- Mechs walk with the kit's AndarMech, and turrets and torsos turn with UnitAim.
- Vehicles taller than 6 m are scaled down to 6 m, so they stay below the planes' flight plane. That applies to the Zancudo and the Caldera.
- A knocked-out vehicle blows up, darkens and smokes for a few seconds. A mech slumps.

## Destructible structures (added 2026-11-12)

- **Hangars** now have 40 hit points and their own object (Hangar). A bomb within 3 m does 10 damage, so four bombs bring a hangar down. A destroyed hangar collapses. It is only a target: the planes keep taking off from the runway.
- **Bunkers, flak towers and command posts** can now be attacked by every unit type, not only by bombs.
- **Crates and barrels** at the bases, and the **barbed wire** in stretches of four spans, are breakable (Breakable). Any blast breaks them: bombs, shells, grenades, flames and charges.
- **New battle:** buildings, crates and wire all come back when a new battle starts.

Some things stay indestructible on purpose:
- the bridges and the ford, which carry the lanes;
- the trenches and the terrain;
- the rocks and the cliff around the map;
- the airfield's runway markings.

Trees still fall to bombs and to large blasts.

## Soldier AI: suppression, squads and danger (added 2026-11-12)

Every unit still advances along its lane, takes cover, moves up from cover to cover and fires its own weapon (see the sections above). On top of that, there are three tactics layers: suppression, squads and danger.

**Suppression (SoldierAI.Suppress).** Each soldier carries a value from 0 to 1.
- What adds to it:
  - a rifle shot at him: 0.15;
  - each machine-gun round at him: 0.12, plus 0.06 per round to everyone within 3 m;
  - a bunker round: 0.15;
  - a blast, within 1.8 times its radius: 0.4 at the centre, half at the edge, and 0.5 for flames;
  - a bomb, within 14 m: 0.7.
- In cover he takes 60 % of it.
- It wears off at 0.08 per second, or 0.16 in cover.
- What it does:
  - His hit chance drops by up to half.
  - Above 0.7, a charge breaks off, and a soldier closing in goes from cover to cover instead of running.
  - From 0.4 up, he does not move up from cover.
  - From 0.8 (pinned), he fires one and a half times more slowly.
- Vehicles ignore it.

**Squads (Squad, WaveSpawner → Squads).** The infantry of one lane and wave is a squad; vehicles are not in squads.
- **Focus fire:** the target picked by at least two men becomes the squad's, and the others prefer it.
- **Escort:** a friendly tank or mech of the same lane, within 70 m and not behind the squad, leads it.
  - The men run to catch it up, then walk behind it in rows of three.
  - The vehicle slows to a third of its speed when the nearest of them is more than 16 m away.
  - When the vehicle is gone, the men go back to their lane.
- **Falling back:** a squad of two or more falls back when either:
  - it has lost half its men (Break At Loss) and the enemy within 45 m outnumbers it (vehicles count as three); or
  - its average suppression reaches 0.65 (Break At Suppression) while it is outnumbered by more than 1.2 to 1.
- When it falls back, each man runs to cover within 25 m that is at least 6 m farther from the enemy (trenches first), or 15 m back down his lane. He fires from there.
- **Regrouping:** after 8 s it regroups for up to 14 s. If a fresh friendly squad of its lane comes within 16 m, it joins it. Otherwise it moves on with the men it has.
- Squads holding a position for the player never fall back on their own.

**Danger (Combat.WarnBlast, SoldierAI.Evade).**
- When a grenade is thrown, the men within its blast see it coming.
- When a plane drops a bomb, the men within 6 m of where it will land see it coming.
- Each of them has a 65 % chance (less when suppressed) to run to just outside the blast. He has the grenade's flight time (about 0.6–1 s) or the bomb's fall (about 1.3 s) to get there.

**Targets (SoldierAI.Priority).**
- The base is distance, divided by how much the weapon hurts that target (bullets prefer soldiers to armour).
- Multipliers:
  - the squad's target: 0.6;
  - wounded: 0.85;
  - a machine gunner, flamethrower or sapper within 30 m: 0.8;
  - for grenadiers and flamethrower troops, a man in cover: 0.7;
  - for everyone else, a man in cover: 1.15.

**Roles.**
- Machine gunners stay in cover 14 s before moving up (the others 6 s), and never charge.
- Grenadiers and flamethrower troops close in on men in cover and on bunkers within 40 m.
- Nobody charges with a squad that has lost a quarter of its men.

## RTS test screen (added 2026-11-12)

`Assets/Redbaron/Scenes/Redbaron_PruebaIA.unity` is a copy of the battle scene with no planes (AirWar disabled) and no phone controls, plus the **Banco_IA** object (AITestBench, Scripts/Testing). You command one side with the mouse; the other side is the normal AI. Open it and press Play. It is not in the build settings, so the APK does not include it.

**What happens by default.**
- Both bases send their waves (the **Oleadas** button turns them off).
- Your units, from the waves or recruited, head for the enemy command post along their lane. They fight whatever they meet, until you give them an order.

**Selecting.**
- Left-click a unit of yours.
- Drag a box to take every unit inside it.
- Shift adds to the selection, or takes a unit out of it.
- Double-click a unit to take every unit of that type in view.
- Ctrl+1–9 stores the selection as a group, and 1–9 recalls it. Press the number again to move the camera there.
- Clicking an enemy or a building shows it in the panel with a red ring; you cannot give it orders.

**Orders (SoldierAI.Command…).**

| Input | Order | What the units do |
|---|---|---|
| Right-click the ground | Move | They run there in a block, firing on the way without stopping, then hold the spot with their stance. |
| Right-click an enemy or building | Attack | They go after it whatever their stance, close in until their weapon reaches, and stay there once it is down. |
| A + click, or Ctrl + right-click | Advance fighting | They walk there and fight everything they meet (cover, closing in, charges), then hold the spot. |
| M + click | Move | The same as a right-click on the ground. |
| S | Stop | They hold where they are. |
| H | Hold position | They stop and never move again, not even to take cover; they fire at whatever their weapon reaches. |
| G | To the enemy command post | The default order: they head for the enemy command post and fight on the way. |

**Stances (SoldierAI.Stance).**
- **Aggressive** (default): holding a spot, the units go after enemies they see within 35 m of it, and come back when nothing is left.
- **Defensive:** they only fire at what their weapon reaches from where they are, taking cover close by; they never chase.
- **Hold position:** they do not move at all.
- An attack order is obeyed whatever the stance.

**Recruiting.** The buttons put any of the 11 unit types at your base, taking the three lanes in turn. Infantry comes in squads of 4 (Squad Size) and vehicles one at a time. Each type has the troop bar's cooldown (see "Troop bar"): the button shows the seconds left and stays off until then.

**On screen.**
- Green rings mark your selection, and a red ring the enemy or building you are looking at.
- Yellow lines are the NavMesh paths of the selected units, and red lines show what each one is aiming at.
- A green disc shows the cover or escape spot of a single selected unit.
- Coloured rings mark where an order was given: green for move, orange for advance fighting, red for attack.
- Labels give type, state and a life bar. They show for the selection by default; the button switches between selection, all and none.
- The bottom-left panel describes the selection. With one unit selected, it shows:
  - its state, order, stance, target and weapon;
  - its suppression and squad;
  - a log of its decisions.
- The top bar gives both command posts and unit counts, the game speed (also F5/F6/F7), the waves switch, labels, lines, **Bando** (switches sides and starts a new battle) and **Ayuda** (all the controls).

**Camera.**
- The arrows, the screen edges or dragging with the middle mouse button move it.
- The wheel zooms and Q/E turn it.
- Space centres it on the selection.
- The letters are orders, so WASD does not move the camera.

**Technical notes.**
- Quick RTS stays in the scene, because its units register with its managers. Its own selection, orders, shortcuts and camera are switched off (SelectorRTS, ShortcutsSelectable, RTSCameraController).
- The game's G/H keys for all troops (RedbaronGame → Global Order Keys) and its 1/2/3 speed keys (moved to F5–F7) are off here.

**About Low Poly Shooter Pack - Free Sample (Infima Games).** It was checked for this scene and not imported. It has no enemy AI: it is a first-person player controller with weapons, pop-up targets and explosive barrels. It needs the new Input System and Post Processing, and it ships its own ProjectSettings and Packages/manifest.json.

## Asset Store mechs as units (added 2026-11-13)

Two rigged mechs from the Asset Store fight alongside the kit's units: **Mech blindado** (Triplebrick, *The Assault Mech*, `Assets/Mech`) and **Mech táctico** (RetroStyle Games, *Stylized Sci Fi Mech Robot*, `Assets/RSG_UnderWater_Pack`). Neither package is in the repository.

**Rigged looks (UnitModels.ApplyAnimated).** A unit type whose model has a SkinnedMeshRenderer is not baked: the prefab or FBX is instanced under the unit, scaled down to Max Height, stripped of the pack's scripts and colliders, tinted with the side's flag colour (55 %, `_Color`/`_BaseColor` property block), and its Animator gets the type's **Animator** controller. `AnimatedLook` feeds the controller's `Speed` float with the agent's speed over the type's walking speed. The whole body turns towards the target (no turret). **Look Yaw** turns a model that does not face +Z. The two controllers (`Assets/Redbaron/Animation/Mech_Asalto.controller`, `Mech_ISO.controller`) are a 1D blend tree, idle at 0 and walk at 1 (the Assault Mech also runs at 1.8).

**What each package needed.**
- *The Assault Mech*: its FBX comes with no materials, so `Assets/Redbaron/Prefabs/P_MechBlindado.prefab` is the FBX with its `Mech_Metal` material. Its `PostProcessing` folder is the old stack v1 with the same GUIDs as `com.unity.postprocessing`; it was removed (the mech does not use it, its demo scenes do). Its demo scripts move the mech by root motion and are stripped at spawn.
- *Stylized Sci Fi Mech*: an HDRP package. `Assets/Redbaron/Animation/M_ISO_Mech_Standard.mat` is a Standard material from its textures (albedo, normal, HDRP mask map as metallic/smoothness, emission), set on its prefab's renderers. Its walk and idle clips come as separate FBX files; Loop Time is on.
- Both types are in `UnitType.Defaults()` and at the end of the default rotation; the scene rosters hold their models and controllers (set by a script, like the rest).

**Recovered after the imports.** Importing *The Assault Mech* and *Giant Mech Pilot* (`Assets/Robot&Pilot`) removed a file that defined the `InputManager` class the Ultimate Joystick Lite reads from (GUID collision); `Assets/UltimateJoystickLite/Core/Scripts/InputManager.cs` now stands in with a wrapper over `UnityEngine.Input`, and the joystick's custom inspector (`Core/Editor/Editor.cs`, which needed a `FormatGames.Editor` namespace that is gone too) was taken out.

**Player builds after the imports.** Two packages use Editor-only API in runtime scripts and broke the Android build: Candice AI for Games now sits in an Editor-only assembly (`Assets/Candice AI for Games/CandiceAI.EditorOnly.asmdef`, with references to TextMeshPro, UI, Input System, Cinemachine and Post Processing), so it is left out of players; the Ultimate Joystick's `MenuManager.cs` is wrapped in `#if UNITY_EDITOR`. The APK is about 74 MB now: the two mechs bring 4K textures.

**Statistics panel.** RedbaronGame's panel starts folded to a + button (Panel Open off) so it never covers the phone's joystick; − folds it back.

## Terrain and bridges sized for every unit (added 2026-11-13)

Every lane can be walked by every unit type, infantry and vehicles alike, and the map is built so.

**Two NavMeshes (RedbaronMapBuilder.BakeNavMesh).** The infantry mesh is baked as before (radius 0.4 m, height 1.8 m, slope 40°, step 0.45 m). A second mesh for the vehicles is baked from the same geometry with the builder's **Vehicle Agent Radius / Height / Slope / Climb** (2.7 m, 4.6 m, 32°, 0.9 m: half the width of the heavy tank plus a margin). `NavMesh.CreateSettings` gives it its own agent type, kept in `RedbaronMapBuilder.VehicleAgentTypeID`; WaveSpawner puts every vehicle's NavMeshAgent on it, and SoldierAI samples destinations on the unit's own mesh (8 m of reach for a vehicle). So a tank never plans a route through a gap, a trench or a slope its size cannot take.

**What the map does for them (RedbaronMap, RedbaronMapBuilder).**
- `RedbaronMap.LaneClear` (5.5 m) is the half-width kept clear along every lane: the ground is flattened that far from the lane line, and the trenches stop there with a 1.5 m ramp (before: 3.6 m and 1 m).
- The bridges are **Bridge Width** wide (9 m; before 5.5 m for the stone bridge and 3.6 m for the plank one), and the lane heights over them (`WalkHeight`) cover 4 design units each side.
- The ford of the west lane has two rows of stepping stones, and the three mountain passes are wider (Gaussian width 8 instead of 6.5 design units).

**Checking it.** `RedbaronMapBuilder.PathExists(a, b, agentTypeID, out length)` asks each mesh for a complete path. The test (scratchpad `lanes_test.cs`) runs it for the six lane runs on both meshes, then spawns one unit of each of the 11 types on each lane and watches them reach the enemy base.

## Lane orders: the flags (added 2026-11-13)

`Scripts/Game/LaneFlags.cs` holds the player's orders, one per lane, as three big flags in the side's colour with the lane's number on the cloth. RedbaronGame creates the object (**Banderas**) in every scene; WaveSpawner's Player Team says whose lanes they are.

**The two orders.**
- **Flag at the enemy command post** (where all three start, and where "Banderas al cuartel" or a new battle puts them back): the default. The lane's troops advance along it and destroy the enemy position, as before.
- **Flag anywhere else:** an intermediate objective. The lane's troops go to the flag, fighting whatever comes within reach on the way. Each unit has its own spot on the stretch (`SpreadPoint`: a fixed random place within Spread Side = ±14 m across the lane and Spread Along = ±16 m along it, half as wide for vehicles), so a squad spreads out instead of piling on the flag. Once there a unit holds like an aggressive player's unit: it fires at what its weapon reaches, chases only what comes within 35 m of its spot and comes back, and looks for a tactical spot within 18 m (`FlagCoverRadius`): a rock, a trench or high ground. With nothing in sight it already takes cover facing the enemy base.
- Cover scoring (SoldierAI.PickCover) now counts height: a spot up to 3 m above the enemy is worth that many metres of distance.
- After each shot (`seekBetter`) the soldier weighs the free cover spots around it with the same scoring and moves only to one clearly better than its own (2 m of score). A unit refused a shot three times for want of a line of fire (`losBlocked`) leaves its cover and moves 7 m (vehicles 10 m) towards its target, then settles again. `SoldierAI.Shots`, `RepositionTries`, `Repositions` and `LineMoves` count these for the tests.
- Move the flag and the lane moves with it on its next decision (half a second); back at the post, each unit picks up its lane at the nearest waypoint. Infantry does not escort a vehicle while a flag is set: everyone goes to their own spot.

**Moving a flag.** In the RTS test screen, a left click on the ground with nothing selected moves the flag of the nearest lane (by distance to the lane's path) to that point; the top bar's **Banderas al cuartel** returns all three. From code: `LaneFlags.Instance.Set(lane, worldPoint)`, `ResetAll()`, `NearestLane(point)`, `TryObjective(team, lane, out p)`. Switching sides in the test screen rebuilds the flags for the new side.

## Multiplayer FPS demo (added 2026-11-12)

`Assets/Redbaron/Scenes/Redbaron_Multijugador.unity` runs the Alteruna **Multiplayer FPS Template** (Asset Store, by Alteruna AB) on the Redbaron map: the battle scene with the planes, the waves and the Quick RTS managers switched off, plus the template's player, cameras, UI, score board and effects, the SDK's room menu and eight spawn points (**PuntosAparicion**, four on each half of the map). It is an Editor demo (mouse and keyboard) and is not in the build settings.

**What is in the repository.** Only our part: the scene, `Scripts/Multiplayer/RedbaronFpsDemo.cs`, the tags and layers, and the package entries. The template (`Assets/AlterunaFPS`) and the SDK are not: they are Alteruna's, under their EULA.

**Setting it up on another machine.**
1. The Alteruna Multiplayer SDK v2 comes from the Package Manager: **+ → Install package from git URL** → `https://github.com/Alteruna/multiplayer-sdk-unity-package.git` (the entry is already in `Packages/manifest.json`, so Unity fetches it when the project opens; git must be installed).
2. Import the template's **Assets/AlterunaFPS** folder from the Asset Store package. Import only that folder: the package also carries `ProjectSettings` and `Packages/manifest.json`, which overwrite Quick RTS's layers, tags and input, and an `Editor` DLL that is not needed. Skip `URP-Materials.unitypackage` (the project is Built-in).
3. Three changes to the template, all small:
   - `Scripts/CinemachineVirtualCameraInstance.cs`: `using Unity.Cinemachine;` instead of `using Cinemachine;`, with `#pragma warning disable 618`. Unity 6 ships Cinemachine 6, which keeps the old `CinemachineVirtualCamera` as obsolete, so the template's camera prefab still loads.
   - `Prefab/Player.prefab`: its objects on layers 6 and 7 go to **19 (Bullet)** and **20 (BulletIgnore)**, because 6 and 7 are Quick RTS's fog layers here; `BulletCollisionLayers` becomes every layer but Ignore Raycast, UI and BulletIgnore, so shots hit the map; `GroundLayers` adds Terrain (3) and Placeable (15), the layers of the Redbaron ground.
   - `Scripts/UI/ResizeController.cs`: a null check on the parent, because the Editor runs its Awake while the UI prefab is being placed.
   - `Scripts/UI/ScoreBoard.cs`: a missing score row (an offline player) returns quietly instead of logging an error.
   - `Scripts/Player/PlayerController.Camera.cs`: a `MouseSensitivity` factor on the mouse axes (the template turned 0.1° per pixel, ten times slower than usual) and the smoothing range widened to 0–1. RedbaronFpsDemo sets both on every player (Mouse Sensitivity 10, Camera Smoothing 0.9).
4. Tags `Player` and `CinemachineTarget`, layers 19 `Bullet` and 20 `BulletIgnore` (already in `ProjectSettings/TagManager.asset`).
5. Register the Alteruna licence: **Window → Alteruna → Register license**, with the project key from portal.mp2.alteruna.com. Without it the service refuses every connection ("Disconnected from server. Reason: [Failed]"), also on the LAN.

**The demo panel (RedbaronFpsDemo).** Top left: the connection state, and buttons for **Jugar solo** (the template's offline mode: the Player prefab with its Avatar component off and `PlayerController.Avatar` cleared, at a random spawn point; the Multiplayer object is disabled), **Crear sala**, **Buscar en LAN** and **Conectar a IP** (`MultiplayerManager.CreateRoom`, `JoinLan`, `DirectConnect`). While playing the cursor is locked and the panel hides; Escape opens the template's menu. The Multiplayer object spawns one avatar per player who joins a room, at the spawn points in turn.

**The battle around the players.** WaveSpawner is on in this scene with a wave every 60 s from each base, and both sides' AI advances on the enemy command post as in the battle scene (RedbaronGame restarts the battle when a post falls). The Quick RTS managers stay active because the spawned units register with them, with SelectorRTS, ShortcutsSelectable and RTSCameraController off and the Quick RTS camera disabled.

**Two damage systems, joined (FpsBridges).** The AI fights with Redbaron.Health; the template's players and bullets use AlterunaFPS.Health. Ten FPS points are one Redbaron point.
- `FpsPlayerBridge`, on each player: a Redbaron.Health the AI can see (registered in `Health.Fighters`, which SoldierAI.PickTarget reads as soldiers in the open) and shoot; every hit is passed on to the FPS health, and the FPS health is mirrored back each frame, so other players' bullets and respawns count too. The player's side is the half of the map it is standing on when it (re)spawns: its own side's AI leaves it alone.
- `FpsTargetBridge`, on every AI unit and building: an FPS health the pistol's raycast can hit; what it loses is passed to the Redbaron health as bullet damage, so armour applies (a tenth to vehicles).
- RedbaronFpsDemo adds both bridges every half second to whatever is missing one.

**Immortal.** DemoFPS → Immortal (on by default, also a toggle on the panel): your own player (the offline one, or the avatar you own) takes no damage: the AI's hits are dropped and its FPS health is kept full against other players' bullets.

**Controls.** WASD moves, the mouse aims, left click fires, right click zooms, R reloads, Space jumps, Shift sprints, V switches first and third person, Escape the menu.

## WWI map: POLYGON War Map dressing (added 2026-11-13)

`Assets/Redbaron/Scenes/Redbaron_WWI.unity` is the battle scene with the same lanes, bases, bunkers, water and bridges, dressed with Synty's **POLYGON War Map** pack (Asset Store, by Synty Studios; imported in `Assets/Synty`, which is not in the repository). It is mission 04, "Tierra de nadie", in the main menu, and in the build settings after the battle scene.

**How it works: MapDressing.** `Scripts/Map/MapDressing.cs` is a component that sits next to RedbaronMapBuilder and takes over parts of the build: `Configure` (trench profile and palette, before anything is built), `BuildTrenches`, `BuildWire`, `BuildCommandPost`, `BuildBunker`, `DressAirfield` and `Dress` (everything else, last), plus `NavMeshIgnoredGroups`. Each hook can decline, and the classic low-poly part is built instead. The battle scene has no dressing; the WWI scene has `WwiDressing`, whose prefab lists are set in the scene (nothing from the pack is referenced by the kit's code).

**What WwiDressing places.**
- *Trenches:* the terrain cut is deeper and narrower than the classic one (`RedbaronMap.SetTrenchProfile`: 2.1 m deep, 1.1 m half-width, ramp to 2.1 m, a 3.2 m ramp where a lane crosses, and the lines are straight: `TrenchZigzag` 0, because the 6.3 m wide pieces only chain on a straight line). One `SM_Env_Trench_Deep_Straight` piece per segment, sunk 0.45 m so its outer skirt meets the ground; every seventh piece is a dugout or an insert; fire steps, ladders and duckboards inside. The Trinchera CoverPoints sit on the terrain floor. A soldier in a trench fires from 2.75 m above the floor (`RedbaronMap.TrenchEye`), just over the parapet; the classic map keeps 1.55 m.
- *Wire:* `SM_Prop_Wire_Fence` pieces laid end to end along the wire lines, with tangles beside them, each one a Breakable like the classic stretches. The lanes stay open.
- *No man's land:* ten wrecks (tanks, planes, a gun) and a crashed airship (`SM_Veh_Blimp_01_Destroyed_01` at 60 %, between the centre and east lanes) as directional cover and obstacles; 36 dirt mounds and 14 sandbag barricades per side as low cover; water decals scaled into the larger shell holes.
- *Rear:* two artillery batteries per side behind the rear trench (two guns each, with crates and sacks), iron sheets and planks behind the trench lines.
- *Bunkers:* a machine-gun nest: sandbags in a U, the side's heavy machine gun on its tripod, four poles and ten corrugated iron sheets. Same box as the classic bunker, so Bunker and Health work unchanged.
- *Command post:* a 10 × 7.5 m hut of `SM_Bld_Base_Wall` pieces with a door towards the enemy, windows at the sides, ceiling tiles, three sandbag walls in front and the side's flag (built with MeshAccumulator as a child).
- *Airfields:* a parked plane (Fokker for the Central Powers, Sopwith for the Entente), a tank in reserve and two anti-aircraft guns per side, away from the runway and the flak tower.
- *Builder look fields* in the scene: Synty trees, dead trees, rocks, cliffs for the boundary wall, grass and base props; mud from design Z 92 instead of 106.

**Phone rules learned here.**
- Synty's meshes are not Read/Write. The runtime NavMesh bake cannot read such a mesh collider in a player, so every prop gets box colliders (`WwiDressing.Solid`) or none (`Decor`), and `RedbaronMapBuilder.PhoneSafeColliders` now swaps any non-readable mesh collider on trees, rocks and mountain rocks for a box. The trench pieces keep their mesh colliders for the line of fire but their group (`WWI_Trincheras`, with `WWI_Decorado`) is left out of the NavMesh: soldiers walk the terrain cut under them.
- Do not enable GPU instancing on Synty's materials: their Built-in Shader Graph shaders then draw nothing but shadows (every piece vanished until it was switched off again).
- The pack's shader graphs have Built-in and URP targets, so they work in this project without conversion; its demo scenes are not used.

**Lines of fire.** The first version halved the shooting: box colliders over a wrecked plane's whole bounds (3 m tall, 8.7 m wide) and 1.5–1.9 m barricades blocked the lines of fire across no man's land, and soldiers kept leaving cover to find one (`SoldierAI.LineMoves` 922 against 100 on the classic map after ten minutes at 4×). Now wrecks, guns, parked planes and the airship get hull-high boxes only (`SolidLow`, 1.2–2.5 m) and no CoverPoint, barricades are squashed to 1.1 m like the classic rocks, and the trench eye is 3.0 m. Same test afterwards: 854 shots and 414 line moves against the classic map's 1062 and 100; the rest comes from the deeper trench cut itself.

**Testing.** `scratchpad/wwi_probe.cs` (group counts, lane paths for both NavMeshes, trench covers reachable), `wwi_battle.cs` (states, trench cover use, shots, line moves) and `wwi_los.cs` (what blocks each shooter's line of fire, by map group). Redbaron > Vista previa del mapa builds the dressed map in the Editor without Play, which is the quickest way to look at it.
