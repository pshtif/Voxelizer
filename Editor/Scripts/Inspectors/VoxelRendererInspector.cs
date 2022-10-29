/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using UnityEditor;
using UnityEngine;

namespace BinaryEgo.Voxelizer.Editor
{
    [CustomEditor(typeof(VoxelRenderer))]
    public class VoxelRendererInspector : UnityEditor.Editor
    {
        public static GUISkin Skin => (GUISkin)Resources.Load("Skins/VoxelizerEditorSkin");

        public override void OnInspectorGUI()
        {
            var voxelRenderer = (target as VoxelRenderer);
            
            GUILayout.Label("<color=#FF8800>VOXEL RENDERER</color>", Skin.GetStyle("editor_title"), GUILayout.Height(24));
            GUILayout.Label("VERSION "+Voxelizer.VERSION, Skin.GetStyle("editor_version"), GUILayout.Height(16));

            EditorGUI.BeginChangeCheck();

            voxelRenderer.voxelMeshType =
                (VoxelMeshType)EditorGUILayout.EnumPopup("Voxel Mesh Type", voxelRenderer.voxelMeshType);

            if (voxelRenderer.voxelMeshType == VoxelMeshType.CUSTOM)
            {
                voxelRenderer.customVoxelMesh =
                    (Mesh)EditorGUILayout.ObjectField(new GUIContent("Voxel Mesh"), voxelRenderer.customVoxelMesh,
                        typeof(Mesh), false);
            }
            
            voxelRenderer.voxelMaterial =
                (Material)EditorGUILayout.ObjectField(new GUIContent("Voxel Material"), voxelRenderer.voxelMaterial,
                    typeof(Material), false);

            voxelRenderer.enableCulling = EditorGUILayout.Toggle("Enable Culling", voxelRenderer.enableCulling);

            if (voxelRenderer.enableCulling)
            {
                voxelRenderer.cullingDistance =
                    EditorGUILayout.FloatField("Culling Distance", voxelRenderer.cullingDistance);
                voxelRenderer.cullingShader = (ComputeShader)EditorGUILayout.ObjectField(
                    new GUIContent("Culling Shader"),
                    voxelRenderer.cullingShader, typeof(ComputeShader), false);
            }
            
            
            // voxelizer.autoVoxelize = EditorGUILayout.Toggle("Auto Voxelize", voxelizer.autoVoxelize);
            //
            // voxelizer.voxelizationType =
            //     (VoxelizationType)EditorGUILayout.EnumPopup("Voxelization", voxelizer.voxelizationType);
            //
            // voxelizer.voxelDensityType = (VoxelDensityType)EditorGUILayout.EnumPopup("Density Type", voxelizer.voxelDensityType);
            // voxelizer.voxelDensity = EditorGUILayout.IntSlider("Voxel Density", voxelizer.voxelDensity, 1, 100);
            //
            // voxelizer.generateMesh = EditorGUILayout.Toggle("Generate Unity Mesh", voxelizer.generateMesh);
            //
            // if (EditorGUI.EndChangeCheck())
            // {
            //     if (voxelizer.autoVoxelize)
            //     {
            //         voxelizer.Voxelize();
            //         SceneView.lastActiveSceneView?.Repaint();
            //     }
            // }
            //
            // if (GUILayout.Button("Voxelize", GUILayout.Height(32)))
            // {
            //     voxelizer.Voxelize();
            // }
        }
    }
}