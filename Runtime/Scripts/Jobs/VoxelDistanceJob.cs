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
    public struct VoxelDistanceJob : IJobParallelForFilter
    {
        public Vector3 point;
        public float radius;
        [ReadOnly] public NativeArray<Matrix4x4> inMatrices;

        public bool Execute(int p_index)
        {
            return (inMatrices[p_index].GetPosition() - point).magnitude < radius;
        }
    }
}