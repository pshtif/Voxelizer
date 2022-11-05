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

        public List<VoxelMesh> VoxelMeshes => _voxelMeshes;

        public void AddMesh(VoxelMesh p_voxelMesh)
        {
            _voxelMeshes.Add(p_voxelMesh);
        }

        public void ClearMeshes()
        {
            _voxelMeshes?.ForEach(vm => vm.Dispose());
            _voxelMeshes.Clear();
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

        public bool Hit(Ray p_ray, out VoxelMesh p_voxelMesh, out int p_index)
        {
            p_index = -1;
            p_voxelMesh = null;
            bool hit = false;
            int hitIndex;
            float closestDistance = float.PositiveInfinity;
            foreach (var voxelMesh in _voxelMeshes)
            {
                if (voxelMesh.Hit(p_ray, out hitIndex))
                {
                    var distance = (voxelMesh.GetVoxelPosition(hitIndex) - p_ray.origin).magnitude;
                    if (hit)
                    {
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            p_voxelMesh = voxelMesh;
                            p_index = hitIndex;
                        }
                    }
                    else
                    {
                        closestDistance = distance;
                        p_voxelMesh = voxelMesh;
                        p_index = hitIndex;
                        hit = true;
                    }
                }
            }

            return hit;
        }

        public void Paint(Vector3 p_point, float p_radius, Color p_color)
        {
            _voxelMeshes.ForEach(vm => vm.Paint(p_point, p_radius, p_color));
        }

        public void Dispose()
        {
            _voxelMeshes?.ForEach(vm => vm.Dispose());
        }

        public void Unhighlight()
        {
            _voxelMeshes.ForEach(vm => vm.Unhighlight());
        }
    }
}