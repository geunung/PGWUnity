using UnityEngine;

public class ClothTintJson : MonoBehaviour
{
    [SerializeField] private bool applyOnStart = false; // ±âº» false·Î!
    [SerializeField] private Color tint = Color.white;

    private SkinnedMeshRenderer[] parts;
    private MaterialPropertyBlock mpb;

    private static readonly int ColorId_Standard = Shader.PropertyToID("_Color");
    private static readonly int ColorId_URP = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        parts = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        if (applyOnStart) ApplyTint();
    }

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
