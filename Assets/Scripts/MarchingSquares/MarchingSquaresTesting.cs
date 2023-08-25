using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class MarchingSquaresTesting : MonoBehaviour
{
    public bool4 Corners;

    public byte SquareIndex;

    public float DebugHalfSize;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

[CustomEditor(typeof(MarchingSquaresTesting))]
public class BiomeEditor : Editor
{
    public void OnSceneGUI()
    {
        var t = target as MarchingSquaresTesting;

        Handles.color = Color.blue;
        Handles.DrawWireCube(new float3(5, 5, 0), new float3(10, 10, 0.1f));

        Handles.color = Color.magenta;

        if (t.Corners.x)
        {
            Handles.DrawWireCube(new float3(0, 0, 0), new float3(1, 1, 0.1f));
        }

        if (t.Corners.y)
        {
            Handles.DrawWireCube(new float3(10, 0, 0), new float3(1, 1, 0.1f));
        }

        if (t.Corners.z)
        {
            Handles.DrawWireCube(new float3(10, 10, 0), new float3(1, 1, 0.1f));
        }

        if (t.Corners.w)
        {
            Handles.DrawWireCube(new float3(0, 10, 0), new float3(1, 1, 0.1f));
        }

        t.SquareIndex = MarchingSquares.CalculateSquareIndex(t.Corners);

        NativeArray<byte> TestArray = new NativeArray<byte>(1, Allocator.Temp);
        TestArray[0] = t.SquareIndex;

        NativeArray<int3> TestPositions = new NativeArray<int3>(1, Allocator.Temp);
        TestPositions[0] = new int3(5,5,0);

        SimpleMesh<Vertex,uint> TestMesh = MarchingSquares.BetterTri(TestArray, TestPositions, t.DebugHalfSize);

        var VertexAttributes = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Persistent);
        VertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
        VertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);

        var TestMeshArray = TestMesh.AsMeshDataWithNoSubMesh(VertexAttributes);

        var TestMeshData = TestMeshArray[0];
        TestMeshData.subMeshCount = 1;

        Bounds SubMeshBounds = new Bounds()
        {
            center = new float3(0,0,0),
            extents = new float3(10, 10, 50) / 2 // 50 should be enough
        };

        SubMeshDescriptor SubMeshInfo = new()
        {
            baseVertex = 0, // for now this is correct, but will be an issue eventually
            bounds = SubMeshBounds,
            firstVertex = 0,
            indexCount = TestMesh.Indices.Length, // 2 triangles with each triangle needing 3 then that for every block.
            indexStart = 0, //potentially lol
            topology = MeshTopology.Triangles, // 3 indices per face
            vertexCount = TestMesh.Vertices.Length
        };

        TestMeshData.SetSubMesh(0, SubMeshInfo);

        Mesh.ApplyAndDisposeWritableMeshData(TestMeshArray, t.GetComponent<MeshFilter>().sharedMesh);

        TestMesh.Dispose();
        VertexAttributes.Dispose();
    }
}
