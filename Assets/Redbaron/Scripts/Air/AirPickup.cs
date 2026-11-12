using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Floating icon over an airfield. A plane of the same side that flies through it gets its energy,
    /// ammunition and bombs back. It hangs on the flight plane and has no collider, so it is never an obstacle.
    /// </summary>
    public class AirPickup : MonoBehaviour
    {
        public static readonly List<AirPickup> All = new List<AirPickup>();

        public Team team;
        [Tooltip("How close (m) a plane must pass, added to the plane's own size.")]
        public float radius = 4f;
        [Tooltip("Seconds before the icon can be used again.")]
        public float cooldown = 6f;

        Transform icon;
        Light glow;
        float readyAt;

        public bool Ready => Time.time >= readyAt;

        public static AirPickup For(Team t)
        {
            foreach (var p in All) if (p != null && p.team == t) return p;
            return null;
        }

        public static AirPickup Create(Team t, Vector3 position, Material mat)
        {
            var go = new GameObject("Recarga_" + t);
            go.transform.position = position;
            var pk = go.AddComponent<AirPickup>();
            pk.team = t;

            var icon = BuildIcon(go.transform, mat, out pk.glow);
            pk.icon = icon.transform;
            return pk;
        }

        static readonly Color Green = new Color32(0x3f, 0xc8, 0x4c, 255);

        /// <summary>The icon itself (a supply crate with green crosses inside a gold ring) and its green light.
        /// Shared with the menu's building cards.</summary>
        public static GameObject BuildIcon(Transform parent, Material mat, out Light glow)
        {
            var acc = new MeshAccumulator();
            Color wood = new Color32(0x7a, 0x56, 0x34, 255), green = Green, gold = new Color32(0xe0, 0xb0, 0x30, 255);
            acc.AddBox(Vector3.zero, new Vector3(1.6f, 1.6f, 1.6f), wood);
            foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up })
            {
                Vector3 c = n * 0.82f;
                Vector3 a = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up, side = Vector3.Cross(n, a);
                Vector3 thin = new Vector3(Mathf.Abs(n.x) * 0.06f, Mathf.Abs(n.y) * 0.06f, Mathf.Abs(n.z) * 0.06f);
                acc.AddBox(c, thin + Abs(a) * 1.2f + Abs(side) * 0.36f, green);
                acc.AddBox(c, thin + Abs(a) * 0.36f + Abs(side) * 1.2f, green);
            }
            for (int i = 0; i < 16; i++)
            {
                float ang = i * Mathf.PI * 2f / 16f;
                Vector3 p = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 2.4f;
                acc.AddBox(p, new Vector3(0.22f, 0.22f, 0.95f), Quaternion.Euler(0, -ang * Mathf.Rad2Deg, 0), gold);
            }
            var icon = acc.ToObject("Icono", parent, mat, false);
            icon.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            glow = new GameObject("Brillo").AddComponent<Light>();
            glow.transform.SetParent(parent, false);
            glow.type = LightType.Point; glow.color = green; glow.range = 9f; glow.intensity = 2f; glow.shadows = LightShadows.None;
            return icon;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            float dt = Time.deltaTime;
            if (icon != null)
            {
                icon.Rotate(0, 80f * dt, 0, Space.World);
                icon.localPosition = Vector3.up * Mathf.Sin(Time.time * 2f) * 0.3f;
                float s = Mathf.MoveTowards(icon.localScale.x, Ready ? 1f : 0.35f, dt * 2f);
                icon.localScale = Vector3.one * s;
            }
            if (glow != null) glow.intensity = Ready ? 2f + Mathf.Sin(Time.time * 4f) * 0.6f : 0.3f;
            if (!Ready) return;

            foreach (var b in BiplaneAI.All)
            {
                if (!b.Alive || b.team != team || !b.NeedsRefill) continue;
                Vector3 d = b.Body.Position - transform.position;
                if (d.magnitude > radius + b.BodyRadius) continue;
                b.Refill();
                readyAt = Time.time + cooldown;
                Combat.Puff(transform.position, new Color(0.45f, 0.95f, 0.5f, 0.7f), 2.2f, 1.2f, 0.4f);
                if (RedbaronGame.Instance != null) RedbaronGame.Log($"{b.pilotName} ({team.Name()}) recarga en su aeródromo");
                break;
            }
        }
    }
}
