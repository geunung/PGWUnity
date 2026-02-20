using UnityEngine;

public class DepthOnlyApplier : MonoBehaviour
{
    public Material depthOnlyMaterial;
    public bool includeChildren = true;

    void Awake()
    {
        if (depthOnlyMaterial == null)
        {
            Debug.LogError("[DepthOnlyApplier] depthOnlyMaterial is null");
            return;
        }

        var renderers = includeChildren
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponents<Renderer>();

        foreach (var r in renderers)
        {
            r.sharedMaterial = depthOnlyMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
    }
}
