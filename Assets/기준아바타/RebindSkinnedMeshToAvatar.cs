using System;
using System.Collections.Generic;
using UnityEngine;

public class RebindSkinnedMeshToAvatar : MonoBehaviour
{
    [Header("Target")]
    public GameObject avatarRoot;

    [Header("Options")]
    public bool includeInactive = true;
    public bool rebindRootBone = true;

    public void Rebind()
    {
        if (avatarRoot == null)
        {
            Debug.LogError("avatarRoot is null");
            return;
        }

        var nameToBone = BuildBoneMap(avatarRoot.transform, includeInactive);
        if (nameToBone.Count == 0)
        {
            Debug.LogError("No bones found under avatarRoot");
            return;
        }

        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive);
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning("No SkinnedMeshRenderer found under this object");
            return;
        }

        int changedCount = 0;

        foreach (var smr in renderers)
        {
            if (smr == null) continue;

            var oldBones = smr.bones;
            if (oldBones == null || oldBones.Length == 0)
            {
                Debug.LogWarning($"SMR '{smr.name}' has no bones array");
                continue;
            }

            var newBones = new Transform[oldBones.Length];
            bool anyMissing = false;

            for (int i = 0; i < oldBones.Length; i++)
            {
                var b = oldBones[i];
                if (b == null)
                {
                    newBones[i] = null;
                    anyMissing = true;
                    continue;
                }

                if (nameToBone.TryGetValue(b.name, out var mapped))
                {
                    newBones[i] = mapped;
                }
                else
                {
                    newBones[i] = null;
                    anyMissing = true;
                }
            }

            smr.bones = newBones;

            if (rebindRootBone && smr.rootBone != null)
            {
                if (nameToBone.TryGetValue(smr.rootBone.name, out var mappedRoot))
                {
                    smr.rootBone = mappedRoot;
                }
            }

            smr.sharedMesh.RecalculateBounds();

            changedCount++;

            if (anyMissing)
            {
                Debug.LogWarning($"Rebind completed with missing bones: {smr.name}");
            }
            else
            {
                Debug.Log($"Rebind OK: {smr.name}");
            }
        }

        Debug.Log($"Rebind finished. Renderers processed: {changedCount}");
    }

    private Dictionary<string, Transform> BuildBoneMap(Transform root, bool includeInact)
    {
        var dict = new Dictionary<string, Transform>(StringComparer.Ordinal);
        var stack = new Stack<Transform>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (t == null) continue;

            if (includeInact || t.gameObject.activeInHierarchy)
            {
                if (!dict.ContainsKey(t.name))
                {
                    dict.Add(t.name, t);
                }
            }

            for (int i = 0; i < t.childCount; i++)
            {
                stack.Push(t.GetChild(i));
            }
        }

        return dict;
    }
}
