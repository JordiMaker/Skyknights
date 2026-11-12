using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// The infantry a lane sends together in one wave. A squad:
    /// - concentrates its fire: the target most of its men have picked becomes the squad's, and the others prefer it;
    /// - follows a friendly tank or mech in its lane, walking behind it as cover (the vehicle waits if they fall behind);
    /// - falls back to cover when it has lost half its men to a stronger enemy, or when it is pinned down and outnumbered;
    /// - then regroups: it joins the next friendly squad that comes up its lane, or moves on again after a while.
    /// Vehicles are not in squads; squads of the player's that are holding a position never fall back on their own.
    /// </summary>
    public class Squad
    {
        public enum Order { Advance, Fallback, Regroup }
        public static readonly List<Squad> All = new List<Squad>();
        /// <summary>Counters for the tests: how often squads fell back, and merged into another squad.</summary>
        public static int Fallbacks, Merges;

        [System.Serializable]
        public class Settings
        {
            [Tooltip("Share of the squad lost (with a stronger enemy near) that makes it fall back.")]
            public float breakAtLoss = 0.5f;
            [Tooltip("Average suppression (0–1) that makes an outnumbered squad fall back.")]
            public float breakAtSuppression = 0.65f;
            [Tooltip("Seconds spent running back, then seconds holding while it regroups.")]
            public float fallbackTime = 8f, regroupTime = 14f;
            [Tooltip("A friendly vehicle ahead in the lane within this distance (m) is followed as escort: the men run to catch it up and it waits for them.")]
            public float escortRange = 70f;
            [Tooltip("A regrouping squad joins another friendly squad within this distance (m).")]
            public float mergeRange = 16f;
        }
        public static Settings Config = new Settings();

        public readonly Team team;
        public readonly int lane;
        public readonly List<SoldierAI> members = new List<SoldierAI>();
        public int Size { get; private set; }
        public Order CurrentOrder { get; private set; } = Order.Advance;
        public Health Focus { get; private set; }
        public SoldierAI Escort { get; private set; }
        float orderT, nextThink;

        public Squad(Team t, int laneIndex) { team = t; lane = laneIndex; All.Add(this); }

        public void Add(SoldierAI u)
        {
            if (u == null) return;
            if (u.Squad != null && u.Squad != this) u.Squad.members.Remove(u);
            if (!members.Contains(u)) { members.Add(u); Size++; }
            u.Squad = this;
        }

        /// <summary>A member died: a squad with nobody left is gone.</summary>
        public void Leave(SoldierAI u)
        {
            members.Remove(u);
            if (AliveCount == 0) All.Remove(this);
        }

        public int AliveCount { get { int n = 0; foreach (var m in members) if (m != null && m.Alive) n++; return n; } }
        public float Strength => Size > 0 ? AliveCount / (float)Size : 0f;
        public int RankOf(SoldierAI u) => Mathf.Max(0, members.IndexOf(u));

        public Vector3 Center
        {
            get
            {
                Vector3 c = Vector3.zero; int n = 0;
                foreach (var m in members) if (m != null && m.Alive) { c += m.transform.position; n++; }
                return n > 0 ? c / n : Vector3.zero;
            }
        }

        public float Suppression
        {
            get
            {
                float s = 0; int n = 0;
                foreach (var m in members) if (m != null && m.Alive) { s += m.Suppression; n++; }
                return n > 0 ? s / n : 0f;
            }
        }

        bool HoldsForPlayer
        {
            get { foreach (var m in members) if (m != null && m.Alive) return m.playerControlled && !m.autoAdvance; return false; }
        }

        /// <summary>Any member calls this each frame; the squad decides twice a second.</summary>
        public void Think()
        {
            if (Time.time < nextThink) return;
            nextThink = Time.time + 0.5f;
            members.RemoveAll(m => m == null || !m.Alive);
            if (members.Count == 0) { All.Remove(this); return; }
            Vector3 c = Center;

            // Focus fire: the target most men have picked.
            Health best = null; int votes = 0;
            foreach (var m in members)
            {
                var t = m.Target;
                if (t == null || !t.Alive) continue;
                int n = 0;
                foreach (var o in members) if (o.Target == t) n++;
                if (n > votes) { votes = n; best = t; }
            }
            Focus = votes >= 2 || members.Count == 1 ? best : null;

            // Escort: the nearest friendly vehicle of the lane that is not behind the squad.
            Escort = null;
            if (CurrentOrder == Order.Advance)
            {
                float bd = Config.escortRange;
                var post = CommandPost.Of(team.Enemy());
                Vector3 goal = post != null ? post.transform.position : c;
                float mine = Flat(goal - c);
                foreach (var v in SoldierAI.All)
                {
                    if (!v.IsVehicle || !v.Alive || v.team != team || v.lane != lane) continue;
                    float d = Flat(v.transform.position - c);
                    if (d < bd && Flat(goal - v.transform.position) <= mine + 4f) { bd = d; Escort = v; }
                }
            }

            switch (CurrentOrder)
            {
                case Order.Advance:
                    if (Size < 2 || HoldsForPlayer) break;
                    Odds(c, out int friends, out int foes);
                    if (foes == 0) break;
                    float odds = friends / (float)foes;
                    bool beaten = 1f - Strength >= Config.breakAtLoss && odds < 0.9f;
                    bool pinned = Suppression >= Config.breakAtSuppression && odds < 1.2f && Strength < 1f;
                    if (beaten || pinned)
                    {
                        CurrentOrder = Order.Fallback; orderT = Config.fallbackTime; Fallbacks++;
                        RedbaronGame.Log($"Una escuadra de {team.Name()} se repliega");
                    }
                    break;
                case Order.Fallback:
                    orderT -= 0.5f;
                    if (orderT <= 0) { CurrentOrder = Order.Regroup; orderT = Config.regroupTime; }
                    break;
                case Order.Regroup:
                    orderT -= 0.5f;
                    foreach (var other in All)
                    {
                        if (other == this || other.team != team || other.lane != lane || other.CurrentOrder != Order.Advance || other.members.Count == 0) continue;
                        if (Flat(other.Center - c) > Config.mergeRange) continue;
                        foreach (var m in members.ToArray()) other.Add(m); // the fresh squad takes them along
                        members.Clear(); Merges++;
                        All.Remove(this);
                        return;
                    }
                    if (orderT <= 0) { CurrentOrder = Order.Advance; Size = members.Count; } // moves on with the men it has
                    break;
            }
        }

        /// <summary>Friendly units within 30 m of p and enemy units within 45 m (a vehicle counts as three).</summary>
        void Odds(Vector3 p, out int friends, out int foes)
        {
            friends = foes = 0;
            foreach (var o in SoldierAI.All)
            {
                if (!o.Alive) continue;
                float d = Flat(o.transform.position - p);
                int w = o.IsVehicle ? 3 : 1;
                if (o.team == team && d < 30f) friends += w;
                else if (o.team != team && d < 45f) foes += w;
            }
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        public static void ResetAll() { All.Clear(); }
    }
}
