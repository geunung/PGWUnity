using UnityEngine;

public class ClothTintPerInstance : MonoBehaviour
{
    [SerializeField] private Color tint = Color.red;

    private SkinnedMeshRenderer[] parts;
    private MaterialPropertyBlock mpb;

    private static readonly int ColorId_Standard = Shader.PropertyToID("_Color");     // Standard
    private static readonly int ColorId_URP = Shader.PropertyToID("_BaseColor");      // URP Lit

    void Awake()
    {
        parts = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        ApplyTint();
    }

    [ContextMenu("Apply Tint")]
    public void ApplyTint()
    {
        foreach (var r in parts)
        {
            if (!r) continue;

            r.GetPropertyBlock(mpb);

            var sm = r.sharedMaterial;
            if (sm != null && sm.HasProperty(ColorId_Standard))
                mpb.SetColor(ColorId_Standard, tint);
            else
                mpb.SetColor(ColorId_URP, tint);

            r.SetPropertyBlock(mpb);
        }
    }

    public void SetTint(Color c)
    {
        tint = c;
        ApplyTint();
    }
}
