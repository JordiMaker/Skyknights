using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// The WWI map: the same lanes, bases and terrain as the classic map, dressed with Synty's POLYGON War Map (Assets/Synty).
    /// Modular trench pieces sit over a deeper terrain cut, barbed-wire fences replace the posts and wire, sandbag barricades,
    /// dirt mounds, shell-hole puddles, wrecked tanks and planes and a crashed airship fill no man's land, artillery batteries
    /// stand behind the rear trench, the bunkers are machine-gun nests under corrugated iron, the command post is a hut with
    /// sandbags and the side's flag, and a plane, a tank and anti-aircraft guns wait by each airfield.
    /// Everything is placed at Play from the prefabs assigned in the scene (Redbaron_WWI), so the kit holds no Asset Store files.
    /// Phone rule: Synty's meshes are not Read/Write, so every prop gets box colliders (Solid) or none (Decor), and the trench
    /// pieces, which keep their mesh colliders for the line of fire, stay out of the NavMesh: soldiers walk the terrain cut under them.
    /// </summary>
    public class WwiDressing : MapDressing
    {
        public const string GroupTrenches = "WWI_Trincheras", GroupObstacles = "WWI_Obstaculos", GroupDecor = "WWI_Decorado";

        [Header("Trench cut in the terrain (m)")]
        [Tooltip("Depth, half-width at full depth and half-width where the cut ends. The Synty floor is 2.0 m under the ground once the piece is sunk.")]
        public float trenchDepth = 2.1f, trenchFull = 1.1f, trenchRamp = 2.1f;
        [Tooltip("Length of the ramp where a lane crosses a trench line: long enough for infantry to walk in.")]
        public float trenchCutLen = 3.2f;
        [Tooltip("How high above the trench floor a soldier fires from (and is hit at, 0.2 m lower): over the piece's parapet, which reaches 0.5 m above the ground 3 m from the centre line.")]
        public float trenchEye = 3.0f;
        [Tooltip("How far below the ground the trench pieces' pivot sits, so their outer skirt (+0.45 m) meets the ground.")]
        public float trenchSink = 0.45f;
        [Tooltip("Zigzag of the trench lines (design m). The 6.3 m wide pieces only chain cleanly on a straight line, so 0.")]
        public float trenchZigzag = 0f;

        [Header("Trenches (SM_Env_Trench_Deep_*)")]
        [Tooltip("Straight pieces, 6.3 m across and 3.2 m long. One per 2.5-unit segment of the trench lines.")]
        public GameObject[] trenchStraight;
        [Tooltip("Same footprint as a straight piece (dugout, inserts): one in every seven pieces.")]
        public GameObject[] trenchSpecial;
        public GameObject[] firesteps, ladders, planks, ironSheets;

        [Header("Wire and sandbags")]
        [Tooltip("Wire fences (long axis x) laid end to end along the wire lines, each one Breakable.")]
        public GameObject[] wireFences;
        public GameObject[] wireTangled;
        public GameObject[] barricades;
        public int barricadesPerSide = 14;
        [Tooltip("The sandbag wall (SM_Prop_Barricade_03): the front of the machine-gun nests and of the command post.")]
        public GameObject sandbags;

        [Header("No man's land")]
        [Tooltip("Destroyed tanks and planes: directional cover, obstacles for every unit.")]
        public GameObject[] wrecks;
        public int wreckCount = 10;
        [Tooltip("A crashed airship lying along the middle of the map, between the centre and east lanes.")]
        public GameObject airshipWreck;
        public float airshipScale = 0.6f;
        public GameObject[] mounds;
        public int moundCount = 36;
        [Tooltip("Flat water decals scaled into the larger shell holes.")]
        public GameObject[] craterWater;

        [Header("Rear: batteries, nests, command post, airfields")]
        public GameObject artillery, aaGun;
        public GameObject machineGunCentrales, machineGunTripodCentrales, machineGunEntente, machineGunTripodEntente;
        public GameObject trenchPole;
        [Tooltip("Wall pieces 2.5 m wide with the pivot at their right end (SM_Bld_Base_Wall_*).")]
        public GameObject[] hutWalls;
        public GameObject hutDoor, hutWindow, hutCeiling;
        public GameObject planeCentrales, planeEntente, tankCentrales, tankEntente;

        [Header("Palette")]
        [Tooltip("The mud of no man's land starts earlier than on the classic map.")]
        public float mudFromZ = 92f, mudFullZ = 112f;

        public override IEnumerable<string> NavMeshIgnoredGroups => new[] { GroupTrenches, GroupDecor };

        Mulberry32 r;
        static float K => RedbaronMap.K;

        public override void Configure(RedbaronMapBuilder b)
        {
            b.mudFromZ = mudFromZ; b.mudFullZ = mudFullZ;
            RedbaronMap.SetTrenchProfile(trenchDepth, trenchFull, trenchRamp, trenchCutLen, trenchEye, trenchZigzag);
        }

        // ------------------------------------------------------------------ helpers
        static bool Has(GameObject[] a) => a != null && a.Length > 0;
        GameObject Pick(GameObject[] pool) => RedbaronMapBuilder.Pick(pool, r);
        static Transform GroupOf(RedbaronMapBuilder b, string name) { var t = b.Root.Find(name); return t != null ? t : b.Group(name); }
        /// <summary>Models point +z; the Central Powers face south (-z).</summary>
        static float Yaw(Team t) => t == Team.Centrales ? 180f : 0f;

        GameObject Put(GameObject prefab, Vector3 pos, float yaw, Transform parent) => Instantiate(prefab, pos, Quaternion.Euler(0, yaw, 0), parent);

        /// <summary>Phone-safe obstacle: a box per mesh instead of Synty's mesh colliders. In the NavMesh, blocks bullets.</summary>
        static void Solid(GameObject go, float narrow = 0.85f) { RedbaronMapBuilder.StripColliders(go); RedbaronMapBuilder.AddBoxColliders(go, narrow); }
        /// <summary>Like Solid, but no box is taller than <paramref name="maxHeight"/> m above its mesh's bottom: a wreck's hull
        /// or a gun's carriage stops men and bullets, while wings, masts and barrels let the lines of fire through.</summary>
        static void SolidLow(GameObject go, float narrow, float maxHeight)
        {
            RedbaronMapBuilder.StripColliders(go);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var bc = mf.gameObject.AddComponent<BoxCollider>();
                Bounds mb = mf.sharedMesh.bounds;
                float sy = Mathf.Abs(mf.transform.lossyScale.y); if (sy < 1e-3f) sy = 1f;
                float h = Mathf.Min(mb.size.y, maxHeight / sy);
                bc.size = new Vector3(mb.size.x * narrow, h, mb.size.z * narrow);
                bc.center = new Vector3(mb.center.x, mb.min.y + h / 2f, mb.center.z);
            }
        }

        /// <summary>Scales a prop on y so that it stands <paramref name="height"/> m tall: cover a soldier can fire over.</summary>
        static void Squash(GameObject go, float height)
        {
            float h = RedbaronMapBuilder.RenderBounds(go).size.y;
            if (h > height + 0.05f) go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(1, height / h, 1));
        }

        /// <summary>Decoration: no colliders at all.</summary>
        static void Decor(GameObject go) => RedbaronMapBuilder.StripColliders(go);

        static CoverPoint Cover(GameObject go, CoverType type, float back, int capacity)
        {
            var cp = go.AddComponent<CoverPoint>();
            cp.type = type; cp.back = back; cp.capacity = capacity;
            return cp;
        }

        /// <summary>Open ground away from water, trenches, lanes, mountains and the airfields.</summary>
        static bool Free(float X, float Z, float laneMargin)
        {
            var s = RedbaronMap.Sample(X, Z);
            if (s.wet || s.trench > 0.02f || s.mountain > 2.5f || s.h < -0.55f) return false;
            if (s.laneDist * K < RedbaronMap.LaneClear + laneMargin) return false;
            if (RedbaronMap.OnAirfield(X, s.zm, 3f)) return false;
            foreach (float L in RedbaronMap.TrenchLines) if (Mathf.Abs(s.zm - L) < 4.5f) return false;
            return true;
        }

        static bool NearBunker(float X, float Zm, float d)
        {
            foreach (var bk in RedbaronMap.Bunkers) if ((bk - new Vector2(X, Zm)).sqrMagnitude < d * d) return true;
            return false;
        }

        static bool Near(List<Vector2> pts, float X, float Z, float d)
        {
            foreach (var p in pts) if ((p - new Vector2(X, Z)).sqrMagnitude < d * d) return true;
            return false;
        }

        void Remove(GameObject go) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }

        // ------------------------------------------------------------------ Trenches
        public override bool BuildTrenches(RedbaronMapBuilder b, Transform covers)
        {
            if (!Has(trenchStraight)) return false;
            var g = b.Group(GroupTrenches);
            r = new Mulberry32((uint)b.seed * 71u + 3u);
            int n = 0;
            foreach (float bz in RedbaronMap.TrenchLines)
                for (float X = 0; X < 99.99f; X += 2.5f)
                {
                    float X1 = X + 2.5f, z0 = RedbaronMap.TrenchZ(bz, X), z1 = RedbaronMap.TrenchZ(bz, X1);
                    // the pieces stop short of the lanes, where the terrain ramps the cut out for the vehicles
                    if (RedbaronMap.LaneDist((X + X1) / 2, (z0 + z1) / 2) * K < RedbaronMap.LaneClear + 2.2f) continue;
                    n++;
                    var prefab = Has(trenchSpecial) && n % 7 == 3 ? Pick(trenchSpecial) : Pick(trenchStraight);
                    bool step = r.Next() < 0.4f, ladder = n % 9 == 4, plank = r.Next() < 0.3f;
                    foreach (bool south in new[] { false, true })
                    {
                        Team team = south ? Team.Entente : Team.Centrales;
                        float za = south ? RedbaronMap.MirrorZ(z0) : z0, zb = south ? RedbaronMap.MirrorZ(z1) : z1;
                        Vector3 A = RedbaronMap.ToWorld(X, za, 0), B = RedbaronMap.ToWorld(X1, zb, 0);
                        Vector3 dir = B - A; float Lg = dir.magnitude; dir /= Lg;
                        Vector3 c = (A + B) / 2f;
                        float g0 = RedbaronMap.Sample((X + X1) / 2, (za + zb) / 2).h0;
                        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up); // the piece's long axis is z
                        var piece = Instantiate(prefab, new Vector3(c.x, g0 - trenchSink, c.z), rot, g);
                        piece.transform.localScale = new Vector3(1, 1, (Lg + 0.12f) / 3.2f); // end to end, with a hair of overlap
                        // furniture against the enemy's wall: fire steps, a ladder, duckboards
                        Vector3 toEnemy = Vector3.forward * BuildingModels.Face(team);
                        Vector3 nrm = new Vector3(dir.z, 0, -dir.x);
                        if (Vector3.Dot(nrm, toEnemy) < 0) nrm = -nrm;
                        float floor = g0 - trenchSink - 1.45f;
                        if (step && Has(firesteps)) Decor(Instantiate(Pick(firesteps), new Vector3(c.x, floor, c.z) + nrm * 0.75f, Quaternion.LookRotation(nrm, Vector3.up), piece.transform));
                        if (ladder && Has(ladders)) Decor(Instantiate(Pick(ladders), new Vector3(c.x, floor + 0.05f, c.z) + nrm * 0.9f, Quaternion.LookRotation(-nrm, Vector3.up) * Quaternion.Euler(-14f, 0, 0), piece.transform));
                        if (plank && Has(planks)) Decor(Instantiate(Pick(planks), new Vector3(c.x, floor + 0.02f, c.z) - nrm * 0.3f, rot * Quaternion.Euler(0, r.Range(-10f, 10f), 0), piece.transform));
                        var cp = new GameObject("Trinchera").AddComponent<CoverPoint>();
                        cp.transform.SetParent(covers, false);
                        cp.transform.position = new Vector3(c.x, g0 - RedbaronMap.TrenchDepth, c.z);
                        cp.type = CoverType.Trinchera; cp.back = 0; cp.capacity = 2;
                    }
                }
            return true;
        }

        // ------------------------------------------------------------------ Wire
        public override bool BuildWire(RedbaronMapBuilder b, Transform g)
        {
            if (!Has(wireFences)) return false;
            r = new Mulberry32((uint)b.seed * 83u + 9u);
            int n = 0;
            foreach (float Zw in new[] { 145f, RedbaronMap.MirrorZ(145f) })
            {
                const float Zm = 145f;
                float xw = 2.2f * K;
                while (xw < 97.8f * K)
                {
                    var go = Instantiate(Pick(wireFences), g);
                    float len = RedbaronMapBuilder.RenderBounds(go).size.x;
                    float X0 = xw / K, X1 = (xw + len) / K, Xc = (X0 + X1) / 2f;
                    float clear = RedbaronMap.LaneClear / K + 0.4f; // the lanes stay open
                    if (RedbaronMap.LaneDist(X0, Zm) < clear || RedbaronMap.LaneDist(X1, Zm) < clear || RedbaronMap.LaneDist(Xc, Zm) < clear)
                    {
                        Remove(go); xw += 1.5f; continue;
                    }
                    Vector3 p = RedbaronMap.ToWorld(Xc, Zw + (r.Next() - 0.5f) * 0.9f);
                    go.transform.SetPositionAndRotation(p + Vector3.up * 0.1f, Quaternion.Euler(0, r.Range(-5f, 5f), 0));
                    go.name = "Alambrada_" + n++;
                    Decor(go); go.AddComponent<Breakable>();
                    if (Has(wireTangled) && r.Next() < 0.25f)
                    {
                        float Zt = Zw + (r.Next() < 0.5f ? -2f : 2f);
                        var t = Put(Pick(wireTangled), RedbaronMap.ToWorld(Xc, Zt) + Vector3.up * 0.1f, r.Next() * 360f, g);
                        t.name = "Alambrada_" + n++;
                        Decor(t); t.AddComponent<Breakable>();
                    }
                    xw += len * 0.96f;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ Command post: a hut with sandbags and the flag
        public override GameObject BuildCommandPost(RedbaronMapBuilder b, Team t, Transform parent, Vector3 at)
        {
            if (!Has(hutWalls) || hutCeiling == null) return null;
            if (r == null) r = new Mulberry32((uint)b.seed * 13u + 1u);
            var root = new GameObject("PuestoDeMando_" + t);
            root.transform.SetParent(parent, false);
            root.transform.position = at;
            float face = BuildingModels.Face(t);
            // 10 x 7.5 m: four wall pieces along each long side (the door faces the enemy), three along each short side
            for (int i = 0; i < 4; i++)
            {
                float x0 = -5f + i * 2.5f;
                Wall(root.transform, i == 2 && hutDoor != null ? hutDoor : Pick(hutWalls), new Vector3(x0, 0, face * 3.75f), Vector3.right);
                Wall(root.transform, Pick(hutWalls), new Vector3(x0, 0, -face * 3.75f), Vector3.right);
            }
            for (int j = 0; j < 3; j++)
            {
                float z0 = -3.75f + j * 2.5f;
                Wall(root.transform, j == 1 && hutWindow != null ? hutWindow : Pick(hutWalls), new Vector3(-5f, 0, z0), Vector3.forward);
                Wall(root.transform, j == 1 && hutWindow != null ? hutWindow : Pick(hutWalls), new Vector3(5f, 0, z0), Vector3.forward);
            }
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 3; j++)
                {
                    var c = Instantiate(hutCeiling, root.transform);
                    c.transform.localPosition = new Vector3(-5f + i * 2.5f + 2.5f, 3f, -3.75f + j * 2.5f);
                    c.transform.localRotation = Quaternion.identity;
                }
            var bags = sandbags != null ? sandbags : Has(barricades) ? Pick(barricades) : null;
            if (bags != null)
                for (int i = -1; i <= 1; i++)
                    Instantiate(bags, at + new Vector3(i * 3.7f, 0.25f, face * 6.2f), Quaternion.Euler(0, Yaw(t), 0), root.transform);
            var acc = new MeshAccumulator();
            BuildingModels.AddFlag(acc, new Vector3(3.6f, 3.0f, 0), t, 7.5f, 1.6f);
            acc.ToObject("Bandera", root.transform, b.vertexColorMaterial, false);
            RedbaronMapBuilder.StripColliders(root);
            var bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 1.6f, 0); bc.size = new Vector3(10.4f, 3.2f, 7.9f);
            return root;
        }

        /// <summary>One wall piece from <paramref name="from"/> along <paramref name="dir"/> (local coordinates). The pieces' pivot is at their right end.</summary>
        void Wall(Transform parent, GameObject prefab, Vector3 from, Vector3 dir)
        {
            float yaw = Mathf.Atan2(dir.z, -dir.x) * Mathf.Rad2Deg;
            var w = Instantiate(prefab, parent);
            w.transform.localPosition = from;
            w.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        // ------------------------------------------------------------------ Bunker: a machine-gun nest under corrugated iron
        public override GameObject BuildBunker(RedbaronMapBuilder b, Team t, Transform parent, Vector3 at)
        {
            if (!Has(barricades)) return null;
            if (r == null) r = new Mulberry32((uint)b.seed * 13u + 1u);
            var root = new GameObject("Casamata_" + t);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0, Yaw(t), 0)); // local +z faces the enemy
            void Child(GameObject prefab, Vector3 lp, Vector3 euler)
            {
                if (prefab == null) return;
                var c = Instantiate(prefab, root.transform);
                c.transform.localPosition = lp; c.transform.localRotation = Quaternion.Euler(euler);
            }
            Child(sandbags != null ? sandbags : Pick(barricades), new Vector3(0, 0.25f, 1.7f), Vector3.zero);
            Child(Pick(barricades), new Vector3(-2.1f, 0.25f, 0.3f), new Vector3(0, 90, 0));
            Child(Pick(barricades), new Vector3(2.1f, 0.25f, 0.3f), new Vector3(0, 90, 0));
            Child(t == Team.Centrales ? machineGunTripodCentrales : machineGunTripodEntente, new Vector3(0, 0, 0.4f), Vector3.zero);
            Child(t == Team.Centrales ? machineGunCentrales : machineGunEntente, new Vector3(0, 0.55f, 0.45f), Vector3.zero);
            if (trenchPole != null)
                foreach (float x in new[] { -2.2f, 2.2f })
                    foreach (float z in new[] { -1.4f, 1.4f }) Child(trenchPole, new Vector3(x, 0, z), Vector3.zero);
            if (Has(ironSheets))
                for (int i = -2; i <= 2; i++)
                    foreach (float z in new[] { -0.9f, 0.9f })
                        Child(Pick(ironSheets), new Vector3(i * 0.9f, 2.15f + r.Range(-0.05f, 0.05f), z), new Vector3(r.Range(-4f, 4f), r.Range(-3f, 3f), r.Range(-6f, 6f)));
            RedbaronMapBuilder.StripColliders(root);
            var bc = root.AddComponent<BoxCollider>();
            bc.center = BuildingModels.BunkerCenter; bc.size = BuildingModels.BunkerSize;
            return root;
        }

        // ------------------------------------------------------------------ Airfield: a parked plane, a tank in reserve, anti-aircraft guns
        public override void DressAirfield(RedbaronMapBuilder b, Team t, Transform parent)
        {
            if (r == null) r = new Mulberry32((uint)b.seed * 13u + 1u);
            var obst = GroupOf(b, GroupObstacles);
            var plane = t == Team.Centrales ? planeCentrales : planeEntente;
            if (plane != null)
            {
                var p = Put(plane, RedbaronMap.ToWorld(33f, RedbaronMap.ForTeam(t, 8.5f)) + Vector3.up * 1.7f, Yaw(t) + r.Range(-10f, 10f), obst);
                SolidLow(p, 0.7f, 1.3f); // obstacles, not cover: a man behind something this tall has no line of fire
            }
            var tank = t == Team.Centrales ? tankCentrales : tankEntente;
            if (tank != null)
            {
                var k = Put(tank, RedbaronMap.ToWorld(69f, RedbaronMap.ForTeam(t, 6.5f)), Yaw(t) + r.Range(-20f, 20f), obst);
                Solid(k, 0.9f);
            }
            if (aaGun != null)
                foreach (float X in new[] { 12f, 88f })
                {
                    var a = Put(aaGun, RedbaronMap.ToWorld(X, RedbaronMap.ForTeam(t, 9.5f)), Yaw(t), obst);
                    Solid(a, 0.9f);
                }
        }

        // ------------------------------------------------------------------ No man's land and the rear
        public override void Dress(RedbaronMapBuilder b)
        {
            r = new Mulberry32((uint)b.seed * 97u + 5u);
            var obst = GroupOf(b, GroupObstacles);
            var deco = GroupOf(b, GroupDecor);

            // Wrecks: cover and obstacles, scattered over both halves of no man's land
            if (Has(wrecks))
            {
                var placed = new List<Vector2>();
                for (int k = 0, tries = 0; k < wreckCount && tries < wreckCount * 60; tries++)
                {
                    float X = 4 + r.Next() * 92, Zm = 114 + r.Next() * 33;
                    if (!Free(X, Zm, 5f) || NearBunker(X, Zm, 8f) || Near(placed, X, Zm, 12f) || Mathf.Abs(X - 67) < 9f) continue;
                    placed.Add(new Vector2(X, Zm));
                    float Z = r.Next() < 0.5f ? Zm : RedbaronMap.MirrorZ(Zm);
                    // Hull-high boxes: they stop men and close shots, but the lines of fire across no man's land pass over the wings.
                    // No CoverPoint: a soldier behind a 3 m wreck cannot fire over it and would keep leaving it to find a line.
                    var w = Put(Pick(wrecks), RedbaronMap.ToWorld(X, Z), r.Next() * 360f, obst);
                    SolidLow(w, 0.9f, 1.3f);
                    k++;
                }
            }
            if (airshipWreck != null)
            {
                var a = Instantiate(airshipWreck, RedbaronMap.ToWorld(67f, 150f), Quaternion.identity, obst);
                a.transform.localScale = Vector3.one * airshipScale;
                SolidLow(a, 0.8f, 2.5f);
            }

            // Dirt mounds: low cover
            if (Has(mounds))
                for (int k = 0, tries = 0; k < moundCount && tries < moundCount * 40; tries++)
                {
                    float X = 3 + r.Next() * 94, Zm = 100 + r.Next() * 50;
                    if (!Free(X, Zm, 2.5f) || NearBunker(X, Zm, 5f)) continue;
                    float Z = r.Next() < 0.5f ? Zm : RedbaronMap.MirrorZ(Zm);
                    var m = Put(Pick(mounds), RedbaronMap.ToWorld(X, Z) - Vector3.up * 0.15f, r.Next() * 360f, obst);
                    Solid(m, 0.8f); Cover(m, CoverType.Roca, 1.1f, 2);
                    k++;
                }

            // Sandbag barricades between the forward trench and the wire, facing the enemy
            if (Has(barricades))
                foreach (bool south in new[] { false, true })
                    for (int k = 0, tries = 0; k < barricadesPerSide && tries < barricadesPerSide * 30; tries++)
                    {
                        float X = 3 + r.Next() * 94, Zm = 139.5f + r.Next() * 3.5f;
                        if (RedbaronMap.LaneDist(X, Zm) * K < RedbaronMap.LaneClear + 2.5f || RedbaronMap.Sample(X, Zm).wet) continue;
                        var bb = Put(Pick(barricades), RedbaronMap.ToWorld(X, south ? RedbaronMap.MirrorZ(Zm) : Zm) + Vector3.up * 0.25f, (south ? 0f : 180f) + r.Range(-12f, 12f), obst);
                        Squash(bb, 1.1f); // chest high, like the classic rocks: a soldier fires over it from behind
                        Solid(bb, 0.9f); Cover(bb, CoverType.Roca, 1.0f, 2);
                        k++;
                    }

            // Puddles in the larger shell holes
            if (Has(craterWater))
                foreach (var c in RedbaronMap.Craters)
                {
                    if (c.r < 2.0f || r.Next() < 0.35f) continue;
                    foreach (float Z in new[] { c.z, RedbaronMap.MirrorZ(c.z) })
                    {
                        var s = RedbaronMap.Sample(c.x, Z);
                        if (s.wet || s.laneDist * K < RedbaronMap.LaneClear) continue;
                        var w = Put(Pick(craterWater), RedbaronMap.ToWorld(c.x, Z) + Vector3.up * 0.12f, r.Next() * 360f, deco);
                        float sz = RedbaronMapBuilder.RenderBounds(w).size.x;
                        w.transform.localScale *= c.r * K / Mathf.Max(0.1f, sz);
                        Decor(w);
                    }
                }

            // Artillery batteries behind the rear trench, with their ammunition
            if (artillery != null)
                foreach (Team t in new[] { Team.Centrales, Team.Entente })
                    foreach (float Xs in new[] { 30f, 68f })
                    {
                        for (int k = -1; k <= 1; k += 2)
                        {
                            var a = Put(artillery, RedbaronMap.ToWorld(Xs + k * 2.8f, RedbaronMap.ForTeam(t, 31f + r.Range(-0.5f, 0.5f))), Yaw(t) + r.Range(-8f, 8f), obst);
                            SolidLow(a, 0.8f, 1.2f);
                        }
                        if (Has(b.basePropPrefabs))
                            for (int k = 0; k < 5; k++)
                            {
                                var p = Put(Pick(b.basePropPrefabs), RedbaronMap.ToWorld(Xs + r.Range(-5f, 5f), RedbaronMap.ForTeam(t, 31f - r.Range(1.5f, 4f))), r.Next() * 360f, deco);
                                Decor(p); p.AddComponent<Breakable>();
                            }
                    }

            // Iron sheets and planks left about behind the trench lines
            if (Has(ironSheets) || Has(planks))
                foreach (Team t in new[] { Team.Centrales, Team.Entente })
                    for (int k = 0, tries = 0; k < 24 && tries < 200; tries++)
                    {
                        float X = 2 + r.Next() * 96, Zm = (r.Next() < 0.5f ? 29f : 132f) + r.Range(0f, 2.5f);
                        if (!Free(X, Zm, 1.5f) || Mathf.Abs(X - 30f) < 7f && Zm < 40 || Mathf.Abs(X - 68f) < 7f && Zm < 40) continue;
                        var pool = r.Next() < 0.5f && Has(ironSheets) ? ironSheets : Has(planks) ? planks : ironSheets;
                        Decor(Put(Pick(pool), RedbaronMap.ToWorld(X, RedbaronMap.ForTeam(t, Zm)) + Vector3.up * 0.05f, r.Next() * 360f, deco));
                        k++;
                    }
        }
    }
}
