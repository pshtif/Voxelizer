/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using System;
using System.Collections.Generic;
using g3;
using UnityEngine;
using UnityEngine.Rendering;

namespace BinaryEgo.Voxelizer
{
    public class DMeshUtils
    {
        public static Mesh DMeshToUnityMesh(DMesh3 p_mesh, MeshTopology p_topology = MeshTopology.Triangles, bool p_allowLargeMeshes = true, bool p_recalcNormalsIfMissing = true)
        {
            if (p_allowLargeMeshes == false) {
                if (p_mesh.MaxVertexID > 65000 || p_mesh.MaxTriangleID > 65000) {
                    Debug.Log("Attempted to import object larger than 65000 verts/tris, not supported by Unity!");
                    return null;
                }
            }

            Mesh unityMesh = new Mesh();

            Vector3[] vertices = DVectorToVector3(p_mesh.VerticesBuffer);
            Vector3[] normals = (p_mesh.HasVertexNormals) ? DVectorToVector3(p_mesh.NormalsBuffer) : null;

            unityMesh.vertices = vertices;
            if (p_mesh.HasVertexNormals)
                unityMesh.normals = normals;
            if (p_mesh.HasVertexColors)
                unityMesh.colors = DVectorToColor(p_mesh.ColorsBuffer);
            if (p_mesh.HasVertexUVs)
                unityMesh.uv = DVectorToVector2(p_mesh.UVBuffer);

            if (p_allowLargeMeshes && (p_mesh.MaxVertexID > 65000 || p_mesh.TriangleCount > 65000) )
                unityMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            if (p_topology == MeshTopology.Triangles)
            {
                if (p_mesh.IsCompactT)
                {
                    int[] triangles = DVectorToInt(p_mesh.TrianglesBuffer);

                    Dictionary<int,List<int>> materialGroups = new Dictionary<int,List<int>>();

                    for (int i = 0; i < triangles.Length; i++)
                    {
                        int group = p_mesh.GetMaterialGroup(i/3);
                        if (!materialGroups.ContainsKey(group))
                            materialGroups[group] = new List<int>();
                        
                        materialGroups[group].Add(triangles[i]);
                    }

                    if (materialGroups.Count == 1)
                    {
                        unityMesh.triangles = triangles;
                    }
                    else
                    {
                        unityMesh.subMeshCount = materialGroups.Count;
                        foreach (var group in materialGroups)
                        {
                            unityMesh.SetTriangles(group.Value, group.Key, true);
                        }
                    }
                }
                else
                {
                    int[] triangles = new int[p_mesh.TriangleCount * 3];
                    int ti = 0;
                    for (int k = 0; k < p_mesh.MaxTriangleID; ++k)
                    {
                        if (p_mesh.IsTriangle(k))
                        {
                            Index3i t = p_mesh.GetTriangle(k);
                            int j = 3 * ti;
                            triangles[j] = t.a;
                            triangles[j + 1] = t.b;
                            triangles[j + 2] = t.c;
                            ti++;
                        }
                    }

                    unityMesh.triangles = triangles;
                }
            } else if (p_topology == MeshTopology.Lines)
            {
                List<int> lines = new List<int>();
                foreach (Index4i edge in p_mesh.Edges())
                {
                    lines.Add(edge.a);
                    lines.Add(edge.b);
                }
                
                unityMesh.SetIndices(lines.ToArray(), MeshTopology.Lines, 0);
            }
            else
            {
                Debug.LogError("Topology type "+p_topology+" not implemented.");
            }

            if (p_mesh.HasVertexNormals == false && p_recalcNormalsIfMissing && unityMesh.GetTopology(0) == MeshTopology.Triangles) {
                unityMesh.RecalculateNormals();
            }
            
            // List<SubMeshDescriptor> descriptors = GetSubMeshDescriptors(p_mesh);
            // if (descriptors.Count > 1)
            // {
            //     unityMesh.subMeshCount = descriptors.Count;
            //     for (int i = 0; i < descriptors.Count; i++)
            //     {
            //         Debug.Log(descriptors[i].indexStart+" : "+descriptors[i].indexCount+" : "+descriptors[i].baseVertex+" : "+descriptors[i].topology);
            //         unityMesh.SetSubMesh(i, descriptors[i]);
            //     }
            // }

            return unityMesh;
        }
        
        public static DMesh3 UnityMeshToDMesh(Mesh p_mesh, bool p_splitSubMeshes)
        {
            Vector3[] vertices = p_mesh.vertices;
            Vector3[] normals = p_mesh.normals;
            Color32[] colors32 = p_mesh.colors32;
            Color[] colors = p_mesh.colors;
            Vector2[] uv = p_mesh.uv;
            
            bool bNormals = (normals.Length == p_mesh.vertexCount);
            bool bColors = (colors.Length == p_mesh.vertexCount || colors32.Length == p_mesh.vertexCount);
            bool bByteColors = (colors32.Length == p_mesh.vertexCount);
            bool bUVs = (uv.Length == p_mesh.vertexCount);

            DMesh3 dmesh = new DMesh3(bNormals, bColors, bUVs, true);

            for ( int i = 0; i < p_mesh.vertexCount; ++i ) {
                Vector3d v = vertices[i];
                NewVertexInfo vInfo = new NewVertexInfo(v);
                if ( bNormals ) {
                    vInfo.bHaveN = true;
                    vInfo.n = normals[i];
                }
                if (bColors) {
                    vInfo.bHaveC = true;
                    if (bByteColors)
                        vInfo.c = new Colorf(colors32[i].r, colors32[i].g, colors32[i].b, 255);
                    else
                        vInfo.c = colors[i];
                }
                if ( bUVs ) {
                    vInfo.bHaveUV = true;
                    vInfo.uv = uv[i];
                }

                int vid = dmesh.AppendVertex(vInfo);
                
                if (vid != i)
                    throw new InvalidOperationException("UnityUtil.UnityMeshToDMesh: indices weirdness...");
            }

            int[] triangles = p_mesh.triangles;

            for (int i = 0; i < triangles.Length / 3; i++)
            {
                int tid = dmesh.AppendTriangle(triangles[3 * i], triangles[3 * i + 1], triangles[3 * i + 2]);
                dmesh.SetMaterialGroup(tid, GetTriangleSubMesh(p_mesh, i*3));
            }

            return dmesh;
        }

        static public int GetTriangleSubMesh(Mesh p_mesh, int p_index)
        {
            for (int i = 0; i < p_mesh.subMeshCount; i++)
            {
                SubMeshDescriptor smd = p_mesh.GetSubMesh(i);
                if (p_index >= smd.indexStart && p_index < smd.indexStart + smd.indexCount)
                    return i;
            }

            return 0;
        }

        public static Material StandardMaterial(Colorf color)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            return mat;
        }

        public static Material SafeLoadMaterial(string sPath)
        {
            Material mat = null;
            try {
                Material loaded = Resources.Load<Material>(sPath);
                mat = new Material(loaded);
            } catch (Exception e) {
                Debug.Log("g3UnityUtil.SafeLoadMaterial: exception: " + e.Message);
                mat = new Material(Shader.Find("Standard"));
                mat.color = Color.red;
            }
            return mat;
        }

        // generateMeshF() meshes the input implicit function at
        // the given cell resolution, and writes out the resulting mesh    
        public static DMesh3 GenerateMeshF(BoundedImplicitFunction3d p_root, int p_numcells)
        {
            MarchingCubes c = new MarchingCubes();
            c.Implicit = p_root;
            c.RootMode = MarchingCubes.RootfindingModes.LerpSteps;      // cube-edge convergence method
            c.RootModeSteps = 5;                                        // number of iterations
            c.Bounds = p_root.Bounds();
            c.CubeSize = c.Bounds.MaxDim / p_numcells;
            c.Bounds.Expand(3 * c.CubeSize);                            // leave a buffer of cells
            c.Generate();
            MeshNormals.QuickCompute(c.Mesh);                           // generate normals

            return c.Mesh;
        }
                   
        public static DenseGridTrilinearImplicit MeshToImplicitF(DMesh3 p_mesh, int p_cellCount, double p_maxOffset = 0)
        {
            double meshCellsize = p_mesh.CachedBounds.MaxDim / p_cellCount;
            MeshSignedDistanceGrid levelSet = new MeshSignedDistanceGrid(p_mesh, meshCellsize);
            levelSet.ExactBandWidth = (int)(p_maxOffset / meshCellsize) + 1;
            levelSet.Compute();
            return new DenseGridTrilinearImplicit(levelSet.Grid, levelSet.GridOrigin, levelSet.CellSize);
        }

        public static DenseGridTrilinearImplicit MeshToBlendImplicitF(DMesh3 p_mesh, int p_cellCount)
        {
            double meshCellsize = p_mesh.CachedBounds.MaxDim / p_cellCount;
            MeshSignedDistanceGrid levelSet = new MeshSignedDistanceGrid(p_mesh, meshCellsize);
            levelSet.ExpandBounds = p_mesh.CachedBounds.Diagonal * 0.25;        // need some values outside mesh
            levelSet.ComputeMode = MeshSignedDistanceGrid.ComputeModes.FullGrid;
            levelSet.Compute();
            return new DenseGridTrilinearImplicit(levelSet.Grid, levelSet.GridOrigin, levelSet.CellSize);
        }

        public static Vector3[] DVectorToVector3(DVector<double> vec)
        {
            int nLen = vec.Length / 3;
            Vector3[] result = new Vector3[nLen];
            for (int i = 0; i < nLen; ++i) {
                result[i].x = (float)vec[3 * i];
                result[i].y = (float)vec[3 * i + 1];
                result[i].z = (float)vec[3 * i + 2];
            }
            return result;
        }
        
        public static Vector3[] DVectorToVector3(DVector<float> vec)
        {
            int nLen = vec.Length / 3;
            Vector3[] result = new Vector3[nLen];
            for (int i = 0; i < nLen; ++i) {
                result[i].x = vec[3 * i];
                result[i].y = vec[3 * i + 1];
                result[i].z = vec[3 * i + 2];
            }
            return result;
        }
        
        public static Vector2[] DVectorToVector2(DVector<float> vec)
        {
            int nLen = vec.Length / 2;
            Vector2[] result = new Vector2[nLen];
            for (int i = 0; i < nLen; ++i) {
                result[i].x = vec[2 * i];
                result[i].y = vec[2 * i + 1];
            }
            return result;
        }
        
        public static Color[] DVectorToColor(DVector<float> vec)
        {
            int nLen = vec.Length / 3;
            Color[] result = new Color[nLen];
            for (int i = 0; i < nLen; ++i) {
                result[i].r = vec[3 * i];
                result[i].g = vec[3 * i + 1];
                result[i].b = vec[3 * i + 2];
            }
            return result;
        }
        
        public static int[] DVectorToInt(DVector<int> vec)
        {
            // todo this could be faster because we can directly copy chunks...
            int nLen = vec.Length;
            int[] result = new int[nLen];
            for (int i = 0; i < nLen; ++i)
                result[i] = vec[i];
            return result;
        }
    }
}