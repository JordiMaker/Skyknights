# Hangar Redbaron · naves low poly

Quince naves de la Primera Guerra Mundial con toques steampunk, en metros (1 unidad = 1 m), con el morro hacia +Z y el origen en el suelo bajo el centro del aparato.

| Archivo | Nave | Tamaño (ancho × alto × largo) | Triángulos | Piezas que giran |
|---|---|---|---|---|
| Halcon_de_Laton | Caza triplano (Potencias Centrales) | 7,5 × 2,7 × 6,8 m | 1.928 | Helice_1 |
| Vibora_de_Vapor | Caza biplano (Entente) | 8,6 × 3,1 × 7,7 m | 1.744 | Helice_1 |
| Leviatan | Bombardero cuatrimotor (Potencias Centrales) | 24,0 × 6,9 × 16,4 m | 3.824 | Helice_1 a Helice_4 |
| Matriarca | Zepelín de mando (Entente) | 18,8 × 21,1 × 74,6 m | 2.808 | Helice_1, Helice_2 |
| Libelula | Girocóptero de observación (Entente) | 9,1 × 3,6 × 10,1 m (rotor de 10,3 m) | 1.202 | Rotor, Helice_1 |

## Diez naves más (estilo curvo, una sola versión)

| Archivo | Nave | Tamaño (ancho × alto × largo) | Triángulos | Piezas animables |
|---|---|---|---|---|
| Aguja | Caza monoplano (Potencias Centrales) | 9,6 × 2,8 × 7,0 m | 1.976 | Helice_1 |
| Mantis | Caza empujador (Entente) | 8,6 × 2,9 × 8,7 m | 2.456 | Helice_1 |
| Lechuza | Caza nocturno con reflector (Potencias Centrales) | 8,2 × 3,7 × 6,9 m | 2.808 | Helice_1 |
| Corneja | Biplaza de reconocimiento (Entente) | 11,4 × 3,1 × 8,2 m | 2.724 | Helice_1 |
| Garza | Hidroavión de patrulla (Potencias Centrales) | 12,0 × 4,1 × 8,1 m | 2.596 | Helice_1 |
| Coloso | Bombardero trimotor (Entente) | 22,0 × 5,4 × 14,9 m | 3.852 | Helice_1 a Helice_3 |
| Vigia | Globo cautivo con torno de vapor (Entente) | 6,3 × 23,6 × 14,4 m | 1.496 | — |
| Dorada | Dirigible ligero (Potencias Centrales) | 9,2 × 11,4 × 29,4 m | 1.184 | Helice_1 |
| Murcielago | Ornitóptero experimental (Potencias Centrales) | 13,1 × 3,3 × 6,7 m | 2.192 | Ala_I, Ala_D (aleteo) |
| Bastion | Acorazado aéreo (Entente) | 26,7 × 17,1 × 37,7 m | 3.928 | Rotor_1 a Rotor_4, Helice_1, Helice_2 |

La Vigía incluye el carro con el torno y el cable: el globo flota a unos 20 m sobre el suelo. La Dorada tiene el origen en el suelo y su góndola queda a unos 3 m de altura.

## Versión curva

Cada nave tiene también una versión curva, con el sufijo `_Curva`. Tiene los mismos nombres de nodos y materiales y casi las mismas medidas, así que se puede cambiar una por otra sin tocar nada. Cambian las formas: fuselajes torneados, alas de puntas redondeadas o elípticas, timones curvos, góndolas en gota, tubos de vapor doblados y cúpulas.

| Archivo | Triángulos |
|---|---|
| Halcon_de_Laton_Curva | 3.208 |
| Vibora_de_Vapor_Curva | 2.944 |
| Leviatan_Curva | 6.052 |
| Matriarca_Curva | 4.340 |
| Libelula_Curva | 2.160 |

Cada nave viene en dos formatos:

- **.glb (recomendado):** conserva los materiales (color, metalizado y rugosidad), la transparencia de los cristales y las piezas que giran como nodos separados. En Unity 2021/2022 con Built-in, instala **glTFast** (Package Manager > Add package by name > `com.unity.cloud.gltfast`) y arrastra el .glb a Assets.
- **.obj + .mtl:** Unity los importa sin paquetes. Cada material del .mtl llega con su color. Las hélices y el rotor salen como objetos separados dentro del modelo.

Todas las caras son planas (cada triángulo tiene sus propios vértices), así que el aspecto low poly se mantiene con cualquier material.

## Color de bando

Las piezas pintadas con el color del bando usan los materiales **Equipo** y **EquipoOscuro**. Cambia esos dos materiales para pasar cualquier nave al otro bando:

- **Potencias Centrales:** rojo #A3271F / #7A1C16.
- **Entente:** azul #2F4F8A / #22385F.

## Hélices y rotor

Añade **GiroPiezas.cs** a la raíz del modelo. Gira las hélices (`Helice_*`, eje Z local) y los rotores (`Rotor`, `Rotor_*`, eje Y local), y bate las alas del Murciélago (`Ala_I`, `Ala_D`).

## Escala en el mapa Redbaron

- **Cazas:** el kit de Unity usaba los aviones al 60 % (escala de juego). Para que el Halcón y la Víbora encajen igual, pon la escala del objeto en 0,6.
- **Leviatán:** a tamaño real su envergadura (24 m) cubre una franja de lado a lado. Al 0,6 queda en unos 14 m.
- **Matriarca:** ya está a escala de juego (un zepelín real mediría el doble). Úsalo a 1.
