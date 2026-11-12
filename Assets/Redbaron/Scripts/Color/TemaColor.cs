using System;
using UnityEngine;

namespace Redbaron
{
    public enum ModoInsignia { Propia, Escarapela, Cruz, Ninguna }
    public enum TemaPreset { Original, Verde, Gris, Arena, Naval, Invierno, BaronRojo, Noche, Personalizado }

    /// <summary>
    /// Colores de un tema. Un color con alfa 0 significa "no tocar": esa zona conserva el color original del modelo.
    /// </summary>
    [Serializable]
    public class TemaDatos
    {
        public string nombre = "Personalizado";
        public ModoInsignia insignia = ModoInsignia.Propia;
        public Color bando = Color.clear;
        public Color bandoOscuro = Color.clear;
        public Color pintura = Color.clear;
        public Color lona = Color.clear;
        public Color uniforme = Color.clear;
        public Color casco = Color.clear;
        public Color insignia1 = Color.clear;
        public Color insignia2 = Color.clear;
        public Color insigniaFondo = Color.clear;
        public Color laton = Color.clear;

        public Color Zona(string zona)
        {
            switch (zona)
            {
                case "bando": return bando;
                case "bandoOsc": return bandoOscuro;
                case "pintura": return pintura;
                case "lona": return lona;
                case "uniforme": return uniforme;
                case "casco": return casco;
                case "ins1": return insignia1;
                case "ins2": return insignia2;
                case "insFondo": return insigniaFondo;
                case "laton": return laton;
                default: return Color.clear;
            }
        }

        public TemaDatos Copia() => (TemaDatos)MemberwiseClone();

        static Color H(string hex) { return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.clear; }

        static TemaDatos P(string nombre, ModoInsignia ins, params string[] c) => new TemaDatos
        {
            nombre = nombre, insignia = ins,
            bando = H(c[0]), bandoOscuro = H(c[1]), pintura = H(c[2]), lona = H(c[3]), uniforme = H(c[4]),
            casco = H(c[5]), insignia1 = H(c[6]), insignia2 = H(c[7]), insigniaFondo = H(c[8]), laton = H(c[9])
        };

        /// <summary>Los mismos temas que el panel Pintura del hangar.</summary>
        public static TemaDatos Preset(TemaPreset p)
        {
            switch (p)
            {
                case TemaPreset.Verde: return P("Verde", ModoInsignia.Escarapela, "#3f6b3a", "#2c4a29", "#55663a", "#b9c08f", "#6f7f4e", "#4f5b3a", "#2c4a29", "#7fae45", "#ece6d2", "#b5893b");
                case TemaPreset.Gris: return P("Gris", ModoInsignia.Cruz, "#6e737a", "#4a4e54", "#6b6e70", "#c9c9c2", "#7c8085", "#55595e", "#1c1a17", "#9aa0a6", "#ece6d2", "#b5893b");
                case TemaPreset.Arena: return P("Arena", ModoInsignia.Escarapela, "#c2a46a", "#8f7444", "#b09a6a", "#dccfa8", "#b8a578", "#9c8a5c", "#7a3a20", "#e0b45a", "#f0e6cc", "#b5893b");
                case TemaPreset.Naval: return P("Naval", ModoInsignia.Escarapela, "#2f4f8a", "#22385f", "#3e4a5c", "#c9cdd2", "#34405a", "#3a4660", "#22385f", "#c9a24a", "#ece6d2", "#c9a24a");
                case TemaPreset.Invierno: return P("Invierno", ModoInsignia.Cruz, "#d8dde0", "#9aa3a8", "#e4e6e3", "#f2f2ee", "#cfd4d6", "#b9c0c3", "#3a4a5a", "#a3271f", "#ffffff", "#b5893b");
                case TemaPreset.BaronRojo: return P("Barón Rojo", ModoInsignia.Cruz, "#b32418", "#7a1c16", "#9c2a1e", "#b8352a", "#7a2a22", "#5d1f1a", "#1c1a17", "#ece6d2", "#ece6d2", "#c9a24a");
                case TemaPreset.Noche: return P("Noche", ModoInsignia.Cruz, "#2a2d33", "#17191c", "#26292e", "#3a3d42", "#2e3238", "#202326", "#8a8f96", "#c9a24a", "#4a4e54", "#8a6a32");
                default: return new TemaDatos { nombre = "Originales", insignia = ModoInsignia.Propia };
            }
        }

        [Serializable] class JsonInterno { public string nombre, insignia, bando, bandoOsc, pintura, lona, uniforme, casco, ins1, ins2, insFondo, laton; }
        [Serializable] class JsonTema { public string modelo, id; public JsonInterno tema; }

        /// <summary>Lee el JSON que copia el botón «Copiar tema para Unity» del hangar.</summary>
        public static bool DesdeJson(string json, out TemaDatos datos)
        {
            datos = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            JsonTema j;
            try { j = JsonUtility.FromJson<JsonTema>(json); } catch { return false; }
            if (j == null || j.tema == null) return false;
            var t = j.tema;
            datos = new TemaDatos
            {
                nombre = string.IsNullOrEmpty(t.nombre) ? "Personalizado" : t.nombre,
                insignia = t.insignia == "escarapela" ? ModoInsignia.Escarapela : t.insignia == "cruz" ? ModoInsignia.Cruz : t.insignia == "ninguna" ? ModoInsignia.Ninguna : ModoInsignia.Propia,
                bando = Opc(t.bando), bandoOscuro = Opc(t.bandoOsc), pintura = Opc(t.pintura), lona = Opc(t.lona), uniforme = Opc(t.uniforme),
                casco = Opc(t.casco), insignia1 = Opc(t.ins1), insignia2 = Opc(t.ins2), insigniaFondo = Opc(t.insFondo), laton = Opc(t.laton)
            };
            return true;
        }
        static Color Opc(string hex) => string.IsNullOrEmpty(hex) ? Color.clear : H(hex);
    }

    /// <summary>Tema guardado como asset: Assets ▸ Create ▸ Redbaron ▸ Tema de color.</summary>
    [CreateAssetMenu(fileName = "TemaColor", menuName = "Redbaron/Tema de color")]
    public class TemaColor : ScriptableObject
    {
        public TemaDatos datos = new TemaDatos();

        [ContextMenu("Cargar preset Verde")] void CargarVerde() => datos = TemaDatos.Preset(TemaPreset.Verde);
        [ContextMenu("Cargar preset Gris")] void CargarGris() => datos = TemaDatos.Preset(TemaPreset.Gris);
    }
}
