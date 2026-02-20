using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class OutfitEntry
{
    public string id;
    public GameObject prefab;
}

public class OutfitCatalog : MonoBehaviour
{
    [Header("Top Prefabs")]
    public List<OutfitEntry> tops = new List<OutfitEntry>();

    [Header("Pants Prefabs")]
    public List<OutfitEntry> pants = new List<OutfitEntry>();

    private Dictionary<string, GameObject> _topMap;
    private Dictionary<string, GameObject> _pantsMap;

    private void Awake()
    {
        BuildMaps();
    }

    private void OnValidate()
    {
        // 인스펙터에서 값이 바뀌면 에디터에서도 바로 반영되도록
        BuildMaps();
    }

    private void BuildMaps()
    {
        _topMap = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        _pantsMap = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        for (int i = 0; i < tops.Count; i++)
        {
            var e = tops[i];
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.id)) continue;
            if (e.prefab == null) continue;

            _topMap[e.id] = e.prefab;
        }

        for (int i = 0; i < pants.Count; i++)
        {
            var e = pants[i];
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.id)) continue;
            if (e.prefab == null) continue;

            _pantsMap[e.id] = e.prefab;
        }
    }

    public GameObject GetTopPrefab(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_topMap != null && _topMap.TryGetValue(id, out var p)) return p;
        return null;
    }

    public GameObject GetPantsPrefab(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_pantsMap != null && _pantsMap.TryGetValue(id, out var p)) return p;
        return null;
    }
}
