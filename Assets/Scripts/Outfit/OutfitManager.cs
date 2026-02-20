using UnityEngine;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;

public class OutfitManager : MonoBehaviour
{
    [Header("References")]
    public OutfitCatalog catalog;
    public Transform topAnchor;
    public Transform pantsAnchor;

    [Header("Pose Inputs (Scene Objects)")]
    public PoseLandmarkerRunner_ad_spin runner;
    public Camera targetCamera;

    [Header("Current Instances (ReadOnly)")]
    [SerializeField] private GameObject currentTop;
    [SerializeField] private GameObject currentPants;

    // Android에서 온 값으로 "한 번 생성(기존 제거 후 생성)" + 색상 적용
    public void SpawnOutfitOnce(string topId, string pantsId, string topColorHex, string pantsColorHex)
    {
        ClearCurrentOutfit();

        if (catalog == null)
        {
            Debug.LogError("OutfitManager: catalog is null");
            return;
        }
        if (topAnchor == null || pantsAnchor == null)
        {
            Debug.LogError("OutfitManager: anchors are null");
            return;
        }

        // 상의 생성
        if (!string.IsNullOrEmpty(topId))
        {
            var topPrefab = catalog.GetTopPrefab(topId);
            if (topPrefab != null)
            {
                currentTop = Instantiate(topPrefab, topAnchor, false);
                WireDrivers(currentTop);
                UnlockScaleOnOne(currentTop);

                // 색상 적용(빈 문자열이면 적용 안 함)
                ApplyColorToRoot(currentTop, topColorHex);
            }
            else
            {
                Debug.LogWarning("Top prefab not found: " + topId);
            }
        }

        // 하의 생성
        if (!string.IsNullOrEmpty(pantsId))
        {
            var pantsPrefab = catalog.GetPantsPrefab(pantsId);
            if (pantsPrefab != null)
            {
                currentPants = Instantiate(pantsPrefab, pantsAnchor, false);
                WireDrivers(currentPants);
                UnlockScaleOnOne(currentPants);

                ApplyColorToRoot(currentPants, pantsColorHex);
            }
            else
            {
                Debug.LogWarning("Pants prefab not found: " + pantsId);
            }
        }
    }

    // ----------------------------
    // UI 버튼: 스케일 잠금/해제
    // ----------------------------
    public void LockScale()
    {
        LockScaleOnOne(currentTop);
        LockScaleOnOne(currentPants);
    }

    public void UnlockScale()
    {
        UnlockScaleOnOne(currentTop);
        UnlockScaleOnOne(currentPants);
    }

    // ----------------------------
    // UI 버튼: 뒤로가기(메인 화면으로)
    // finish 금지, Android 메서드 호출
    // ----------------------------
    public void BackToAndroid()
    {
        if (Application.isEditor)
        {
            Debug.Log("BackToAndroid: editor mode");
            return;
        }

#if UNITY_ANDROID
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("BackToMainFromUnity");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("BackToAndroid failed: " + e.Message);
        }
#endif
    }

    // ----------------------------
    // 내부
    // ----------------------------
    private void ClearCurrentOutfit()
    {
        if (currentTop != null)
        {
            Destroy(currentTop);
            currentTop = null;
        }
        if (currentPants != null)
        {
            Destroy(currentPants);
            currentPants = null;
        }
    }

    private void WireDrivers(GameObject outfitRoot)
    {
        if (outfitRoot == null) return;

        if (runner == null) Debug.LogWarning("OutfitManager: runner is null (Inspector 연결 필요)");
        if (targetCamera == null) Debug.LogWarning("OutfitManager: targetCamera is null (Inspector 연결 필요)");

        var topDrivers = outfitRoot.GetComponentsInChildren<MP_ClothRigDriver_ad_depthfix_spin>(true);
        for (int i = 0; i < topDrivers.Length; i++)
        {
            var d = topDrivers[i];
            if (d == null) continue;
            d.runner = runner;
            d.targetCamera = targetCamera;
        }

        var pantsDrivers = outfitRoot.GetComponentsInChildren<MP_PantsRigDriver_ad_depthfix_spin>(true);
        for (int i = 0; i < pantsDrivers.Length; i++)
        {
            var d = pantsDrivers[i];
            if (d == null) continue;
            d.runner = runner;
            d.targetCamera = targetCamera;
        }
    }

    private void LockScaleOnOne(GameObject root)
    {
        if (root == null) return;

        var tops = root.GetComponentsInChildren<MP_ClothRigDriver_ad_depthfix_spin>(true);
        for (int i = 0; i < tops.Length; i++) tops[i].LockScaleNow();

        var pants = root.GetComponentsInChildren<MP_PantsRigDriver_ad_depthfix_spin>(true);
        for (int i = 0; i < pants.Length; i++) pants[i].LockScaleNow();
    }

    private void UnlockScaleOnOne(GameObject root)
    {
        if (root == null) return;

        var tops = root.GetComponentsInChildren<MP_ClothRigDriver_ad_depthfix_spin>(true);
        for (int i = 0; i < tops.Length; i++) tops[i].UnlockScale();

        var pants = root.GetComponentsInChildren<MP_PantsRigDriver_ad_depthfix_spin>(true);
        for (int i = 0; i < pants.Length; i++) pants[i].UnlockScale();
    }

    // ----------------------------
    // 색상 적용(MaterialPropertyBlock)
    // ----------------------------
    private void ApplyColorToRoot(GameObject root, string hex)
    {
        if (root == null) return;
        if (string.IsNullOrEmpty(hex)) return;

        if (!ColorUtility.TryParseHtmlString(hex, out var col))
        {
            Debug.LogWarning("Invalid color hex: " + hex);
            return;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        var mpb = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(mpb);

            // URP Lit / Standard 대응
            mpb.SetColor("_BaseColor", col);
            mpb.SetColor("_Color", col);

            r.SetPropertyBlock(mpb);
        }
    }
}
