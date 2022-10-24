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

            voxelizer.sourceRenderer =
                (MeshRenderer)EditorGUILayout.ObjectField("Source Renderer", voxelizer.sourceRenderer,
                    typeof(MeshRenderer), true);

            voxelizer.autoVoxelize = EditorGUILayout.Toggle("Auto Voxelize", voxelizer.autoVoxelize);

            voxelizer.voxelizationType =
                (VoxelizationType)EditorGUILayout.EnumPopup("Voxelization", voxelizer.voxelizationType);

            voxelizer.voxelDensityType = (VoxelDensityType)EditorGUILayout.EnumPopup("Density Type", voxelizer.voxelDensityType);
            voxelizer.voxelDensity = EditorGUILayout.IntSlider("Voxel Density", voxelizer.voxelDensity, 1, 100);
            
            voxelizer.generateMesh = EditorGUILayout.Toggle("Generate Unity Mesh", voxelizer.generateMesh);

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