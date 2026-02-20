using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MannequinTouchController_Final : MonoBehaviour
{
    [Header("Refs")]
    public Transform mannequinRoot;
    public Camera mainCam;

    [Header("Layers")]
    public LayerMask boneLayerMask;

    [Header("UI")]
    public Button resetButton;

    [Header("Speed")]
    public float rootDegPerPixel = 0.45f;
    public float boneDegPerPixel = 0.75f;
    public bool allowPitch = true;
    public float pitchDegPerPixel = 0.30f;
    public float maxPitch = 25f;

    [Header("Smoothing")]
    public float rootSmoothing = 14f;
    public float boneSmoothing = 18f;

    [Header("Selection Marker")]
    public bool showSelectionMarker = true;
    public float markerScale = 0.08f;

    [Header("Joint Limits")]
    public List<HingeLimit> hingeLimits = new List<HingeLimit>();

    [Serializable]
    public class HingeLimit
    {
        public Transform bone;
        public Axis axis = Axis.X;
        public float minDeg = -5f;
        public float maxDeg = 140f;
    }

    public enum Axis { X, Y, Z }

    Transform _selectedBone;
    bool _pressedOnBone;

    Vector2 _prevPos;
    float _pitch;

    Quaternion _rootTargetRot;
    Quaternion _boneTargetWorldRot;

    readonly Dictionary<Transform, Quaternion> _restLocalRot = new Dictionary<Transform, Quaternion>();
    readonly Dictionary<Transform, HingeLimit> _hingeMap = new Dictionary<Transform, HingeLimit>();

    GameObject _marker;
    Material _markerMat;

    void Awake()
    {
        if (mainCam == null) mainCam = Camera.main;

        CacheRestPose();
        BuildHingeMap();
        EnsureMarker();

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(ResetPose);
            resetButton.onClick.AddListener(ResetPose);
        }

        if (mannequinRoot != null)
            _rootTargetRot = mannequinRoot.rotation;

        if (hingeLimits == null || hingeLimits.Count == 0)
            AutoFillCommonHinges();
    }

    void CacheRestPose()
    {
        _restLocalRot.Clear();
        if (mannequinRoot == null) return;

        var trs = mannequinRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
            _restLocalRot[trs[i]] = trs[i].localRotation;
    }

    void BuildHingeMap()
    {
        _hingeMap.Clear();
        if (hingeLimits == null) return;

        for (int i = 0; i < hingeLimits.Count; i++)
        {
            var h = hingeLimits[i];
            if (h != null && h.bone != null)
                _hingeMap[h.bone] = h;
        }
    }

    void AutoFillCommonHinges()
    {
        hingeLimits = new List<HingeLimit>();

        AddHingeIfFound("mixamorig:LeftForeArm", Axis.X, -5f, 140f);
        AddHingeIfFound("mixamorig:RightForeArm", Axis.X, -5f, 140f);
        AddHingeIfFound("mixamorig:LeftLeg", Axis.X, -5f, 140f);
        AddHingeIfFound("mixamorig:RightLeg", Axis.X, -5f, 140f);

        BuildHingeMap();
    }

    void AddHingeIfFound(string boneName, Axis axis, float minDeg, float maxDeg)
    {
        Transform t = FindDeepChildByName(mannequinRoot, boneName);
        if (t == null) return;

        hingeLimits.Add(new HingeLimit
        {
            bone = t,
            axis = axis,
            minDeg = minDeg,
            maxDeg = maxDeg
        });
    }

    Transform FindDeepChildByName(Transform root, string name)
    {
        if (root == null) return null;

        var trs = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
            if (trs[i].name == name) return trs[i];

        return null;
    }

    void EnsureMarker()
    {
        if (!showSelectionMarker) return;

        if (_marker == null)
        {
            _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _marker.name = "SelectedBoneMarker";
            DestroyCollider(_marker);
        }

        if (_markerMat == null)
        {
            Shader sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            _markerMat = new Material(sh);
            _markerMat.color = Color.yellow;
        }

        var r = _marker.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = _markerMat;

        _marker.transform.localScale = Vector3.one * markerScale;
        _marker.SetActive(false);
    }

    void DestroyCollider(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
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

            if (_pressedOnBone && _selectedBone != null)
                RotateBoneByDelta(_selectedBone, delta);
            else
                RotateRootByDelta(delta);
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

            if (_pressedOnBone && _selectedBone != null)
                RotateBoneByDelta(_selectedBone, delta);
            else
                RotateRootByDelta(delta);
        }
        else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
        {
            ClearPress();
        }
    }

    void EvaluatePress(Vector2 screenPos)
    {
        _selectedBone = RayPickBoneViewport(screenPos);
        _pressedOnBone = (_selectedBone != null);

        _rootTargetRot = mannequinRoot.rotation;

        if (_pressedOnBone)
            _boneTargetWorldRot = _selectedBone.rotation;
    }

    void ClearPress()
    {
        _selectedBone = null;
        _pressedOnBone = false;
    }

    Transform RayPickBoneViewport(Vector2 screenPos)
    {
        Ray ray = ScreenPosToViewportRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, boneLayerMask, QueryTriggerInteraction.Collide))
        {
            Transform t = hit.collider.transform;
            return ResolveBoneFromHit(t);
        }

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

    Ray ScreenPosToViewportRay(Vector2 screenPos)
    {
        Vector3 vp = mainCam.ScreenToViewportPoint(screenPos);
        return mainCam.ViewportPointToRay(vp);
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

    void RotateBoneByDelta(Transform bone, Vector2 delta)
    {
        float yaw = delta.x * boneDegPerPixel;
        float pitch = -delta.y * boneDegPerPixel;

        Quaternion r = _boneTargetWorldRot;
        r = Quaternion.AngleAxis(yaw, mannequinRoot.up) * r;
        r = Quaternion.AngleAxis(pitch, mannequinRoot.right) * r;

        _boneTargetWorldRot = r;
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
            _selectedBone.rotation = Quaternion.Slerp(
                _selectedBone.rotation,
                _boneTargetWorldRot,
                1f - Mathf.Exp(-boneSmoothing * Time.deltaTime)
            );

            ClampIfHinge(_selectedBone);

            _boneTargetWorldRot = _selectedBone.rotation;
        }
    }

    void ClampIfHinge(Transform bone)
    {
        if (bone == null) return;
        if (!_hingeMap.TryGetValue(bone, out HingeLimit h)) return;
        if (!_restLocalRot.TryGetValue(bone, out Quaternion rest)) return;

        Quaternion rel = Quaternion.Inverse(rest) * bone.localRotation;
        Vector3 e = NormalizeEuler(rel.eulerAngles);

        float v;
        if (h.axis == Axis.X)
        {
            v = Mathf.Clamp(e.x, h.minDeg, h.maxDeg);
            e = new Vector3(v, 0f, 0f);
        }
        else if (h.axis == Axis.Y)
        {
            v = Mathf.Clamp(e.y, h.minDeg, h.maxDeg);
            e = new Vector3(0f, v, 0f);
        }
        else
        {
            v = Mathf.Clamp(e.z, h.minDeg, h.maxDeg);
            e = new Vector3(0f, 0f, v);
        }

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

    public void ResetPose()
    {
        foreach (var kv in _restLocalRot)
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
