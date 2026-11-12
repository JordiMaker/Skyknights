using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// The colours each side flies in, built on the Redbaron colour kit (PaletaColor and its themes):
    /// the Entente in the Naval theme with roundels, the Central Powers in the Green theme with crosses.
    /// The player's plane is painted in the Red Baron theme but keeps its own side's insignia, so it stands out
    /// from every other plane and still shows which side it fights for.
    /// </summary>
    public static class TeamLivery
    {
        /// <summary>The theme for a plane of this side (the player's own plane when player is true).</summary>
        public static TemaDatos For(Team team, bool player)
        {
            var side = TemaDatos.Preset(team == Team.Centrales ? TemaPreset.Verde : TemaPreset.Naval);
            side.insignia = team == Team.Centrales ? ModoInsignia.Cruz : ModoInsignia.Escarapela;
            if (!player) return side;
            var red = TemaDatos.Preset(TemaPreset.BaronRojo);
            red.nombre = "Barón Rojo · " + side.nombre;
            red.insignia = side.insignia;
            red.insignia1 = side.insignia1; red.insignia2 = side.insignia2; red.insigniaFondo = side.insigniaFondo;
            return red;
        }

        /// <summary>Whether a model is a Redbaron hangar ship, painted by colour zones (it has "Equipo" materials).</summary>
        public static bool HasZones(Renderer[] rs)
        {
            foreach (var r in rs) foreach (var m in r.sharedMaterials) if (m != null && m.name.StartsWith("Equipo")) return true;
            return false;
        }

        /// <summary>Paints a hangar ship for its side and switches on the side's insignia. Returns the main side colour.</summary>
        public static Color Apply(GameObject model, Team team, bool player)
        {
            var theme = For(team, player);
            var paint = model.GetComponent<PaletaColor>();
            if (paint == null) paint = model.AddComponent<PaletaColor>();
            paint.UsarTema(theme);
            return theme.bando;
        }
    }
}
