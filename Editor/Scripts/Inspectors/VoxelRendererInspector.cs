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
        
        public VoxelRenderer voxelRenderer => (target as VoxelRenderer);

        public override void OnInspectorGUI()
        {

            GUILayout.Label("<color=#FF8800>VOXEL RENDERER</color>", Skin.GetStyle("editor_title"), GUILayout.Height(24));
            GUILayout.Label("VERSION "+Voxelizer.VERSION, Skin.GetStyle("editor_version"), GUILayout.Height(16));
            GUILayout.Space(4);

            EditorGUI.BeginChangeCheck();

            DrawRenderSection();
            
            GUILayout.Space(2);
            
            DrawEditorSection();
            
            GUILayout.Space(2);
            
            DrawMeshesSection();
        }

        public void DrawRenderSection()
        {
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

        public void DrawEditorSection()
        {
            if (!GUIUtils.DrawMinimizableSectionTitle("EDITING SETTINGS", ref voxelRenderer.editorSectionMinimized))
                return;
            
            voxelRenderer.enablePainting = EditorGUILayout.Toggle("Enable Painting", voxelRenderer.enablePainting);
            voxelRenderer.brushColor = EditorGUILayout.ColorField("Brush Color", voxelRenderer.brushColor);
            voxelRenderer.brushSize = EditorGUILayout.Slider("Brush Size", voxelRenderer.brushSize, 0.01f, 1f);
        }
        
        public void DrawMeshesSection()
        {
            if (!GUIUtils.DrawMinimizableSectionTitle("VOXEL MESHES", ref voxelRenderer.meshesSectionMinimized))
                return;

            foreach (var voxelGroup in voxelRenderer.VoxelGroups)
            {
                foreach (var voxelMesh in voxelGroup.VoxelMeshes)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(voxelMesh.name);
                    GUILayout.FlexibleSpace();
                    GUI.color = Color.yellow;
                    GUILayout.Label(voxelMesh.VoxelCount.ToString());
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }
            }
            
            if (GUIUtils.DrawButton("CLEAR ALL"))
            {
                voxelRenderer.ClearVoxelGroups();
                EditorUtility.SetDirty(voxelRenderer);
            }
        }
    }
}