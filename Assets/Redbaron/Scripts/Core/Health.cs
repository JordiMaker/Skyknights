using System;
using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    public enum TargetKind { Soldier, Bunker, CommandPost, Plane, Hangar }

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

        public void Restore()
        {
            hp = maxHp;
        }
    }
}
