using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

public class TilemapSettings : MonoBehaviour
{
    public int TilemapSize;
    public int Trials;
    public int2 Pos;
    public int GenerationWidth;
}

public class TilemapBaker : Baker<TilemapSettings>
{
    public override void Bake(TilemapSettings authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new TilemapSettingsData
        {
            TilemapSize = authoring.TilemapSize,
            Trials = authoring.Trials,
            Pos = authoring.Pos,
            GenerationWidth = authoring.GenerationWidth
        });
    }
}

public struct TilemapSettingsData : IComponentData
{
    public int TilemapSize;
    public int Trials;
    public int2 Pos;
    public int GenerationWidth;
}

public unsafe struct BasicTreeNode
{
    BasicTreeNode* ChildNode1;
    bool HasChild1;

    BasicTreeNode* ChildNode2;
    bool HasChild2;

    int2 Pos;

    bool ContainsChunk;

    ChunkData* ChunkInfo;
}

public unsafe struct ChunkData
{
    int* BlockArray;
}

public struct WorldChunk
{

}

[BurstCompile]
public partial struct TilemapSystem : ISystem, ISystemStartStop
{
    NativeArray<int> TilemapArray;

    //BasicTreeNode* RootNode;

    public void OnStartRunning(ref SystemState state)
    {
        ref var TilemapSettingsInfo = ref SystemAPI.GetSingletonRW<TilemapSettingsData>().ValueRW;

        TilemapArray = new NativeArray<int>(TilemapSettingsInfo.TilemapSize*TilemapSettingsInfo.TilemapSize, Allocator.Persistent);

        //unsafe
        //{
        //    RootNode = (BasicTreeNode*)UnsafeUtility.Malloc(sizeof(BasicTreeNode), UnsafeUtility.AlignOf<BasicTreeNode>(), Allocator.Persistent);
        //}

        System.Diagnostics.Stopwatch Watch = new();
        Watch.Start();
        for (int i = 0; i < TilemapSettingsInfo.Trials; i++)
        {
            GenerateBlocksWithRegularArray(ref TilemapSettingsInfo);
        }
        Watch.Stop();
        Debug.Log("Regular Array: " + Watch.Elapsed/TilemapSettingsInfo.Trials);

        Watch.Reset();
        Watch.Start();
        for (int i = 0; i < TilemapSettingsInfo.Trials; i++)
        {
            //GenerateBlocksWithBasicTree(ref TilemapSettingsInfo);
        }
        Watch.Stop();
        Debug.Log("Basic Tree: " + Watch.Elapsed / TilemapSettingsInfo.Trials);
    }

    public void OnStopRunning(ref SystemState state)
    {
        TilemapArray.Dispose();

        //UnsafeUtility.Free(RootNode, Allocator.Persistent); // only works if there is one node
    }

    public int GenerateBlock(int2 Pos, Random Rand)
    {
        return Rand.NextInt();
    }

    [BurstCompile]
    public void GenerateBlocksWithRegularArray(ref TilemapSettingsData TilemapSettingsInfo)
    {
        var RegularArrayGeneratorJob = new RegularArrayGenerator
        {
            Tilemap = TilemapArray,
            GridWidth = TilemapSettingsInfo.TilemapSize,
            Seed = 3, // lol
            Pos = TilemapSettingsInfo.Pos,
            GenerationWidth = TilemapSettingsInfo.GenerationWidth
        };
        RegularArrayGeneratorJob.ScheduleParallel(TilemapArray.Length, 64, new JobHandle()).Complete();
    }

    public static int PosToIndex(int2 Pos, int GridWidth)
    {
        return Pos.y * GridWidth + Pos.x;
    }

    public static int2 IndexToPos(int Index, int GridWidth)
    {
        return new int2(Index % GridWidth, Index / GridWidth);
    }

    [BurstCompile]
    public struct RegularArrayGenerator : IJobFor
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<int> Tilemap;

        public int GridWidth;

        public uint Seed;

        public int2 Pos;

        public int GenerationWidth;

        public void Execute(int i)
        {
            int2 CurrentPos = IndexToPos(i, GenerationWidth) + Pos;
            int CurrentIndex = PosToIndex(CurrentPos, GridWidth);

            if (CurrentIndex > Tilemap.Length || CurrentIndex < 0)
            {
                return;
            }

            Random ParallelRandom = Random.CreateFromIndex((uint)(Seed + i));
            Tilemap[CurrentIndex] = ParallelRandom.NextInt(0, 10);
        }
    }
}
