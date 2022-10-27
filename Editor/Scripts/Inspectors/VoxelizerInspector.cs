/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using UnityEditor;
using UnityEngine;

namespace BinaryEgo.Voxelizer.Editor
{
    [CustomEditor(typeof(Voxelizer))]
    public class VoxelizerInspector : UnityEditor.Editor
    {
        public static GUISkin Skin => (GUISkin)Resources.Load("Skins/VoxelizerEditorSkin");
        
        public override void OnInspectorGUI()
        {
            var voxelizer = (target as Voxelizer);
            
            GUILayout.Label("VOXELIZER", Skin.GetStyle("editor_title"), GUILayout.Height(32));

            EditorGUI.BeginChangeCheck();

            voxelizer.sourceTransform =
                (Transform)EditorGUILayout.ObjectField("Source", voxelizer.sourceTransform,
                    typeof(Transform), true);

            GUI.enabled = voxelizer.sourceTransform != null;

            if (voxelizer.sourceTransform != null && GUILayout.Button(voxelizer.sourceTransform.gameObject.activeSelf ? "Hide Source" : "Show Source", GUILayout.Height(32)))
            {
                voxelizer.sourceTransform.gameObject.SetActive(!voxelizer.sourceTransform.gameObject.activeSelf);
            }

            voxelizer.autoVoxelize = EditorGUILayout.Toggle("Auto Voxelize", voxelizer.autoVoxelize);

            voxelizer.voxelizationType =
                (VoxelizationType)EditorGUILayout.EnumPopup("Voxelization", voxelizer.voxelizationType);

            voxelizer.voxelSizeType = (VoxelSizeType)EditorGUILayout.EnumPopup("Voxel Size Type", voxelizer.voxelSizeType);

            switch (voxelizer.voxelSizeType)
            {
                case VoxelSizeType.ABSOLUTE:
                    voxelizer.voxelSize = EditorGUILayout.FloatField("Voxel Size", voxelizer.voxelSize);
                    break;
                case VoxelSizeType.RELATIVE:
                    voxelizer.voxelDensityType =
                        (VoxelDensityType)EditorGUILayout.EnumPopup("Density Type", voxelizer.voxelDensityType);
                    voxelizer.voxelDensity = EditorGUILayout.IntSlider("Voxel Density", voxelizer.voxelDensity, 1, 100);
                    break;
            }
            
            voxelizer.voxelBakeTransform = (VoxelBakeTransform)EditorGUILayout.EnumPopup("Voxel Bake Transform", voxelizer.voxelBakeTransform);

            voxelizer.generateMesh = EditorGUILayout.Toggle("Generate Unity Mesh", voxelizer.generateMesh);

            voxelizer.enableVoxelCache = EditorGUILayout.Toggle("Enable Voxel Cache", voxelizer.enableVoxelCache);
            
            if (EditorGUI.EndChangeCheck())
            {
                if (voxelizer.autoVoxelize)
                {
                    voxelizer.Voxelize();
                    SceneView.lastActiveSceneView?.Repaint();
                }
            }

            if (GUILayout.Button("Voxelize", GUILayout.Height(32)))
            {
                voxelizer.Voxelize();
            }
        }
    }
}