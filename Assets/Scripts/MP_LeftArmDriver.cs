using UnityEngine;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;

// 왼팔(LeftArm, LeftForeArm, LeftHand)만 "2D 포즈"로 따라가게 만드는 최소 드라이버
public class MP_LeftArmDriver : MonoBehaviour
{
    [Header("References")]
    public PoseLandmarkerRunner runner;

    [Header("Bones (from clothing skeleton)")]
    public Transform leftArm;       // Upper arm bone
    public Transform leftForeArm;   // Lower arm bone
    public Transform leftHand;      // Hand bone

    [Header("Tuning")]
    [Tooltip("2D 기반이므로 회전이 튈 수 있어 보정값이 필요할 수 있음")]
    public Vector3 armRotationOffsetEuler = Vector3.zero;
    public Vector3 foreArmRotationOffsetEuler = Vector3.zero;
    public Vector3 handRotationOffsetEuler = Vector3.zero;

    [Range(0f, 1f)]
    public float smoothing = 0.25f;

    private Quaternion _armOffset, _foreArmOffset, _handOffset;

    void Awake()
    {
        _armOffset = Quaternion.Euler(armRotationOffsetEuler);
        _foreArmOffset = Quaternion.Euler(foreArmRotationOffsetEuler);
        _handOffset = Quaternion.Euler(handRotationOffsetEuler);
    }

    void Update()
    {
        if (runner == null || leftArm == null || leftForeArm == null || leftHand == null) return;

        // MediaPipe 좌표(0~1)를 runner에서 가져온다고 가정
        // 너 프로젝트에 runner.TryGetShoulders01 같은 함수가 이미 있으니,
        // 같은 방식으로 "왼쪽: 어깨/팔꿈치/손목"만 runner에서 꺼내오는 함수를 쓰면 됨.
        // 지금은 runner에 아래 함수가 "있다"고 가정하고 호출한다.
        if (!runner.TryGetLeftArm3Points01(out var sh, out var el, out var wr))
            return;

        // 2D 방향벡터(화면 공간)로부터 각 뼈 회전 계산
        // UpperArm: shoulder -> elbow
        // ForeArm : elbow -> wrist
        Vector2 vUpper = (el - sh);
        Vector2 vLower = (wr - el);

        if (vUpper.sqrMagnitude < 1e-6f || vLower.sqrMagnitude < 1e-6f) return;

        float angUpper = Mathf.Atan2(vUpper.y, vUpper.x) * Mathf.Rad2Deg;
        float angLower = Mathf.Atan2(vLower.y, vLower.x) * Mathf.Rad2Deg;

        // 여기서는 "Z축 회전"만 사용(2D 기반)
        Quaternion qUpper = Quaternion.AngleAxis(angUpper, Vector3.forward) * _armOffset;
        Quaternion qLower = Quaternion.AngleAxis(angLower, Vector3.forward) * _foreArmOffset;

        // Hand는 손목 방향만 대충 맞추거나, forearm과 동일하게 둠
        Quaternion qHand = qLower * _handOffset;

        // 부드럽게 적용
        leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, qUpper, 1f - Mathf.Pow(1f - smoothing, 60f * Time.deltaTime));
        leftForeArm.localRotation = Quaternion.Slerp(leftForeArm.localRotation, qLower, 1f - Mathf.Pow(1f - smoothing, 60f * Time.deltaTime));
        leftHand.localRotation = Quaternion.Slerp(leftHand.localRotation, qHand, 1f - Mathf.Pow(1f - smoothing, 60f * Time.deltaTime));
    }
}
