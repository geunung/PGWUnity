using UnityEngine;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;

public class ClothFollowShoulders2D : MonoBehaviour
{
    [Header("References")]
    public PoseLandmarkerRunner runner;     // Solution의 PoseLandmarkerRunner 연결
    public Transform clothPivot;            // ClothPivot 연결

    [Header("Tuning")]
    public float depthZ = 2.0f;
    public float scaleMultiplier = 1.0f;

    [Header("Fix (Model Axis / Flip)")]
    public Vector3 rotationOffsetEuler = new Vector3(0, 180, 0); //  여기 바꿔가며 해결
    public Vector3 pivotLocalOffset = Vector3.zero;              // 위치 미세조정(옵션)

    void Update()
    {
        if (runner == null || clothPivot == null) return;
        if (!runner.TryGetShoulders01(out var left01, out var right01)) return;

        var cam = Camera.main;
        if (cam == null) return;

        // normalized -> screen
        Vector3 leftScreen = new Vector3(left01.x * Screen.width, (1f - left01.y) * Screen.height, depthZ);
        Vector3 rightScreen = new Vector3(right01.x * Screen.width, (1f - right01.y) * Screen.height, depthZ);

        // screen -> world
        Vector3 leftWorld = cam.ScreenToWorldPoint(leftScreen);
        Vector3 rightWorld = cam.ScreenToWorldPoint(rightScreen);

        // position midpoint
        Vector3 mid = (leftWorld + rightWorld) * 0.5f;
        clothPivot.position = mid;

        // base rotation from shoulder line (2D)
        Vector3 dir = rightWorld - leftWorld;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        clothPivot.rotation = Quaternion.Euler(0f, 0f, angle);

        // ★ apply model axis fix (180 flip etc.)
        clothPivot.rotation *= Quaternion.Euler(rotationOffsetEuler);

        // scale by shoulder width
        float width = dir.magnitude;
        float s = width * scaleMultiplier;
        clothPivot.localScale = new Vector3(s, s, s);

        // optional offset in local space
        if (pivotLocalOffset != Vector3.zero)
        {
            clothPivot.position += clothPivot.TransformVector(pivotLocalOffset);
        }
    }
}
