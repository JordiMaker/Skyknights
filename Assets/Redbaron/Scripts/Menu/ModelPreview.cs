using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Redbaron
{
    /// <summary>
    /// Shows a 3D model inside a UI rect: a small studio (camera, turntable) far from everything else renders
    /// into a texture that this RawImage displays. The model turns slowly; drag to turn it by hand.
    /// Each preview gets its own studio, so several can be on screen at once.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ModelPreview : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public float spinSpeed = 18f;
        [Tooltip("Camera height above the model's centre, as a share of the distance.")]
        public float elevation = 0.32f;

        static int studios;
        RawImage view;
        Camera cam;
        Transform turntable, current;
        RenderTexture rt;
        float yaw = 135f, pitch, idle;
        bool dragging;

        void Awake()
        {
            view = GetComponent<RawImage>();
            var studio = new GameObject("Estudio_" + name).transform;
            studio.position = new Vector3(20000f + 1000f * studios++, 0, 0);
            turntable = new GameObject("Plataforma").transform;
            turntable.SetParent(studio, false);
            cam = new GameObject("Camara").AddComponent<Camera>();
            cam.transform.SetParent(studio, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.fieldOfView = 30f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 400f;
            cam.enabled = false;
        }

        /// <summary>Puts a model on the turntable (the previous one is destroyed) and frames it.</summary>
        public void Show(GameObject model)
        {
            if (current != null) Destroy(current.gameObject);
            current = null;
            if (model == null) return;
            current = model.transform;
            current.SetParent(turntable, false);
            current.localPosition = Vector3.zero; current.localRotation = Quaternion.identity;
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            current.position -= b.center - turntable.position; // centre the model on the turntable
            float radius = Mathf.Max(0.5f, b.extents.magnitude);
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            Vector3 dir = new Vector3(0, elevation, -1f).normalized;
            cam.transform.position = turntable.position + dir * dist;
            cam.transform.LookAt(turntable.position);
            cam.farClipPlane = dist + radius * 4f;
            yaw = 135f; pitch = 0f; // three-quarter view from the front left
        }

        void LateUpdate()
        {
            bool visible = view.isActiveAndEnabled && current != null;
            cam.enabled = visible;
            if (!visible) return;
            EnsureTexture();
            if (!dragging)
            {
                idle += Time.unscaledDeltaTime;
                if (idle > 1.5f) yaw += spinSpeed * Time.unscaledDeltaTime;
                pitch = Mathf.MoveTowards(pitch, 0, 20f * Time.unscaledDeltaTime);
            }
            turntable.localRotation = Quaternion.Euler(pitch, yaw, 0);
        }

        void EnsureTexture()
        {
            var size = ((RectTransform)transform).rect.size;
            var canvas = GetComponentInParent<Canvas>();
            float k = canvas != null ? canvas.scaleFactor : 1f;
            int w = Mathf.Clamp(Mathf.RoundToInt(size.x * k), 64, 2048), h = Mathf.Clamp(Mathf.RoundToInt(size.y * k), 64, 2048);
            if (rt != null && rt.width == w && rt.height == h) return;
            if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = QualitySettings.antiAliasing > 1 ? QualitySettings.antiAliasing : 2, name = "Vista_" + name };
            cam.targetTexture = rt;
            cam.aspect = w / (float)h;
            view.texture = rt;
            view.color = Color.white;
        }

        public void OnBeginDrag(PointerEventData e) { dragging = true; }
        public void OnEndDrag(PointerEventData e) { dragging = false; idle = 0; }
        public void OnDrag(PointerEventData e)
        {
            yaw -= e.delta.x * 0.35f;
            pitch = Mathf.Clamp(pitch + e.delta.y * 0.2f, -25f, 35f);
        }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (cam != null) Destroy(cam.transform.parent.gameObject);
        }
    }
}
