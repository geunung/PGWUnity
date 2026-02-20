// MP_HmPantsRigDriver_ad_depthfix_spin.cs
using System;
using System.Reflection;
using UnityEngine;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection
{
    // 바지 드라이버 (최종: yaw + 위치/스케일 + 소프트 하체 느낌)
    // - PantsRoot(이 스크립트): 위치/스케일
    // - PantsPivot: yaw(좌우 회전)만
    // - PantsVisual: 좌우 기울기(roll)로만 "다리 따라오는 느낌" 살짝 추가
    // - 다리 본(LeftUpLeg/RightUpLeg)은 건드리지 않는다 (교차/벌어짐 방지)
    public class MP_HmPantsRigDriver_ad_depthfix_spin : MonoBehaviour
    {
        [Header("Required")]
        public PoseLandmarkerRunner_ad_spin runner;
        public Camera targetCamera;

        [Header("Scene Anchors")]
        public Transform pantsPivot;

        [Header("Optional Visual (recommended)")]
        public Transform pantsVisual;

        [Header("Root Follow (position only)")]
        public bool driveRoot = true;
        public Vector3 rootPosOffset = new Vector3(0f, -0.12f, 0f);

        [Header("Auto Depth (approx)")]
        public bool autoDepth = false;
        public float baseDepthMeters = 2.2f;
        public float refHipWidth01 = 0.22f;
        public float depthMin = 1.2f;
        public float depthMax = 4.0f;

        [Header("Auto Scale (hip width based)")]
        public bool autoScale = true;
        public float scaleMultiplier = 2.0f;

        [Header("Mannequin Body Scale (optional)")]
        [Tooltip("Drag your OutfitScaleCalibrator component here (any MonoBehaviour with float 'bodyScaleFactor' field/property).")]
        public MonoBehaviour bodyScaleProvider;

        [Tooltip("Member name to read from bodyScaleProvider (field or property). Default: bodyScaleFactor")]
        public string bodyScaleMemberName = "bodyScaleFactor";

        [Tooltip("If provider missing, use this.")]
        public float bodyScaleFallback = 1.0f;

        [Tooltip("Multiply autoscale by mannequin-derived scale factor.")]
        public bool multiplyAutoScaleByBodyScale = true;

        [Range(0f, 1f)]
        [Tooltip("0 = ignore mannequin scale, 1 = fully apply mannequin scale.")]
        public float bodyScaleInfluence = 1.0f;

        [Header("Scale Lock")]
        public bool allowAutoScaleUpdate = true;
        private Vector3 _lockedScale = Vector3.one;
        private bool _hasLockedScale = false;

        [Header("Smoothing")]
        public float posSmoothTime = 0.06f;
        public float scaleLerpSpeed = 10f;

        [Header("Yaw (PantsPivot) - Camera Relative Yaw Only")]
        public bool drivePantsPivotYaw = true;

        [Header("Yaw Smooth/Stabilize")]
        [Range(0f, 1f)] public float yawFollow = 0.20f;
        public float maxYawSpeedDegPerSec = 160f;
        public bool useYawContinuity = true;

        [Header("Freeze yaw update when shoulder looks too narrow")]
        public bool freezeYawWhenNarrow = true;
        public bool useYawHysteresis = true;
        public float shoulderFreezeIn01 = 0.06f;
        public float shoulderFreezeOut01 = 0.10f;
        public float shoulderNarrowThreshold01 = 0.06f;
        private bool _yawFrozen = false;
        private bool _yawFrozenPrev = false;

        [Header("Use MP Z (approx)")]
        public bool useLandmarkZ = true;
        public float zToMetersMultiplier = 0.35f;
        public bool invertZ = true;

        [Header("Pants Yaw Offset")]
        public float yawOffsetDeg = 0f;

        // -------------------------
        // Soft leg feel (no bones)
        // -------------------------
        [Header("Soft Leg Feel (no bones)")]
        public bool driveSoftLegFeel = true;

        [Tooltip("좌우 기울기 최대 각도(작게)")]
        public float hipRollMaxDeg = 10f;

        [Range(0f, 1f)]
        [Tooltip("기울기 따라오는 속도")]
        public float hipRollFollow = 0.15f;

        [Tooltip("roll 방향이 반대로 느껴지면 체크")]
        public bool invertRoll = false;

        private Vector3 _posVel;

        private Quaternion _pantsPivotBaseLocalRot;
        private bool _pantsPivotBaseSaved = false;

        private Quaternion _pantsVisualBaseLocalRot;
        private bool _pantsVisualBaseSaved = false;

        private float _yawDegSmoothed = 0f;
        private bool _yawInitialized = false;
        private Vector3 _prevForwardFlat = Vector3.zero;

        private float _rollDegSmoothed = 0f;

        void Reset()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        void Start()
        {
            if (pantsPivot != null && !_pantsPivotBaseSaved)
            {
                _pantsPivotBaseLocalRot = pantsPivot.localRotation;
                _pantsPivotBaseSaved = true;
            }

            if (pantsVisual != null && !_pantsVisualBaseSaved)
            {
                _pantsVisualBaseLocalRot = pantsVisual.localRotation;
                _pantsVisualBaseSaved = true;
            }
        }

        void LateUpdate()
        {
            if (runner == null || targetCamera == null) return;

            if (!runner.TryGetHips01(out var lH2, out var rH2)) return;

            float hipWidth01 = Vector2.Distance(lH2, rH2);
            if (hipWidth01 < 1e-5f) return;

            float depth = baseDepthMeters;
            if (autoDepth)
            {
                depth = baseDepthMeters * (refHipWidth01 / hipWidth01);
                depth = Mathf.Clamp(depth, depthMin, depthMax);
            }

            Vector3 lHw = targetCamera.ViewportToWorldPoint(new Vector3(lH2.x, 1f - lH2.y, depth));
            Vector3 rHw = targetCamera.ViewportToWorldPoint(new Vector3(rH2.x, 1f - rH2.y, depth));

            Vector3 hipMid = (lHw + rHw) * 0.5f;
            float hipWidthWorld = (rHw - lHw).magnitude;

            if (drivePantsPivotYaw)
            {
                DrivePantsPivotYaw_CameraRelative(depth);
            }

            if (driveRoot)
            {
                Vector3 targetPos = hipMid + rootPosOffset;
                transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _posVel, posSmoothTime);
            }

            if (autoScale)
            {
                if (allowAutoScaleUpdate)
                {
                    float s = hipWidthWorld * scaleMultiplier;

                    if (multiplyAutoScaleByBodyScale)
                    {
                        float raw = ReadBodyScaleFactorSafe();
                        float applied = Mathf.Lerp(1f, raw, Mathf.Clamp01(bodyScaleInfluence));
                        s *= applied;
                    }

                    Vector3 targetScale = new Vector3(s, s, s);
                    float t = 1f - Mathf.Exp(-scaleLerpSpeed * Time.deltaTime);
                    transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);

                    _lockedScale = transform.localScale;
                    _hasLockedScale = true;
                }
                else
                {
                    if (_hasLockedScale) transform.localScale = _lockedScale;
                }
            }

            if (driveSoftLegFeel)
            {
                DriveSoftLegFeel(depth);
            }
        }

        public void LockScaleNow()
        {
            _lockedScale = transform.localScale;
            _hasLockedScale = true;
            allowAutoScaleUpdate = false;
        }

        public void UnlockScale()
        {
            allowAutoScaleUpdate = true;
        }

        private float ReadBodyScaleFactorSafe()
        {
            if (bodyScaleProvider == null) return Mathf.Max(0.0001f, bodyScaleFallback);

            try
            {
                var t = bodyScaleProvider.GetType();

                // Property first
                var p = t.GetProperty(bodyScaleMemberName, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(float))
                {
                    float v = (float)p.GetValue(bodyScaleProvider);
                    return Mathf.Max(0.0001f, v);
                }

                // Field
                var f = t.GetField(bodyScaleMemberName, BindingFlags.Public | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(float))
                {
                    float v = (float)f.GetValue(bodyScaleProvider);
                    return Mathf.Max(0.0001f, v);
                }

                // Common fallback names
                string[] fallbacks = { "BodyScaleFactor", "bodyScale", "scaleFactor", "factor" };
                foreach (var name in fallbacks)
                {
                    p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (p != null && p.PropertyType == typeof(float))
                    {
                        float v = (float)p.GetValue(bodyScaleProvider);
                        return Mathf.Max(0.0001f, v);
                    }
                    f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    if (f != null && f.FieldType == typeof(float))
                    {
                        float v = (float)f.GetValue(bodyScaleProvider);
                        return Mathf.Max(0.0001f, v);
                    }
                }
            }
            catch { }

            return Mathf.Max(0.0001f, bodyScaleFallback);
        }

        private void DrivePantsPivotYaw_CameraRelative(float depth)
        {
            if (pantsPivot == null || runner == null || targetCamera == null) return;

            if (!_pantsPivotBaseSaved)
            {
                _pantsPivotBaseLocalRot = pantsPivot.localRotation;
                _pantsPivotBaseSaved = true;
            }

            if (!runner.TryGetShoulders01Z(out var lS, out var rS)) return;
            if (!runner.TryGetHips01Z(out var lH, out var rH)) return;

            float shoulderWidth01 = Vector2.Distance(new Vector2(lS.x, lS.y), new Vector2(rS.x, rS.y));
            if (shoulderWidth01 < 1e-4f) return;

            // 측면/후면에서 어깨폭이 좁아질 때 yaw 업데이트를 멈춘다
            if (freezeYawWhenNarrow)
            {
                bool frozenNow = false;

                if (useYawHysteresis)
                {
                    if (!_yawFrozen && shoulderWidth01 < shoulderFreezeIn01) _yawFrozen = true;
                    else if (_yawFrozen && shoulderWidth01 > shoulderFreezeOut01) _yawFrozen = false;
                    frozenNow = _yawFrozen;
                }
                else
                {
                    frozenNow = (shoulderWidth01 < shoulderNarrowThreshold01);
                }

                if (frozenNow && !_yawFrozenPrev)
                {
                    _prevForwardFlat = Vector3.zero;
                }
                else if (!frozenNow && _yawFrozenPrev)
                {
                    _prevForwardFlat = Vector3.zero;
                    _yawInitialized = false;
                }

                _yawFrozenPrev = frozenNow;

                if (frozenNow) return;
            }

            Vector3 camF = targetCamera.transform.forward;

            Vector3 lSw0 = targetCamera.ViewportToWorldPoint(new Vector3(lS.x, 1f - lS.y, depth));
            Vector3 rSw0 = targetCamera.ViewportToWorldPoint(new Vector3(rS.x, 1f - rS.y, depth));
            Vector3 lHw0 = targetCamera.ViewportToWorldPoint(new Vector3(lH.x, 1f - lH.y, depth));
            Vector3 rHw0 = targetCamera.ViewportToWorldPoint(new Vector3(rH.x, 1f - rH.y, depth));

            float shoulderWidthWorld0 = (rSw0 - lSw0).magnitude;
            float metersPer01 = shoulderWidthWorld0 / shoulderWidth01;

            Vector3 lSw = lSw0, rSw = rSw0, lHw = lHw0, rHw = rHw0;

            if (useLandmarkZ)
            {
                float lzS = lS.z * metersPer01 * zToMetersMultiplier;
                float rzS = rS.z * metersPer01 * zToMetersMultiplier;
                float lzH = lH.z * metersPer01 * zToMetersMultiplier;
                float rzH = rH.z * metersPer01 * zToMetersMultiplier;

                if (invertZ)
                {
                    lzS = -lzS; rzS = -rzS;
                    lzH = -lzH; rzH = -rzH;
                }

                lSw = lSw0 + camF * lzS;
                rSw = rSw0 + camF * rzS;
                lHw = lHw0 + camF * lzH;
                rHw = rHw0 + camF * rzH;
            }

            Vector3 shoulderCenter = (lSw + rSw) * 0.5f;
            Vector3 hipCenter = (lHw + rHw) * 0.5f;

            Vector3 right = (rSw - lSw);
            Vector3 up = (shoulderCenter - hipCenter);

            if (right.sqrMagnitude < 1e-6f || up.sqrMagnitude < 1e-6f) return;
            right.Normalize();
            up.Normalize();

            Vector3 forward = Vector3.Cross(right, up);
            if (forward.sqrMagnitude < 1e-6f) return;
            forward.Normalize();

            Vector3 fW = forward;
            fW.y = 0f;
            if (fW.sqrMagnitude < 1e-6f) return;
            fW.Normalize();

            if (useYawContinuity && _prevForwardFlat.sqrMagnitude > 1e-6f)
            {
                if (Vector3.Dot(fW, _prevForwardFlat) < 0f) fW = -fW;
            }
            _prevForwardFlat = fW;

            Vector3 camForwardFlat = targetCamera.transform.forward;
            Vector3 camRightFlat = targetCamera.transform.right;
            camForwardFlat.y = 0f;
            camRightFlat.y = 0f;
            if (camForwardFlat.sqrMagnitude < 1e-6f || camRightFlat.sqrMagnitude < 1e-6f) return;
            camForwardFlat.Normalize();
            camRightFlat.Normalize();

            float x = Vector3.Dot(fW, camRightFlat);
            float z = Vector3.Dot(fW, camForwardFlat);

            float yawDeg = -Mathf.Atan2(x, z) * Mathf.Rad2Deg;

            if (!_yawInitialized)
            {
                _yawDegSmoothed = yawDeg;
                _yawInitialized = true;
            }

            float blended = Mathf.LerpAngle(_yawDegSmoothed, yawDeg, Mathf.Clamp01(yawFollow));
            float maxStep = Mathf.Max(1f, maxYawSpeedDegPerSec) * Time.deltaTime;
            float delta = Mathf.DeltaAngle(_yawDegSmoothed, blended);
            delta = Mathf.Clamp(delta, -maxStep, maxStep);
            _yawDegSmoothed = _yawDegSmoothed + delta;

            Quaternion yawLocal = Quaternion.Euler(0f, _yawDegSmoothed + yawOffsetDeg, 0f);
            Quaternion targetLocal = _pantsPivotBaseLocalRot * yawLocal;

            float t = 1f - Mathf.Exp(-10f * Time.deltaTime);
            pantsPivot.localRotation = Quaternion.Slerp(pantsPivot.localRotation, targetLocal, t);
        }

        // 바지의 "다리 따라오는 느낌"만 주기 위한 소프트 roll
        private void DriveSoftLegFeel(float depth)
        {
            Transform t = (pantsVisual != null) ? pantsVisual : null;
            if (t == null) return;

            if (!_pantsVisualBaseSaved)
            {
                _pantsVisualBaseLocalRot = t.localRotation;
                _pantsVisualBaseSaved = true;
            }

            if (!runner.TryGetHips01(out var lH, out var rH)) return;

            Vector3 lHw = targetCamera.ViewportToWorldPoint(new Vector3(lH.x, 1f - lH.y, depth));
            Vector3 rHw = targetCamera.ViewportToWorldPoint(new Vector3(rH.x, 1f - rH.y, depth));

            Vector3 dir = (rHw - lHw);
            if (dir.sqrMagnitude < 1e-6f) return;

            Vector3 camUp = targetCamera.transform.up;

            float upAmount = Vector3.Dot(dir.normalized, camUp);
            float rollDeg = -upAmount * hipRollMaxDeg;
            if (invertRoll) rollDeg = -rollDeg;

            _rollDegSmoothed = Mathf.Lerp(_rollDegSmoothed, rollDeg, Mathf.Clamp01(hipRollFollow));

            Quaternion rollLocal = Quaternion.Euler(0f, 0f, _rollDegSmoothed);
            t.localRotation = _pantsVisualBaseLocalRot * rollLocal;
        }
    }
}
