using UnityEngine;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection
{
    public class MP_PantsHipDriver_ad_depthfix : MonoBehaviour
    {
        [Header("Required")]
        public PoseLandmarkerRunner_ad runner;
        public Camera targetCamera;

        [Header("Optional Pivot (recommended)")]
        // Put your pants prefab under this pivot so you can adjust local offset per asset.
        public Transform pantsPivot;

        [Header("Root Follow")]
        public Vector3 rootPosOffset = new Vector3(0f, -0.12f, 0f);
        public Vector3 rootRotOffsetEuler = new Vector3(0f, 180f, 180f);

        [Header("Auto Depth (approx)")]
        public bool autoDepth = true;
        public float baseDepthMeters = 2.2f;
        public float refHipWidth01 = 0.22f;
        public float depthMin = 1.2f;
        public float depthMax = 4.0f;

        [Header("Auto Scale (hip width based)")]
        public bool autoScale = true;
        public float scaleMultiplier = 1.25f;
        public float scaleMin = 0.10f;
        public float scaleMax = 10.0f;

        [Header("Smoothing")]
        public float posSmoothTime = 0.06f;
        public float rotLerpSpeed = 14f;
        public float scaleLerpSpeed = 10f;

        private Vector3 _posVel;

        void Reset()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        void LateUpdate()
        {
            if (runner == null || targetCamera == null) return;

            if (!runner.TryGetHips01(out var lH, out var rH)) return;

            float hipWidth01 = Vector2.Distance(lH, rH);
            if (hipWidth01 < 1e-5f) return;

            float depth = baseDepthMeters;
            if (autoDepth)
            {
                depth = baseDepthMeters * (refHipWidth01 / hipWidth01);
                depth = Mathf.Clamp(depth, depthMin, depthMax);
            }

            // Convert hips to world
            Vector3 lw = targetCamera.ViewportToWorldPoint(new Vector3(lH.x, 1f - lH.y, depth));
            Vector3 rw = targetCamera.ViewportToWorldPoint(new Vector3(rH.x, 1f - rH.y, depth));

            Vector3 mid = (lw + rw) * 0.5f;
            Vector3 dir = (rw - lw);
            float hipWidthWorld = dir.magnitude;

            // Position
            Vector3 targetPos = mid + rootPosOffset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _posVel, posSmoothTime);

            // Rotation (stable): use camera forward, hips line as "right", derive "up"
            if (dir.sqrMagnitude > 1e-8f)
            {
                Vector3 forward = targetCamera.transform.forward;
                Vector3 right = dir.normalized;

                Vector3 up = Vector3.Cross(forward, right);
                if (up.sqrMagnitude < 1e-8f) up = targetCamera.transform.up;
                else up.Normalize();

                Quaternion baseRot = Quaternion.LookRotation(forward, up);
                Quaternion targetRot = baseRot * Quaternion.Euler(rootRotOffsetEuler);

                float t = 1f - Mathf.Exp(-rotLerpSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
            }

            // Scale
            if (autoScale)
            {
                float s = hipWidthWorld * scaleMultiplier;
                s = Mathf.Clamp(s, scaleMin, scaleMax);

                Vector3 targetScale = new Vector3(s, s, s);
                float t = 1f - Mathf.Exp(-scaleLerpSpeed * Time.deltaTime);
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);
            }

            // Pivot is only for per-asset local offset tuning (no code needed).
            // Example: adjust pantsPivot.localPosition in inspector for waist height, etc.
        }
    }
}
