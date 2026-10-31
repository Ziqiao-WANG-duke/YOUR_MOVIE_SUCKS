using UnityEditor;
using UnityEngine;

namespace YourMovieSucks.Retro.Editor
{
    [CustomEditor(typeof(RetroRenderingController))]
    public sealed class RetroRenderingControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (RetroRenderingController)target;
            EditorGUILayout.HelpBox("Play Mode only. The original camera, textures and materials are restored when disabled. Preset buttons copy values; subsequent edits remain editable.", MessageType.Info);
            if (GUILayout.Button("Copy values from preset asset")) { Undo.RecordObject(controller, "Apply retro preset"); controller.ApplyPreset(); EditorUtility.SetDirty(controller); }
            foreach (RetroStyle style in System.Enum.GetValues(typeof(RetroStyle)))
                if (GUILayout.Button(style.ToString())) { Undo.RecordObject(controller, "Apply retro style"); controller.ApplyStyle(style); EditorUtility.SetDirty(controller); }
        }
    }
}
