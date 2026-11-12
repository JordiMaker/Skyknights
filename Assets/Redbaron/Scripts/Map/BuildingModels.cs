using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Geometry of the buildings, shared by the battle map and the menu's building cards, so both always show
    /// the same thing. Each method adds one building to a MeshAccumulator around an origin on the ground.
    /// The door, the firing slit and the sandbags face the enemy side.
    /// </summary>
    public static class BuildingModels
    {
        public static readonly Color Concrete = new Color32(0x8d, 0x8a, 0x80, 255), Concrete2 = new Color32(0x76, 0x73, 0x6a, 255),
            Sandbag = new Color32(0x9b, 0x8a, 0x62, 255), Slit = new Color32(0x1f, 0x1c, 0x16, 255), Pole = new Color32(0x3a, 0x33, 0x28, 255),
            HangarWall = new Color32(0x9a, 0x94, 0x80, 255), HangarRoof = new Color32(0x7d, 0x7a, 0x6c, 255);

        /// <summary>Collision box of each building (centre, size), relative to its origin.</summary>
        public static readonly Vector3 CommandPostCenter = new Vector3(0, 2.5f, 0), CommandPostSize = new Vector3(15, 5, 10);
        public static readonly Vector3 BunkerCenter = new Vector3(0, 1, 0), BunkerSize = new Vector3(5.6f, 2.2f, 4);

        /// <summary>-1 or 1: the world z direction towards the enemy.</summary>
        public static float Face(Team t) => t == Team.Centrales ? -1 : 1;

        public static void AddFlag(MeshAccumulator acc, Vector3 basePos, Team t, float h, float size)
        {
            acc.AddBox(basePos + Vector3.up * (h / 2), new Vector3(0.18f, h, 0.18f), Pole);
            acc.AddBox(basePos + new Vector3(size * 0.75f + 0.1f, h - size / 2 - 0.1f, 0), new Vector3(size * 1.5f, size, 0.08f), t.FlagColor());
        }

        /// <summary>Command post: a concrete block with a tower, a firing slit, sandbags and the side's flag.</summary>
        public static void AddCommandPost(MeshAccumulator acc, Team t)
        {
            float face = Face(t);
            acc.AddBox(new Vector3(0, 1.4f, 0), new Vector3(14, 3.2f, 9), Concrete);
            acc.AddBox(new Vector3(0, 3.2f, 0), new Vector3(15, 0.6f, 10), Concrete2);
            acc.AddBox(new Vector3(-3, 5.2f, 0), new Vector3(4.5f, 3.5f, 4.5f), Concrete);
            acc.AddBox(new Vector3(0, 1.6f, face * 4.55f), new Vector3(10, 0.5f, 0.6f), Slit);
            for (int i = -3; i <= 3; i++) acc.AddBox(new Vector3(i * 2.1f, 0.4f, face * 6.4f), new Vector3(1.7f, 0.8f, 1), Sandbag);
            AddFlag(acc, new Vector3(3.5f, 3.4f, 0), t, 7.5f, 1.6f);
        }

        /// <summary>Bunker: a low concrete box with a machine-gun slit, sandbags and a small flag.</summary>
        public static void AddBunker(MeshAccumulator acc, Team t)
        {
            float face = Face(t);
            acc.AddBox(new Vector3(0, 0.7f, 0), new Vector3(5, 1.8f, 3.4f), Concrete);
            acc.AddBox(new Vector3(0, 1.75f, 0), new Vector3(5.6f, 0.45f, 4), Concrete2);
            acc.AddBox(new Vector3(0, 1.05f, face * 1.72f), new Vector3(3, 0.3f, 0.2f), Slit);
            for (int i = -2; i <= 2; i++) acc.AddBox(new Vector3(i * 1.15f, 0.3f, face * 2.6f), new Vector3(1.1f, 0.6f, 0.8f), Sandbag);
            AddFlag(acc, new Vector3(2.2f, 1.9f, 0), t, 3.2f, 0.8f);
        }

        /// <summary>Hangar: walls, a pitched roof, a dark door facing the runway and a tall flag. b is its centre on the ground.</summary>
        public static void AddHangar(MeshAccumulator acc, Vector3 b, Team t)
        {
            acc.AddBox(b + new Vector3(0, 1.3f, 0), new Vector3(8.6f, 2.6f, 12), HangarWall);
            acc.AddBox(b + new Vector3(-2.2f, 3.7f, 0), new Vector3(5.2f, 0.3f, 12.4f), Quaternion.Euler(0, 0, 30), HangarRoof);
            acc.AddBox(b + new Vector3(2.2f, 3.7f, 0), new Vector3(5.2f, 0.3f, 12.4f), Quaternion.Euler(0, 0, -30), HangarRoof);
            float face = t == Team.Centrales ? -1 : 1; // the door faces the runway
            acc.AddBox(b + new Vector3(0, 1.6f, face * 6.02f), new Vector3(7, 3.0f, 0.2f), Slit);
            AddFlag(acc, b + new Vector3(4.6f, 0, 3), t, 9, 1.2f);
        }
    }
}
