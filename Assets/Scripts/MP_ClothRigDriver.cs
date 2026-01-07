using UnityEngine;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection
{
    public class MP_ClothRigDriver : MonoBehaviour
    {
        [Header("Required")]
        public PoseLandmarkerRunner runner;
        public Camera targetCamera;

        [Header("Scene Anchors")]
        public Transform clothPivot;

        [Header("Root Follow")]
        public bool driveRoot = true;
        public Vector3 rootPosOffset = Vector3.zero;
        public Vector3 rootRotOffsetEuler = new Vector3(0f, 180f, 180f);

        [Header("Auto Depth (approx)")]
        public bool autoDepth = true;
        public float baseDepthMeters = 2.2f;
        public float refShoulderWidth01 = 0.25f;
        public float depthMin = 1.2f;
        public float depthMax = 4.0f;

        [Header("Auto Scale (shoulder width based)")]
        public bool autoScale = true;
        public float scaleMultiplier = 1.0f;

        [Header("Smoothing")]
        public float posSmoothTime = 0.06f;
        public float rotLerpSpeed = 14f;
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

        private Vector3 _posVel;

        private bool _bindSaved = false;

        private Quaternion _lArmBindLocal, _lForeBindLocal, _rArmBindLocal, _rForeBindLocal;

        // In parent space (bind)
        private Vector3 _lArmAxisP, _lArmRefForwardP;
        private Vector3 _lForeAxisP, _lForeRefForwardP;
        private Vector3 _rArmAxisP, _rArmRefForwardP;
        private Vector3 _rForeAxisP, _rForeRefForwardP;

        // Bind local positions for length fit
        private Vector3 _lForeBindLocalPos, _lHandBindLocalPos;
        private Vector3 _rForeBindLocalPos, _rHandBindLocalPos;

        void Reset()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        void Start()
        {
            SaveBindPose();
        }

        void LateUpdate()
        {
            if (runner == null || targetCamera == null) return;
            if (!_bindSaved) SaveBindPose();

            if (!runner.TryGetShoulders01(out var lS, out var rS)) return;

            float shoulderWidth01 = Vector2.Distance(lS, rS);
            if (shoulderWidth01 < 1e-4f) return;

            float depth = baseDepthMeters;
            if (autoDepth)
            {
                depth = baseDepthMeters * (refShoulderWidth01 / shoulderWidth01);
                depth = Mathf.Clamp(depth, depthMin, depthMax);
            }

            Vector3 lw = targetCamera.ViewportToWorldPoint(new Vector3(lS.x, 1f - lS.y, depth));
            Vector3 rw = targetCamera.ViewportToWorldPoint(new Vector3(rS.x, 1f - rS.y, depth));

            Vector3 mid = (lw + rw) * 0.5f;
            Vector3 dir = (rw - lw);
            float shoulderWidthWorld = dir.magnitude;

            if (driveRoot)
            {
                Vector3 targetPos = mid + rootPosOffset;
                transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _posVel, posSmoothTime);

                if (dir.sqrMagnitude > 1e-6f)
                {
                    Vector3 forward = targetCamera.transform.forward;
                    Vector3 right = dir.normalized;
                    Vector3 up = Vector3.Cross(forward, right).normalized;

                    Quaternion baseRot = Quaternion.LookRotation(forward, up);
                    Quaternion targetRot = baseRot * Quaternion.Euler(rootRotOffsetEuler);

                    float t = 1f - Mathf.Exp(-rotLerpSpeed * Time.deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
                }
            }

            if (autoScale)
            {
                float s = shoulderWidthWorld * scaleMultiplier;
                Vector3 targetScale = new Vector3(s, s, s);
                float t = 1f - Mathf.Exp(-scaleLerpSpeed * Time.deltaTime);
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);
            }

            // 1) Drive rotations
            DriveArmBones(depth);

            // 2) Fit bone lengths (positions)
            if (fitArmLengths)
            {
                FitArmBoneLengths(depth);
            }
        }

        private void DriveArmBones(float depth)
        {
            Vector3 camForward = targetCamera.transform.forward;

            // Left
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

            // Right
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
            // Need hand bones for best result
            if (leftArm == null || leftForeArm == null || leftHand == null) return;
            if (rightArm == null || rightForeArm == null || rightHand == null) return;

            // Left desired
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

                    // Apply weights (0..1)
                    su = Mathf.Lerp(1f, su, Mathf.Clamp01(upperLenWeight));
                    sl = Mathf.Lerp(1f, sl, Mathf.Clamp01(lowerLenWeight));

                    Vector3 targetForePos = _lForeBindLocalPos * su;
                    Vector3 targetHandPos = _lHandBindLocalPos * sl;

                    float handLerp = Mathf.Clamp01(lengthLerp * 2.2f);

                    leftForeArm.localPosition = Vector3.Lerp(leftForeArm.localPosition, targetForePos, lengthLerp);
                    leftHand.localPosition = Vector3.Lerp(leftHand.localPosition, targetHandPos, handLerp);

                }
            }

            // Right desired
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
//¾Æ