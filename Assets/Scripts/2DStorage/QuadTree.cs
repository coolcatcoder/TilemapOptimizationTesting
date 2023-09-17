using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public struct QuadTree<T> : IDisposable where T : unmanaged // until unity gives us a modern .net version we are stuck with manually choosing what data type we want for node ids
{
    public Box BoundingBox;
    public uint Root;
    public NativeList<uint4> Nodes;

    public uint BuildNode(NativeList<float2> Positions, Box BoundingBox)
    {
        uint PositionsInsideBox = 0;
        for (int i = 0; i < Positions.Length; i++)
        {
            if (!(math.any(Positions[i] > BoundingBox.Max) || math.any(Positions[i] < BoundingBox.Min))) // if not inside the box
            {
                PositionsInsideBox++;
            }
        }

        if (PositionsInsideBox==0)
        {
            return uint.MaxValue;
        }

        int CurrentNode = Nodes.Length; // the current node index will be Nodes.Length cause we Add() a null node right after this
        Nodes.Add(uint.MaxValue);
        float2 Center = StorageMethods.Middle(BoundingBox.Min, BoundingBox.Max);

        Nodes[CurrentNode] = new uint4(
            BuildNode(Positions, new Box { Min = BoundingBox.Min, Max = Center }),
            BuildNode(Positions, new Box { Min = new float2(Center.x, BoundingBox.Min.y), Max = new float2(BoundingBox.Max.x, Center.y) }),
            BuildNode(Positions, new Box { Min = new float2(BoundingBox.Min.x, Center.y), Max = new float2(Center.x, BoundingBox.Max.y) }),
            BuildNode(Positions, new Box { Min = Center, Max = BoundingBox.Max })
            );

        return (uint)CurrentNode;

    }

    public void Dispose()
    {
        Nodes.Dispose();
    }
}

public struct Box
{
    public float2 Min;
    public float2 Max;

    public void Extend(float2 PosToInclude)
    {
        Min.x = math.min(Min.x, PosToInclude.x);
        Min.y = math.min(Min.y, PosToInclude.y);
        Max.x = math.max(Max.x, PosToInclude.x);
        Max.y = math.max(Max.y, PosToInclude.y);
    }
}