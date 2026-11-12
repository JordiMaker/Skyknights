using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// A sapper's charge: a small box with a sparking fuse, set against a building or stuck to a vehicle, that blows up
    /// after a few seconds. The target takes the charge's full damage; anything else of the other side within splash takes
    /// a part of it. The enemies near it are warned, so they may run (Combat.WarnBlast); the sapper runs on its own (SoldierAI).
    /// </summary>
    public class DemolitionCharge : MonoBehaviour
    {
        /// <summary>Counters for the tests.</summary>
        public static int Planted, Exploded;

        Team side;
        float fuse, damage, splash, spark;
        GameObject source;
        Health target;
        static Material mat;

        public static DemolitionCharge Plant(Vector3 at, Health target, Team side, float fuse, float damage, float splash, GameObject source)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Carga";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            go.transform.localScale = new Vector3(0.4f, 0.28f, 0.22f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (target != null && target.kind == TargetKind.Vehicle) go.transform.SetParent(target.transform, true); // it travels with the vehicle

            var c = go.AddComponent<DemolitionCharge>();
            c.side = side; c.fuse = fuse; c.damage = damage; c.splash = splash; c.source = source; c.target = target;
            Combat.WarnBlast(at, splash, side);
            Planted++;
            return c;
        }

        static Material Mat
        {
            get
            {
                if (mat == null) mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.55f, 0.12f, 0.08f) };
                return mat;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            fuse -= dt; spark -= dt;
            if (spark <= 0)
            {
                spark = 0.2f;
                Combat.Puff(transform.position + Vector3.up * 0.25f, new Color(1f, 0.85f, 0.3f, 0.9f), 0.25f, 0.5f, 0.6f);
            }
            if (fuse > 0) return;
            Vector3 p = transform.position;
            transform.SetParent(null, true);
            Combat.Explosion(p, false);
            if (target != null && target.Alive) target.Damage(damage, source, DamageKind.Blast);
            Combat.Blast(p, splash, damage * 0.4f, side, DamageKind.Blast, source);
            Exploded++;
            Destroy(gameObject);
        }
    }
}
