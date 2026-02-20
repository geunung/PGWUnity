using UnityEngine;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection
{
    public class MP_AvatarBoneDriver_ad_spin : MonoBehaviour
    {
        [Header("Required")]
        public PoseLandmarkerRunner_ad_spin runner;
        public Camera targetCamera;
        public Animator animator;

        [Header("Root")]
        public Transform avatarRoot;
        public bool driveRoot = true;
        public Vector3 rootPosOffset = Vector3.zero;
        public Vector3 rootRotOffsetEuler = new Vector3(0f, 180f, 180f);

        [Header("Depth")]
        public bool autoDepth = true;
        public float baseDepthMeters = 2.2f;
        public float refShoulderWidth01 = 0.25f;
        public float minDepthMeters = 1.2f;
        public float maxDepthMeters = 4.0f;

        [Header("Yaw Stabilize")]
        public bool driveYawOnlyOnRoot = true;
        [Range(0.0f, 1.0f)] public float yawLerp = 0.25f;

        [Header("Smoothing")]
        [Range(0.0f, 1.0f)] public float posLerp = 0.35f;
        [Range(0.0f, 1.0f)] public float rotLerp = 0.35f;

        [Header("Option")]
        public bool driveLegs = true;

        // BlazePose indices
        private const int L_HIP = 23;
        private const int R_HIP = 24;
        private const int L_KNEE = 25;
        private const int R_KNEE = 26;
        private const int L_ANKLE = 27;
        private const int R_ANKLE = 28;

        private Transform _hips;
        private Transform _spine;
        private Transform _chest;

        private Transform _lUpperArm;
        private Transform _lLowerArm;
        private Transform _rUpperArm;
        private Transform _rLowerArm;

        private Transform _lUpperLeg;
        private Transform _lLowerLeg;
        private Transform _rUpperLeg;
        private Transform _rLowerLeg;

        private float _yaw;
        private bool _yawInited;

        private void Reset()
        {
            targetCamera = Camera.main;
            animator = GetComponentInChildren<Animator>();
            avatarRoot = transform;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (avatarRoot == null) avatarRoot = transform;
            CacheBones();
        }

        private void CacheBones()
        {
            if (animator == null) return;

            _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = animator.GetBoneTransform(HumanBodyBones.Chest);

            _lUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _lLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _rUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

            _lUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _lLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _rUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _rLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        }

        private void LateUpdate()
        {
            if (runner == null || targetCamera == null || animator == null) return;

            if (!runner.TryGetShoulders01(out var ls, out var rs)) return;
            if (!runner.TryGetHips01(out var lh, out var rh)) return;

            float depth = CalcDepth(ls, rs);

            Vector3 LS = VpToWorld(ls, depth);
            Vector3 RS = VpToWorld(rs, depth);
            Vector3 LH = VpToWorld(lh, depth);
            Vector3 RH = VpToWorld(rh, depth);

            if (driveRoot && avatarRoot != null)
            {
                DriveRoot(LS, RS, LH, RH);
            }

            DriveArms(depth);

            if (driveLegs)
            {
                DriveLegs(depth);
            }

            DriveSpineOptional(LS, RS, LH, RH);
        }

        private float CalcDepth(Vector2 ls, Vector2 rs)
        {
            float depth = baseDepthMeters;

            if (!autoDepth) return depth;

            float sw = Vector2.Distance(ls, rs);
            if (sw > 1e-4f)
            {
                float scale = refShoulderWidth01 / sw;
                depth = Mathf.Clamp(baseDepthMeters * scale, minDepthMeters, maxDepthMeters);
            }
            return depth;
        }

        private void DriveRoot(Vector3 LS, Vector3 RS, Vector3 LH, Vector3 RH)
        {
            Vector3 hipCenter = (LH + RH) * 0.5f;
            Vector3 shoulderCenter = (LS + RS) * 0.5f;

            Vector3 desiredPos = hipCenter + rootPosOffset;

            Vector3 x = (RS - LS);
            Vector3 up = (shoulderCenter - hipCenter);
            if (x.sqrMagnitude < 1e-6f) x = avatarRoot.right;
            if (up.sqrMagnitude < 1e-6f) up = avatarRoot.up;

            Vector3 z = Vector3.Cross(up.normalized, x.normalized);
            if (z.sqrMagnitude < 1e-6f) z = avatarRoot.forward;

            Quaternion fullRot = Quaternion.LookRotation(z.normalized, up.normalized) * Quaternion.Euler(rootRotOffsetEuler);

            if (driveYawOnlyOnRoot)
            {
                float desiredYaw = fullRot.eulerAngles.y;
                if (!_yawInited)
                {
                    _yaw = desiredYaw;
                    _yawInited = true;
                }
                _yaw = Mathf.LerpAngle(_yaw, desiredYaw, 1f - Mathf.Pow(1f - yawLerp, Time.deltaTime * 60f));
                Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f) * Quaternion.Euler(rootRotOffsetEuler.x, 0f, rootRotOffsetEuler.z);

                avatarRoot.position = Vector3.Lerp(avatarRoot.position, desiredPos, 1f - Mathf.Pow(1f - posLerp, Time.deltaTime * 60f));
                avatarRoot.rotation = Quaternion.Slerp(avatarRoot.rotation, yawRot, 1f - Mathf.Pow(1f - rotLerp, Time.deltaTime * 60f));
            }
            else
            {
                avatarRoot.position = Vector3.Lerp(avatarRoot.position, desiredPos, 1f - Mathf.Pow(1f - posLerp, Time.deltaTime * 60f));
                avatarRoot.rotation = Quaternion.Slerp(avatarRoot.rotation, fullRot, 1f - Mathf.Pow(1f - rotLerp, Time.deltaTime * 60f));
            }
        }

        private void DriveSpineOptional(Vector3 LS, Vector3 RS, Vector3 LH, Vector3 RH)
        {
            if (_spine == null && _chest == null) return;

            Vector3 hipCenter = (LH + RH) * 0.5f;
            Vector3 shoulderCenter = (LS + RS) * 0.5f;

            Vector3 up = (shoulderCenter - hipCenter);
            if (up.sqrMagnitude < 1e-6f) return;

            Vector3 leftRight = (RS - LS);
            if (leftRight.sqrMagnitude < 1e-6f) leftRight = avatarRoot.right;

            Vector3 forward = Vector3.Cross(up.normalized, leftRight.normalized);
            if (forward.sqrMagnitude < 1e-6f) forward = avatarRoot.forward;

            Quaternion target = Quaternion.LookRotation(forward.normalized, up.normalized);

            if (_spine != null)
            {
                _spine.rotation = Quaternion.Slerp(_spine.rotation, target, 1f - Mathf.Pow(1f - rotLerp, Time.deltaTime * 60f));
            }
            if (_chest != null)
            {
                _chest.rotation = Quaternion.Slerp(_chest.rotation, target, 1f - Mathf.Pow(1f - rotLerp, Time.deltaTime * 60f));
            }
        }

        private void DriveArms(float depth)
        {
            if (!runner.TryGetLeftArm3Points01(out var ls, out var le, out var lw)) return;
            if (!runner.TryGetRightArm3Points01(out var rs, out var re, out var rw)) return;

            Vector3 LS = VpToWorld(ls, depth);
            Vector3 LE = VpToWorld(le, depth);
            Vector3 LW = VpToWorld(lw, depth);

            Vector3 RS = VpToWorld(rs, depth);
            Vector3 RE = VpToWorld(re, depth);
            Vector3 RW = VpToWorld(rw, depth);

            Vector3 upHint = (avatarRoot != null) ? avatarRoot.up : Vector3.up;

            ApplyBoneAim(_lUpperArm, LS, LE, upHint);
            ApplyBoneAim(_lLowerArm, LE, LW, upHint);

            ApplyBoneAim(_rUpperArm, RS, RE, upHint);
            ApplyBoneAim(_rLowerArm, RE, RW, upHint);
        }

        private void DriveLegs(float depth)
        {
            if (!TryGetIndexXY01(L_HIP, out var lh)) return;
            if (!TryGetIndexXY01(R_HIP, out var rh)) return;
            if (!TryGetIndexXY01(L_KNEE, out var lk)) return;
            if (!TryGetIndexXY01(R_KNEE, out var rk)) return;
            if (!TryGetIndexXY01(L_ANKLE, out var la)) return;
            if (!TryGetIndexXY01(R_ANKLE, out var ra)) return;

            Vector3 LH = VpToWorld(lh, depth);
            Vector3 LK = VpToWorld(lk, depth);
            Vector3 LA = VpToWorld(la, depth);

            Vector3 RH = VpToWorld(rh, depth);
            Vector3 RK = VpToWorld(rk, depth);
            Vector3 RA = VpToWorld(ra, depth);

            Vector3 upHint = (avatarRoot != null) ? avatarRoot.up : Vector3.up;

            ApplyBoneAim(_lUpperLeg, LH, LK, upHint);
            ApplyBoneAim(_lLowerLeg, LK, LA, upHint);

            ApplyBoneAim(_rUpperLeg, RH, RK, upHint);
            ApplyBoneAim(_rLowerLeg, RK, RA, upHint);
        }

        private bool TryGetIndexXY01(int index, out Vector2 xy01)
        {
            xy01 = default;
            if (!runner.TryGetLandmark01(index, out var xyz01)) return false;
            xy01 = new Vector2(xyz01.x, xyz01.y);
            return true;
        }

        private void ApplyBoneAim(Transform bone, Vector3 from, Vector3 to, Vector3 upHint)
        {
            if (bone == null) return;

            Vector3 dir = (to - from);
            if (dir.sqrMagnitude < 1e-6f) return;

            Quaternion desired = Quaternion.LookRotation(dir.normalized, upHint);
            bone.rotation = Quaternion.Slerp(bone.rotation, desired, 1f - Mathf.Pow(1f - rotLerp, Time.deltaTime * 60f));
        }

        private Vector3 VpToWorld(Vector2 vp01, float depth)
        {
            return targetCamera.ViewportToWorldPoint(new Vector3(vp01.x, 1f - vp01.y, depth));
        }
    }
}
