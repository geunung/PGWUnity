using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RebindSkinnedMeshByName : EditorWindow
{
    private SkinnedMeshRenderer smr;
    private Transform targetSkeletonRoot;

    [MenuItem("Tools/Rebind SkinnedMesh (By Name)")]
    public static void Open()
    {
        GetWindow<RebindSkinnedMeshByName>("Rebind SkinnedMesh");
    }

    private void OnGUI()
    {
        smr = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Skinned Mesh Renderer", smr, typeof(SkinnedMeshRenderer), true);
        targetSkeletonRoot = (Transform)EditorGUILayout.ObjectField("Target Skeleton Root", targetSkeletonRoot, typeof(Transform), true);

        using (new EditorGUI.DisabledScope(smr == null || targetSkeletonRoot == null))
        {
            if (GUILayout.Button("Rebind"))
            {
                DoRebind();
            }
        }
    }

    private void DoRebind()
    {
        var map = new Dictionary<string, Transform>(1024);

        foreach (var t in targetSkeletonRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!map.ContainsKey(t.name))
                map.Add(t.name, t);
        }

        var oldBones = smr.bones;
        var newBones = new Transform[oldBones.Length];

        for (int i = 0; i < oldBones.Length; i++)
        {
            var b = oldBones[i];
            if (b == null)
            {
                newBones[i] = null;
                continue;
            }

            if (map.TryGetValue(b.name, out var mapped))
            {
                newBones[i] = mapped;
            }
            else
            {
                newBones[i] = b;
            }
        }

        Undo.RecordObject(smr, "Rebind SkinnedMesh");
        smr.bones = newBones;

        if (smr.rootBone != null && map.TryGetValue(smr.rootBone.name, out var mappedRoot))
        {
            smr.rootBone = mappedRoot;
        }
        else
        {
            var hips = FindByName(map, "mixamorig:Hips");
            if (hips != null) smr.rootBone = hips;
        }

        EditorUtility.SetDirty(smr);
        Debug.Log("Rebind done.");
    }

    private Transform FindByName(Dictionary<string, Transform> map, string name)
    {
        map.TryGetValue(name, out var t);
        return t;
    }
}
