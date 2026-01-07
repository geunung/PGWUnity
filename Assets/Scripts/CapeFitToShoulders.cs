using UnityEngine;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;

public class CapeFitToShoulders : MonoBehaviour
{
    public PoseLandmarkerRunner runner;
    public Transform capeMesh;              // 망토 모델(자식). 방향/위치 보정용
    public float depthZ = 2.0f;

    [Header("Mapping")]
    public bool mirrorX = false;            // 좌우 반전 필요하면 체크
    public float downOffset = 0.0f;         // 어깨선에서 조금 아래로 내릴 때(+면 아래로)
    public float forwardOffset = 0.0f;      // 카메라쪽/뒤쪽 미세 조정(z)

    [Header("Scale")]
    public bool autoScale = true;
    public float shoulderWidthToScaleX = 1.0f; // 어깨너비(월드) → scale.x 변환 계수
    public float yFromX = 1.2f;                // scale.y = scale.x * yFromX (망토 길이 비율)
    public float fixedScale = 1.0f;            // autoScale=false일 때 사용

    [Header("Smoothing")]
    public float smooth = 0.2f;             // 0.1~0.4 (모바일 안정화)

    void Reset()
    {
        // capeMesh를 안 넣으면 자식 중 첫 번째를 대충 잡아줌(선택)
        if (capeMesh == null && transform.childCount > 0)
            capeMesh = transform.GetChild(0);
    }

    void Update()
    {
        if (runner == null) return;

        if (!runner.TryGetShoulders01(out var left01, out var right01))
            return;

        if (mirrorX)
        {
            var tmp = left01;
            left01 = right01;
            right01 = tmp;
        }

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 leftScreen = new Vector3(left01.x * Screen.width, (1f - left01.y) * Screen.height, depthZ);
        Vector3 rightScreen = new Vector3(right01.x * Screen.width, (1f - right01.y) * Screen.height, depthZ);

        Vector3 leftWorld = cam.ScreenToWorldPoint(leftScreen);
        Vector3 rightWorld = cam.ScreenToWorldPoint(rightScreen);

        // 어깨 중간점
        Vector3 mid = (leftWorld + rightWorld) * 0.5f;

        // 어깨 라인 방향(2D 회전)
        Vector3 dir = (rightWorld - leftWorld);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 오프셋(월드 기준)
        Vector3 offset = new Vector3(0f, -downOffset, forwardOffset);

        // 목표 Transform
        Vector3 targetPos = mid + offset;
        Quaternion targetRot = Quaternion.Euler(0, 0, angle);

        //  스무딩 적용(특히 모바일에서 중요)
        transform.position = Vector3.Lerp(transform.position, targetPos, smooth);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, smooth);

        //  스케일: 어깨 너비 기반 자동 조절
        if (autoScale)
        {
            float shoulderWidth = dir.magnitude;               // 월드 단위 너비
            float sx = shoulderWidth * shoulderWidthToScaleX;  // 변환 계수로 보정
            float sy = sx * yFromX;

            Vector3 targetScale = new Vector3(sx, sy, sx);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, smooth);
        }
        else
        {
            Vector3 targetScale = new Vector3(fixedScale, fixedScale, fixedScale);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, smooth);
        }
    }
}
