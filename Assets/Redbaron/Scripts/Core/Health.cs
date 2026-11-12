using System;
using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    public enum TargetKind { Soldier, Bunker, CommandPost, Plane, Hangar, Vehicle }

    /// <summary>Vida y bando de cualquier cosa que se puede atacar.</summary>
    public class Health : MonoBehaviour
    {
        public static readonly List<Health> All = new List<Health>();

        public Team team;
        public TargetKind kind = TargetKind.Soldier;
        public float maxHp = 2;
        public float hp = 2;
        [Tooltip("Altura del punto al que se apunta (m).")]
        public float aimHeight = 1.2f;

        public bool Alive => hp > 0;
        public Vector3 AimPoint => transform.position + Vector3.up * aimHeight;
        /// <summary>A building: a bunker or flak tower, a command post or a hangar.</summary>
        public bool IsStructure => kind == TargetKind.Bunker || kind == TargetKind.CommandPost || kind == TargetKind.Hangar;

        Collider body; bool bodyLooked;
        /// <summary>The collider of a building or vehicle, used to measure distances to its sides instead of its centre.</summary>
        public Collider Body
        {
            get
            {
                if (!bodyLooked) { body = GetComponent<Collider>(); bodyLooked = true; }
                return body;
            }
        }

        /// <summary>Distance from p to this target: to a soldier's feet (on the ground plane), to the box of a building or vehicle.</summary>
        public float DistanceFrom(Vector3 p)
        {
            if (kind != TargetKind.Soldier && Body != null) return Vector3.Distance(Body.bounds.ClosestPoint(p), p);
            Vector3 d = transform.position - p; d.y = 0;
            return d.magnitude;
        }

        public event Action<Health, GameObject> Died;
        public event Action<Health, float, GameObject> Damaged;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public void Damage(float amount, GameObject source)
        {
            if (!Alive) return;
            hp = Mathf.Max(0, hp - amount);
            Sfx.Thud(AimPoint, kind == TargetKind.Soldier ? 0.3f : 0.45f); // hit on a soldier, a bunker or a command post
            Damaged?.Invoke(this, amount, source);
            if (hp <= 0) Died?.Invoke(this, source);
        }

        /// <summary>Damage scaled by what it is and what it hits (Combat.Factor): bullets barely scratch armour.</summary>
        public void Damage(float amount, GameObject source, DamageKind how) => Damage(amount * Combat.Factor(how, kind), source);

        public void Restore()
        {
            hp = maxHp;
        }
    }
}
