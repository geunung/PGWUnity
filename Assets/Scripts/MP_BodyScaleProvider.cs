using UnityEngine;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;

public class MP_BodyScaleProvider : MonoBehaviour
{
    [Header("Required")]
    public PoseLandmarkerRunner runner;
    public Camera targetCamera;

    [Header("Auto Depth (approx)")]
    public bool autoDepth = true;
    public float baseDepthMeters = 2.2f;
    public float refShoulderWidth01 = 0.25f;
    public float depthMin = 1.2f;
    public float depthMax = 4.0f;

    [Header("Scale from widths")]
    public bool useShoulders = true;
    public bool useHips = true;
    [Range(0f, 1f)] public float hipsWeight = 0.5f; // 0 = shoulders only, 1 = hips only

    [Header("Smoothing")]
    public float scaleLerpSpeed = 10f;

    [Header("Output (read only)")]
    [SerializeField] private float _currentDepthMeters = 2.2f;
    [SerializeField] private float _currentScale = 1.0f;

    public float CurrentDepthMeters => _currentDepthMeters;
    public float CurrentScale => _currentScale;

    void Reset()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (runner == null || targetCamera == null) return;

        if (!runner.TryGetShoulders01(out var lS, out var rS)) return;

        float shoulderWidth01 = Vector2.Distance(lS, rS);
        if (shoulderWidth01 < 1e-4f) return;

        float depth = baseDepthMeters;
        if (autoDepth)
        {
            depth = baseDepthMeters * (refShoulderWidth01 / shoulderWidth01);
            depth = Mathf.Clamp(depth, depthMin, depthMax);
        }
        _currentDepthMeters = depth;

        float sShoulder = ComputeWorldWidth(lS, rS, depth);

        float sHip = sShoulder;
        bool hasHip = false;
        if (useHips && runner.TryGetHips01(out var lH, out var rH))
        {
            float hipWidth01 = Vector2.Distance(lH, rH);
            if (hipWidth01 > 1e-4f)
            {
                sHip = ComputeWorldWidth(lH, rH, depth);
                hasHip = true;
            }
        }

        float targetScale = sShoulder;

        if (useShoulders && useHips && hasHip)
        {
            targetScale = Mathf.Lerp(sShoulder, sHip, Mathf.Clamp01(hipsWeight));
        }
        else if (useHips && hasHip && !useShoulders)
        {
            targetScale = sHip;
        }
        else
        {
            targetScale = sShoulder;
        }

        float t = 1f - Mathf.Exp(-scaleLerpSpeed * Time.deltaTime);
        _currentScale = Mathf.Lerp(_currentScale, targetScale, t);
    }

    private float ComputeWorldWidth(Vector2 a01, Vector2 b01, float depth)
    {
        Vector3 aw = targetCamera.ViewportToWorldPoint(new Vector3(a01.x, 1f - a01.y, depth));
        Vector3 bw = targetCamera.ViewportToWorldPoint(new Vector3(b01.x, 1f - b01.y, depth));
        return Vector3.Distance(aw, bw);
    }
}
