using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RebindSkinnedMeshToAvatar))]
public class RebindSkinnedMeshToAvatarEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var t = (RebindSkinnedMeshToAvatar)target;

        GUILayout.Space(8);

        if (GUILayout.Button("Rebind Now"))
        {
            t.Rebind();
            EditorUtility.SetDirty(t);
        }
    }
}
