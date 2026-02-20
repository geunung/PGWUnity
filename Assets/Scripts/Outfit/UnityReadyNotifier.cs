using UnityEngine;

public class UnityReadyNotifier : MonoBehaviour
{
    // Unity 씬이 로드되고 실행 준비가 됐을 때 Android에게 알려준다
    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                // Android Activity의 public 메서드 OnUnityReady() 호출
                activity.Call("OnUnityReady");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("UnityReadyNotifier failed: " + e.Message);
        }
#endif
    }
}
