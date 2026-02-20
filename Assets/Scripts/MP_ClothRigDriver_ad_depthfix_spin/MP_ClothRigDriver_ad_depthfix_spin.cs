using UnityEngine;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection
{
    public class MP_ClothRigDriver_ad_depthfix_spin : MonoBehaviour
    {
        [Header("Required")]
        public PoseLandmarkerRunner_ad_spin runner;
        public Camera targetCamera;

        [Header("Scene Anchors")]
        public Transform clothPivot;

        [Header("Root Follow (position only)")]
        public bool driveRoot = true;
        public Vector3 rootPosOffset = Vector3.zero;

        [Header("Auto Depth (approx)")]
        public bool autoDepth = true;
        public float baseDepthMeters = 2.2f;
        public float refShoulderWidth01 = 0.25f;
        public float depthMin = 1.2f;
        public float depthMax = 4.0f;

        [Header("Auto Scale (shoulder width based)")]
        public bool autoScale = true;
        public float scaleMultiplier = 1.0f;

        [Header("Scale Lock")]
        public bool allowAutoScaleUpdate = true;
        private Vector3 _lockedScale = Vector3.one;
        private bool _hasLockedScale = false;

        [Header("Smoothing")]
        public float posSmoothTime = 0.06f;
        public float scaleLerpSpeed = 10f;

        [Header("Rig Bones (drag from cloth prefab)")]
        public Transform leftArm;
        public Transform leftForeArm;
        public Transform leftHand;
        public Transform rightArm;
        public Transform rightForeArm;
        public Transform rightHand;

        [Header("Bone Rotation Offsets (per bone)")]
        public Vector3 leftArmRotOffsetEuler = Vector3.zero;
        public Vector3 leftForeArmRotOffsetEuler = Vector3.zero;
        public Vector3 rightArmRotOffsetEuler = Vector3.zero;
        public Vector3 rightForeArmRotOffsetEuler = Vector3.zero;

        [Header("Bone Drive Strength")]
        [Range(0.0f, 1.0f)] public float armSlerp = 0.35f;

        [Header("Stabilize")]
        public bool projectToCameraPlane = true;

        [Header("Roll Reference")]
        public bool useCameraForwardAsRollRef = true;
        public bool freezeRollToBindPose = true;

        [Header("Per-side Fix")]
        public bool flipLeftBindForward = true;
        public bool flipLeftForeBindForward = true;

        [Header("Arm Length Fit")]
        public bool fitArmLengths = true;
        [Range(0f, 1f)] public float lengthLerp = 0.25f;
        public float minLenScale = 0.7f;
        public float maxLenScale = 1.35f;
        public float upperLenWeight = 1.0f;
        public float lowerLenWeight = 1.0f;

        [Header("Torso Yaw (ClothPivot) - Camera Relative Yaw Only")]
        public bool driveClothPivotYaw = true;

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

        [Header("Use MP Z (approx)")]
        public bool useLandmarkZ = true;
        public float zToMetersMultiplier = 0.35f;
        public bool invertZ = true;

        [Header("Hand Follow Forearm")]
        public bool stabilizeHands = true;
        [Range(0f, 1f)] public float handFollowSlerp = 0.25f;

        [Header("Root Anchor")]
        public bool anchorRootToHips = true;
        public float hipsYOffset = 0.0f;

        [Header("Avatar Shoulder Align")]
        public bool alignAvatarShoulders = true;
        [Range(0.0f, 1.0f)] public float shoulderAlignLerp = 0.25f;
        public Animator avatarAnimator;

        private Vector3 _posVel;

        private bool _yawFrozenPrev = false;
        private bool _bindSaved = false;

        private Quaternion _lArmBindLocal, _lForeBindLocal, _rArmBindLocal, _rForeBindLocal;
        private Quaternion _lHandBindLocal, _rHandBindLocal;

        private Vector3 _lArmAxisP, _lArmRefForwardP;
        private Vector3 _lForeAxisP, _lForeRefForwardP;
        private Vector3 _rArmAxisP, _rArmRefForwardP;
        private Vector3 _rForeAxisP, _rForeRefForwardP;

        private Vector3 _lForeBindLocalPos, _lHandBindLocalPos;
        private Vector3 _rForeBindLocalPos, _rHandBindLocalPos;

        private Quaternion _clothPivotBaseLocalRot;
        private bool _clothPivotBaseSaved = false;

        private float _yawDegSmoothed = 0f;
        private bool _yawInitialized = false;
        private Vector3 _prevForwardFlat = Vector3.zero;

        void Reset()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        void Start()
        {
            SaveBindPose();

            if (clothPivot != null && !_clothPivotBaseSaved)
            {
                _clothPivotBaseLocalRot = clothPivot.localRotation;
                _clothPivotBaseSaved = true;
            }
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

            Vector3 lSw = targetCamera.ViewportToWorldPoint(new Vector3(lS2.x, 1f - lS2.y, depth));
            Vector3 rSw = targetCamera.ViewportToWorldPoint(new Vector3(rS2.x, 1f - rS2.y, depth));
            Vector3 lHw = targetCamera.ViewportToWorldPoint(new Vector3(lH2.x, 1f - lH2.y, depth));
            Vector3 rHw = targetCamera.ViewportToWorldPoint(new Vector3(rH2.x, 1f - rH2.y, depth));

            Vector3 midShoulder = (lSw + rSw) * 0.5f;
            Vector3 midHip = (lHw + rHw) * 0.5f;
            midHip.y += hipsYOffset;

            Vector3 dirShoulder = (rSw - lSw);
            float shoulderWidthWorld = dirShoulder.magnitude;

            if (driveClothPivotYaw)
            {
                DriveClothPivotYaw_CameraRelative(depth);
            }

            if (driveRoot)
            {
                Vector3 anchor = anchorRootToHips ? midHip : midShoulder;
                Vector3 targetPos = anchor + rootPosOffset;

                if (alignAvatarShoulders && avatarAnimator != null)
                {
                    Vector3 delta = CalcShoulderAlignDeltaWorld(midShoulder);
                    targetPos += delta;
                }

                transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _posVel, posSmoothTime);
            }

            if (autoScale)
            {
                if (allowAutoScaleUpdate)
                {
                    float s = shoulderWidthWorld * scaleMultiplier;
                    Vector3 targetScale = new Vector3(s, s, s);
                    float t = 1f - Mathf.Exp(-scaleLerpSpeed * Time.deltaTime);
                    transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);

                    _lockedScale = transform.localScale;
                    _hasLockedScale = true;
                }
                else
                {
                    if (_hasLockedScale)
                    {
                        transform.localScale = _lockedScale;
                    }
                }
            }

            DriveArmBones(depth);

            if (fitArmLengths)
            {
                FitArmBoneLengths(depth);
            }

            if (stabilizeHands)
            {
                if (leftHand != null)
                    leftHand.localRotation = Quaternion.Slerp(leftHand.localRotation, _lHandBindLocal, handFollowSlerp);
                if (rightHand != null)
                    rightHand.localRotation = Quaternion.Slerp(rightHand.localRotation, _rHandBindLocal, handFollowSlerp);
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

        private Vector3 CalcShoulderAlignDeltaWorld(Vector3 trackedShoulderCenterWorld)
        {
            if (avatarAnimator == null) return Vector3.zero;

            Transform l = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform r = avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (l == null || r == null) return Vector3.zero;

            Vector3 avatarShoulderCenter = (l.position + r.position) * 0.5f;

            Vector3 rawDelta = trackedShoulderCenterWorld - avatarShoulderCenter;

            float t = 1f - Mathf.Pow(1f - shoulderAlignLerp, Time.deltaTime * 60f);
            return rawDelta * t;
        }

        private void DriveClothPivotYaw_CameraRelative(float depth)
        {
            if (clothPivot == null || runner == null || targetCamera == null) return;

            if (!_clothPivotBaseSaved)
            {
                _clothPivotBaseLocalRot = clothPivot.localRotation;
                _clothPivotBaseSaved = true;
            }

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

            Vector3 camF = targetCamera.transform.forward;

            Vector3 lw0 = targetCamera.ViewportToWorldPoint(new Vector3(lS.x, 1f - lS.y, depth));
            Vector3 rw0 = targetCamera.ViewportToWorldPoint(new Vector3(rS.x, 1f - rS.y, depth));
            Vector3 lh0 = targetCamera.ViewportToWorldPoint(new Vector3(lH.x, 1f - lH.y, depth));
            Vector3 rh0 = targetCamera.ViewportToWorldPoint(new Vector3(rH.x, 1f - rH.y, depth));

            float shoulderWidthWorld0 = (rw0 - lw0).magnitude;
            float metersPer01 = shoulderWidthWorld0 / shoulderWidth01;

            Vector3 lw = lw0, rw = rw0, lh = lh0, rh = rh0;
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

                lw = lw0 + camF * lzS;
                rw = rw0 + camF * rzS;
                lh = lh0 + camF * lzH;
                rh = rh0 + camF * rzH;
            }

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

            Quaternion yawLocal = Quaternion.Euler(0f, _yawDegSmoothed, 0f);
            Quaternion targetLocal = _clothPivotBaseLocalRot * yawLocal;

            float t = 1f - Mathf.Exp(-10f * Time.deltaTime);
            clothPivot.localRotation = Quaternion.Slerp(clothPivot.localRotation, targetLocal, t);
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

                ApplyBoneTarget(rightArm, upperDir, _rArmBindLocal, _rArmAxisP, _rArmRefForwardP, rightArmRotOffsetEuler);
                ApplyBoneTarget(rightForeArm, lowerDir, _rForeBindLocal, _rForeAxisP, _rForeRefForwardP, rightForeArmRotOffsetEuler);
            }
        }

        private void FitArmBoneLengths(float depth)
        {
            if (leftArm == null || leftForeArm == null || leftHand == null) return;
            if (rightArm == null || rightForeArm == null || rightHand == null) return;

            if (runner.TryGetLeftArm3Points01(out var ls, out var le, out var lw))
            {
                Vector3 S = targetCamera.ViewportToWorldPoint(new Vector3(ls.x, 1f - ls.y, depth));
                Vector3 E = targetCamera.ViewportToWorldPoint(new Vector3(le.x, 1f - le.y, depth));
                Vector3 W = targetCamera.ViewportToWorldPoint(new Vector3(lw.x, 1f - lw.y, depth));

                float desiredUpper = Vector3.Distance(S, E);
                float desiredLower = Vector3.Distance(E, W);

                float bindUpper = Vector3.Distance(leftArm.position, leftForeArm.position);
                float bindLower = Vector3.Distance(leftForeArm.position, leftHand.position);

                if (bindUpper > 1e-4f && bindLower > 1e-4f)
                {
                    float su = Mathf.Clamp(desiredUpper / bindUpper, minLenScale, maxLenScale);
                    float sl = Mathf.Clamp(desiredLower / bindLower, minLenScale, maxLenScale);

                    su = Mathf.Lerp(1f, su, Mathf.Clamp01(upperLenWeight));
                    sl = Mathf.Lerp(1f, sl, Mathf.Clamp01(lowerLenWeight));

                    Vector3 targetForePos = _lForeBindLocalPos * su;
                    Vector3 targetHandPos = _lHandBindLocalPos * sl;

                    float handLerp = Mathf.Clamp01(lengthLerp * 2.2f);

                    leftForeArm.localPosition = Vector3.Lerp(leftForeArm.localPosition, targetForePos, lengthLerp);
                    leftHand.localPosition = Vector3.Lerp(leftHand.localPosition, targetHandPos, handLerp);
                }
            }

            if (runner.TryGetRightArm3Points01(out var rs, out var re, out var rw))
            {
                Vector3 S = targetCamera.ViewportToWorldPoint(new Vector3(rs.x, 1f - rs.y, depth));
                Vector3 E = targetCamera.ViewportToWorldPoint(new Vector3(re.x, 1f - re.y, depth));
                Vector3 W = targetCamera.ViewportToWorldPoint(new Vector3(rw.x, 1f - rw.y, depth));

                float desiredUpper = Vector3.Distance(S, E);
                float desiredLower = Vector3.Distance(E, W);

                float bindUpper = Vector3.Distance(rightArm.position, rightForeArm.position);
                float bindLower = Vector3.Distance(rightForeArm.position, rightHand.position);

                if (bindUpper > 1e-4f && bindLower > 1e-4f)
                {
                    float su = Mathf.Clamp(desiredUpper / bindUpper, minLenScale, maxLenScale);
                    float sl = Mathf.Clamp(desiredLower / bindLower, minLenScale, maxLenScale);

                    su = Mathf.Lerp(1f, su, Mathf.Clamp01(upperLenWeight));
                    sl = Mathf.Lerp(1f, sl, Mathf.Clamp01(lowerLenWeight));

                    Vector3 targetForePos = _rForeBindLocalPos * su;
                    Vector3 targetHandPos = _rHandBindLocalPos * sl;

                    float handLerp = Mathf.Clamp01(lengthLerp * 2.2f);

                    rightForeArm.localPosition = Vector3.Lerp(rightForeArm.localPosition, targetForePos, lengthLerp);
                    rightHand.localPosition = Vector3.Lerp(rightHand.localPosition, targetHandPos, handLerp);
                }
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

            Vector3 rollRefWorld = useCameraForwardAsRollRef ? targetCamera.transform.forward : transform.forward;
            Vector3 rollRefP = parent.InverseTransformDirection(rollRefWorld);

            Vector3 targetForwardP = Vector3.ProjectOnPlane(rollRefP, targetAxisP);
            if (targetForwardP.sqrMagnitude < 1e-6f)
            {
                targetForwardP = refForwardInParentBind;
            }
            targetForwardP.Normalize();

            if (freezeRollToBindPose)
            {
                targetForwardP = Vector3.Slerp(targetForwardP, refForwardInParentBind.normalized, 0.65f).normalized;
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
                _lForeBindLocalPos = leftForeArm.localPosition;
            }
            if (leftHand != null)
            {
                _lHandBindLocalPos = leftHand.localPosition;
                _lHandBindLocal = leftHand.localRotation;
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
                _rForeBindLocalPos = rightForeArm.localPosition;
            }
            if (rightHand != null)
            {
                _rHandBindLocalPos = rightHand.localPosition;
                _rHandBindLocal = rightHand.localRotation;
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
