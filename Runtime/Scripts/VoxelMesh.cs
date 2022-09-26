/*
 *	Created by:  Peter @sHTiF Stefcek
 */


using System;
using System.Collections.Generic;
using System.Linq;
using g3;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public class VoxelMesh
{
    public static bool IsCached(string p_name)
    {
        return POSITION_CACHE != null ? POSITION_CACHE.ContainsKey(p_name) : false;
    }
    
    private static Dictionary<string, NativeList<Vector3>> POSITION_CACHE;
    private static Dictionary<string, NativeList<Vector4>> COLOR_CACHE;
    private static Dictionary<string, NativeList<int>> INDEX_CACHE;
    private static bool _cacheDisposed = false;

    public bool usePhysics = false;

    public Bitmap3 voxelData;
    public NativeList<int> voxelIndices { get; protected set; }

    private Transform _transform;
    private NativeList<Matrix4x4> _matrices;
    public NativeList<Vector4> colors;

    public bool IsInitialized => _initialized;
    [NonSerialized]
    private bool _initialized = false;
    
    public bool _usingCache = false;
    private string _cacheName;

    private Dictionary<Vector3i, Transform> _physicsLookup;
    private Transform _physicsContainer;
    private List<Transform> _physicsTransforms;
    private TransformAccessArray _physicsTransformAccessArray;

    public VoxelMesh(string p_name, Transform p_transform)
    {
        _transform = p_transform;
        _cacheName = p_name;
        
        if (POSITION_CACHE.ContainsKey(p_name))
        {
            _usingCache = true;
            //_vertices = POSITION_CACHE[p_name];
            colors = COLOR_CACHE[p_name];
            voxelIndices = INDEX_CACHE[p_name];
        }
        else
        {
            Debug.LogError("Trying to create VoxelMesh from cache that doesn't exist.");
        }
        
        _initialized = true;
    }
    
    public VoxelMesh(IBinaryVoxelGrid p_voxelGrid, Vector4[] p_colors, Transform p_transform, AxisAlignedBox3d p_bounds, bool p_generateInside, float p_voxelSize, Vector3 p_offset)
    {
        _transform = p_transform;

        if (p_voxelGrid == null)
            return;

        // if (POSITION_CACHE == null)
        // {
        //     POSITION_CACHE = new Dictionary<string, NativeList<Vector3>>();
        //     COLOR_CACHE = new Dictionary<string, NativeList<Vector4>>();
        //     INDEX_CACHE = new Dictionary<string, NativeList<int>>();
        // }
        //
        // if (POSITION_CACHE.ContainsKey(_cacheName))
        // {
        //     //_vertices = POSITION_CACHE[_cacheName];
        //     _colors = COLOR_CACHE[_cacheName];
        //     voxelIndices = INDEX_CACHE[_cacheName];
        // }
        // else
        {
            //var bounds = mesh.CachedBounds;
            var offset = (Vector3)p_bounds.Min - Vector3.one * p_voxelSize * 2.5f;
            //Vector4[] colors = p_machina.cachedOutput.GetAttribute<Vector4[]>("colorBuffer");
            
            //_vertices = new NativeList<Vector3>(Allocator.Persistent);
            _matrices = new NativeList<Matrix4x4>(Allocator.Persistent);
            this.colors = new NativeList<Vector4>(Allocator.Persistent);
            voxelIndices = new NativeList<int>(Allocator.Persistent);
            int colorIndex = 0;
            int voxelIndex = 0;
            foreach (Vector3i voxelPosition in p_voxelGrid.NonZeros())
            {
                if (p_generateInside || !IsInside(p_voxelGrid, voxelPosition))
                {
                    if (usePhysics)
                    {
                        var position = new Vector3(voxelPosition.x, voxelPosition.y, voxelPosition.z) * p_voxelSize
                                       + offset;
                        position = _transform.localToWorldMatrix.MultiplyPoint(position);
                        _matrices.Add(Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * p_voxelSize));
                        //AddPhysicsBox(position, voxelPosition, voxelIndex);
                        //_vertices.Add(_transform.localToWorldMatrix * position);
                    }
                    else
                    {
                        _matrices.Add(Matrix4x4.TRS(new Vector3(voxelPosition.x, voxelPosition.y, voxelPosition.z) * p_voxelSize + offset, Quaternion.identity, Vector3.one * p_voxelSize));
                        //_vertices.Add(new Vector3(nz.x, nz.y, nz.z) * voxelSize + offset);
                    }

                    this.colors.Add(p_colors[colorIndex]);
                    voxelIndices.Add(voxelIndex++);
                }

                colorIndex++;
            }
            
            // int index = 0;
            // foreach (Vector3i voxelPosition in voxels.NonZeros())
            // {
            //     if (p_generateInside || !IsInside(voxels, voxelPosition))
            //     {
            //         var body1 = _physicsLookup[voxelPosition].GetComponent<Rigidbody>();
            //         Rigidbody body2;
            //         Vector3i n = new Vector3i(voxelPosition.x + 1, voxelPosition.y, voxelPosition.z);
            //         if (_physicsLookup.ContainsKey(n))
            //         {
            //             body2 = _physicsLookup[n].GetComponent<Rigidbody>();
            //             VoxelRenderer.Instance.physics.AddConnection(body1, body2);
            //         }
            //         
            //         n = new Vector3i(voxelPosition.x, voxelPosition.y+1, voxelPosition.z);
            //         if (_physicsLookup.ContainsKey(n))
            //         {
            //             body2 = _physicsLookup[n].GetComponent<Rigidbody>();
            //             VoxelRenderer.Instance.physics.AddConnection(body1, body2);
            //         }
            //         voxelIndex++;
            //     }
            // }

            //POSITION_CACHE[_cacheName] = _vertices;
            //COLOR_CACHE[_cacheName] = _colors;
            //INDEX_CACHE[_cacheName] = voxelIndices;
        }

        // if (usePhysics)
        // {
        //     _physicsTransformAccessArray = new TransformAccessArray(_physicsTransforms.ToArray());
        // }

        _initialized = true;
    }

    public void CloneCache()
    {
        //_vertices = new NativeList<Vector3>(_vertices.Length, Allocator.Persistent);
        //_vertices.AddRangeNoResize(POSITION_CACHE[_cacheName]);
        colors = new NativeList<Vector4>(colors.Length, Allocator.Persistent);
        colors.AddRangeNoResize(COLOR_CACHE[_cacheName]);
        voxelIndices = new NativeList<int>(voxelIndices.Length, Allocator.Persistent);
        voxelIndices.AddRangeNoResize(INDEX_CACHE[_cacheName]);
        _usingCache = false;
    }
    
    public void Invalidate(ComputeBuffer p_matrixBuffer, NativeArray<Matrix4x4> p_matrixArray, ComputeBuffer p_colorBuffer, NativeArray<Vector4> p_colorArray, int p_index)
    {
        if (_transform == null)
            return;
        
        //if (!_transform.hasChanged || !IsInitialized)
        //    return;

        VoxelRenderer.isDirty = true;
        
        NativeSlice<Matrix4x4> outMatrixSlice = new NativeSlice<Matrix4x4>(p_matrixArray, p_index);
        NativeSlice<Vector4> outColorSlice = new NativeSlice<Vector4>(p_colorArray, p_index);

        NativeSlice<Matrix4x4> inMatrixSlice = new NativeSlice<Matrix4x4>(_matrices.AsArray());

        PositionUpdateJob positionUpdateJob = new PositionUpdateJob
        {
            usePhysics = true,
            voxelSize = 1,
            matrix = _transform.localToWorldMatrix,
            c = colors,
            ids = voxelIndices,
            inMatrices = _matrices,
            outMatrices =  outMatrixSlice,
            colors = outColorSlice,
        };

        if (usePhysics)
        {
            PhysicsUpdateJob physicsUpdateJob = new PhysicsUpdateJob
            {
                matrices = inMatrixSlice
            };
            JobHandle physicsJobHandle = physicsUpdateJob.Schedule(_physicsTransformAccessArray);
            
            JobHandle jobHandle = positionUpdateJob.Schedule(voxelIndices.Length, 100, physicsJobHandle);
            jobHandle.Complete();
        }
        else
        {
            JobHandle jobHandle = positionUpdateJob.Schedule(voxelIndices.Length, 100);
            jobHandle.Complete();
        }
        
        _transform.hasChanged = false;

        p_matrixBuffer.SetData(p_matrixArray, p_index, p_index, voxelIndices.Length);
        p_colorBuffer.SetData(p_colorArray, p_index, p_index, voxelIndices.Length);
    }

    public void Erase(Vector3 p_point, float p_radius)
    {
        Paint(p_point, p_radius, new Color(0,0,0,0));
    }

    public void Hit(Vector3 p_point, float p_radius)
    {
        if (_usingCache)
            CloneCache();
        
        for (int i = 0; i<_matrices.Length; i++)
        {
            if (Vector3.Distance(_matrices[i].GetColumn(3), p_point) < p_radius)
            {
                _physicsTransforms[i].GetComponent<Rigidbody>().isKinematic = false;
            }
        }
    }
    
    public void Paint(Vector3 p_point, float p_radius, Color p_color)
    {
        if (_usingCache)
            CloneCache();
        
        for (int i = 0; i<_matrices.Length; i++)
        {
            if (Vector3.Distance(_matrices[i].GetColumn(3), p_point) < p_radius)
            {
                colors[i] = p_color;
            }
        }
        
        _transform.hasChanged = true;
    }

    protected bool IsInside(IBinaryVoxelGrid p_grid, Vector3i p_index)
    {
        AxisAlignedBox3i bounds = p_grid.GridBounds;
        bounds.Max -= Vector3i.One;
        
        Vector3i n = new Vector3i(p_index.x + 1, p_index.y, p_index.z);
        if (!bounds.Contains(n) || !p_grid.Get(n))
            return false;
        n = new Vector3i(p_index.x - 1, p_index.y, p_index.z);
        if (!bounds.Contains(n) || !p_grid.Get(n))
            return false;
        n = new Vector3i(p_index.x, p_index.y + 1, p_index.z);
        if (!bounds.Contains(n) || !p_grid.Get(n))
            return false;
        n = new Vector3i(p_index.x, p_index.y - 1, p_index.z);
        if (!bounds.Contains(n) || !p_grid.Get(n))
            return false;
        n = new Vector3i(p_index.x, p_index.y, p_index.z + 1);
        if (!bounds.Contains(n) || !p_grid.Get(n))
            return false;
        n = new Vector3i(p_index.x, p_index.y, p_index.z - 1);
        if (!bounds.Contains(n) || !p_grid.Get(n))
            return false;

        return true;
    }

    public void Dispose()
    {
        if (!_usingCache && _matrices.IsCreated)
        {
            colors.Dispose();
            colors = default;
            voxelIndices.Dispose();
            voxelIndices = default;
            _matrices.Dispose();
            _matrices = default;
        }
        else
        {
            // Warning I assume here that this dispose will be called when application exits/stopsplaying otherwise you shouldn't dispose cache :)
            if (!_cacheDisposed)
            {
                foreach (var key in POSITION_CACHE.Keys.ToArray())
                {
                    if (POSITION_CACHE[key].IsCreated)
                    {
                        POSITION_CACHE[key].Dispose();
                        POSITION_CACHE[key] = default;
                    }

                    if (COLOR_CACHE[key].IsCreated) 
                    {
                        COLOR_CACHE[key].Dispose();
                        COLOR_CACHE[key] = default;
                    }

                    if (INDEX_CACHE[key].IsCreated) 
                    {
                        INDEX_CACHE[key].Dispose();
                        INDEX_CACHE[key] = default;
                    }
                }
            }
        }

        if (usePhysics)
        {
            _physicsTransformAccessArray.Dispose();
        }
    }
    
    
    [BurstCompile]
    public struct PositionUpdateJob : IJobParallelFor
    {
        public bool usePhysics;
        
        [ReadOnly] public float4x4 matrix;
        //[ReadOnly] public NativeList<Vector3> positions;
        [ReadOnly] public NativeList<Matrix4x4> inMatrices;
        [ReadOnly] public NativeList<Vector4> c;
        [ReadOnly] public NativeList<int> ids;
        [ReadOnly] public float voxelSize;

        public NativeSlice<Matrix4x4> outMatrices;
        public NativeSlice<Vector4> colors;

        public void Execute(int index)
        {
            // float3 vertex;
            //
            // Matrix4x4 m = outMatrices[index];
            // float v = c[index].w == 0 ? 0 : voxelSize;
            // m.SetColumn(0, new Vector4(v,0,0,0));
            // m.SetColumn(1, new Vector4(0,v,0,0));
            // m.SetColumn(2, new Vector4(0,0,v,0));
            // m.SetColumn(3, new Vector4(vertex.x, vertex.y, vertex.z, 1));
            outMatrices[index] = inMatrices[index];
            colors[index] = c[index];
        }
    }
    
    [BurstCompile]
    struct PhysicsUpdateJob : IJobParallelForTransform
    {
        public NativeSlice<Matrix4x4> matrices;
    
        public void Execute(int p_index, TransformAccess transform)
        {
            matrices[p_index] = transform.localToWorldMatrix;
        }
    }
}
