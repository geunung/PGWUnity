using System;
using UnityEngine;

public class UnityMessageReceiver : MonoBehaviour
{
    [Header("References")]
    public OutfitManager manager;

    [Serializable]
    private class SetOutfitMessage
    {
        public string type;
        public string topId;
        public string pantsId;

        // 추가: HEX 색상
        public string topColor;
        public string pantsColor;
    }

    public void OnMessageFromAndroid(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("OnMessageFromAndroid: empty json");
            return;
        }

        SetOutfitMessage msg;
        try
        {
            msg = JsonUtility.FromJson<SetOutfitMessage>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("JSON parse failed: " + e.Message + " json=" + json);
            return;
        }

        if (msg == null)
        {
            Debug.LogWarning("JSON parse result is null");
            return;
        }

        if (msg.type != "SET_OUTFIT")
        {
            Debug.Log("Unknown message type: " + msg.type);
            return;
        }

        if (manager == null)
        {
            Debug.LogError("OutfitManager is null");
            return;
        }

        manager.SpawnOutfitOnce(msg.topId, msg.pantsId, msg.topColor, msg.pantsColor);
    }
}
