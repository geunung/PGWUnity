using System;
using System.Collections.Generic;
using UnityEngine;

public class OutfitController : MonoBehaviour
{
    [Serializable]
    public class KeyItem
    {
        public string key;
        public GameObject target;
    }

    [Header("Roots")]
    public Transform topRoot;
    public Transform bottomRoot;

    [Header("Catalog")]
    public List<KeyItem> topItems = new List<KeyItem>();
    public List<KeyItem> bottomItems = new List<KeyItem>();

    [Header("Options")]
    public bool deactivateAllOnAwake = true;
    public bool keepPreviousOnMissingKey = true;
    public bool rebuildCatalogOnEveryRequest = false;
    public bool logCatalogSummaryOnAwake = false;

    private readonly Dictionary<string, GameObject> _topMap = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly Dictionary<string, GameObject> _bottomMap = new Dictionary<string, GameObject>(StringComparer.Ordinal);

    private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int PropColor = Shader.PropertyToID("_Color");

    [Serializable]
    private class OutfitRequest
    {
        public string topKey;
        public string bottomKey;
        public string topColor;
        public string bottomColor;
    }

    private void Awake()
    {
        RebuildCatalog();

        if (deactivateAllOnAwake)
        {
            DeactivateAll(topRoot);
            DeactivateAll(bottomRoot);
        }

        if (logCatalogSummaryOnAwake)
        {
            Debug.Log($"[OutfitController] Catalog ready. top={_topMap.Count}, bottom={_bottomMap.Count}");
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        RebuildCatalog();
    }
#endif

    [ContextMenu("Rebuild Catalog")]
    public void RebuildCatalog()
    {
        BuildMaps();
    }

    private void BuildMaps()
    {
        _topMap.Clear();
        _bottomMap.Clear();

        for (int i = 0; i < topItems.Count; i++)
        {
            var item = topItems[i];
            if (item == null) continue;
            if (item.target == null) continue;
            if (string.IsNullOrWhiteSpace(item.key)) continue;

            if (_topMap.ContainsKey(item.key))
            {
                Debug.LogWarning($"[OutfitController] Duplicate top key: {item.key} (target={item.target.name})");
                continue;
            }

            _topMap.Add(item.key, item.target);
        }

        for (int i = 0; i < bottomItems.Count; i++)
        {
            var item = bottomItems[i];
            if (item == null) continue;
            if (item.target == null) continue;
            if (string.IsNullOrWhiteSpace(item.key)) continue;

            if (_bottomMap.ContainsKey(item.key))
            {
                Debug.LogWarning($"[OutfitController] Duplicate bottom key: {item.key} (target={item.target.name})");
                continue;
            }

            _bottomMap.Add(item.key, item.target);
        }
    }

    private static void DeactivateAll(Transform root)
    {
        if (root == null) return;
        for (int i = 0; i < root.childCount; i++)
        {
            var t = root.GetChild(i);
            if (t == null) continue;
            t.gameObject.SetActive(false);
        }
    }

    private static void ActivateOne(Transform root, GameObject target)
    {
        if (root == null) return;
        DeactivateAll(root);
        if (target != null) target.SetActive(true);
    }

    public void ApplyTop(string topKey, string hexColor = null)
    {
        if (rebuildCatalogOnEveryRequest) BuildMaps();

        if (string.IsNullOrWhiteSpace(topKey))
        {
            DeactivateAll(topRoot);
            return;
        }

        if (_topMap.TryGetValue(topKey, out var go) && go != null)
        {
            ActivateOne(topRoot, go);
            if (!string.IsNullOrWhiteSpace(hexColor)) ApplyColor(go, hexColor);
            return;
        }

        Debug.LogWarning($"[OutfitController] Top key not found: {topKey}");
        if (!keepPreviousOnMissingKey) DeactivateAll(topRoot);
    }

    public void ApplyBottom(string bottomKey, string hexColor = null)
    {
        if (rebuildCatalogOnEveryRequest) BuildMaps();

        if (string.IsNullOrWhiteSpace(bottomKey))
        {
            DeactivateAll(bottomRoot);
            return;
        }

        if (_bottomMap.TryGetValue(bottomKey, out var go) && go != null)
        {
            ActivateOne(bottomRoot, go);
            if (!string.IsNullOrWhiteSpace(hexColor)) ApplyColor(go, hexColor);
            return;
        }

        Debug.LogWarning($"[OutfitController] Bottom key not found: {bottomKey}");
        if (!keepPreviousOnMissingKey) DeactivateAll(bottomRoot);
    }

    public void ApplyOutfitJson(string json)
    {
        if (rebuildCatalogOnEveryRequest) BuildMaps();

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[OutfitController] ApplyOutfitJson: empty json");
            return;
        }

        OutfitRequest req;
        try
        {
            req = JsonUtility.FromJson<OutfitRequest>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OutfitController] ApplyOutfitJson parse failed: {e.Message}");
            return;
        }

        if (req == null)
        {
            Debug.LogWarning("[OutfitController] ApplyOutfitJson: null request");
            return;
        }

        ApplyTop(req.topKey, req.topColor);
        ApplyBottom(req.bottomKey, req.bottomColor);
    }

    private static void ApplyColor(GameObject go, string hex)
    {
        if (go == null) return;

        if (!TryParseHexColor(hex, out var c))
        {
            Debug.LogWarning($"[OutfitController] Invalid color hex: {hex}");
            return;
        }

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        var block = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) continue;

            bool canWrite = false;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat == null) continue;
                if (mat.HasProperty(PropBaseColor) || mat.HasProperty(PropColor))
                {
                    canWrite = true;
                    break;
                }
            }

            if (!canWrite) continue;

            r.GetPropertyBlock(block);
            block.SetColor(PropBaseColor, c);
            block.SetColor(PropColor, c);
            r.SetPropertyBlock(block);
        }
    }

    private static bool TryParseHexColor(string hex, out Color color)
    {
        color = Color.white;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        var s = hex.Trim();
        if (s.StartsWith("#")) s = s.Substring(1);

        if (s.Length != 6) return false;

        if (!byte.TryParse(s.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)) return false;
        if (!byte.TryParse(s.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)) return false;
        if (!byte.TryParse(s.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)) return false;

        color = new Color(r / 255f, g / 255f, b / 255f, 1f);
        return true;
    }
}