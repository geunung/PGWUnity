using UnityEngine;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection
{
    public class SimpleClothFollow2D : MonoBehaviour
    {
        [Header("Refs")]
        public PoseLandmarkerRunner runner;
        public Camera targetCamera;

        [Header("Tuning")]
        public float depthMeters = 2.0f;      // distance from camera
        public Vector3 positionOffset = Vector3.zero;
        public Vector3 rotationOffsetEuler = new Vector3(0f, 180f, 180f);
        public float scaleMultiplier = 1.0f;

        void Reset()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        void LateUpdate()
        {
            if (runner == null || targetCamera == null) return;

            if (!runner.TryGetShoulders01(out var l01, out var r01)) return;

            // MediaPipe normalized: origin at top-left. Unity viewport: origin at bottom-left.
            var lvp = new Vector3(l01.x, 1f - l01.y, depthMeters);
            var rvp = new Vector3(r01.x, 1f - r01.y, depthMeters);

            var lw = targetCamera.ViewportToWorldPoint(lvp);
            var rw = targetCamera.ViewportToWorldPoint(rvp);

            var mid = (lw + rw) * 0.5f;
            var dir = (rw - lw);
            var shoulderWidth = dir.magnitude;

            // position
            transform.position = mid + positionOffset;

            // rotation: face camera, align X axis to shoulder line
            var forward = targetCamera.transform.forward;
            if (dir.sqrMagnitude > 1e-6f)
            {
                var right = dir.normalized;
                var up = Vector3.Cross(forward, right).normalized;
                var rot = Quaternion.LookRotation(forward, up);
                transform.rotation = rot * Quaternion.Euler(rotationOffsetEuler);
            }

            // scale based on shoulder width
            var s = shoulderWidth * scaleMultiplier;
            transform.localScale = new Vector3(s, s, s);
        }
    }
}
