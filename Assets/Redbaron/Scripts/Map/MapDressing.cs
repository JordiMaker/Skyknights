using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// An alternative set dressing for the battle map, placed on the same object as RedbaronMapBuilder.
    /// The map's geometry (terrain, lanes, bases, bunkers, water, bridges) and everything the AI relies on stay the same;
    /// the dressing lines the trenches, strings the wire, builds the bunkers and the command post and adds its own props,
    /// so one map can look like the classic low-poly front or like a WWI front built from an Asset Store pack (WwiDressing).
    /// Every hook returns false or null to fall back to the classic look for that part.
    /// </summary>
    public abstract class MapDressing : MonoBehaviour
    {
        /// <summary>Called first, before anything is built: trench profile (RedbaronMap.SetTrenchProfile), palette, counts.</summary>
        public virtual void Configure(RedbaronMapBuilder b) { }

        /// <summary>Line the trench cut the terrain already has, and add one Trinchera CoverPoint per segment under <paramref name="covers"/>.
        /// Return false to keep the classic boards and sandbags.</summary>
        public virtual bool BuildTrenches(RedbaronMapBuilder b, Transform covers) => false;

        /// <summary>Barbed wire in front of the forward trenches, each stretch its own Breakable object under <paramref name="group"/>.</summary>
        public virtual bool BuildWire(RedbaronMapBuilder b, Transform group) => false;

        /// <summary>The command post of a side at <paramref name="at"/>, with a BoxCollider on its root (the builder adds Health, CommandPost and the NavMesh obstacle).</summary>
        public virtual GameObject BuildCommandPost(RedbaronMapBuilder b, Team t, Transform parent, Vector3 at) => null;

        /// <summary>A bunker of a side at <paramref name="at"/>, with a BoxCollider on its root (the builder adds Health and Bunker).</summary>
        public virtual GameObject BuildBunker(RedbaronMapBuilder b, Team t, Transform parent, Vector3 at) => null;

        /// <summary>Props around a side's airfield, after its hangars are built.</summary>
        public virtual void DressAirfield(RedbaronMapBuilder b, Team t, Transform parent) { }

        /// <summary>Called last: wrecks, batteries, extra cover and decoration.</summary>
        public virtual void Dress(RedbaronMapBuilder b) { }

        /// <summary>Groups under Redbaron_Mapa the NavMesh bake must skip: decoration, and anything whose meshes a phone cannot read.</summary>
        public virtual IEnumerable<string> NavMeshIgnoredGroups => System.Array.Empty<string>();
    }
}
