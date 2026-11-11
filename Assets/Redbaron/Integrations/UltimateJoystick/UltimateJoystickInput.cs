using FormatGames.VirtualJoystick.Lite;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Uses Ultimate Joystick Lite (Format Games) as the mobile flight joystick instead of the built-in one.
    /// Its DefaultJoystick prefab is placed bottom left inside MobileFlightControls' safe area; its axis drives
    /// roll (left/right) and speed (up/down). The FUEGO, BOMBA and CÁM buttons stay as they are.
    /// Remove or disable this component to go back to the built-in joystick.
    /// </summary>
    public class UltimateJoystickInput : MonoBehaviour
    {
        [Tooltip("Ultimate Joystick Lite's DefaultJoystick prefab.")]
        public GameObject joystickPrefab;
        [Tooltip("Centre of the joystick from the bottom-left corner of the safe area (reference 1920 × 1080).")]
        public Vector2 position = new Vector2(290, 270);
        public float scale = 1.3f;

        Joystick joystick;

        void Start()
        {
            var controls = FindAnyObjectByType<MobileFlightControls>();
            if (controls == null || controls.SafeArea == null || joystickPrefab == null) return;
            var go = Instantiate(joystickPrefab, controls.SafeArea);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.anchoredPosition = position;
            rt.localScale = Vector3.one * scale;
            var extras = go.transform.Find("Body/Complements"); // arrow, cross and tail need the full version
            if (extras != null) extras.gameObject.SetActive(false);
            joystick = go.GetComponentInChildren<Joystick>();
            MobileFlightControls.ExternalJoystick = Axis;
        }

        /// <summary>How far the handle is pushed (0..1 on each axis). The package's own AXIS is all-or-nothing
        /// (a unit direction past a large dead zone), so it is not used: proportional input allows fine corrections.</summary>
        Vector2 Axis()
        {
            if (joystick == null || !joystick.isActiveAndEnabled || !joystick.IS_USING || joystick.Handle == null) return Vector2.zero;
            float r = Mathf.Max(1f, joystick.Ratio); // the handle is clamped to Ratio, so this is full deflection
            return Vector2.ClampMagnitude((Vector2)joystick.Handle.rectTransform.localPosition / r, 1f);
        }

        void OnDisable()
        {
            if (MobileFlightControls.ExternalJoystick == (System.Func<Vector2>)Axis) MobileFlightControls.ExternalJoystick = null;
        }

        void OnEnable()
        {
            if (joystick != null) MobileFlightControls.ExternalJoystick = Axis;
        }
    }
}
