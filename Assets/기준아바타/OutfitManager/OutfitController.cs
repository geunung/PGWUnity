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

    [Header("Catalog")]
    public List<KeyItem> topItems = new List<KeyItem>();
    public List<KeyItem> bottomItems = new List<KeyItem>();

    [Header("Options")]
    public bool deactivateAllOnAwake = true;

    private readonly Dictionary<string, GameObject> _topMap = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly Dictionary<string, GameObject> _bottomMap = new Dictionary<string, GameObject>(StringComparer.Ordinal);

    private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int PropColor = Shader.PropertyToID("_Color");

    private bool _appliedOnce = false;
    private string _lastAppliedJson = "";

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
        BuildCatalog();

        if (deactivateAllOnAwake)
        {
            DeactivateAll();
        }
    }

    private void Start()
    {
        TryApplyPendingJson();
    }

    private void Update()
    {
        if (!_appliedOnce)
        {
            TryApplyPendingJson();
        }
    }

    private void TryApplyPendingJson()
    {
        string json = SceneController.PendingOutfitJson;

        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        if (_appliedOnce && _lastAppliedJson == json)
        {
            return;
        }

        Debug.Log("[OutfitController] Applying pending json: " + json);
        ApplyOutfitJson(json);
        _lastAppliedJson = json;
        _appliedOnce = true;
    }

    private void BuildCatalog()
    {
        _topMap.Clear();
        _bottomMap.Clear();

        foreach (var item in topItems)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.key) || item.target == null)
                continue;

            if (!_topMap.ContainsKey(item.key))
                _topMap.Add(item.key, item.target);
        }

        foreach (var item in bottomItems)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.key) || item.target == null)
                continue;

            if (!_bottomMap.ContainsKey(item.key))
                _bottomMap.Add(item.key, item.target);
        }
    }

    private void DeactivateAll()
    {
        foreach (var kv in _topMap)
        {
            if (kv.Value != null)
                kv.Value.SetActive(false);
        }

        foreach (var kv in _bottomMap)
        {
            if (kv.Value != null)
                kv.Value.SetActive(false);
        }
    }

    public void ApplyOutfitJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[OutfitController] empty json");
            return;
        }

        OutfitRequest req;

        try
        {
            req = JsonUtility.FromJson<OutfitRequest>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[OutfitController] json parse error: " + e.Message);
            return;
        }

        if (req == null) return;

        ApplyTop(req.topKey, req.topColor);
        ApplyBottom(req.bottomKey, req.bottomColor);
    }

    public void ApplyTop(string key, string colorHex = null)
    {
        foreach (var kv in _topMap)
        {
            if (kv.Value != null)
                kv.Value.SetActive(false);
        }

        if (string.IsNullOrWhiteSpace(key)) return;

        if (_topMap.TryGetValue(key, out var obj) && obj != null)
        {
            obj.SetActive(true);

            if (!string.IsNullOrWhiteSpace(colorHex))
                ApplyColor(obj, colorHex);
        }
        else
        {
            Debug.LogWarning("[OutfitController] Top not found: " + key);
        }
    }

    public void ApplyBottom(string key, string colorHex = null)
    {
        foreach (var kv in _bottomMap)
        {
            if (kv.Value != null)
                kv.Value.SetActive(false);
        }

        if (string.IsNullOrWhiteSpace(key)) return;

        if (_bottomMap.TryGetValue(key, out var obj) && obj != null)
        {
            obj.SetActive(true);

            if (!string.IsNullOrWhiteSpace(colorHex))
                ApplyColor(obj, colorHex);
        }
        else
        {
            Debug.LogWarning("[OutfitController] Bottom not found: " + key);
        }
    }

    private static void ApplyColor(GameObject go, string hex)
    {
        if (go == null) return;

        if (!ColorUtility.TryParseHtmlString(hex, out var color))
        {
            Debug.LogWarning("Invalid color: " + hex);
            return;
        }

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        var block = new MaterialPropertyBlock();

        foreach (var r in renderers)
        {
            if (r == null) continue;

            r.GetPropertyBlock(block);
            block.SetColor(PropBaseColor, color);
            block.SetColor(PropColor, color);
            r.SetPropertyBlock(block);
        }
    }
}