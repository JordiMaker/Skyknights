using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Cámaras extra sobre la cámara de Quick RTS.
    /// R: cámara libre (la de Quick RTS) · C: detrás de tu avión (o del Fokker) · V: cenital siguiendo a ese avión ·
    /// F: primera persona, desde la cabina (teclas configurables). El botón CÁM del móvil las recorre todas.
    /// En la cenital, la rueda del ratón cambia la altura.
    /// </summary>
    public class RedbaronCameras : MonoBehaviour
    {
        public enum Mode { Free, Chase, Top, Cockpit }

        public Camera cam;
        [Tooltip("Scripts de cámara de Quick RTS que hay que desactivar mientras se sigue al avión.")]
        public Behaviour[] rtsCameraScripts;
        public AirWar airWar;
        public float topHeight = 90f;
        public Mode mode = Mode.Free;
        [Tooltip("Camera at Play. Chase = behind your plane (or the Fokker when nobody flies).")]
        public Mode startMode = Mode.Free;

        public static RedbaronCameras Instance { get; private set; }
        /// <summary>The plane the chase and top cameras follow: yours if you fly one, otherwise the Fokker.</summary>
        BiplaneAI Followed => airWar == null ? null : airWar.Player != null ? airWar.Player : airWar.Fokker;

        [Tooltip("Keys for the Quick RTS camera, the camera behind your plane and the top view.")]
        public KeyCode freeKey = KeyCode.R, chaseKey = KeyCode.C, topKey = KeyCode.V, cockpitKey = KeyCode.F;
        public string KeysText => $"{freeKey} / {chaseKey} / {cockpitKey} / {topKey}";

        [Tooltip("Field of view of the chase and top cameras. The Quick RTS camera is orthographic; it is restored with the free-camera key.")]
        public float flightFov = 60f;
        [Tooltip("Chase camera: distance behind the plane and height above it (m).")]
        public float chaseBack = 15f, chaseUp = 5.5f;
        [Tooltip("First-person (cockpit) camera: field of view, and height above the plane's centre (m). Your own plane is hidden in this view.")]
        public float cockpitFov = 70f, cockpitUp = 0.7f;
        Transform hiddenVisual;

        Vector3 savedPos; Quaternion savedRot;
        bool savedOrtho; float savedSize, savedFov;

        void Awake()
        {
            Instance = this;
            if (cam == null) cam = Camera.main;
            if (airWar == null) airWar = FindAnyObjectByType<AirWar>();
            // The Quick RTS camera has no AudioListener; without one nothing (engines included) is heard.
            if (cam != null && FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        }

        void Start()
        {
            if (startMode != Mode.Free) SetMode(startMode);
        }

        void SetMode(Mode m)
        {
            if (mode == Mode.Free && m != Mode.Free)
            {
                savedPos = cam.transform.position; savedRot = cam.transform.rotation;
                savedOrtho = cam.orthographic; savedSize = cam.orthographicSize; savedFov = cam.fieldOfView;
                cam.orthographic = false; cam.fieldOfView = flightFov;
            }
            if (m == Mode.Free && mode != Mode.Free)
            {
                cam.transform.SetPositionAndRotation(savedPos, savedRot);
                cam.orthographic = savedOrtho; cam.orthographicSize = savedSize; cam.fieldOfView = savedFov;
            }
            if (m != Mode.Free) cam.fieldOfView = m == Mode.Cockpit ? cockpitFov : flightFov;
            if (m != Mode.Cockpit) ShowModel(null);
            mode = m;
            if (rtsCameraScripts != null) foreach (var b in rtsCameraScripts) if (b != null) b.enabled = m == Mode.Free;
        }

        void Update()
        {
            if (Input.GetKeyDown(freeKey)) SetMode(Mode.Free);
            if (Input.GetKeyDown(chaseKey)) SetMode(Mode.Chase);
            if (Input.GetKeyDown(topKey)) SetMode(Mode.Top);
            if (Input.GetKeyDown(cockpitKey)) SetMode(Mode.Cockpit);
            // Mobile CÁM button: behind the plane → cockpit → top view → Quick RTS view → behind the plane...
            if (MobileFlightControls.CameraPressed)
                SetMode(mode == Mode.Chase ? Mode.Cockpit : mode == Mode.Cockpit ? Mode.Top : mode == Mode.Top ? Mode.Free : Mode.Chase);
            if (mode == Mode.Top) topHeight = Mathf.Clamp(topHeight - Input.mouseScrollDelta.y * 6f, 25f, 260f);
        }

        /// <summary>Hides the given plane model (the one you sit in) and shows again the one hidden before.</summary>
        void ShowModel(Transform hide)
        {
            if (hiddenVisual == hide) return;
            if (hiddenVisual != null) foreach (var r in hiddenVisual.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            hiddenVisual = hide;
            if (hide != null) foreach (var r in hide.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        void LateUpdate()
        {
            if (mode == Mode.Free || cam == null || Followed == null) return;
            var f = Followed;
            if (!f.Alive || f.Visual == null) return;
            Vector3 p = f.transform.position;
            float k = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 6f);
            if (mode == Mode.Cockpit)
            {
                // Sitting in the plane: the view turns and banks with it.
                ShowModel(f.Visual);
                cam.transform.SetPositionAndRotation(p + f.transform.up * (f.BodyRadius * 0.2f + cockpitUp), f.transform.rotation);
                return;
            }
            if (mode == Mode.Chase)
            {
                Vector3 fwd = f.transform.forward; fwd.y = 0; fwd.Normalize();
                Vector3 want = p - fwd * chaseBack + Vector3.up * chaseUp;
                cam.transform.position = Vector3.Lerp(cam.transform.position, want, k);
                Vector3 look = p + fwd * 4f + Vector3.up * 0.5f; // a little ahead of the plane, so you see where you are going
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look - cam.transform.position), k * 2);
            }
            else
            {
                Vector3 want = p + Vector3.up * topHeight;
                cam.transform.position = Vector3.Lerp(cam.transform.position, want, k);
                cam.transform.rotation = Quaternion.Euler(90, 0, 0);
            }
        }
    }
}
