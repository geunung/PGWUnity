using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }

    public static string PendingOutfitJson { get; private set; } = "";
    public static string PendingSceneName { get; private set; } = "";

    [Header("Loading UI")]
    public GameObject loadingCanvas;
    public TMP_Text mainText;
    public TMP_Text tipText;

    private bool _isLoading;
    private string _currentLoadingScene = "";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        ShowBootstrapWaitingUI();
    }

    private void Start()
    {
        Debug.Log("[SceneController] Bootstrap Start");
        NotifyAndroidBootstrapReady();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    public void SetSceneName(string sceneName)
    {
        Debug.Log("[SceneController] SetSceneName: " + sceneName);

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[SceneController] sceneName is empty");
            return;
        }

        PendingSceneName = sceneName;

        if (_isLoading)
        {
            Debug.LogWarning("[SceneController] already loading: " + _currentLoadingScene);
            return;
        }

        UpdateLoadingUIText(sceneName);
        StartCoroutine(LoadSceneAsyncRoutine(sceneName));
    }

    public void SetOutfitJson(string json)
    {
        PendingOutfitJson = json ?? "";
        Debug.Log("[SceneController] SetOutfitJson length: " + PendingOutfitJson.Length);

        TryApplyPendingOutfitInActiveScene();
    }

    private IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        _isLoading = true;
        _currentLoadingScene = sceneName;

        float startTime = Time.realtimeSinceStartup;
        Debug.Log("[SceneController] LoadSceneAsync start: " + sceneName);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (op == null)
        {
            Debug.LogError("[SceneController] LoadSceneAsync failed: " + sceneName);
            _isLoading = false;
            _currentLoadingScene = "";
            HideLoadingUI();
            yield break;
        }

        op.allowSceneActivation = true;

        while (!op.isDone)
        {
            yield return null;
        }

        float endTime = Time.realtimeSinceStartup;
        Debug.Log("[SceneController] LoadSceneAsync done: " + sceneName + " time=" + (endTime - startTime));

        _isLoading = false;
        _currentLoadingScene = "";
        HideLoadingUI();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("[SceneController] OnSceneLoaded: " + scene.name);
        StartCoroutine(ApplyPendingOutfitNextFrame());
    }

    private IEnumerator ApplyPendingOutfitNextFrame()
    {
        yield return null;
        TryApplyPendingOutfitInActiveScene();
    }

    private void TryApplyPendingOutfitInActiveScene()
    {
        if (string.IsNullOrEmpty(PendingOutfitJson))
        {
            return;
        }

        OutfitController outfitController = FindFirstObjectByType<OutfitController>();
        if (outfitController == null)
        {
            Debug.Log("[SceneController] OutfitController not found yet");
            return;
        }

        Debug.Log("[SceneController] Applying pending outfit json");
        outfitController.ApplyOutfitJson(PendingOutfitJson);
    }

    private void ShowBootstrapWaitingUI()
    {
        if (loadingCanvas != null)
        {
            loadingCanvas.SetActive(true);
        }

        if (mainText != null)
        {
            mainText.text = "피팅룸을 준비하고 있습니다\n잠시만 기다려주세요";
        }

        if (tipText != null)
        {
            tipText.text = "tip. 곧 선택하신 화면으로 이동합니다";
        }
    }

    private void UpdateLoadingUIText(string sceneName)
    {
        if (loadingCanvas != null)
        {
            loadingCanvas.SetActive(true);
        }

        if (mainText != null)
        {
            mainText.text = "직원이 옷을 가지고 오는 중입니다\n잠시만 기다려주세요";
        }

        if (tipText != null)
        {
            if (sceneName == "FittingScene")
            {
                tipText.text = "tip. 아바타를 터치로 직접 조작할 수 있습니다";
            }
            else if (sceneName == "MainARScene")
            {
                tipText.text = "tip. AR 체험을 위해 기기 카메라 접근 허용 부탁드립니다";
            }
            else
            {
                tipText.text = "tip. 잠시만 기다려주세요";
            }
        }
    }

    private void HideLoadingUI()
    {
        if (loadingCanvas != null)
        {
            loadingCanvas.SetActive(false);
        }
    }

    private void NotifyAndroidBootstrapReady()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity != null)
                {
                    activity.Call("OnUnityBootstrapReady");
                    Debug.Log("[SceneController] Notified Android: bootstrap ready");
                }
                else
                {
                    Debug.LogWarning("[SceneController] currentActivity is null");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SceneController] NotifyAndroidBootstrapReady failed: " + e.Message);
        }
#else
        Debug.Log("[SceneController] Editor mode bootstrap ready");
#endif
    }
}