using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    public enum Team { Centrales = 0, Entente = 1 }

    public static class TeamUtil
    {
        public static Team Enemy(this Team t) => t == Team.Centrales ? Team.Entente : Team.Centrales;
        public static string Name(this Team t) => t == Team.Centrales ? "Potencias Centrales" : "Entente";
        public static Color FlagColor(this Team t) => t == Team.Centrales ? new Color32(104, 150, 76, 255) : new Color32(95, 134, 176, 255); // Central Powers green, Entente navy
        public static Color Uniform(this Team t) => t == Team.Centrales ? new Color32(111, 116, 102, 255) : new Color32(125, 147, 173, 255);
    }

    /// <summary>Resultado de muestrear el terreno en un punto de diseño.</summary>
    public struct MapSample
    {
        public float h;         // altura final (con trinchera excavada)
        public float h0;        // altura del suelo sin trinchera
        public float laneDist;  // distancia a la línea más cercana (m de diseño)
        public float trench;    // 0..1, cuánto está dentro de una trinchera
        public float mountain;  // altura aportada por la cordillera
        public bool wet;        // río o lago
        public float zm;        // Z reflejada en la mitad norte (0..150)
    }

    /// <summary>
    /// Datos del mapa Redbaron. Todo se define en "metros de diseño" (100 x 300)
    /// y se escala por K = sqrt(1.5) al mundo (122,5 x 367,4 m), igual que en el prototipo web.
    /// Diseño: X de oeste (0) a este (100). Z de norte (0, Potencias Centrales) a sur (300, Entente).
    /// Mundo Unity: x = X*K, z = (300 - Z)*K, así el norte del mapa queda hacia +z.
    /// </summary>
    public static class RedbaronMap
    {
        public const float DesignW = 100f, DesignL = 300f;
        public static readonly float K = Mathf.Sqrt(1.5f);
        /// <summary>Half-width kept clear of trenches and flattened along every lane (m), for the widest vehicle and its NavMesh margin.</summary>
        public const float LaneClear = 5.5f;
        public static float WorldW => DesignW * K;
        public static float WorldL => DesignL * K;
        public const float WallHeight = 28f;
        public const float WaterLevel = -0.9f;
        public const float TrenchWidth = 2.0f, TrenchDepth = 1.3f;
        public static readonly float[] TrenchLines = { 26f, 137f };

        // Líneas en la mitad norte (X, Zm). La mitad sur es su reflejo.
        public static readonly Vector2[][] Lanes =
        {
            new[] { new Vector2(40, 12), new Vector2(15, 22), new Vector2(16, 63), new Vector2(15, 105), new Vector2(17, 150) },
            new[] { new Vector2(50, 14), new Vector2(50, 150) },
            new[] { new Vector2(60, 12), new Vector2(85, 22), new Vector2(72, 45), new Vector2(85, 58), new Vector2(85, 150) }
        };

        // Aeródromo (mitad norte) y hangares.
        public static readonly Rect Runway = Rect.MinMaxRect(8f, 13.8f, 92f, 21.2f);
        public static readonly Vector2[] Hangars = { new Vector2(22, 6), new Vector2(78, 6) };
        public static readonly Vector2 CommandPost = new Vector2(50, 9);
        // Casamatas: (X, Zm) en la mitad norte.
        public static readonly Vector2[] Bunkers =
        {
            new Vector2(21, 63), new Vector2(55, 63), new Vector2(80, 63),
            new Vector2(22, 131), new Vector2(55, 131), new Vector2(80, 131)
        };
        // Grupos de rocas (X, Zm, cantidad).
        public static readonly Vector3[] RockClusters =
        {
            new Vector3(12, 13, 5), new Vector3(88, 14, 5), new Vector3(94, 75, 6), new Vector3(33, 133, 4), new Vector3(68, 133, 4),
            new Vector3(24, 68, 3), new Vector3(42, 84, 3), new Vector3(58, 66, 3), new Vector3(77, 84, 3), new Vector3(6, 98, 3)
        };

        public struct Crater { public float x, z, r; }
        public static readonly List<Crater> Craters = new List<Crater>();

        static RedbaronMap()
        {
            var rng = new Mulberry32(1916);
            for (int i = 0; i < 34; i++) Craters.Add(new Crater { x = 3 + rng.Next() * 94, z = 116 + rng.Next() * 34, r = 1.4f + rng.Next() * 1.8f });
        }

        // ---------- Conversión de coordenadas ----------
        public static Vector3 ToWorld(float X, float Z, float y) => new Vector3(X * K, y, (DesignL - Z) * K);
        public static Vector3 ToWorld(float X, float Z) => ToWorld(X, Z, Height(X, Z));
        public static Vector2 ToDesign(Vector3 w) => new Vector2(w.x / K, DesignL - w.z / K);
        public static float MirrorZ(float Z) => DesignL - Z;
        /// <summary>Z de diseño de un punto de la mitad norte para un bando.</summary>
        public static float ForTeam(Team t, float zNorth) => t == Team.Centrales ? zNorth : MirrorZ(zNorth);

        // ---------- Funciones auxiliares ----------
        static float G(float d, float s) => Mathf.Exp(-(d * d) / (s * s));
        static float Hyp(float a, float b) => Mathf.Sqrt(a * a + b * b);
        public static float Tri(float u) => Mathf.Abs(u - Mathf.Floor(u + 0.5f)) * 2f - 0.5f;
        public static float TrenchZ(float baseZ, float X) => baseZ + 1.6f * Tri(X / 5f);

        static float SegDist(float px, float pz, Vector2 a, Vector2 b)
        {
            float dx = b.x - a.x, dz = b.y - a.y;
            float t = ((px - a.x) * dx + (pz - a.y) * dz) / (dx * dx + dz * dz);
            t = Mathf.Clamp01(t);
            return Hyp(px - (a.x + t * dx), pz - (a.y + t * dz));
        }

        public static float LaneDist(float X, float Zm)
        {
            float d = 1e9f;
            foreach (var L in Lanes)
                for (int i = 0; i < L.Length - 1; i++) d = Mathf.Min(d, SegDist(X, Zm, L[i], L[i + 1]));
            return d;
        }

        public static bool OnAirfield(float X, float Zm, float pad = 0f)
        {
            if (X > Runway.xMin - pad && X < Runway.xMax + pad && Zm > Runway.yMin - pad && Zm < Runway.yMax + pad) return true;
            foreach (var hg in Hangars)
                if (Mathf.Abs(X - hg.x) < 5.6f + pad && Mathf.Abs(Zm - hg.y) < 4.2f + pad) return true;
            return false;
        }

        public static float Height(float X, float Z) => Sample(X, Z).h;

        /// <summary>Función de altura del prototipo web, línea a línea.</summary>
        public static MapSample Sample(float X, float Z)
        {
            float Zm = Z <= 150 ? Z : 300 - Z;
            float ld = LaneDist(X, Zm);
            float baseFlat = G(Hyp((X - 50) * 0.7f, Zm - 9), 9);
            float lf = Mathf.Max(G(ld, LaneClear / K), Mathf.Max(baseFlat, OnAirfield(X, Zm, 2.5f) ? 1f : 0f)); // the lane and its shoulders are flat
            float h = 0.35f * Mathf.Sin(X * 0.37f + Zm * 0.11f) + 0.25f * Mathf.Sin(X * 0.13f - Zm * 0.29f + 2) + 0.2f * Mathf.Sin(Zm * 0.53f + X * 0.07f);
            h += 1.3f * G(Hyp(X - 30, Zm - 47), 9) + 0.9f * G(Hyp(X - 66, Zm - 30), 7);
            float m = 0;
            if (Zm > 56 && Zm < 94)
            {
                float t = (Zm - 56) / 38f;
                float s = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 1.3f);
                float peaks = 9 + 5 * Mathf.Sin(X * 0.23f + 1.3f) + 3 * Mathf.Sin(X * 0.61f + 0.4f);
                float pass = Mathf.Max(G(X - 16, 8f), Mathf.Max(G(X - 50, 8f), G(X - 85, 8f))); // the passes take a tank
                m = s * peaks * (1 - pass * 0.95f);
            }
            h += m;
            h *= 1 - 0.85f * lf;
            float rc = 105 + 1.5f * Mathf.Sin(X * 0.12f);
            float rd = 2.8f * G(Zm - rc, 3.4f);
            rd *= 1 - 0.58f * G(X - 16, 5);
            float e = ((X - 90) / 9.5f) * ((X - 90) / 9.5f) + ((Zm - 45) / 10.5f) * ((Zm - 45) / 10.5f);
            float lk = e < 1 ? 3.4f * Mathf.Pow(1 - e, 0.6f) : 0;
            h -= rd + lk;
            bool wet = rd > 0.25f || lk > 0.25f;
            if (Zm > 112)
            {
                foreach (var c in Craters)
                {
                    float d = Hyp(X - c.x, Zm - c.z);
                    if (d < c.r * 1.8f) h += -0.9f * G(d, c.r * 0.6f) + 0.3f * G(d - c.r, 0.6f);
                }
            }
            if (!wet && h < -0.7f) h = -0.7f;
            float h0 = h;
            float dT = 1e9f;
            foreach (var b in TrenchLines) dT = Mathf.Min(dT, Mathf.Abs(Zm - TrenchZ(b, X)) * 0.842f * K);
            float laneCut = Mathf.Clamp01((ld * K - LaneClear) / 1.5f); // no trench across a lane, with a ramp to it
            float trd = (dT < 2.3f ? 1f : dT < 3.6f ? (3.6f - dT) / 1.3f : 0f) * laneCut;
            h -= TrenchDepth * trd;
            return new MapSample { h = h, h0 = h0, laneDist = ld, trench = trd, mountain = m, wet = wet, zm = Zm };
        }

        /// <summary>Altura sobre la que se camina: añade los tableros de los puentes.</summary>
        public static float WalkHeight(float X, float Z)
        {
            float Zm = Z <= 150 ? Z : 300 - Z;
            float y = Height(X, Z);
            if (Zm > 96 && Zm < 113 && (Mathf.Abs(X - 50) < 4f || Mathf.Abs(X - 85) < 4f)) y = Mathf.Max(y, 0.45f);
            return y;
        }

        /// <summary>Recorrido completo de una línea desde la base de un bando hasta la base enemiga, en mundo.</summary>
        public static List<Vector3> LanePath(Team team, int lane)
        {
            var pts = new List<Vector2> { new Vector2(50, 15) };
            var L = Lanes[lane];
            foreach (var p in L) pts.Add(p);
            for (int i = L.Length - 2; i >= 0; i--) pts.Add(new Vector2(L[i].x, 300 - L[i].y));
            pts.Add(new Vector2(50, 285));
            if (team == Team.Entente) pts.Reverse();
            var w = new List<Vector3>();
            foreach (var p in pts) w.Add(ToWorld(p.x, p.y, WalkHeight(p.x, p.y)));
            return w;
        }

        /// <summary>Rectángulo del aeródromo de un bando en coordenadas de mundo (x,z).</summary>
        public static Rect AirfieldWorldRect(Team t)
        {
            float z0 = ForTeam(t, Runway.yMin), z1 = ForTeam(t, Runway.yMax);
            Vector3 a = ToWorld(Runway.xMin, z0, 0), b = ToWorld(Runway.xMax, z1, 0);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z) - 6f, Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z) + 6f);
        }
    }

    /// <summary>Generador pseudoaleatorio determinista (mismo que el prototipo web).</summary>
    public class Mulberry32
    {
        uint a;
        public Mulberry32(uint seed) { a = seed; }
        public float Next()
        {
            unchecked
            {
                a += 0x6D2B79F5;
                uint t = a;
                t = (t ^ (t >> 15)) * (1 | t);
                t = (t + ((t ^ (t >> 7)) * (61 | t))) ^ t;
                return ((t ^ (t >> 14))) / 4294967296f;
            }
        }
        public float Range(float min, float max) => min + Next() * (max - min);
    }
}
