/*
 *	Created by:  Peter @sHTiF Stefcek
 */


using System;
using System.Collections.Generic;
using g3;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Jobs;
using UnityEngine;

namespace BinaryEgo.Voxelizer
{
    [Serializable]
    public class VoxelMesh : ISerializationCallbackReceiver
    {
        // public bool usePhysics = false;

        [SerializeField] private float _voxelSize = 1;

        [SerializeField] private Transform _transform;

        [SerializeField] private List<int> _indices = new List<int>();
        [SerializeField] private List<Matrix4x4> _matrices = new List<Matrix4x4>();
        [SerializeField] private List<Vector4> _colors = new List<Vector4>();
        
        // private Dictionary<Vector3i, Transform> _physicsLookup;
        // private Transform _physicsContainer;
        // private List<Transform> _physicsTransforms;
        // private TransformAccessArray _physicsTransformAccessArray;

        [SerializeField] private VoxelTransformBakeType _voxelTransformBakeType;

        [SerializeField] private int _voxelCount;
        public int VoxelCount => _voxelCount;

        [NonSerialized] private bool _forceInvalidateJobs;
        [NonSerialized] private Matrix4x4 _previousTransformMatrix;

        private NativeList<int> _nativeIndices;
        private NativeList<Matrix4x4> _nativeMatrices;
        private NativeList<Vector4> _nativeColors;

        public string name;

        public VoxelMesh(string p_name, IBinaryVoxelGrid p_voxelGrid, Vector4[] p_colors, Transform p_transform,
            AxisAlignedBox3d p_bounds, Vector3 p_offset, bool p_generateInside, float p_voxelSize,
            VoxelTransformBakeType p_voxelTransformBakeType)
        {
            name = p_name;
            _transform = p_transform;
            _voxelSize = p_voxelSize;
            _voxelTransformBakeType = p_voxelTransformBakeType;

            if (p_voxelGrid == null)
                return;

            int colorIndex = 0;
            int voxelIndex = 0;
            foreach (Vector3i voxelPosition in p_voxelGrid.NonZeros())
            {
                if (p_generateInside || !VoxelUtils.IsInside(p_voxelGrid, voxelPosition))
                {
                    var position = new Vector3(voxelPosition.x, voxelPosition.y, voxelPosition.z) * _voxelSize +
                                   p_offset;

                    _matrices.Add(Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * _voxelSize));
                    _colors.Add(p_colors[colorIndex]);
                    _indices.Add(voxelIndex++);
                }

                colorIndex++;
            }

            // if (usePhysics)
            // {
            //     _physicsTransformAccessArray = new TransformAccessArray(_physicsTransforms.ToArray());
            // }

            _voxelCount = _indices.Count;
            _forceInvalidateJobs = true;
            _previousTransformMatrix = Matrix4x4.identity;
        }

        private void CreateNative()
        {
            _nativeIndices = new NativeList<int>(Allocator.Persistent);
            _nativeMatrices = new NativeList<Matrix4x4>(Allocator.Persistent);
            _nativeColors = new NativeList<Vector4>(Allocator.Persistent);

            _nativeIndices.CopyFromNBC(_indices.ToArray());
            _nativeMatrices.CopyFromNBC(_matrices.ToArray());
            _nativeColors.CopyFromNBC(_colors.ToArray());
        }

        public void Invalidate(ComputeBuffer p_matrixBuffer, NativeArray<Matrix4x4> p_matrixArray,
            ComputeBuffer p_colorBuffer, NativeArray<Vector4> p_colorArray, int p_index)
        {
            if (!_nativeMatrices.IsCreated)
                CreateNative();

            if (_nativeIndices.Length == 0)
                return;

            if (_voxelTransformBakeType == VoxelTransformBakeType.SCALE_ROTATION_POSITION)
            {
                p_matrixBuffer.SetData(_nativeMatrices.AsArray(), 0, p_index, _nativeIndices.Length);
                p_colorBuffer.SetData(_nativeColors.AsArray(), 0, p_index, _nativeIndices.Length);
                return;
            }
            
            if (!_forceInvalidateJobs && (_transform == null || !_transform.hasChanged))
            {
                {
                    p_matrixBuffer.SetData(_nativeMatrices.AsArray(), 0, p_index, _nativeIndices.Length);
                    p_colorBuffer.SetData(_nativeColors.AsArray(), 0, p_index, _nativeIndices.Length);
                    return;
                }
            }

            Matrix4x4 transformMatrix;

            switch (_voxelTransformBakeType)
            {
                case VoxelTransformBakeType.SCALE:
                    transformMatrix = Matrix4x4.Translate(_transform.localToWorldMatrix.GetPosition()) *
                                      Matrix4x4.Rotate(_transform.localToWorldMatrix.rotation);
                    break;
                case VoxelTransformBakeType.SCALE_ROTATION:
                    transformMatrix = Matrix4x4.Translate(_transform.localToWorldMatrix.GetPosition());
                    break;
                default:
                    transformMatrix = _transform.localToWorldMatrix;
                    break;
            }
            
            VoxelPositionUpdateJob positionUpdateJob = new VoxelPositionUpdateJob()
            {
                previousTransformMatrixI = _previousTransformMatrix.inverse,
                transformMatrix = transformMatrix,
                inOutMatrices = _nativeMatrices,
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
                JobHandle jobHandle = positionUpdateJob.Schedule(_nativeIndices.Length, 100);
                jobHandle.Complete();
            }

            _transform.hasChanged = false;
            _previousTransformMatrix = transformMatrix;

            p_matrixBuffer.SetData(_nativeMatrices.AsArray(), 0, p_index, _nativeIndices.Length);
            p_colorBuffer.SetData(_nativeColors.AsArray(), 0, p_index, _nativeIndices.Length);
        }

        public Vector3 GetVoxelPosition(int p_index)
        {
            if (!_nativeMatrices.IsCreated || _nativeMatrices.Length <= p_index)
                return Vector3.zero;
            
            return _nativeMatrices[p_index].GetPosition();
        }
        
        public Color GetVoxelColor(int p_index)
        {
            if (!_nativeColors.IsCreated || _nativeColors.Length <= p_index)
                return Color.white;
            
            return _nativeColors[p_index];
        }

        // Very slow implementation using bruteforce jobs, refactor to something like octree later
        public bool Hit(Ray p_ray, out int p_index)
        {
            p_index = -1;
            if (!_nativeMatrices.IsCreated || _nativeMatrices.Length == 0)
                return false;
            
            NativeArray<int> outputIndex = new NativeArray<int>(1, Allocator.TempJob);
            outputIndex[0] = -1;

            VoxelRaycastJob voxelRaycastJob = new VoxelRaycastJob()
            {
                ray = p_ray,
                // Bounding sphere
                voxelSize = Mathf.Sqrt(2*_voxelSize*_voxelSize),
                inMatrices = _nativeMatrices,
                
                outputIndex = outputIndex,
            };
            JobHandle jobHandle = voxelRaycastJob.Schedule();
            jobHandle.Complete();

            bool hit = outputIndex[0] >= 0;
            if (hit)
            {
                p_index = outputIndex[0];
            }
            
            outputIndex.Dispose();
            
            return hit;
        }
        
        public void Paint(Vector3 p_point, float p_radius, Color p_color)
        {
            if (!_nativeMatrices.IsCreated || _nativeMatrices.Length == 0)
                return;

            var hitList = new NativeList<int>(Allocator.TempJob);
            VoxelDistanceJob voxelDistanceJob = new VoxelDistanceJob()
            {
                point = p_point,
                radius = p_radius,
                inMatrices = _nativeMatrices,
            };
            JobHandle jobHandle = voxelDistanceJob.ScheduleAppend(hitList, _nativeIndices.Length, 100);
            jobHandle.Complete();

            for (int i = 0; i < hitList.Length; i++)
            {
                _nativeColors[hitList[i]] = p_color;
            }
            
            hitList.Dispose();
        }

        private int[] highlightedIndices;
        private Color[] highlightedColors;
        public void Unhighlight()
        {
            if (!_nativeColors.IsCreated || highlightedIndices == null)
                return;
            
            for (int i = 0; i < highlightedIndices.Length; i++)
            {
                _nativeColors[highlightedIndices[i]] = highlightedColors[i];
            }

            highlightedIndices = null;
        }
        public void Highlight(Vector3 p_point, float p_radius, Color p_color)
        {
            if (!_nativeMatrices.IsCreated || _nativeMatrices.Length == 0)
                return;

            var hitList = new NativeList<int>(Allocator.TempJob);
            VoxelDistanceJob voxelDistanceJob = new VoxelDistanceJob()
            {
                point = p_point,
                radius = p_radius,
                inMatrices = _nativeMatrices,
            };
            JobHandle jobHandle = voxelDistanceJob.ScheduleAppend(hitList, _nativeIndices.Length, 100);
            jobHandle.Complete();

            highlightedIndices = hitList.ToArray();
            highlightedColors = new Color[highlightedIndices.Length];
            

            for (int i = 0; i < highlightedIndices.Length; i++)
            {
                highlightedColors[i] = _nativeColors[highlightedIndices[i]];
                _nativeColors[highlightedIndices[i]] = p_color;
            }
            
            hitList.Dispose();
        }

        public void Dispose()
        {
            _forceInvalidateJobs = true;
            _previousTransformMatrix = Matrix4x4.identity;
            if (_nativeMatrices.IsCreated)
            {
                _nativeColors.Dispose();
                _nativeColors = default;
                _nativeIndices.Dispose();
                _nativeIndices = default;
                _nativeMatrices.Dispose();
                _nativeMatrices = default;
            }

            // if (usePhysics)
            // {
            //     _physicsTransformAccessArray.Dispose();
            // }
        }


        // [BurstCompile]
        // struct PhysicsUpdateJob : IJobParallelForTransform
        // {
        //     public NativeSlice<Matrix4x4> matrices;
        //
        //     public void Execute(int p_index, TransformAccess transform)
        //     {
        //         matrices[p_index] = transform.localToWorldMatrix;
        //     }
        // }

        #region SERIALIZATION

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            _forceInvalidateJobs = true;
            _previousTransformMatrix = Matrix4x4.identity;
        }

        #endregion
    }
}
