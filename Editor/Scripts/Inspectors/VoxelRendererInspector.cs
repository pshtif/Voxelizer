/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using Plugins.Voxelizer.Editor.Scripts;
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
            GUILayout.Space(4);

            EditorGUI.BeginChangeCheck();

            DrawRenderSection();

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

        public void DrawRenderSection()
        {
            var voxelRenderer = (target as VoxelRenderer);

            if (!GUIUtils.DrawMinimizableSectionTitle("RENDER SETTINGS", ref voxelRenderer.renderSectionMinimized))
                return;
            
            voxelRenderer.voxelMeshType =
                (VoxelMeshType)EditorGUILayout.EnumPopup("Voxel Mesh Type", voxelRenderer.voxelMeshType);

            switch (voxelRenderer.voxelMeshType)
            {
                case VoxelMeshType.CUSTOM:
                    voxelRenderer.customVoxelMesh =
                        (Mesh)EditorGUILayout.ObjectField(new GUIContent("Voxel Mesh"), voxelRenderer.customVoxelMesh,
                            typeof(Mesh), false);
                    break;
            }

            voxelRenderer.voxelMaterial =
                (Material)EditorGUILayout.ObjectField(new GUIContent("Voxel Material"), voxelRenderer.voxelMaterial,
                    typeof(Material), false);

            if (voxelRenderer.voxelMaterial == null)
            {
                EditorGUILayout.HelpBox("Material not set on voxel renderer cannot check shader support.", MessageType.Warning);
            }
            
            voxelRenderer.voxelScale = EditorGUILayout.FloatField("Voxel Scale", voxelRenderer.voxelScale);
            voxelRenderer.enableCulling = EditorGUILayout.Toggle("Enable Culling", voxelRenderer.enableCulling);

            if (voxelRenderer.enableCulling)
            {
                voxelRenderer.cullingShader = (ComputeShader)EditorGUILayout.ObjectField(
                    new GUIContent("Culling Shader"),
                    voxelRenderer.cullingShader, typeof(ComputeShader), false);
                
                voxelRenderer.cullingDistance =
                    EditorGUILayout.FloatField("Culling Distance", voxelRenderer.cullingDistance);
            }
            
            if (voxelRenderer.voxelMaterial != null)
            {
                if (!voxelRenderer.voxelMaterial.HasFloat("_VoxelScale"))
                {
                    EditorGUILayout.HelpBox("Material does not contain support for _VoxelScale property.", MessageType.Warning);    
                }

                if (!voxelRenderer.voxelMaterial.HasFloat("_EnableCulling"))
                {
                    EditorGUILayout.HelpBox("Material does not support culling.", MessageType.Warning);
                }
            }
        }
    }
}