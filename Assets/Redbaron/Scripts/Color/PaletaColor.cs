using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Pinta cualquier modelo de Redbaron con un tema de color, sin duplicar materiales ni archivos.
    /// Cada material del modelo pertenece a una zona (bando, pintura, lona, uniforme, casco, insignia, latón).
    /// El script cambia su color con MaterialPropertyBlock, así que el mismo material sirve para todas las unidades,
    /// y enciende la insignia elegida (nodos "Insignia_Escarapela" / "Insignia_Cruz"; la propia del modelo lleva "_N").
    /// Funciona también en el editor, para ver el prefab ya pintado.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PaletaColor : MonoBehaviour
    {
        public enum Origen { Preset, Asset, Json }

        [Tooltip("De dónde sale el tema.")]
        public Origen origen = Origen.Preset;
        public TemaPreset preset = TemaPreset.Original;
        [Tooltip("Tema guardado como asset (Create ▸ Redbaron ▸ Tema de color).")]
        public TemaColor asset;
        [Tooltip("Pega aquí el JSON del botón «Copiar tema para Unity» del hangar.")]
        [TextArea(3, 12)] public string temaJson;
        [Tooltip("Tema en uso, editable a mano cuando el preset es Personalizado.")]
        public TemaDatos tema = new TemaDatos();

        [Header("Textura de material")]
        [Tooltip("Usa el shader Redbaron/LowPolyGrano: pintura moteada, metal cepillado, veta de madera, tela y piedra, para que no parezca plástico.")]
        public bool grano = true;
        [Range(0f, 2f)] public float intensidadGrano = 1f;

        [System.Serializable] class Originales { public Renderer r; public Material[] mats; }
        [SerializeField, HideInInspector] List<Originales> originales = new List<Originales>();

        // Un material con grano por material original, compartido por todas las unidades
        static readonly Dictionary<Material, Material> Cache = new Dictionary<Material, Material>();
        static Shader shGrano;
        static readonly int IdGrano = Shader.PropertyToID("_Grano");

        // Material → (tipo de grano, metálico, rugosidad). Tipos: 0 pintura, 1 metal, 2 madera, 3 tela, 4 piedra, 5 liso
        static readonly Dictionary<string, Vector3> Fisica = new Dictionary<string, Vector3>
        {
            { "Laton", new Vector3(1, 0.65f, 0.42f) }, { "Cobre", new Vector3(1, 0.6f, 0.45f) }, { "Hierro", new Vector3(1, 0.45f, 0.6f) },
            { "Acero", new Vector3(1, 0.5f, 0.5f) }, { "Plata", new Vector3(1, 0.3f, 0.6f) }, { "Oro", new Vector3(1, 0.55f, 0.4f) },
            { "Blindaje", new Vector3(0, 0.45f, 0.55f) }, { "CascoAzul", new Vector3(0, 0.35f, 0.55f) }, { "CascoGris", new Vector3(0, 0.35f, 0.55f) },
            { "Madera", new Vector3(2, 0, 0.85f) }, { "MaderaOscura", new Vector3(2, 0, 0.85f) }, { "Mimbre", new Vector3(2, 0, 0.9f) },
            { "Lona", new Vector3(3, 0, 0.95f) }, { "LonaOscura", new Vector3(3, 0, 0.95f) }, { "Cuero", new Vector3(3, 0, 0.8f) }, { "Goma", new Vector3(3, 0, 0.95f) },
            { "Horizonte", new Vector3(3, 0, 0.9f) }, { "HorizonteOscuro", new Vector3(3, 0, 0.9f) }, { "Feldgrau", new Vector3(3, 0, 0.9f) }, { "FeldgrauOscuro", new Vector3(3, 0, 0.9f) },
            { "Hormigon", new Vector3(4, 0, 0.95f) }, { "Ladrillo", new Vector3(4, 0, 0.9f) }, { "Pizarra", new Vector3(4, 0.1f, 0.8f) }, { "Arena", new Vector3(4, 0, 0.95f) },
            { "Piel", new Vector3(5, 0, 0.85f) }, { "Mejilla", new Vector3(5, 0, 0.9f) }, { "Pelo", new Vector3(5, 0, 0.9f) }, { "Fuego", new Vector3(5, 0, 0.6f) },
            { "Negro", new Vector3(0, 0.2f, 0.7f) }, { "CruzA", new Vector3(0, 0.2f, 0.7f) }, { "Crema", new Vector3(0, 0, 0.9f) }
        };

        // Material → zona y factor de tono (igual que en el hangar)
        static readonly Dictionary<string, KeyValuePair<string, float>> Mapa = new Dictionary<string, KeyValuePair<string, float>>
        {
            { "Equipo", Z("bando", 1f) }, { "EquipoOscuro", Z("bandoOsc", 1f) },
            { "Oliva", Z("pintura", 1f) }, { "Caqui", Z("pintura", 1.12f) }, { "Blindaje", Z("pintura", 0.88f) },
            { "Lona", Z("lona", 1f) }, { "LonaOscura", Z("lona", 0.88f) },
            { "Horizonte", Z("uniforme", 1f) }, { "Feldgrau", Z("uniforme", 1f) }, { "HorizonteOscuro", Z("uniforme", 0.78f) }, { "FeldgrauOscuro", Z("uniforme", 0.78f) },
            { "CascoAzul", Z("casco", 1f) }, { "CascoGris", Z("casco", 1f) },
            { "EscA", Z("ins1", 1f) }, { "CruzA", Z("ins1", 1f) }, { "Azul", Z("ins1", 1f) },
            { "EscB", Z("ins2", 1f) }, { "Rojo", Z("ins2", 1f) },
            { "EscFondo", Z("insFondo", 1f) }, { "CruzFondo", Z("insFondo", 1f) },
            { "Laton", Z("laton", 1f) }
        };
        static KeyValuePair<string, float> Z(string z, float f) => new KeyValuePair<string, float>(z, f);
        static readonly int[] Props = { Shader.PropertyToID("baseColorFactor"), Shader.PropertyToID("_BaseColor"), Shader.PropertyToID("_Color") };

        MaterialPropertyBlock mpb;

        void OnEnable() => Aplicar();
        void OnValidate()
        {
#if UNITY_EDITOR
            // Activar o apagar objetos dentro de OnValidate da avisos: se aplica en el siguiente ciclo del editor.
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) Aplicar(); };
#else
            Aplicar();
#endif
        }

        /// <summary>Cambia a un tema listo desde código (por ejemplo, al asignar la unidad a un bando).</summary>
        public void UsarPreset(TemaPreset p) { origen = Origen.Preset; preset = p; Aplicar(); }

        /// <summary>Usa un tema concreto desde código.</summary>
        public void UsarTema(TemaDatos datos) { origen = Origen.Preset; preset = TemaPreset.Personalizado; tema = datos.Copia(); Aplicar(); }

        /// <summary>Usa el JSON copiado del hangar.</summary>
        public bool UsarJson(string json)
        {
            temaJson = json; origen = Origen.Json;
            if (!TemaDatos.DesdeJson(json, out var d)) return false;
            tema = d; Aplicar(); return true;
        }

        TemaDatos Resolver()
        {
            switch (origen)
            {
                case Origen.Asset: return asset != null ? asset.datos : TemaDatos.Preset(TemaPreset.Original);
                case Origen.Json: return TemaDatos.DesdeJson(temaJson, out var d) ? d : tema;
                default: return preset == TemaPreset.Personalizado ? tema : TemaDatos.Preset(preset);
            }
        }

        [ContextMenu("Aplicar tema")]
        public void Aplicar()
        {
            if (this == null || !gameObject.scene.IsValid()) return; // solo en objetos de escena, nunca en el asset del prefab
            var t = Resolver();
            if (t == null) return;
            PrepararMateriales();
            if (origen != Origen.Preset || preset != TemaPreset.Personalizado) tema = t.Copia();
            if (mpb == null) mpb = new MaterialPropertyBlock();

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    mpb.Clear();
                    if (grano && mats[i].HasProperty(IdGrano)) mpb.SetFloat(IdGrano, intensidadGrano);
                    if (Mapa.TryGetValue(Limpia(mats[i].name), out var z))
                    {
                        Color c = t.Zona(z.Key);
                        if (c.a > 0f)
                        {
                            Color f = new Color(Mathf.Clamp01(c.r * z.Value), Mathf.Clamp01(c.g * z.Value), Mathf.Clamp01(c.b * z.Value), 1f);
                            foreach (int id in Props) if (mats[i].HasProperty(id)) mpb.SetColor(id, f);
                        }
                    }
                    r.SetPropertyBlock(mpb, i);
                }
            }

            foreach (var tr in GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name;
                if (!n.StartsWith("Insignia_") || n.EndsWith("_Malla")) continue;
                bool cruz = n.Contains("Cruz"), propia = n.EndsWith("_N");
                bool on;
                switch (t.insignia)
                {
                    case ModoInsignia.Escarapela: on = !cruz; break;
                    case ModoInsignia.Cruz: on = cruz; break;
                    case ModoInsignia.Ninguna: on = false; break;
                    default: on = propia; break;
                }
                if (tr.gameObject.activeSelf != on) tr.gameObject.SetActive(on);
            }
        }

        // Devuelve los materiales originales y, si hay grano, los cambia por su versión con el shader LowPolyGrano.
        void PrepararMateriales()
        {
            foreach (var o in originales) if (o != null && o.r != null && o.mats != null) o.r.sharedMaterials = o.mats;
            originales.Clear();
            if (!grano) return;
            if (shGrano == null) shGrano = Shader.Find("Redbaron/LowPolyGrano");
            if (shGrano == null) return; // el shader no está en el proyecto: se queda con los materiales de siempre
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                var nuevos = new Material[mats.Length];
                bool cambia = false;
                for (int i = 0; i < mats.Length; i++) { nuevos[i] = MaterialGrano(mats[i]); cambia |= nuevos[i] != mats[i]; }
                if (!cambia) continue;
                originales.Add(new Originales { r = r, mats = mats });
                r.sharedMaterials = nuevos;
            }
        }

        static Material MaterialGrano(Material src)
        {
            if (src == null || src.shader == shGrano) return src;
            string n = Limpia(src.name);
            if (n == "Cristal") return src; // el cristal sigue transparente
            if (Cache.TryGetValue(src, out var m) && m != null) return m;
            m = new Material(shGrano) { name = n, hideFlags = HideFlags.DontSave };
            Color c = Color.white;
            foreach (int id in Props) if (src.HasProperty(id)) { c = src.GetColor(id); break; }
            Vector3 f = Fisica.TryGetValue(n, out var v) ? v : new Vector3(0, 0, 0.8f);
            m.SetColor("_Color", c);
            m.SetFloat("_Tipo", f.x);
            m.SetFloat("_Metallic", f.y);
            m.SetFloat("_Glossiness", 1f - f.z);
            Cache[src] = m;
            return m;
        }

        // Quita los sufijos que añaden Unity y los importadores: "Equipo (Instance)", "Equipo.001"…
        static string Limpia(string n)
        {
            int k = n.IndexOf(" (");
            if (k >= 0) n = n.Substring(0, k);
            k = n.IndexOf('.');
            if (k >= 0) n = n.Substring(0, k);
            return n.Trim();
        }
    }
}
