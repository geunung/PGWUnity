using System.Collections.Generic;
using UnityEngine;

public class SkinnedMeshBoneRemap : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public SkinnedMeshRenderer targetSMR;   // Shirt's SMR
    public Transform targetRoot;            // Defeated's mixamorig:Hips (or Armature root)

    [ContextMenu("Remap Now")]
    public void RemapNow()
    {
        if (targetSMR == null)
            targetSMR = GetComponentInChildren<SkinnedMeshRenderer>();

        if (targetSMR == null || targetRoot == null)
        {
            Debug.LogError("Missing targetSMR or targetRoot");
            return;
        }

        var map = new Dictionary<string, Transform>(1024);
        var all = targetRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (!map.ContainsKey(t.name)) map.Add(t.name, t);
        }

        var oldBones = targetSMR.bones;
        var newBones = new Transform[oldBones.Length];

        int missing = 0;
        for (int i = 0; i < oldBones.Length; i++)
        {
            var b = oldBones[i];
            if (b == null)
            {
                newBones[i] = null;
                continue;
            }

            if (map.TryGetValue(b.name, out var newB))
                newBones[i] = newB;
            else
            {
                newBones[i] = null;
                missing++;
            }
        }

        targetSMR.bones = newBones;

        if (targetSMR.rootBone != null && map.TryGetValue(targetSMR.rootBone.name, out var newRoot))
            targetSMR.rootBone = newRoot;
        else
            targetSMR.rootBone = targetRoot;

        targetSMR.updateWhenOffscreen = true;

        Debug.Log($"Remap done. Missing bones: {missing}/{oldBones.Length}");
    }
}
