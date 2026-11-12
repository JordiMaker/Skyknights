# Changelog

## 0.2 (in progress)

**Menu**
- The game starts on a main menu with missions, aircraft cards and building cards (#9).
- Three missions, each its own data asset. For now they all play the same battle.
- Cards with a 3D model you can turn, and each ship's or building's values.
- Version number on the main screen and in the pause panel (#6).
- Pause in the battle (II button or Escape), with Continue and Main menu.

**Map**
- The cliff around the map is built from complete low-poly rock models, with no more see-through gaps (#8).
- Mountain rocks keep their collisions in the phone build (#7).
- An anti-aircraft tower stands beside each side's command post. It fires flak at enemy planes in reach. Planes fly over it, and bombs destroy it.

**Colours**
- The hangar ships come from the Redbaron colour kit. Each ship has colour zones and carries both insignias.
- The Entente flies in navy blue with roundels, and the Central Powers in green with crosses.
- Your own plane is red, with your side's roundels.
- Paint, metal, wood, canvas and stone have a grain texture, so the models no longer look like plastic.
- The Central Powers' flags, HUD bars and menu cards are green now. The Entente keeps its blue.

**Ground forces**
- The soldiers are the big-headed soldiers of the Redbaron colour kit. The Entente wears the Adrian helmet and navy blue, and the Central Powers the steel helmet and green.
- They march with the kit's walking animation. When killed, they topple onto their back in half a second and lie on the ground.
- Tanks and mechs join the waves: a light tank, a flamethrower tankette, a heavy tank, a light mech, a scout mech and an assault mech, painted in their side's colours.
- The waves take turns: each lane sends an infantry squad, then a vehicle, then another squad. Infantry squads rotate through riflemen, grenadiers, machine gunners, flamethrower troops and sappers, and vehicles alternate between tanks and mechs.
- Each type fights with its own weapon:
  - riflemen fire single shots;
  - machine gunners fire bursts of four;
  - grenadiers throw grenades up to 26 m;
  - flamethrower troops close in to 12 m and burn through cover;
  - sappers go for buildings and vehicles and blow them up with charges;
  - tanks and the assault mech fire shells that burst;
  - the other mechs fire machine guns;
  - the tankette burns at 14 m.
- Grenadiers, flamethrower troops and sappers also carry a rifle for targets out of their own weapon's reach.
- Armour takes a tenth of a bullet's damage and half of a flame's. Shells, grenades, charges and bombs do full damage. Vehicles never take cover; they close in until their weapon reaches.
- Turrets and mech torsos turn towards their target before they fire. A knocked-out vehicle blows up, goes dark and smokes.

**Soldier AI**
- **Suppression:** fire landing around a soldier keeps his head down. Machine guns suppress most, then bursting shells, grenades, flames and bombs, then single rifle shots.
  - A suppressed soldier aims worse and heads for cover.
  - A pinned soldier stays in cover and fires more slowly.
  - Suppression wears off within seconds, faster in cover. Vehicles are not affected.
- **Squads:** the men a lane sends in one wave fight as a squad.
  - They concentrate fire on the target most of them have picked.
  - They walk behind a friendly tank or mech in their lane, and the vehicle slows down if they fall behind.
  - A squad that has lost half its men to a stronger enemy, or is pinned and outnumbered, falls back to cover behind it.
  - It then regroups: it joins the next friendly squad coming up its lane, or moves on again with the men it has.
- **Danger:** soldiers try to jump clear of a grenade or bomb they see coming.
- **Targets:** the squad's target, wounded men and dangerous weapons close by come first. Rifles and machine guns prefer men in the open; grenades and flames prefer men in cover.
- **Roles:**
  - machine gunners hold their post longer and never charge;
  - grenadiers and flamethrower troops work their way up to men in cover and to bunkers;
  - a charge breaks down under heavy fire;
  - nobody charges with a squad that has lost a quarter of its men.
- Your squads holding a position for you never fall back on their own.

**Destructible structures**
- Hangars can be destroyed (40 points). Bombs, shells, charges and gunfire bring them down; the planes keep taking off from the runway.
- Infantry, tanks and mechs now attack the anti-aircraft towers too, as well as bunkers, hangars and the command post. Before, only bombs reached the towers.
- Crates and barrels at the bases and the barbed wire break in any blast. They come back at the start of the next battle, along with the buildings.
- The building cards in the menu say so.

**Fixes**
- Smoke and fire effects no longer throw an error every frame after the scripts are recompiled in Play mode. That error filled the Editor log and left the effect stuck on screen.

**Combat**
- A collision no longer destroys a plane outright. This covers tree crowns, mountains, buildings, the rock wall and other planes. Each collision costs 2 of the 6 energy points and pushes the plane clear. For the next 1.5 s, further collisions do no damage. In a mid-air collision, both planes are pushed apart.
- Every gun fires 20 % more slowly:
  - your plane: one burst every 0.5 s (was 0.4);
  - AI pilots: one burst every 2.75 s (was 2.2);
  - riflemen: one shot every 3.25 s (was 2.6);
  - bunkers: one burst every 1.75 s (was 1.4).
- Plane rounds fly 20 % more slowly: 96 m/s plus the plane's speed (was 120).
- Nobody gets aim assist:
  - your rounds fly where the nose points, and there is no target box or bomb sight;
  - AI pilots aim at where an enemy is now, with no computed lead, and drop bombs by eye;
  - the towers do not lead their targets either.
- One rule stays: everything in the air flies on one flight plane.

## 0.1 (2026-11-12): first version on GitHub

The prototype as it stands after the first development sessions.

**Battlefield**
- Trench map with a soft terrain, dead trees, grass, mountains with rocks, and an airfield and props at each base.
- Infantry of both sides advances on its own from the start. Bunkers and command posts can be destroyed.

**Flying**
- You fly one plane. Left/right only rolls it, and the plane turns by banking: turn = 65°/s × sin(bank). The bank holds when you let go, and below 3° the wings level by themselves.
- Up/down changes speed between a minimum and a maximum. Altitude is fixed: every aircraft and every air-to-air round shares one flight plane, low enough to meet tree crowns and mountains.
- Fire with Z or Space, with a light aim assist (removed in 0.2); drop bombs with X.
- No friendly fire; planes collide with each other.
- 15 aircraft models from the Redbaron hangar, picked at random at each respawn. You respawn at your own base.
- Refill icon over each airfield: fly through it to restore energy, rounds and bombs.
- Each plane takes 6 hits. Energy bars and friend-or-foe triangles (green or red) are shown over each aircraft. An ammo counter shows rounds and bombs left.
- AI pilots put avoiding crashes first.

**Cameras**
- Quick RTS view, chase camera, cockpit (first person) and top view.

**Phone (Android)**
- Ultimate Joystick Lite flight joystick and round FUEGO, BOMBA and CÁM buttons.
- The joystick response is proportional, with a sensitivity curve: small pushes for fine corrections, a full push for fast turns.

**Sound**
- Engine sounds from Vehicle - Essentials; the pitch of your engine follows your speed.
- Synthesised battle sounds for machine guns, rifles, bunkers, hits and explosions.
