using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MannequinTouchController_Final : MonoBehaviour
{
    [Header("Refs")]
    public Transform mannequinRoot;
    public Camera mainCam;
    public Animator animator;

    [Header("Layers")]
    public LayerMask boneLayerMask;

    [Header("UI Buttons")]
    public Button resetButton;
    public Button idleButton;
    public Button walkButton;
    public Button sitButton;

    [Header("Animation State Names")]
    public string idleStateName = "Idle";
    public string walkStateName = "Walk";
    public string sitStateName = "Sit";

    [Header("Animation Start")]
    public bool playAnimationOnStart = false;
    public string startStateName = "Idle";
    public bool stopAnimationWhenManualBoneControlStarts = true;

    [Header("Root")]
    public float rootDegPerPixel = 0.42f;
    public bool allowPitch = true;
    public float pitchDegPerPixel = 0.24f;
    public float maxPitch = 20f;

    [Header("Bone")]
    public float boneSwingDegPerPixel = 0.30f;
    public float boneTwistDegPerPixel = 0.14f;
    public float selectedBoneSensitivity = 1.02f;
    public float dragDeadZonePixels = 0.35f;

    [Header("Input Stabilize")]
    public float inputScale = 0.52f;
    public float maxDeltaPerFrame = 8.0f;
    public float stationaryDeltaScale = 0.24f;

    [Header("Pick")]
    public float pickSphereRadius = 0.07f;

    [Header("Smoothing")]
    public float rootSmoothing = 14f;
    public float boneSmoothing = 18f;

    [Header("Selection Marker")]
    public bool showSelectionMarker = true;
    public float markerScale = 0.08f;

    [Header("Joint Limits")]
    public List<JointLimit> jointLimits = new List<JointLimit>();

    [Serializable]
    public class JointLimit
    {
        public Transform bone;
        public bool limitX = true;
        public float minX = -80f;
        public float maxX = 80f;
        public bool limitY = true;
        public float minY = -60f;
        public float maxY = 60f;
        public bool limitZ = true;
        public float minZ = -90f;
        public float maxZ = 90f;
        public float sensitivity = 1f;
        public bool invertX;
        public bool invertY;
        public bool invertZ;
    }

    Transform _selectedBone;
    bool _pressedOnBone;

    Vector2 _prevPos;
    float _pitch;

    Quaternion _rootTargetRot;
    Quaternion _boneTargetLocalRot;

    readonly Dictionary<Transform, Quaternion> _restLocalRot = new Dictionary<Transform, Quaternion>();
    readonly Dictionary<Transform, JointLimit> _jointMap = new Dictionary<Transform, JointLimit>();

    GameObject _marker;
    Material _markerMat;

    void Awake()
    {
        if (mainCam == null) mainCam = Camera.main;

        CacheRestPose();

        if (jointLimits == null || jointLimits.Count == 0)
            AutoFillHumanJoints();

        BuildJointMap();
        EnsureMarker();

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(ResetPose);
            resetButton.onClick.AddListener(ResetPose);
        }

        if (idleButton != null)
        {
            idleButton.onClick.RemoveListener(PlayIdle);
            idleButton.onClick.AddListener(PlayIdle);
        }

        if (walkButton != null)
        {
            walkButton.onClick.RemoveListener(PlayWalk);
            walkButton.onClick.AddListener(PlayWalk);
        }

        if (sitButton != null)
        {
            sitButton.onClick.RemoveListener(PlaySit);
            sitButton.onClick.AddListener(PlaySit);
        }

        if (mannequinRoot != null)
            _rootTargetRot = mannequinRoot.rotation;
    }

    void Start()
    {
        if (animator != null)
        {
            animator.applyRootMotion = false;

            if (playAnimationOnStart)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
                animator.Play(startStateName, 0, 0f);
            }
            else
            {
                animator.enabled = false;
            }
        }
    }

    void CacheRestPose()
    {
        _restLocalRot.Clear();
        if (mannequinRoot == null) return;

        Transform[] trs = mannequinRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
            _restLocalRot[trs[i]] = trs[i].localRotation;
    }

    void BuildJointMap()
    {
        _jointMap.Clear();
        if (jointLimits == null) return;

        for (int i = 0; i < jointLimits.Count; i++)
        {
            JointLimit j = jointLimits[i];
            if (j != null && j.bone != null)
                _jointMap[j.bone] = j;
        }
    }

    void AutoFillHumanJoints()
    {
        jointLimits = new List<JointLimit>();

        AddJointIfFound("mixamorig:LeftArm", true, -95f, 110f, true, -45f, 55f, true, -115f, 70f, 1.00f, false, false, false);
        AddJointIfFound("mixamorig:RightArm", true, -95f, 110f, true, -55f, 45f, true, -70f, 115f, 1.00f, false, false, false);

        AddJointIfFound("mixamorig:LeftForeArm", true, -5f, 138f, false, 0f, 0f, false, 0f, 0f, 0.92f, false, false, false);
        AddJointIfFound("mixamorig:RightForeArm", true, -5f, 138f, false, 0f, 0f, false, 0f, 0f, 0.92f, false, false, false);

        AddJointIfFound("mixamorig:LeftUpLeg", true, -78f, 88f, true, -22f, 22f, true, -22f, 42f, 0.95f, false, false, false);
        AddJointIfFound("mixamorig:RightUpLeg", true, -78f, 88f, true, -22f, 22f, true, -42f, 22f, 0.95f, false, false, false);

        AddJointIfFound("mixamorig:LeftLeg", true, -2f, 142f, false, 0f, 0f, false, 0f, 0f, 0.90f, false, false, false);
        AddJointIfFound("mixamorig:RightLeg", true, -2f, 142f, false, 0f, 0f, false, 0f, 0f, 0.90f, false, false, false);
    }

    void AddJointIfFound(
        string boneName,
        bool limitX, float minX, float maxX,
        bool limitY, float minY, float maxY,
        bool limitZ, float minZ, float maxZ,
        float sensitivity,
        bool invertX, bool invertY, bool invertZ)
    {
        Transform t = FindDeepChildByName(mannequinRoot, boneName);
        if (t == null) return;

        JointLimit j = new JointLimit();
        j.bone = t;
        j.limitX = limitX;
        j.minX = minX;
        j.maxX = maxX;
        j.limitY = limitY;
        j.minY = minY;
        j.maxY = maxY;
        j.limitZ = limitZ;
        j.minZ = minZ;
        j.maxZ = maxZ;
        j.sensitivity = sensitivity;
        j.invertX = invertX;
        j.invertY = invertY;
        j.invertZ = invertZ;

        jointLimits.Add(j);
    }

    Transform FindDeepChildByName(Transform root, string name)
    {
        if (root == null) return null;

        Transform[] trs = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i].name == name)
                return trs[i];
        }

        return null;
    }

    void EnsureMarker()
    {
        if (!showSelectionMarker) return;

        if (_marker == null)
        {
            _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _marker.name = "SelectedBoneMarker";
            Collider c = _marker.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        if (_markerMat == null)
        {
            Shader sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            _markerMat = new Material(sh);
            _markerMat.color = Color.yellow;
        }

        Renderer r = _marker.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = _markerMat;

        _marker.transform.localScale = Vector3.one * markerScale;
        _marker.SetActive(false);
    }

    void Update()
    {
        if (mainCam == null || mannequinRoot == null) return;

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.R)) ResetPose();
        HandleMouse();
#else
        HandleTouch();
#endif

        ApplySmoothingAndClamp();
        UpdateMarker();
    }

    void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _prevPos = Input.mousePosition;
            EvaluatePress(_prevPos);
        }
        else if (Input.GetMouseButton(0))
        {
            Vector2 pos = Input.mousePosition;
            Vector2 delta = pos - _prevPos;
            _prevPos = pos;
            ProcessDrag(delta, false);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            ClearPress();
        }
    }

    void HandleTouch()
    {
        if (Input.touchCount == 0)
        {
            ClearPress();
            return;
        }

        if (Input.touchCount >= 2)
        {
            ClearPress();
            return;
        }

        Touch t = Input.GetTouch(0);

        if (t.phase == TouchPhase.Began)
        {
            _prevPos = t.position;
            EvaluatePress(_prevPos);
        }
        else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
        {
            Vector2 delta = t.position - _prevPos;
            _prevPos = t.position;
            ProcessDrag(delta, t.phase == TouchPhase.Stationary);
        }
        else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
        {
            ClearPress();
        }
    }

    void ProcessDrag(Vector2 delta, bool isStationary)
    {
        if (delta.magnitude < dragDeadZonePixels) return;

        if (isStationary)
            delta *= stationaryDeltaScale;

        if (_pressedOnBone && _selectedBone != null)
            RotateBoneByDeltaLocal(_selectedBone, delta);
        else
            RotateRootByDelta(delta);
    }

    void EvaluatePress(Vector2 screenPos)
    {
        _selectedBone = RayPickBone(screenPos);
        _pressedOnBone = (_selectedBone != null);

        _rootTargetRot = mannequinRoot.rotation;

        if (_pressedOnBone)
        {
            if (stopAnimationWhenManualBoneControlStarts)
                EnterManualMode();

            _boneTargetLocalRot = _selectedBone.localRotation;
        }
    }

    void ClearPress()
    {
        _selectedBone = null;
        _pressedOnBone = false;
    }

    Transform RayPickBone(Vector2 screenPos)
    {
        Ray ray = mainCam.ScreenPointToRay(screenPos);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 500f, boneLayerMask, QueryTriggerInteraction.Collide))
            return ResolveBoneFromHit(hit.collider.transform);

        if (Physics.SphereCast(ray, pickSphereRadius, out hit, 500f, boneLayerMask, QueryTriggerInteraction.Collide))
            return ResolveBoneFromHit(hit.collider.transform);

        return null;
    }

    Transform ResolveBoneFromHit(Transform hitT)
    {
        if (hitT == null) return null;

        Transform t = hitT;

        if (t.name.StartsWith("COL_"))
        {
            Transform p = t.parent;
            while (p != null && p != mannequinRoot)
            {
                if (p.name.StartsWith("mixamorig:")) return p;
                p = p.parent;
            }
            return hitT.parent != null ? hitT.parent : hitT;
        }

        while (t != null && t != mannequinRoot)
        {
            if (t.name.StartsWith("mixamorig:")) return t;
            t = t.parent;
        }

        return hitT;
    }

    void RotateRootByDelta(Vector2 delta)
    {
        float yaw = -delta.x * rootDegPerPixel;

        Quaternion r = _rootTargetRot;
        r = Quaternion.AngleAxis(yaw, Vector3.up) * r;

        if (allowPitch)
        {
            _pitch = Mathf.Clamp(_pitch + (-delta.y * pitchDegPerPixel), -maxPitch, maxPitch);
            Vector3 e = r.eulerAngles;
            r = Quaternion.Euler(_pitch, e.y, 0f);
        }

        _rootTargetRot = r;
    }

    void RotateBoneByDeltaLocal(Transform bone, Vector2 delta)
    {
        if (bone == null) return;
        if (!_restLocalRot.TryGetValue(bone, out Quaternion rest)) return;

        JointLimit j = null;
        _jointMap.TryGetValue(bone, out j);

        float mul = selectedBoneSensitivity;
        if (j != null) mul *= Mathf.Max(0.01f, j.sensitivity);

        Vector2 d = delta * inputScale;
        d.x = Mathf.Clamp(d.x, -maxDeltaPerFrame, maxDeltaPerFrame);
        d.y = Mathf.Clamp(d.y, -maxDeltaPerFrame, maxDeltaPerFrame);

        Quaternion rel = Quaternion.Inverse(rest) * _boneTargetLocalRot;
        Vector3 e = NormalizeEuler(rel.eulerAngles);

        float addX = -d.y * boneSwingDegPerPixel * mul;
        float addY = d.x * boneTwistDegPerPixel * mul;
        float addZ = -d.x * boneSwingDegPerPixel * 0.22f * mul;

        addX *= 0.88f;
        addY *= 0.82f;
        addZ *= 0.72f;

        if (j != null)
        {
            if (j.invertX) addX = -addX;
            if (j.invertY) addY = -addY;
            if (j.invertZ) addZ = -addZ;
        }

        e.x += addX;
        e.y += addY;
        e.z += addZ;

        if (j != null)
        {
            if (j.limitX) e.x = Mathf.Clamp(e.x, j.minX, j.maxX);
            if (j.limitY) e.y = Mathf.Clamp(e.y, j.minY, j.maxY);
            if (j.limitZ) e.z = Mathf.Clamp(e.z, j.minZ, j.maxZ);
        }

        _boneTargetLocalRot = rest * Quaternion.Euler(e);
    }

    void ApplySmoothingAndClamp()
    {
        mannequinRoot.rotation = Quaternion.Slerp(
            mannequinRoot.rotation,
            _rootTargetRot,
            1f - Mathf.Exp(-rootSmoothing * Time.deltaTime)
        );

        if (_selectedBone != null)
        {
            _selectedBone.localRotation = Quaternion.Slerp(
                _selectedBone.localRotation,
                _boneTargetLocalRot,
                1f - Mathf.Exp(-boneSmoothing * Time.deltaTime)
            );

            ClampJoint(_selectedBone);
            _boneTargetLocalRot = _selectedBone.localRotation;
        }
    }

    void ClampJoint(Transform bone)
    {
        if (bone == null) return;
        if (!_jointMap.TryGetValue(bone, out JointLimit j)) return;
        if (!_restLocalRot.TryGetValue(bone, out Quaternion rest)) return;

        Quaternion rel = Quaternion.Inverse(rest) * bone.localRotation;
        Vector3 e = NormalizeEuler(rel.eulerAngles);

        if (j.limitX) e.x = Mathf.Clamp(e.x, j.minX, j.maxX);
        if (j.limitY) e.y = Mathf.Clamp(e.y, j.minY, j.maxY);
        if (j.limitZ) e.z = Mathf.Clamp(e.z, j.minZ, j.maxZ);

        bone.localRotation = rest * Quaternion.Euler(e);
    }

    Vector3 NormalizeEuler(Vector3 e)
    {
        return new Vector3(NormAngle(e.x), NormAngle(e.y), NormAngle(e.z));
    }

    float NormAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        if (a < -180f) a += 360f;
        return a;
    }

    void UpdateMarker()
    {
        if (!showSelectionMarker) return;
        if (_marker == null) return;

        if (_selectedBone == null)
        {
            if (_marker.activeSelf) _marker.SetActive(false);
            return;
        }

        if (!_marker.activeSelf) _marker.SetActive(true);
        _marker.transform.position = _selectedBone.position;
    }

    public void PlayIdle()
    {
        PlayAnimationState(idleStateName);
    }

    public void PlayWalk()
    {
        PlayAnimationState(walkStateName);
    }

    public void PlaySit()
    {
        PlayAnimationState(sitStateName);
    }

    void PlayAnimationState(string stateName)
    {
        if (animator == null) return;

        _selectedBone = null;
        _pressedOnBone = false;

        animator.applyRootMotion = false;
        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);
        animator.CrossFade(stateName, 0.15f, 0);
    }

    public void EnterManualMode()
    {
        if (animator == null) return;
        animator.enabled = false;
    }

    public void ResetPose()
    {
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.enabled = false;
        }

        foreach (KeyValuePair<Transform, Quaternion> kv in _restLocalRot)
        {
            if (kv.Key == null) continue;
            kv.Key.localRotation = kv.Value;
        }

        _rootTargetRot = mannequinRoot.rotation;
        _selectedBone = null;
        _pressedOnBone = false;
        _pitch = 0f;

        if (_marker != null) _marker.SetActive(false);
    }
}