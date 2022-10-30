/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using g3;
using UnityEditor;
using UnityEngine;

namespace BinaryEgo.Voxelizer
{
    public class Voxelizer : MonoBehaviour
    {
        public static string VERSION = "0.2.0";
        
        #if UNITY_EDITOR
        public bool sourceSectionMinimized = false;
        public bool voxelSectionMinimized = false;
        public bool additionalSectionMinimized = false;
        #endif
        
        public Transform sourceTransform;
        public int sourceLayerMask;
        
        public Vector3 voxelMeshOffset = Vector3.zero;
        public VoxelDensityType voxelDensityType = VoxelDensityType.MAXDIM;

        public VoxelSizeType voxelSizeType = VoxelSizeType.RELATIVE;
        public float voxelSize = 1;
        
        [Range(1,100)]
        public int voxelDensity = 20;
        
        public VoxelizationType voxelizationType = VoxelizationType.SDF;
        public VoxelBakeTransform voxelBakeTransform = VoxelBakeTransform.NONE;
        public bool enableColorSampling = true;
        public bool interpolateColorSampling = true;
        public bool enableVoxelCache = true;
        public bool generateMesh = true;
        public bool autoVoxelize = false;

        private MeshRenderer _outputRenderer;

        private static Dictionary<string, Bitmap3> _voxelBitmapCache;
        private static Dictionary<string, Vector4[]> _voxelColorCache;
        
        public void Voxelize()
        {
            _voxelBitmapCache?.Clear();
            _voxelColorCache?.Clear();
            VoxelRenderer.Instance.RemoveAllGroups();
            // Cache for baked transforms not implemented yet
            enableVoxelCache = enableVoxelCache && voxelBakeTransform == VoxelBakeTransform.NONE;
            
            if (sourceTransform == null)
                return;

            OnProgress("Voxelizer", "Voxelization initialized", 0);

            MeshRenderer[] meshRenderers = sourceTransform.GetComponentsInChildren<MeshRenderer>();
            
            for (int i = 0; i<meshRenderers.Length; i++)
            {
                MeshRenderer meshRenderer = meshRenderers[i];
                
                if (sourceLayerMask != (sourceLayerMask | (1 << meshRenderer.gameObject.layer)))
                    continue;
                
                OnProgress("Voxelizer", "Voxelizing " + meshRenderer.name, (i+1)/(meshRenderers.Length+1));
                VoxelizeMesh(meshRenderer);
            }
            
            OnComplete();
        }

        public void VoxelizeMesh(MeshRenderer p_meshRenderer)
        {
            MeshFilter filter = p_meshRenderer.GetComponent<MeshFilter>();
            
            if (filter == null)
                return;

            Mesh mesh;
            if (voxelBakeTransform == VoxelBakeTransform.NONE)
            {
                mesh = filter.sharedMesh;
            }
            else
            {
                mesh = Instantiate(filter.sharedMesh);
                var matrix = p_meshRenderer.transform.localToWorldMatrix;
                switch (voxelBakeTransform)
                {
                    case VoxelBakeTransform.SCALE_ROTATION:
                        matrix.SetColumn(3, new Vector4(0, 0, 0, 1));
                        break;
                    case VoxelBakeTransform.SCALE:
                        matrix = Matrix4x4.Scale(matrix.lossyScale);
                        break;
                }

                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                }
                mesh.vertices = vertices;
            }
            
            Material[] materials = p_meshRenderer.sharedMaterials.ToArray();

            DMesh3 dmesh = Geome3Utils.UnityMeshToDMesh(mesh, false);
            dmesh.name = mesh.name;
            
            switch (voxelSizeType)
            {
                case VoxelSizeType.RELATIVE:
                    switch (voxelDensityType)
                    {
                        case VoxelDensityType.MAXDIM:
                            voxelSize = (float)dmesh.CachedBounds.MaxDim / voxelDensity;
                            break;
                        case VoxelDensityType.WIDTH:
                            voxelSize = (float)dmesh.CachedBounds.Width / voxelDensity;
                            break;
                        case VoxelDensityType.HEIGHT:
                            voxelSize = (float)dmesh.CachedBounds.Height / voxelDensity;
                            break;
                        case VoxelDensityType.DEPTH:
                            voxelSize = (float)dmesh.CachedBounds.Depth / voxelDensity;
                            break;
                        case VoxelDensityType.DIAGONAL:
                        default:
                            voxelSize = (float)dmesh.CachedBounds.DiagonalLength / voxelDensity;
                            break;
                    }
                    break;
            }

            Vector3i voxelDims = new Vector3i(Mathf.FloorToInt((float)dmesh.CachedBounds.Width / voxelSize),
                Mathf.FloorToInt((float)dmesh.CachedBounds.Height / voxelSize),
                Mathf.FloorToInt((float)dmesh.CachedBounds.Depth / voxelSize));
            
            if (voxelSize == 0)
                return;
            
            DMeshAABBTree3 spatial = new DMeshAABBTree3(dmesh, autoBuild: true);
            ShiftGridIndexer3 indexer = new ShiftGridIndexer3(dmesh.CachedBounds.Min, voxelSize);
            
            Bitmap3 bitmap = new Bitmap3(Vector3i.Zero);
            Vector3d voxelOffset = Vector3d.Zero;
            Vector3 finalOffset = Vector3.zero;
            if (!enableVoxelCache || dmesh.name.IsNullOrWhitespace() || _voxelBitmapCache == null ||
                !_voxelBitmapCache.ContainsKey(dmesh.name))
            {
                switch (voxelizationType)
                {
                    case VoxelizationType.SDF:
                        voxelOffset = -Vector3d.One * voxelSize * 2;
                        bitmap = VoxelUtils.VoxelizeMeshUsingSDF(dmesh, spatial, indexer, voxelSize, enableColorSampling, interpolateColorSampling);
                        finalOffset = (Vector3)dmesh.CachedBounds.Min - voxelSize * 2f * Vector3.one;
                        break;
                    case VoxelizationType.GRID:
                        voxelOffset = Vector3d.One * voxelSize/2;
                        bitmap = VoxelUtils.VoxelizeMeshUsingGrid(dmesh, spatial, indexer, voxelSize, enableColorSampling,
                            interpolateColorSampling,  voxelDims);
                        finalOffset = (Vector3)dmesh.CachedBounds.Min + voxelSize/2 * Vector3.one;
                        break;
                    default:
                        voxelOffset = Vector3d.One * voxelSize/2;
                        bitmap = VoxelUtils.VoxelizeMeshUsingWinding(dmesh, spatial, indexer, voxelSize, enableColorSampling,
                            interpolateColorSampling, voxelDims);
                        finalOffset = (Vector3)dmesh.CachedBounds.Min + voxelSize/2 * Vector3.one;
                        break;
                }
                
                if (enableVoxelCache && !dmesh.name.IsNullOrWhitespace())
                {
                    if (_voxelBitmapCache == null)
                    {
                        _voxelBitmapCache = new Dictionary<string, Bitmap3>();
                    }

                    _voxelBitmapCache[dmesh.name] = bitmap;
                }
            }
            else
            {
                bitmap = _voxelBitmapCache[dmesh.name];
            }

            Vector4[] color;
            if (enableColorSampling)
            {
                if (!enableVoxelCache || dmesh.name.IsNullOrWhitespace() || _voxelColorCache == null ||
                    !_voxelColorCache.ContainsKey(dmesh.name))
                {
                    color = GenerateColorBuffer(bitmap, voxelOffset, dmesh, indexer, spatial, materials,
                        interpolateColorSampling);

                    if (enableVoxelCache && !dmesh.name.IsNullOrWhitespace())
                    {
                        if (_voxelColorCache == null)
                        {
                            _voxelColorCache = new Dictionary<string, Vector4[]>();
                        }

                        _voxelColorCache[dmesh.name] = color;
                    }
                }
                else
                {
                    color = _voxelColorCache[dmesh.name];
                }
            }
            else
            {
                color = Enumerable.Repeat(Vector4.one, bitmap.NonZeros().Count()).ToArray();
            }

            var voxelMesh = new VoxelMesh(bitmap, color, p_meshRenderer.transform, dmesh.CachedBounds, finalOffset, false, voxelSize, voxelBakeTransform);
            VoxelRenderer.Instance.Add(voxelMesh);

            if (generateMesh)
            {
                DMesh3 outputMesh = GenerateVoxelMesh(bitmap, voxelSize, voxelOffset, enableColorSampling, dmesh, indexer,
                    spatial, interpolateColorSampling, materials);
                MeshTransforms.Translate(outputMesh, new Vector3(
                    (float)dmesh.CachedBounds.Min.x,
                    (float)dmesh.CachedBounds.Min.y,
                    (float)dmesh.CachedBounds.Min.z));


                var go = new GameObject("VoxelizedMesh");
                go.transform.parent = p_meshRenderer.transform;
                var outputFilter = go.AddComponent<MeshFilter>();
                outputFilter.sharedMesh = Geome3Utils.DMeshToUnityMesh(outputMesh);
            }
        }

        public DMesh3 GenerateVoxelMesh(Bitmap3 p_bitmap, double p_voxelSize, Vector3d p_voxelOffset,
            bool p_sampleColor, DMesh3 p_mesh, ShiftGridIndexer3 p_indexer, DMeshAABBTree3 p_spatial,
            bool p_interpolateColorSampling, Material[] p_materials)
        {
            VoxelSurfaceGenerator voxGen = new VoxelSurfaceGenerator();
            voxGen.voxelSize = (float)p_voxelSize;
            voxGen.voxelOffset = p_voxelOffset;
            voxGen.Voxels = p_bitmap;
            if (p_sampleColor && VoxelUtils.CheckTextureReadability(p_materials))
            {
                voxGen.ColorSourceF = (idx) =>
                {
                    Vector3d point = p_indexer.FromGrid(idx) + p_voxelOffset;
                    int t = p_spatial.FindNearestTriangle(point);
                    return VoxelUtils.GetColorAtPoint(p_mesh, t, point, p_materials,
                        p_interpolateColorSampling);
                };
                
                // Vector4[] colorBuffer = new Vector4[p_bitmap.NonZeros().Count()];
                // int i = 0;
                // foreach (var voxel in p_bitmap.NonZeros())
                // {
                //     colorBuffer[i++] = voxGen.ColorSourceF(voxel);
                // }
            }

            voxGen.Generate();
            return voxGen.Meshes[0];
        }

        public static Vector4[] GenerateColorBuffer(Bitmap3 p_bitmap, Vector3d p_voxelOffset,
            DMesh3 p_mesh, ShiftGridIndexer3 p_indexer, DMeshAABBTree3 p_spatial, Material[] p_materials,
            bool p_interpolateUV)
        {
            var voxels = p_bitmap.NonZeros();
            Vector4[] colorBuffer = new Vector4[voxels.Count()];
            int i = 0;
            foreach (var voxel in voxels)
            {
                Vector3d point = p_indexer.FromGrid(voxel) + p_voxelOffset;
                int t = p_spatial.FindNearestTriangle(point);
                colorBuffer[i++] = VoxelUtils.GetColorAtPoint(p_mesh, t, point, p_materials, p_interpolateUV);
            }

            return colorBuffer;
        }

        void OnProgress(string p_title, string p_info, float p_progress)
        {
            #if UNITY_EDITOR
            EditorUtility.DisplayProgressBar(p_title, p_info, p_progress);
            Thread.Sleep(200);
            #endif
        }

        void OnComplete()
        {
            #if UNITY_EDITOR
            EditorUtility.ClearProgressBar();
            #endif
        }
    }
}