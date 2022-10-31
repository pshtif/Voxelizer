/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using UnityEngine;

namespace BinaryEgo.Voxelizer
{
    public class MeshUtils
    {
         public static Mesh CreateCube()
        {
            Vector3[] vertices = {
                new Vector3 (-0.5f, -0.5f, -0.5f),
                new Vector3 (0.5f, -0.5f, -0.5f),
                new Vector3 (0.5f, 0.5f, -0.5f),
                new Vector3 (-0.5f, 0.5f, -0.5f),
                new Vector3 (-0.5f, 0.5f, 0.5f),
                new Vector3 (0.5f, 0.5f, 0.5f),
                new Vector3 (0.5f, -0.5f, 0.5f),
                new Vector3 (-0.5f, -0.5f, 0.5f),
            };

            int[] triangles = {
                0, 2, 1, // front
                0, 3, 2,
                2, 3, 4, // top
                2, 4, 5,
                1, 2, 5, // right
                1, 5, 6,
                4, 0, 7, // left
                4, 3, 0,
                5, 4, 7, // back
                5, 7, 6,
                6, 7, 0, // bottom
                6, 0, 1
            };
            
            Vector3[] normals  = {
                new Vector3 (0, 0, -1),
                new Vector3 (1, 0, 0),
                new Vector3 (0, 1, 0),
                new Vector3 (0, 0, 0), 
                new Vector3 (-1, 0, 0),
                new Vector3 (0, 0, 1),
                new Vector3 (0, -1, 0),
                new Vector3 (0, 0, 0),
            };

            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.Optimize();

            return mesh;
        }
        
        public static Mesh CreateQuad()
        {
            Vector3[] vertices = {
                new Vector3 (-0.5f, -0.5f, 0f),
                new Vector3 (0.5f, -0.5f, 0f),
                new Vector3 (-0.5f, 0.5f, 0f),
                new Vector3 (0.5f, 0.5f, 0f),
            };

            int[] triangles = {
                0, 2, 1, // front
                1, 2, 3,
            };
            
            Vector3[] normals  = {
                new Vector3 (0, 0, 1),
                new Vector3 (0, 0, 1),
                new Vector3 (0, 0, 1),
                new Vector3 (0, 0, 1),
            };

            Vector2[] uvs =
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };

            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.Optimize();

            return mesh;
        }
    }
}