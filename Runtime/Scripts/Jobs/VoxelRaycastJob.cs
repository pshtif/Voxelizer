/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace BinaryEgo.Voxelizer
{
    [BurstCompile(CompileSynchronously = true)]
    public struct VoxelRaycastJob : IJob
    {
        public Ray ray;
        public float voxelSize;
        [ReadOnly] public NativeArray<Matrix4x4> inMatrices;
        
        public NativeArray<int> outputIndex;

        public void Execute()
        {
            float3 rayOrigin = ray.origin;
            float pointDistanceSqr = float.PositiveInfinity;
            float voxelSizeSqr = voxelSize * voxelSize;
            for (int i = 0; i < inMatrices.Length; i++)
            {
                float3 voxelPosition = inMatrices[i].GetPosition();
                Vector3 originVector = voxelPosition - rayOrigin;
                float distance = Vector3.Cross(ray.direction, originVector).sqrMagnitude;
                if (distance < voxelSizeSqr)
                {
                    float originDistanceSqr = originVector.sqrMagnitude;
                    if (originDistanceSqr < pointDistanceSqr)
                    {
                        outputIndex[0] = i;
                        pointDistanceSqr = originDistanceSqr;
                    }
                }
            }
        }
    }
}