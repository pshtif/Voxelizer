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
        public MeshRenderer sourceRenderer;

        public MeshFilter outputFilter;

        public Vector3 voxelMeshOffset = Vector3.zero;
        public VoxelDensityType voxelDensityType = VoxelDensityType.MAXDIM;
        [Range(1,100)]
        public int voxelDensity = 20;
        public VoxelizationType voxelizationType = VoxelizationType.SDF;
        public bool sampleColor = true;
        public bool interpolateColorSampling = true;
        public bool enableVoxelCache = true;
        public bool generateMesh = true;
        public bool autoVoxelize = false;

        private MeshRenderer _outputRenderer;

        private static Dictionary<string, Bitmap3> _voxelBitmapCache;
        private static Dictionary<string, Vector4[]> _voxelColorCache;
        
        public void Voxelize()
        {
            OnProgress("Voxelizer", "Voxelization initialized", 0);
            
            Mesh mesh = sourceRenderer.GetComponent<MeshFilter>().sharedMesh;
            Material[] materials = sourceRenderer.sharedMaterials.ToArray();

            DMesh3 dmesh = DMeshUtils.UnityMeshToDMesh(mesh, false);
            
            int cellCount = voxelDensity;
            float voxelSize;
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
             
            if (voxelSize > 0)
            {
                DMeshAABBTree3 spatial = new DMeshAABBTree3(dmesh, autoBuild: true);
                ShiftGridIndexer3 indexer = new ShiftGridIndexer3(dmesh.CachedBounds.Min, voxelSize);
                
                Bitmap3 bitmap = new Bitmap3(Vector3i.Zero);
                Vector3d voxelOffset = Vector3d.Zero;
                if (!enableVoxelCache || dmesh.name.IsNullOrWhitespace() || _voxelBitmapCache == null ||
                    !_voxelBitmapCache.ContainsKey(dmesh.name))
                {
                    switch (voxelizationType)
                    {
                        case VoxelizationType.SDF:
                            voxelOffset = -Vector3d.One * voxelSize * 2;
                            OnProgress("Voxelizer", "Running SDF voxelization.", .2f);
                            bitmap = VoxelizeMeshUsingSDF(dmesh, spatial, indexer, voxelSize, sampleColor, interpolateColorSampling);
                            break;
                        case VoxelizationType.GRID:
                            voxelOffset = Vector3d.One * voxelSize/2;
                            OnProgress("Voxelizer", "Running Grid voxelization.", .2f);
                            bitmap = VoxelizeMeshUsingGrid(dmesh, voxelSize, spatial, indexer, cellCount, sampleColor,
                                interpolateColorSampling);
                            break;
                        default:
                            voxelOffset = Vector3d.One * voxelSize/2;
                            OnProgress("Voxelizer", "Running Winding voxelization.", .2f);
                            bitmap = VoxelizeMeshUsingWinding(dmesh, voxelSize, spatial, indexer, cellCount, sampleColor,
                                interpolateColorSampling);
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
                if (!enableVoxelCache || dmesh.name.IsNullOrWhitespace() || _voxelColorCache == null ||
                    !_voxelColorCache.ContainsKey(dmesh.name))
                {
                    OnProgress("Voxelizer", "Generating color buffer.", .6f);
                    color = GenerateColorBuffer(bitmap, voxelOffset, dmesh, indexer, spatial, materials, interpolateColorSampling);
                }
                else
                {
                    color = _voxelColorCache[dmesh.name];
                }
                
                OnProgress("Voxelizer", "Generating voxel mesh.", .7f);
                var voxelMesh = new VoxelMesh(bitmap, color, sourceRenderer.transform, dmesh.CachedBounds, false, voxelSize, Vector3.zero);
                VoxelRenderer.Instance.RemoveAllGroups();
                VoxelRenderer.Instance.Add(voxelMesh);

                if (generateMesh)
                {
                    OnProgress("Voxelizer", "Generating triangulized mesh.", .8f);
                    DMesh3 outputMesh = GenerateVoxelMesh(bitmap, voxelSize, voxelOffset, sampleColor, dmesh, indexer,
                        spatial, interpolateColorSampling, materials);
                    MeshTransforms.Translate(outputMesh, new Vector3(
                        (float)dmesh.CachedBounds.Min.x,
                        (float)dmesh.CachedBounds.Min.y,
                        (float)dmesh.CachedBounds.Min.z));

                    if (outputFilter == null)
                    {
                        var go = new GameObject("VoxelizedMesh");
                        outputFilter = go.AddComponent<MeshFilter>();
                    }
                    
                    outputFilter.sharedMesh = DMeshUtils.DMeshToUnityMesh(outputMesh);
                }
            }

            OnComplete();
        }

        public DMesh3 GenerateVoxelMesh(Bitmap3 p_bitmap, double p_voxelSize, Vector3d p_voxelOffset,
            bool p_sampleColor, DMesh3 p_mesh, ShiftGridIndexer3 p_indexer, DMeshAABBTree3 p_spatial,
            bool p_interpolateColorSampling, Material[] p_materials)
        {
            VoxelSurfaceGenerator voxGen = new VoxelSurfaceGenerator();
            voxGen.voxelSize = (float)p_voxelSize;
            voxGen.voxelOffset = p_voxelOffset;
            voxGen.Voxels = p_bitmap;
            if (p_sampleColor && CheckTextureReadability(p_materials))
            {
                voxGen.ColorSourceF = (idx) =>
                {
                    Vector3d point = p_indexer.FromGrid(idx) + p_voxelOffset;
                    int t = p_spatial.FindNearestTriangle(point);
                    return GetColorAtPoint(p_mesh, t, point, p_materials,
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

            Vector4[] colorBuffer = new Vector4[p_bitmap.NonZeros().Count()];
            int i = 0;
            foreach (var voxel in p_bitmap.NonZeros())
            {
                Vector3d point = p_indexer.FromGrid(voxel) + p_voxelOffset;
                int t = p_spatial.FindNearestTriangle(point);
                colorBuffer[i++] = GetColorAtPoint(p_mesh, t, point, p_materials, p_interpolateUV);
            }

            return colorBuffer;
        }

        Bitmap3 VoxelizeMeshUsingSDF(DMesh3 p_mesh, DMeshAABBTree3 p_spatial, ShiftGridIndexer3 p_indexer, double p_voxelSize, bool p_sampleColor, bool p_interpolateUV)
        {
            MeshSignedDistanceGrid sdf = new MeshSignedDistanceGrid(p_mesh, p_voxelSize, p_spatial);
            sdf.Compute();

            Bitmap3 bitmap = new Bitmap3(sdf.Dimensions);
            
            foreach(Vector3i idx in bitmap.Indices()) {
                float f = sdf[idx.x, idx.y, idx.z];
                bitmap.Set(idx, f < 0);
            }

            return bitmap;
        }

        Bitmap3 VoxelizeMeshUsingGrid(DMesh3 p_mesh, double p_voxelSize, DMeshAABBTree3 p_spatial, ShiftGridIndexer3 p_indexer, int p_cellCount, bool p_sampleColor, bool p_interpolateUV)
        {
            Bitmap3 bitmap = new Bitmap3(new Vector3i(p_cellCount, p_cellCount, p_cellCount));
            Vector3d voxelOffset = Vector3d.One * p_voxelSize/2;
            
            //foreach (Vector3i idx in bitmap.Indices())
            gParallel.ForEach(bitmap.Indices(), (idx) =>
            {
                Vector3d v = p_indexer.FromGrid(idx) + voxelOffset;
                bitmap.SafeSet(idx, p_spatial.IsInside(v));
            });

            return bitmap;
        }

        Bitmap3 VoxelizeMeshUsingWinding(DMesh3 p_mesh, double p_voxelSize, DMeshAABBTree3 p_spatial, ShiftGridIndexer3 p_indexer, int p_cellCount, bool p_sampleColor, bool p_interpolateUV)
        {
            p_spatial.WindingNumber(Vector3d.Zero); 
            
            Bitmap3 bitmap = new Bitmap3(new Vector3i(p_cellCount, p_cellCount, p_cellCount));
            Vector3d voxelOffset = Vector3d.One * p_voxelSize/2;
            gParallel.ForEach(bitmap.Indices(), (idx) =>
            {
                Vector3d v = p_indexer.FromGrid(idx) + voxelOffset;
                bitmap.SafeSet(idx, p_spatial.WindingNumber(v) > 0.5);
            });

            return bitmap;
        }
        
        public static Vector2 GetInterpolatedUVInTriangle(Vector3d p_p1, Vector3d p_p2, Vector3d p_p3, Vector3d p_point, Vector2 p_uv1,
            Vector2 p_uv2, Vector2 p_uv3)
        {
            var d1 = p_p1 - p_point;
            var d2 = p_p2 - p_point;
            var d3 = p_p3 - p_point;

            double a = Vector3d.Cross(p_p1 - p_p2, p_p1 - p_p3).Length;
            float a1 = (float) (Vector3d.Cross(d2, d3).Length / a);
            float a2 = (float) (Vector3d.Cross(d3, d1).Length / a);
            float a3 = (float) (Vector3d.Cross(d1, d2).Length / a);
            
            return p_uv1 * a1 + p_uv2 * a2 + p_uv3 * a3;
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
        
        public static Color GetInterpolatedColorInTriangle(Vector3d p_p1, Vector3d p_p2, Vector3d p_p3, Vector3d p_point, Vector3f p_color1,
            Vector3f p_color2, Vector3f p_color3)
        {
            var d1 = p_p1 - p_point;
            var d2 = p_p2 - p_point;
            var d3 = p_p3 - p_point;

            double a = Vector3d.Cross(p_p1 - p_p2, p_p1 - p_p3).Length;
            float a1 = (float) (Vector3d.Cross(d2, d3).Length / a);
            float a2 = (float) (Vector3d.Cross(d3, d1).Length / a);
            float a3 = (float) (Vector3d.Cross(d1, d2).Length / a);
            
            return p_color1 * a1 + p_color2 * a2 + p_color3 * a3;
        }

        public bool CheckTextureReadability(Material[] p_materials)
        {
            var valid = true;
            foreach (var material in p_materials)
            {
                if (material.mainTexture != null)
                {
                    if (!material.mainTexture.isReadable)
                    {
                        Debug.LogWarning("Texture " + material.mainTexture.name +
                                         " is not readable cannot voxelize with color sampling.");
                        valid = false;
                    }
                }
            }

            return valid;
        }

        public static Color GetColorAtPoint(DMesh3 p_mesh, int p_triangleIndex, Vector3d p_point,
            Material[] p_materials, bool p_interpolate)

        {
            if (p_triangleIndex == DMesh3.InvalidID)
                return Color.black;

            DistPoint3Triangle3 dist = MeshQueries.TriangleDistance(p_mesh, p_triangleIndex, p_point);
            Vector3d nearestPoint = dist.TriangleClosest;
            Index3i ti = p_mesh.GetTriangle(p_triangleIndex);

            Color texColor = Color.white;
            
            if (p_materials != null)
            {
                int materialGroup = p_mesh.GetMaterialGroup(p_triangleIndex);

                if (materialGroup < p_materials.Length)
                {
                    var texture = (Texture2D)p_materials[materialGroup].mainTexture;

                    if (texture != null)
                    {
                        if (texture.isReadable)
                        {
                            Vector2d uv;
                            if (p_interpolate)
                            {
                                uv = GetInterpolatedUVInTriangle(
                                    p_mesh.GetVertex(ti[0]),
                                    p_mesh.GetVertex(ti[1]),
                                    p_mesh.GetVertex(ti[2]),
                                    nearestPoint,
                                    p_mesh.GetVertexUV(ti[0]),
                                    p_mesh.GetVertexUV(ti[1]),
                                    p_mesh.GetVertexUV(ti[2]));
                            }
                            else
                            {
                                uv = p_mesh.GetVertexUV(ti[0]);
                            }
                            
                            texColor = texture.GetPixelBilinear((float)uv.x, (float)uv.y);
                        }
                        else
                        {
                            texColor = Color.white;
                        }
                    }
                }
            }

            Color vertexColor = Color.white;
            if (p_mesh.HasVertexColors)
            {
                if (p_interpolate)
                {
                    vertexColor = GetInterpolatedColorInTriangle(
                        p_mesh.GetVertex(ti[0]),
                        p_mesh.GetVertex(ti[1]),
                        p_mesh.GetVertex(ti[2]),
                        nearestPoint,
                        p_mesh.GetVertexColor(ti[0]),
                        p_mesh.GetVertexColor(ti[1]),
                        p_mesh.GetVertexColor(ti[2]));
                }
                else
                {
                    vertexColor = p_mesh.GetVertexColor(ti[0]);
                }
            }
            
            return texColor * vertexColor;
        }
    }
}