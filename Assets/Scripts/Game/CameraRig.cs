using UnityEngine;

namespace ChessboardTactics.Game
{
    /// <summary>Frames the whole board from the player's side for any aspect ratio and adds screen shake.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        const float Pitch = 54f;
        const float HalfWidth = 5.3f;   // board plus frame, with a margin
        const float HalfDepth = 5.6f;
        static readonly Vector3 Target = new Vector3(0f, 0f, 0.15f);

        Camera view;
        float aspect = -1f;
        float distance = 16f;
        float trauma;
        float orbit;

        void Awake()
        {
            view = GetComponent<Camera>();
            Frame();
        }

        /// <summary>Adds screen shake; 1 is a strong hit.</summary>
        public void Shake(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        /// <summary>A slow sideways drift for the title screen; 0 keeps the camera still.</summary>
        public float Drift { get; set; }

        void LateUpdate()
        {
            if (!Mathf.Approximately(aspect, view.aspect))
            {
                Frame();
            }
            orbit = Mathf.Lerp(orbit, Mathf.Sin(Time.unscaledTime * 0.25f) * 14f * Drift, Time.unscaledDeltaTime * 2f);
            Quaternion rotation = Quaternion.Euler(Pitch, orbit, 0f);
            Vector3 position = Target + rotation * new Vector3(0f, 0f, -distance);
            trauma = Mathf.Max(trauma - Time.unscaledDeltaTime * 2f, 0f);
            float strength = trauma * trauma;
            float t = Time.unscaledTime * 30f;
            var shake = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (0.35f * strength);
            transform.SetPositionAndRotation(position + rotation * shake, rotation);
        }

        void Frame()
        {
            aspect = view.aspect;
            float halfVertical = view.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * aspect);
            // The board seen at an angle is foreshortened in depth.
            float depth = HalfDepth * Mathf.Sin(Pitch * Mathf.Deg2Rad) + 0.6f;
            distance = Mathf.Max(HalfWidth / Mathf.Tan(halfHorizontal), depth / Mathf.Tan(halfVertical));
            transform.SetPositionAndRotation(Target + Quaternion.Euler(Pitch, 0f, 0f) * new Vector3(0f, 0f, -distance),
                Quaternion.Euler(Pitch, 0f, 0f));
        }
    }
}
