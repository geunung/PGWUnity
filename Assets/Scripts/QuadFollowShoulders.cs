using UnityEngine;
using Mediapipe.Unity.Sample.PoseLandmarkDetection; // PoseLandmarkerRunner 네임스페이스

public class QuadFollowShoulders : MonoBehaviour
{
    public PoseLandmarkerRunner runner;
    public Transform cloth;

    public float depthZ = 2.0f;            // 카메라 기준 앞쪽 거리(값 바꿔가며 맞추기)

    [Header("Scale (Option 1: Fixed)")]
    public bool lockScale = true;          //  크기 고정 모드
    public Vector3 fixedScale = new Vector3(0.25f, 0.25f, 1f); //  원하는 고정 크기

    [Header("Scale (Only used when lockScale = false)")]
    public float scaleMultiplier = 1.0f;   // 기존 방식(어깨너비 기반) 쓸 때만 사용

    void Reset()
    {
        cloth = transform;
    }

    void Update()
    {
        if (runner == null || cloth == null) return;

        if (!runner.TryGetShoulders01(out var left01, out var right01))
            return;

        var cam = Camera.main;
        if (cam == null) return;

        // normalized(0~1) -> screen(px)
        Vector3 leftScreen = new Vector3(left01.x * Screen.width, (1f - left01.y) * Screen.height, 0f);
        Vector3 rightScreen = new Vector3(right01.x * Screen.width, (1f - right01.y) * Screen.height, 0f);

        leftScreen.z = depthZ;
        rightScreen.z = depthZ;

        // screen -> world
        Vector3 leftWorld = cam.ScreenToWorldPoint(leftScreen);
        Vector3 rightWorld = cam.ScreenToWorldPoint(rightScreen);

        // position = midpoint
        Vector3 mid = (leftWorld + rightWorld) * 0.5f;
        cloth.position = mid;

        // rotation = shoulder line
        Vector3 dir = rightWorld - leftWorld;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        cloth.rotation = Quaternion.Euler(0, 0, angle);

        //  scale
        if (lockScale)
        {
            cloth.localScale = fixedScale; // 크기 고정
        }
        else
        {
            float width = dir.magnitude;
            cloth.localScale = new Vector3(width * scaleMultiplier, width * scaleMultiplier, 1f);
        }
    }
}
