using Unity.Mathematics;
using Unity.Collections;
using System;
using UnityEngine;

public struct SimpleMesh<TV,TI> : IDisposable where TV : unmanaged where TI : unmanaged // a simple mesh which is just vertices and indices, with no support for submeshes
{
    public NativeArray<TV> Vertices;

    public NativeArray<TI> Indices;

    public SimpleMesh(Allocator VerticesAllocator, uint VerticesAmount, Allocator IndicesAllocator, uint IndicesAmount, NativeArrayOptions VerticesOptions = NativeArrayOptions.ClearMemory, NativeArrayOptions IndicesOptions = NativeArrayOptions.ClearMemory)
    {
        Vertices = new NativeArray<TV>((int)VerticesAmount, VerticesAllocator, VerticesOptions);
        Indices = new NativeArray<TI>((int)IndicesAmount, IndicesAllocator, IndicesOptions);
    }

    public static SimpleMesh<TV,TI> FromRange(SimpleMesh<TV,TI> OriginalMesh, uint VertexStart, uint VertexEnd, Allocator VerticesAllocator, uint IndexStart, uint IndexEnd, Allocator IndicesAllocator, NativeArrayOptions VerticesOptions = NativeArrayOptions.ClearMemory, NativeArrayOptions IndicesOptions = NativeArrayOptions.ClearMemory)
    {
        SimpleMesh<TV,TI> Mesh = new(VerticesAllocator, VertexEnd, IndicesAllocator, IndexEnd, VerticesOptions, IndicesOptions);

        Mesh.Vertices.CopyFrom(OriginalMesh.Vertices.GetSubArray((int)VertexStart, (int)VertexEnd));
        Mesh.Indices.CopyFrom(OriginalMesh.Indices.GetSubArray((int)IndexStart, (int)IndexEnd));

        return Mesh;
    }

    public void Dispose()
    {
        Vertices.Dispose();
        Indices.Dispose();
    }

    public Mesh.MeshDataArray AsMeshDataWithNoSubMesh(NativeArray<UnityEngine.Rendering.VertexAttributeDescriptor> Attributes) // SubMesh or Submesh I don't know!
    {
        Mesh.MeshDataArray MeshArray = Mesh.AllocateWritableMeshData(1);
        MeshArray[0].SetIndexBufferParams(Indices.Length, UnityEngine.Rendering.IndexFormat.UInt32);
        MeshArray[0].SetVertexBufferParams(Vertices.Length, Attributes);

        Mesh.MeshData Data = MeshArray[0];

        Data.GetIndexData<TI>().CopyFrom(Indices);
        Data.GetVertexData<TV>().CopyFrom(Vertices);

        return MeshArray;
    }
}