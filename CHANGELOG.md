# Changelog

## 0.1 (2026-11-12): first version on GitHub

The prototype as it stands after the first development sessions.

**Battlefield**
- Trench map with a soft terrain, dead trees, grass, mountains with rocks, and an airfield and props at each base.
- Infantry of both sides advances on its own from the start. Bunkers and command posts can be destroyed.

**Flying**
- You fly one plane. Left/right only rolls it, and the plane turns by banking: turn = 65°/s × sin(bank). The bank holds when you let go, and below 3° the wings level by themselves.
- Up/down changes speed between a minimum and a maximum. Altitude is fixed: every aircraft and every air-to-air round shares one flight plane, low enough to meet tree crowns and mountains.
- Fire with Z or Space, with a light aim assist; drop bombs with X.
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
