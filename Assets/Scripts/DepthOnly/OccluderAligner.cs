using UnityEngine;

public class OccluderAligner : MonoBehaviour
{
    [Header("Local offsets (relative to ClothPivot)")]
    public Vector3 localPosOffset = new Vector3(0f, -1.2f, 0.05f);

    [Header("Fix FBX forward axis (one-time constant)")]
    public float yawFixDeg = 0f;   // try 0, 90, -90, 180
    public float pitchFixDeg = 0f;
    public float rollFixDeg = 0f;

    [Header("Optional: scale tweak (keep 1 if already correct)")]
    public float uniformScale = 1f;

    void LateUpdate()
    {
        // This object should be a child of ClothPivot.
        transform.localPosition = localPosOffset;
        transform.localRotation = Quaternion.Euler(pitchFixDeg, yawFixDeg, rollFixDeg);
        transform.localScale = Vector3.one * uniformScale;
    }
}
