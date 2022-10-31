/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace BinaryEgo.Voxelizer
{
    [BurstCompile]
    public struct VoxelPositionUpdateJob : IJobParallelFor
    {
        [ReadOnly] public Matrix4x4 previousTransformMatrixI;
        [ReadOnly] public Matrix4x4 transformMatrix;
        //[ReadOnly] public NativeList<Vector4> c;

        public NativeArray<Matrix4x4> matrices;
        //public NativeSlice<Vector4> colors;

        public void Execute(int index)
        {
            Matrix4x4 matrix = previousTransformMatrixI * matrices[index];
            matrices[index] = transformMatrix * matrix;
            //colors[index] = c[index];
        }
    }
}