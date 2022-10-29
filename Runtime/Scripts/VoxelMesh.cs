/*
 *	Created by:  Peter @sHTiF Stefcek
 */


using System;
using System.Collections.Generic;
using System.Linq;
using BinaryEgo.Voxelizer;
using g3;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

[Serializable]
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

    private float _voxelSize;
    
    public NativeList<int> voxelIndices { get; protected set; }

    private Transform _transform;
    private Matrix4x4 _previousTransformMatrix = Matrix4x4.identity;
    private NativeList<Matrix4x4> _matrices;
    private NativeList<Vector4> _colors;

    public bool IsInitialized => _initialized;
    [NonSerialized]
    private bool _initialized = false;
    
    public bool _usingCache = false;
    private string _cacheName;

    private Dictionary<Vector3i, Transform> _physicsLookup;
    private Transform _physicsContainer;
    private List<Transform> _physicsTransforms;
    private TransformAccessArray _physicsTransformAccessArray;
    private VoxelBakeTransform _voxelBakeTransform;

    public VoxelMesh(string p_name, Transform p_transform)
    {
        _transform = p_transform;
        _cacheName = p_name;
        
        if (POSITION_CACHE.ContainsKey(p_name))
        {
            _usingCache = true;
            //_vertices = POSITION_CACHE[p_name];
            _colors = COLOR_CACHE[p_name];
            voxelIndices = INDEX_CACHE[p_name];
        }
        else
        {
            Debug.LogError("Trying to create VoxelMesh from cache that doesn't exist.");
        }
        
        _initialized = true;
    }
    
    public VoxelMesh(IBinaryVoxelGrid p_voxelGrid, Vector4[] p_colors, Transform p_transform, AxisAlignedBox3d p_bounds, bool p_generateInside, float p_voxelSize, VoxelBakeTransform p_voxelBakeTransform)
    {
        _transform = p_transform;
        _voxelSize = p_voxelSize;
        _voxelBakeTransform = p_voxelBakeTransform; 

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
            var offset = (Vector3)p_bounds.Min - _voxelSize * 2f * Vector3.one;
            //Vector4[] colors = p_machina.cachedOutput.GetAttribute<Vector4[]>("colorBuffer");
            
            //_vertices = new NativeList<Vector3>(Allocator.Persistent);
            _matrices = new NativeList<Matrix4x4>(Allocator.Persistent);
            this._colors = new NativeList<Vector4>(Allocator.Persistent);
            voxelIndices = new NativeList<int>(Allocator.Persistent);
            int colorIndex = 0;
            int voxelIndex = 0;
            foreach (Vector3i voxelPosition in p_voxelGrid.NonZeros())
            {
                if (p_generateInside || !IsInside(p_voxelGrid, voxelPosition))
                {
                    var position = new Vector3(voxelPosition.x, voxelPosition.y, voxelPosition.z) *_voxelSize  +
                               offset;
                    _matrices.Add(Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * _voxelSize));
                    //_vertices.Add(new Vector3(nz.x, nz.y, nz.z) * voxelSize + offset);

                    _colors.Add(p_colors[colorIndex]);
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

        _transform.hasChanged = true;
        _initialized = true;
    }

    public void CloneCache()
    {
        //_vertices = new NativeList<Vector3>(_vertices.Length, Allocator.Persistent);
        //_vertices.AddRangeNoResize(POSITION_CACHE[_cacheName]);
        _colors = new NativeList<Vector4>(_colors.Length, Allocator.Persistent);
        _colors.AddRangeNoResize(COLOR_CACHE[_cacheName]);
        voxelIndices = new NativeList<int>(voxelIndices.Length, Allocator.Persistent);
        voxelIndices.AddRangeNoResize(INDEX_CACHE[_cacheName]);
        _usingCache = false;
    }
    
    public void Invalidate(ComputeBuffer p_matrixBuffer, NativeArray<Matrix4x4> p_matrixArray, ComputeBuffer p_colorBuffer, NativeArray<Vector4> p_colorArray, int p_index)
    {
        if (_transform == null || !_transform.hasChanged || _voxelBakeTransform == VoxelBakeTransform.SCALE_ROTATION_POSITION)
        {
            p_matrixBuffer.SetData(_matrices.AsArray(), 0, p_index, voxelIndices.Length);
            p_colorBuffer.SetData(_colors.AsArray(), 0, p_index, voxelIndices.Length);
            return;
        }

        VoxelRenderer.isDirty = true;
        
        NativeSlice<Vector4> outColorSlice = new NativeSlice<Vector4>(p_colorArray, p_index);

        Matrix4x4 transformMatrix;

        switch (_voxelBakeTransform)
        {
            case VoxelBakeTransform.SCALE:
                transformMatrix = Matrix4x4.Translate(_transform.localToWorldMatrix.GetPosition()) *
                                  Matrix4x4.Rotate(_transform.localToWorldMatrix.rotation);
                break;
            case VoxelBakeTransform.SCALE_ROTATION:
                transformMatrix = Matrix4x4.Translate(_transform.localToWorldMatrix.GetPosition());
                break;
            default:
                transformMatrix = _transform.localToWorldMatrix;
                break;
        }

        PositionUpdateJob positionUpdateJob = new PositionUpdateJob
        {
            voxelSize = _voxelSize,
            previousTransformMatrixI = _previousTransformMatrix.inverse,
            transformMatrix = transformMatrix,
            c = _colors,
            ids = voxelIndices,
            matrices = _matrices,
            //outMatrices =  outMatrixSlice,
            colors = outColorSlice,
        };

        // if (usePhysics)
        // {
        //     PhysicsUpdateJob physicsUpdateJob = new PhysicsUpdateJob
        //     {
        //         matrices = inMatrixSlice
        //     };
        //     JobHandle physicsJobHandle = physicsUpdateJob.Schedule(_physicsTransformAccessArray);
        //     
        //     JobHandle jobHandle = positionUpdateJob.Schedule(voxelIndices.Length, 100, physicsJobHandle);
        //     jobHandle.Complete();
        // }
        // else
        {
            JobHandle jobHandle = positionUpdateJob.Schedule(voxelIndices.Length, 100);
            jobHandle.Complete();
        }
        
        _transform.hasChanged = false;
        _previousTransformMatrix = transformMatrix;

        p_matrixBuffer.SetData(_matrices.AsArray(), 0, p_index, voxelIndices.Length);
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
                _colors[i] = p_color;
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
            _colors.Dispose();
            _colors = default;
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
        [ReadOnly] public Matrix4x4 previousTransformMatrixI;
        [ReadOnly] public Matrix4x4 transformMatrix;
        //[ReadOnly] public NativeList<Vector3> positions;
        //[ReadOnly] public NativeList<Matrix4x4> inMatrices;
        [ReadOnly] public NativeList<Vector4> c;
        [ReadOnly] public NativeList<int> ids;
        [ReadOnly] public float voxelSize;

        public NativeArray<Matrix4x4> matrices;
        public NativeSlice<Vector4> colors;

        public void Execute(int index)
        {
            Matrix4x4 matrix = previousTransformMatrixI * matrices[index];
            matrices[index] = transformMatrix * matrix;
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
