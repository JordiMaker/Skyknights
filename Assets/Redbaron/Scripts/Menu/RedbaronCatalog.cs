using System;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// What the menu's cards show: the hangar ships and the buildings, with their text and values.
    /// Data only (Assets > Create > Redbaron > Catalog); the menu draws the 3D models from it.
    /// The default content is written by Redbaron > Crear menú principal.
    /// </summary>
    [CreateAssetMenu(menuName = "Redbaron/Catalog", fileName = "Redbaron_Catalogo")]
    public class RedbaronCatalog : ScriptableObject
    {
        [Serializable]
        public class Stat
        {
            public string label, value;
            public Stat(string label, string value) { this.label = label; this.value = value; }
        }

        [Serializable]
        public class Ship
        {
            public string name;
            [Tooltip("The model (an OBJ from Assets/Redbaron/Naves).")]
            public GameObject model;
            [Tooltip("The side the ship was designed for; its card shows it in that side's colours.")]
            public Team side;
            public string role;
            [Tooltip("Width × height × length.")]
            public string size;
            [TextArea(2, 5)] public string description;
        }

        public enum BuildingModel { CommandPost, Bunker, Hangar, RefillPoint, FlakTower }

        [Serializable]
        public class Building
        {
            public string name;
            public BuildingModel model;
            [Tooltip("A model to show instead of the one built in code (the anti-aircraft tower uses Torre_Flak).")]
            public GameObject prefab;
            [Tooltip("How many there are, e.g. \"Uno por bando\".")]
            public string count;
            public Stat[] stats;
            [TextArea(2, 5)] public string description;
        }

        public Ship[] ships;
        [Tooltip("Combat values shown on every ship card. In the battle all ships fly the same for now; only the model changes.")]
        public Stat[] shipStats;
        [TextArea(1, 3)] public string shipStatsNote = "En la batalla todas las naves vuelan igual por ahora: lo que cambia es el modelo.";
        public Building[] buildings;
    }
}
