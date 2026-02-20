using System;
using System.IO;
using UnityEngine;

[Serializable]
public class ClothResponse
{
    public string prefabKey;
    public RGB color;
}

[Serializable]
public class RGB
{
    public int r, g, b;
}

public class TestLoadLocalJson : MonoBehaviour
{
    [Header("일단 테스트용으로 프리팹을 직접 넣자(나중에 prefabKey로 매핑)")]
    [SerializeField] private GameObject clothPrefab;

    void Start()
    {
        // 1) JSON 읽기
        var path = Path.Combine(Application.streamingAssetsPath, "cloth.json");
        var json = File.ReadAllText(path);
        Debug.Log("JSON loaded:\n" + json);

        // 2) 파싱
        var data = JsonUtility.FromJson<ClothResponse>(json);
        Debug.Log($"Parsed prefabKey={data.prefabKey}, rgb=({data.color.r},{data.color.g},{data.color.b})");

        // 3) 프리팹 생성
        var go = Instantiate(clothPrefab);

        // 4) MPB 틴트 적용 (너가 방금 성공한 스크립트 활용)
        var tint = go.GetComponent<ClothTintJson>();

        if (tint == null)
        {
            Debug.LogError("ClothTintPerInstance가 프리팹 루트(또는 자식)에 없습니다!");
            return;
        }

        var c = new Color(data.color.r / 255f, data.color.g / 255f, data.color.b / 255f, 1f);
        tint.SetTint(c);
    }
}
