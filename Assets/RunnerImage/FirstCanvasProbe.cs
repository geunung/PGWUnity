using UnityEngine;

public class FirstCanvasProbe : MonoBehaviour
{
    private void LogT(string msg)
    {
        Debug.Log("[FirstCanvasProbe] t=" + Time.realtimeSinceStartup.ToString("F3") + " " + msg);
    }

    private void Awake()
    {
        LogT("Awake");
    }

    private void Start()
    {
        LogT("Start");
    }

    private void OnEnable()
    {
        LogT("OnEnable");
    }

    private void OnDisable()
    {
        LogT("OnDisable");
    }
}