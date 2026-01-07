using UnityEngine;

public class PoseClothFollower : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public Transform cloth;               // ClothQuad_Test
    public Transform debugLeftShoulder;   // (선택) 디버그용 구체
    public Transform debugRightShoulder;

    [Header("Tuning")]
    public float depthZ = 1.0f;           // 카메라 기준 앞쪽 거리
    public float scaleMultiplier = 1.0f;

    void Update()
    {
        // TODO: 여기서 MediaPipe Pose landmark를 가져와야 함.
        // 예시로, 왼/오 어깨의 "정규화 좌표(0~1)"를 얻었다고 가정:
        // left = (x,y), right = (x,y)

        Vector2 left01;
        Vector2 right01;

        bool ok = TryGetShoulders01(out left01, out right01);
        if (!ok) return;

        // 1) 정규화(0~1)를 화면좌표로 변환
        Vector3 leftScreen = new Vector3(left01.x * Screen.width, (1f - left01.y) * Screen.height, 0f);
        Vector3 rightScreen = new Vector3(right01.x * Screen.width, (1f - right01.y) * Screen.height, 0f);

        // 2) 화면좌표를 월드좌표로 변환 (카메라 기준 depthZ)
        var cam = Camera.main;
        if (cam == null) return;

        leftScreen.z = depthZ;
        rightScreen.z = depthZ;

        Vector3 leftWorld = cam.ScreenToWorldPoint(leftScreen);
        Vector3 rightWorld = cam.ScreenToWorldPoint(rightScreen);

        // 3) 위치: 어깨 중간점
        Vector3 mid = (leftWorld + rightWorld) * 0.5f;
        cloth.position = mid;

        // 4) 회전: 어깨 방향 벡터로 Z회전 맞추기 (2D 느낌)
        Vector3 dir = (rightWorld - leftWorld);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        cloth.rotation = Quaternion.Euler(0, 0, angle);

        // 5) 스케일: 어깨 너비에 비례
        float width = dir.magnitude;
        cloth.localScale = new Vector3(width * scaleMultiplier, width * scaleMultiplier, 1f);

        // 디버그(선택)
        if (debugLeftShoulder) debugLeftShoulder.position = leftWorld;
        if (debugRightShoulder) debugRightShoulder.position = rightWorld;
    }

    // 여기만 네 프로젝트의 "Pose 결과 가져오는 방식"에 맞춰서 연결하면 됨
    bool TryGetShoulders01(out Vector2 left01, out Vector2 right01)
    {
        left01 = default;
        right01 = default;

        // TODO: MediaPipe PoseLandmarker 결과에서
        // LEFT_SHOULDER / RIGHT_SHOULDER 인덱스의 x,y (0~1)를 꺼내서 넣어라.

        return false;
    }
}
