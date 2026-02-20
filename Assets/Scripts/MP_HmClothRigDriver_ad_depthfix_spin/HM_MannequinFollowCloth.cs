using UnityEngine;

/// <summary>
/// 오클루전용 마네킹을 옷(ClothRoot)에 붙여서
/// 위치 / yaw / 스케일만 따라가게 만드는 스크립트
/// - Built-in Render Pipeline 기준
/// - 포즈 추적 X, 몸통 쉘 용도
/// </summary>
public class HM_MannequinFollowCloth : MonoBehaviour
{
    [Header("Follow Target")]
    [Tooltip("보통 상의의 ClothRoot")]
    public Transform clothRoot;

    [Header("Follow Options")]
    public bool followPosition = true;
    public bool followYawOnly = true;
    public bool followScale = true;

    [Header("Offsets")]
    public Vector3 positionOffset = Vector3.zero;
    public float yawOffsetDeg = 0f;
    public float scaleMultiplier = 1.0f;

    void LateUpdate()
    {
        if (clothRoot == null) return;

        // 위치
        if (followPosition)
        {
            transform.position = clothRoot.position + positionOffset;
        }

        // 회전 (yaw만)
        if (followYawOnly)
        {
            Vector3 euler = clothRoot.rotation.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, euler.y + yawOffsetDeg, 0f);
        }
        else
        {
            transform.rotation = clothRoot.rotation;
        }

        // 스케일
        if (followScale)
        {
            transform.localScale = clothRoot.localScale * scaleMultiplier;
        }
    }
}
