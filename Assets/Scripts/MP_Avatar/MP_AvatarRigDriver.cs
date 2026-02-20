using UnityEngine;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection
{
    public class MP_AvatarRigDriver : MonoBehaviour
    {
        [Header("Required")]
        public PoseLandmarkerRunner_hm_spin runner;
        public Camera targetCamera;

        [Header("Rig Root / Pivot")]
        public Transform rigRoot;
        public Transform yawPivot;

        [Header("Auto Depth (approx)")]
        public bool autoDepth = true;
        public float baseDepthMeters = 2.2f;
        public float refShoulderWidth01 = 0.18f;
        public float depthMin = 1.2f;
        public float depthMax = 4.0f;

        [Header("Anchor")]
        [Range(0f, 1f)] public float anchorHipToShoulder = 0.30f;
        public float cameraUpOffset = -0.30f;
        public float cameraForwardOffset = 0.00f;

        [Header("Smoothing")]
        public float posSmoothTime = 0.06f;

        [Header("Yaw (YawPivot)")]
        public bool driveYaw = true;
        [Range(0f, 1f)] public float yawFollow = 0.20f;
        public float maxYawSpeedDegPerSec = 160f;
        public bool useYawContinuity = true;
        public bool flipFacing180 = false;

        [Header("Freeze yaw when narrow")]
        public bool freezeYawWhenNarrow = true;
        public bool useYawHysteresis = true;
        public float shoulderFreezeIn01 = 0.06f;
        public float shoulderFreezeOut01 = 0.10f;
        public float shoulderNarrowThreshold01 = 0.06f;

        [Header("Rig Bones (Assign Manually)")]
        public Transform leftArm;
        public Transform leftForeArm;
        public Transform rightArm;
        public Transform rightForeArm;

        [Header("Bone Rotation Offsets (per bone)")]
        public Vector3 leftArmRotOffsetEuler = Vector3.zero;
        public Vector3 leftForeArmRotOffsetEuler = Vector3.zero;
        public Vector3 rightArmRotOffsetEuler = Vector3.zero;
        public Vector3 rightForeArmRotOffsetEuler = Vector3.zero;

        [Header("Per-side Fix")]
        public bool flipLeftBindForward = false;
        public bool flipLeftForeBindForward = false;
        public bool flipRightBindForward = false;
        public bool flipRightForeBindForward = false;

        [Header("Drive Strength")]
        [Range(0.0f, 1.0f)] public float armSlerp = 0.35f;

        [Header("Stabilize")]
        public bool projectToCameraPlane = true;
        public bool useCameraForwardAsRollRef = true;
        public bool freezeRollToBindPose = true;

        private Vector3 _posVel;

        private bool _bindSaved = false;

        private Quaternion _lArmBindLocal, _lForeBindLocal, _rArmBindLocal, _rForeBindLocal;

        private Vector3 _lArmAxisP, _lArmRefForwardP;
        private Vector3 _lForeAxisP, _lForeRefForwardP;
        private Vector3 _rArmAxisP, _rArmRefForwardP;
        private Vector3 _rForeAxisP, _rForeRefForwardP;

        private float _yawDegSmoothed = 0f;
        private bool _yawInitialized = false;
        private Vector3 _prevForwardFlat = Vector3.zero;

        private bool _yawFrozen = false;
        private bool _yawFrozenPrev = false;

        void Reset()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (rigRoot == null) rigRoot = transform;
        }

        void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (rigRoot == null) rigRoot = transform;
        }

        [ContextMenu("Rebind Now (Play mode, arms relaxed)")]
        public void RebindNow()
        {
            _bindSaved = false;
            _prevForwardFlat = Vector3.zero;
            _yawInitialized = false;
        }

        void LateUpdate()
        {
            if (runner == null || targetCamera == null) return;

            if (!_bindSaved) SaveBindPose();

            if (!runner.TryGetShoulders01(out var lS2, out var rS2)) return;
            if (!runner.TryGetHips01(out var lH2, out var rH2)) return;

            float shoulderWidth01 = Vector2.Distance(lS2, rS2);
            if (shoulderWidth01 < 1e-4f) return;

            float depth = baseDepthMeters;
            if (autoDepth)
            {
                depth = baseDepthMeters * (refShoulderWidth01 / shoulderWidth01);
                depth = Mathf.Clamp(depth, depthMin, depthMax);
            }

            Vector3 lwS = targetCamera.ViewportToWorldPoint(new Vector3(lS2.x, 1f - lS2.y, depth));
            Vector3 rwS = targetCamera.ViewportToWorldPoint(new Vector3(rS2.x, 1f - rS2.y, depth));
            Vector3 shoulderMid = (lwS + rwS) * 0.5f;

            Vector3 lwH = targetCamera.ViewportToWorldPoint(new Vector3(lH2.x, 1f - lH2.y, depth));
            Vector3 rwH = targetCamera.ViewportToWorldPoint(new Vector3(rH2.x, 1f - rH2.y, depth));
            Vector3 hipMid = (lwH + rwH) * 0.5f;

            Vector3 anchor = Vector3.Lerp(hipMid, shoulderMid, anchorHipToShoulder);
            anchor += targetCamera.transform.up * cameraUpOffset;
            anchor += targetCamera.transform.forward * cameraForwardOffset;

            rigRoot.position = Vector3.SmoothDamp(rigRoot.position, anchor, ref _posVel, posSmoothTime);

            if (driveYaw) DriveYaw(depth);

            DriveArmBones(depth);
        }

        private void DriveYaw(float depth)
        {
            if (yawPivot == null) return;
            if (!runner.TryGetShoulders01Z(out var lS, out var rS)) return;
            if (!runner.TryGetHips01Z(out var lH, out var rH)) return;

            float shoulderWidth01 = Vector2.Distance(new Vector2(lS.x, lS.y), new Vector2(rS.x, rS.y));
            if (shoulderWidth01 < 1e-4f) return;

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

            Vector3 lw = targetCamera.ViewportToWorldPoint(new Vector3(lS.x, 1f - lS.y, depth));
            Vector3 rw = targetCamera.ViewportToWorldPoint(new Vector3(rS.x, 1f - rS.y, depth));
            Vector3 lh = targetCamera.ViewportToWorldPoint(new Vector3(lH.x, 1f - lH.y, depth));
            Vector3 rh = targetCamera.ViewportToWorldPoint(new Vector3(rH.x, 1f - rH.y, depth));

            Vector3 shoulderCenter = (lw + rw) * 0.5f;
            Vector3 hipCenter = (lh + rh) * 0.5f;

            Vector3 right = (rw - lw);
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

            float finalYaw = _yawDegSmoothed + (flipFacing180 ? 180f : 0f);
            yawPivot.localRotation = Quaternion.Euler(0f, finalYaw, 0f);
        }

        private void DriveArmBones(float depth)
        {
            Vector3 camForward = targetCamera.transform.forward;

            if (leftArm != null && leftForeArm != null &&
                runner.TryGetLeftArm3Points01(out var ls, out var le, out var lw))
            {
                Vector3 S = targetCamera.ViewportToWorldPoint(new Vector3(ls.x, 1f - ls.y, depth));
                Vector3 E = targetCamera.ViewportToWorldPoint(new Vector3(le.x, 1f - le.y, depth));
                Vector3 W = targetCamera.ViewportToWorldPoint(new Vector3(lw.x, 1f - lw.y, depth));

                Vector3 upperDir = (E - S);
                Vector3 lowerDir = (W - E);

                if (projectToCameraPlane)
                {
                    upperDir = Vector3.ProjectOnPlane(upperDir, camForward);
                    lowerDir = Vector3.ProjectOnPlane(lowerDir, camForward);
                }

                Vector3 lArmF = flipLeftBindForward ? -_lArmRefForwardP : _lArmRefForwardP;
                Vector3 lForeF = flipLeftForeBindForward ? -_lForeRefForwardP : _lForeRefForwardP;

                ApplyBoneTarget(leftArm, upperDir, _lArmBindLocal, _lArmAxisP, lArmF, leftArmRotOffsetEuler);
                ApplyBoneTarget(leftForeArm, lowerDir, _lForeBindLocal, _lForeAxisP, lForeF, leftForeArmRotOffsetEuler);
            }

            if (rightArm != null && rightForeArm != null &&
                runner.TryGetRightArm3Points01(out var rs, out var re, out var rw))
            {
                Vector3 S = targetCamera.ViewportToWorldPoint(new Vector3(rs.x, 1f - rs.y, depth));
                Vector3 E = targetCamera.ViewportToWorldPoint(new Vector3(re.x, 1f - re.y, depth));
                Vector3 W = targetCamera.ViewportToWorldPoint(new Vector3(rw.x, 1f - rw.y, depth));

                Vector3 upperDir = (E - S);
                Vector3 lowerDir = (W - E);

                if (projectToCameraPlane)
                {
                    upperDir = Vector3.ProjectOnPlane(upperDir, camForward);
                    lowerDir = Vector3.ProjectOnPlane(lowerDir, camForward);
                }

                Vector3 rArmF = flipRightBindForward ? -_rArmRefForwardP : _rArmRefForwardP;
                Vector3 rForeF = flipRightForeBindForward ? -_rForeRefForwardP : _rForeRefForwardP;

                ApplyBoneTarget(rightArm, upperDir, _rArmBindLocal, _rArmAxisP, rArmF, rightArmRotOffsetEuler);
                ApplyBoneTarget(rightForeArm, lowerDir, _rForeBindLocal, _rForeAxisP, rForeF, rightForeArmRotOffsetEuler);
            }
        }

        private void ApplyBoneTarget(
            Transform bone,
            Vector3 worldDir,
            Quaternion bindLocal,
            Vector3 axisInParentBind,
            Vector3 refForwardInParentBind,
            Vector3 eulerOffset)
        {
            if (bone == null || bone.parent == null) return;
            if (worldDir.sqrMagnitude < 1e-6f) return;

            Transform parent = bone.parent;

            Vector3 targetAxisP = parent.InverseTransformDirection(worldDir.normalized);
            if (targetAxisP.sqrMagnitude < 1e-6f) return;
            targetAxisP.Normalize();

            Vector3 rollRefWorld = useCameraForwardAsRollRef ? targetCamera.transform.forward : rigRoot.forward;
            Vector3 rollRefP = parent.InverseTransformDirection(rollRefWorld);

            Vector3 targetForwardP = Vector3.ProjectOnPlane(rollRefP, targetAxisP);
            if (targetForwardP.sqrMagnitude < 1e-6f)
            {
                targetForwardP = refForwardInParentBind;
            }
            targetForwardP.Normalize();

            if (freezeRollToBindPose)
            {
                Vector3 bf = refForwardInParentBind;
                if (bf.sqrMagnitude < 1e-6f) bf = Vector3.forward;
                bf.Normalize();
                targetForwardP = Vector3.Slerp(targetForwardP, bf, 0.65f).normalized;
            }

            Quaternion targetBasisP = Quaternion.LookRotation(targetForwardP, targetAxisP);

            Vector3 bindForward = refForwardInParentBind;
            Vector3 bindAxis = axisInParentBind;
            if (bindForward.sqrMagnitude < 1e-6f) bindForward = Vector3.forward;
            if (bindAxis.sqrMagnitude < 1e-6f) bindAxis = Vector3.up;

            Quaternion bindBasisP = Quaternion.LookRotation(bindForward.normalized, bindAxis.normalized);
            Quaternion deltaP = targetBasisP * Quaternion.Inverse(bindBasisP);

            Quaternion targetLocal = bindLocal * deltaP * Quaternion.Euler(eulerOffset);

            bone.localRotation = Quaternion.Slerp(bone.localRotation, targetLocal, armSlerp);
        }

        private void SaveBindPose()
        {
            if (_bindSaved) return;

            if (leftArm != null && leftArm.parent != null)
            {
                _lArmBindLocal = leftArm.localRotation;
                CaptureAxisAndForwardInParent(leftArm, out _lArmAxisP, out _lArmRefForwardP);
            }
            if (leftForeArm != null && leftForeArm.parent != null)
            {
                _lForeBindLocal = leftForeArm.localRotation;
                CaptureAxisAndForwardInParent(leftForeArm, out _lForeAxisP, out _lForeRefForwardP);
            }

            if (rightArm != null && rightArm.parent != null)
            {
                _rArmBindLocal = rightArm.localRotation;
                CaptureAxisAndForwardInParent(rightArm, out _rArmAxisP, out _rArmRefForwardP);
            }
            if (rightForeArm != null && rightForeArm.parent != null)
            {
                _rForeBindLocal = rightForeArm.localRotation;
                CaptureAxisAndForwardInParent(rightForeArm, out _rForeAxisP, out _rForeRefForwardP);
            }

            _bindSaved = true;
        }

        private void CaptureAxisAndForwardInParent(Transform bone, out Vector3 axisP, out Vector3 forwardP)
        {
            axisP = Vector3.up;

            if (bone.childCount > 0)
            {
                Vector3 childWorld = bone.GetChild(0).position;
                Vector3 boneWorld = bone.position;
                Vector3 dirWorld = (childWorld - boneWorld);
                if (dirWorld.sqrMagnitude > 1e-6f)
                {
                    axisP = bone.parent.InverseTransformDirection(dirWorld.normalized);
                }
            }

            Vector3 fWorld = bone.forward;
            Vector3 fP = bone.parent.InverseTransformDirection(fWorld);
            forwardP = Vector3.ProjectOnPlane(fP, axisP);
            if (forwardP.sqrMagnitude < 1e-6f)
            {
                Vector3 rP = bone.parent.InverseTransformDirection(bone.right);
                forwardP = Vector3.ProjectOnPlane(rP, axisP);
                if (forwardP.sqrMagnitude < 1e-6f) forwardP = Vector3.forward;
            }
            forwardP.Normalize();

            if (axisP.sqrMagnitude < 1e-6f) axisP = Vector3.up;
            axisP.Normalize();
        }
    }
}
