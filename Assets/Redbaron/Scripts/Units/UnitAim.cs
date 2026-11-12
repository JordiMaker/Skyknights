using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Turns a vehicle's turret ("Torreta", with its gun "Elevacion") or a mech's torso ("Torso") towards SoldierAI's target,
    /// at a set speed, and says when the gun is on target. A vehicle without either turns its whole hull instead (SoldierAI).
    /// </summary>
    public class UnitAim : MonoBehaviour
    {
        public float turnRate = 70f, pitchRate = 30f;
        public float minPitch = -8f, maxPitch = 20f;
        [Tooltip("How far a torso may turn from the legs (degrees).")]
        public float torsoLimit = 100f;

        /// <summary>World point to aim at; null = back to the front.</summary>
        public Vector3? Target;
        /// <summary>Where shots leave from: the gun, or the torso.</summary>
        public Transform Muzzle { get; private set; }

        class Mount { public Transform yaw, pitch; public Quaternion q0, p0; public float a, e, limit; }
        readonly List<Mount> mounts = new List<Mount>();

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Torreta" && t.name != "Torso") continue;
                Transform e = null;
                foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == "Elevacion") { e = c; break; }
                mounts.Add(new Mount { yaw = t, pitch = e, q0 = t.localRotation, p0 = e != null ? e.localRotation : Quaternion.identity, limit = t.name == "Torso" ? torsoLimit : 180f });
            }
            if (mounts.Count > 0) Muzzle = mounts[0].pitch != null ? mounts[0].pitch : mounts[0].yaw;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            foreach (var m in mounts)
            {
                Desired(m, out float yaw, out float pitch);
                m.a = Mathf.MoveTowardsAngle(m.a, yaw, turnRate * dt);
                m.e = Mathf.MoveTowards(m.e, pitch, pitchRate * dt);
                m.yaw.localRotation = m.q0 * Quaternion.Euler(0f, m.a, 0f);
                if (m.pitch != null) m.pitch.localRotation = m.p0 * Quaternion.Euler(-m.e, 0f, 0f);
            }
        }

        void Desired(Mount m, out float yaw, out float pitch)
        {
            yaw = pitch = 0f;
            if (!Target.HasValue) return;
            Vector3 local = m.yaw.parent.InverseTransformPoint(Target.Value) - m.yaw.localPosition;
            yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -m.limit, m.limit);
            float horiz = new Vector2(local.x, local.z).magnitude;
            pitch = Mathf.Clamp(Mathf.Atan2(local.y, Mathf.Max(0.01f, horiz)) * Mathf.Rad2Deg, minPitch, maxPitch);
        }

        /// <summary>True when every turret or torso points at the target within margin degrees.
        /// A torso at its limit is never on target: the mech must turn its legs (SoldierAI faces the target).</summary>
        public bool Aimed(float margin)
        {
            if (!Target.HasValue) return false;
            foreach (var m in mounts)
            {
                Vector3 local = m.yaw.parent.InverseTransformPoint(Target.Value) - m.yaw.localPosition;
                float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(m.a, yaw)) > margin) return false;
            }
            return true;
        }

        public void Freeze() { enabled = false; }
    }
}
