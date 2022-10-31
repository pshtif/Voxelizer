/*
 *	Created by:  Peter @sHTiF Stefcek
 */


using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace BinaryEgo.Voxelizer
{
    [Serializable]
    public class VoxelGroup
    {
        [SerializeField]
        private List<VoxelMesh> _voxelMeshes = new List<VoxelMesh>();

        public void AddMesh(VoxelMesh p_voxelMesh)
        {
            _voxelMeshes.Add(p_voxelMesh);
        }

        public void Invalidate(ComputeBuffer p_transformBuffer, NativeArray<Matrix4x4> p_matrixArray,
            ComputeBuffer p_colorBuffer, NativeArray<Vector4> p_colorArray, ref int p_indexOffset)
        {
            foreach (VoxelMesh voxelMesh in _voxelMeshes)
            {
                voxelMesh.Invalidate(p_transformBuffer, p_matrixArray, p_colorBuffer, p_colorArray, p_indexOffset);
                p_indexOffset += voxelMesh.VoxelCount;
            }
        }

        public void Dispose()
        {
            _voxelMeshes?.ForEach(vm => vm.Dispose());
        }
    }
}