using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Engine sound for a plane: a looping clip that plays while the plane is alive.
    /// Your plane: its pitch follows your speed, and inside the cockpit the cockpit clip plays.
    /// AI planes: 3D sound (louder as they pass close), each with a slightly different pitch so they do not phase.
    /// </summary>
    public class PlaneEngineAudio : MonoBehaviour
    {
        public BiplaneAI plane;
        public AudioClip exterior;
        [Tooltip("Played instead of the exterior clip while you sit in the cockpit (your plane only).")]
        public AudioClip cockpit;
        [Range(0f, 1f)] public float volume = 0.8f;
        public Vector2 pitchRange = new Vector2(0.8f, 1.35f);

        AudioSource src;
        float basePitch = 1f;

        void Start()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.loop = true; src.playOnAwake = false;
            bool mine = plane != null && plane.playerControlled;
            src.spatialBlend = mine ? 0.3f : 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = mine ? 10f : 7f; src.maxDistance = 160f;
            src.dopplerLevel = 0.6f;
            src.volume = mine ? volume * 0.6f : volume;
            basePitch = mine ? 1f : Random.Range(0.9f, 1.1f);
            src.clip = exterior;
        }

        void Update()
        {
            if (src == null || plane == null) return;
            if (!plane.Alive) { if (src.isPlaying) src.Stop(); return; }

            bool inCockpit = plane.playerControlled && cockpit != null && RedbaronCameras.Instance != null && RedbaronCameras.Instance.mode == RedbaronCameras.Mode.Cockpit;
            var want = inCockpit ? cockpit : exterior;
            if (want != null && src.clip != want) { src.clip = want; src.Play(); }
            if (!src.isPlaying && src.clip != null) src.Play();

            float t = plane.playerControlled ? Mathf.InverseLerp(plane.minSpeed, plane.maxSpeed, plane.CurrentSpeed) : 0.4f;
            src.pitch = basePitch * Mathf.Lerp(pitchRange.x, pitchRange.y, t) * Mathf.Max(0.5f, Time.timeScale > 1 ? 1.1f : 1f);
        }
    }
}
