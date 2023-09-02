using Unity.Mathematics;
using Unity.Collections;
using System;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

public struct SimpleMesh<TV,TI> : IDisposable where TV : unmanaged where TI : unmanaged // a simple mesh which is just vertices and indices, with no support for submeshes
{
    public NativeArray<TV> Vertices;

    public NativeArray<TI> Indices;

    public SimpleMesh(uint VerticesAmount, uint IndicesAmount, Allocator allocator, NativeArrayOptions Options = NativeArrayOptions.ClearMemory)
    {
        Vertices = new NativeArray<TV>((int)VerticesAmount, allocator, Options);
        Indices = new NativeArray<TI>((int)IndicesAmount, allocator, Options);
    }

    public static SimpleMesh<TV,TI> FromRange(SimpleMesh<TV,TI> OriginalMesh, uint VertexStart, uint VertexEnd, uint IndexStart, uint IndexEnd, Allocator allocator)
    {
        SimpleMesh<TV,TI> Mesh = new(VertexEnd, IndexEnd, allocator, NativeArrayOptions.UninitializedMemory);

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

    public static SimpleMesh<TV,uint> CombineMeshes(SimpleMesh<TV,uint> Mesh1, SimpleMesh<TV,uint> Mesh2, Allocator allocator) // uint only cause I'm bad with generics
    {
        var MergedMesh = new SimpleMesh<TV, uint>((uint)(Mesh1.Vertices.Length + Mesh2.Vertices.Length), (uint)(Mesh1.Indices.Length + Mesh2.Indices.Length), allocator, NativeArrayOptions.UninitializedMemory); // uninitialized memory cause we will be setting it ourselves immediately

        MergedMesh.Vertices.GetSubArray(0, Mesh1.Vertices.Length).CopyFrom(Mesh1.Vertices);
        MergedMesh.Vertices.GetSubArray(Mesh1.Vertices.Length, Mesh2.Vertices.Length).CopyFrom(Mesh2.Vertices);

        MergedMesh.Indices.GetSubArray(0, Mesh1.Indices.Length).CopyFrom(Mesh1.Indices);


        var Indices2ndHalf = MergedMesh.Indices.GetSubArray(Mesh1.Indices.Length, Mesh2.Indices.Length);

        for (int i = 0; i < Indices2ndHalf.Length; i++)
        {
            Indices2ndHalf[i] = Mesh2.Indices[i] + (uint)Mesh1.Indices.Length; // potentially right? Might have off by 1 error?
        }

        return MergedMesh;
    }

}