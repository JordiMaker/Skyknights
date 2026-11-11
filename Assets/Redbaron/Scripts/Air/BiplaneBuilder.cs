using UnityEngine;

namespace Redbaron
{
    public enum PlaneModel { FokkerTriplano, Albatros, Camel }

    /// <summary>Genera biplanos y el triplano low poly de la Primera Guerra Mundial (escala de juego 60 %).</summary>
    public static class BiplaneBuilder
    {
        public static GameObject Build(PlaneModel model, Material mat, out Transform propeller, out Color bodyColor)
        {
            Color body, wing;
            switch (model)
            {
                case PlaneModel.FokkerTriplano: body = new Color32(0xa3, 0x27, 0x1f, 255); wing = new Color32(0xb5, 0x33, 0x2a, 255); break;
                case PlaneModel.Albatros: body = new Color32(0x6b, 0x75, 0x50, 255); wing = new Color32(0x8c, 0x94, 0x6c, 255); break;
                default: body = new Color32(0x7d, 0x7a, 0x50, 255); wing = new Color32(0xc9, 0xc1, 0x9a, 255); break;
            }
            bodyColor = body;
            Color dark = new Color32(0x3b, 0x2f, 0x22, 255), metal = new Color32(0x4a, 0x47, 0x40, 255);
            var acc = new MeshAccumulator();
            acc.AddBox(Vector3.zero, new Vector3(0.85f, 0.85f, 5.4f), body);
            acc.AddCylinder(new Vector3(0, 0, 2.6f), new Vector3(0, 0, 3.2f), 0.55f, 0.55f, 8, metal);
            float[] wings = model == PlaneModel.FokkerTriplano ? new[] { 1.35f, 0.55f, -0.3f } : new[] { 1.3f, -0.35f };
            float span = model == PlaneModel.FokkerTriplano ? 7.2f : 8.5f;
            for (int i = 0; i < wings.Length; i++) acc.AddBox(new Vector3(0, wings[i], 0.9f), new Vector3(span - i * 0.6f, 0.12f, 1.2f), wing);
            float top = wings[0], bottom = wings[wings.Length - 1];
            foreach (int s in new[] { -1, 1 })
            {
                acc.AddBox(new Vector3(s * span * 0.35f, (top + bottom) / 2, 0.9f), new Vector3(0.08f, top - bottom, 0.08f), dark);
                acc.AddBox(new Vector3(s * 0.6f, -0.75f, 1.6f), new Vector3(0.1f, 0.7f, 0.1f), dark);
                float x = s * (span / 2 - 1);
                if (model == PlaneModel.Camel)
                {
                    acc.AddCylinder(new Vector3(x, top + 0.06f, 0.9f), new Vector3(x, top + 0.08f, 0.9f), 0.55f, 0.55f, 12, new Color32(0x2f, 0x4f, 0x8a, 255));
                    acc.AddCylinder(new Vector3(x, top + 0.08f, 0.9f), new Vector3(x, top + 0.1f, 0.9f), 0.37f, 0.37f, 12, new Color32(0xec, 0xe6, 0xd2, 255));
                    acc.AddCylinder(new Vector3(x, top + 0.1f, 0.9f), new Vector3(x, top + 0.12f, 0.9f), 0.18f, 0.18f, 12, new Color32(0xa3, 0x27, 0x1f, 255));
                }
                else
                {
                    acc.AddBox(new Vector3(x, top + 0.07f, 0.9f), new Vector3(1.0f, 0.02f, 1.0f), new Color32(0xec, 0xe6, 0xd2, 255));
                    acc.AddBox(new Vector3(x, top + 0.09f, 0.9f), new Vector3(0.8f, 0.03f, 0.22f), new Color32(0x1c, 0x1a, 0x16, 255));
                    acc.AddBox(new Vector3(x, top + 0.09f, 0.9f), new Vector3(0.22f, 0.03f, 0.8f), new Color32(0x1c, 0x1a, 0x16, 255));
                }
            }
            acc.AddBox(new Vector3(0, 0.1f, -2.5f), new Vector3(2.8f, 0.1f, 0.9f), wing);
            acc.AddBox(new Vector3(0, 0.6f, -2.5f), new Vector3(0.1f, 1.0f, 0.9f), body);
            acc.AddBox(new Vector3(0, 0.55f, -0.4f), new Vector3(0.5f, 0.4f, 0.5f), new Color32(0x2a, 0x26, 0x20, 255));

            var go = acc.ToObject(model.ToString(), null, mat, false);
            var prop = new MeshAccumulator();
            prop.AddBox(Vector3.zero, new Vector3(2.6f, 0.18f, 0.08f), dark);
            var p = prop.ToObject("Helice", go.transform, mat, false);
            p.transform.localPosition = new Vector3(0, 0, 3.25f);
            propeller = p.transform;
            go.transform.localScale = Vector3.one * 0.6f;
            return go;
        }
    }
}
