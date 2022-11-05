/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace BinaryEgo.Voxelizer
{
    [BurstCompile(CompileSynchronously = true)]
    public struct VoxelRaycastJobFilter : IJobParallelForFilter
    {
        public Ray ray;
        public float radius;
        [ReadOnly] public NativeArray<Matrix4x4> inMatrices;

        public bool Execute(int p_index)
        {
            float distance = Vector3.Cross(ray.direction, inMatrices[p_index].GetPosition() - ray.origin).magnitude;
            return (distance < radius);
        }
    }
}