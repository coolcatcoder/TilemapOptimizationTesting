using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using Unity.Rendering;

[BurstCompile]
public partial struct TilemapSystem : ISystem, ISystemStartStop
{
    NativeArray<int> TilemapArray;
    NativeArray<bool> ChunksGenerated;
    bool ReplaceMesh; // basically just renders the blocks when this is true (not needed every frame, only when stuff changes)

    int ChunkWidth;
    int ChunkWidthSquared;

    int BlockGridWidth;

    uint Seed;

    #region ISystem Methods

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TilemapSettingsData>();
    }

    public void OnStartRunning(ref SystemState state)
    {
        ref var TilemapSettingsInfo = ref SystemAPI.GetSingletonRW<TilemapSettingsData>().ValueRW;

        ChunkWidth = TilemapSettingsInfo.ChunkWidth;
        ChunkWidthSquared = ChunkWidth * ChunkWidth;
        BlockGridWidth = TilemapSettingsInfo.TilemapSize; // why is it called tilemap size???? ughhhhh
        Seed = 3; //temporary lol

        TilemapArray = new NativeArray<int>(TilemapSettingsInfo.TilemapSize * TilemapSettingsInfo.TilemapSize, Allocator.Persistent);
        ChunksGenerated = new NativeArray<bool>((TilemapSettingsInfo.TilemapSize / TilemapSettingsInfo.ChunkWidth) * (TilemapSettingsInfo.TilemapSize / TilemapSettingsInfo.ChunkWidth), Allocator.Persistent);
        Debug.Log(ChunksGenerated.Length);

        System.Diagnostics.Stopwatch Watch = new();
        Watch.Start();
        for (int i = 0; i < TilemapSettingsInfo.Trials; i++)
        {
            GenerateChunk(0).Complete();
        }
        Watch.Stop();
        Debug.Log("Weird Chunks: " + Watch.Elapsed / TilemapSettingsInfo.Trials);

        ReplaceMesh = true;
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!ReplaceMesh)
        {
            return;
        }

        ReplaceMesh = false;

        //state.EntityManager.GetShared

        for (int i = 0; i < ChunksGenerated.Length; i++)
        {
            if (ChunksGenerated[i])
            {
                Debug.Log(WorldPosFromChunkIndex(i, BlockGridWidth));
            }
        }
    }

    public void OnStopRunning(ref SystemState state)
    {
        TilemapArray.Dispose();
        ChunksGenerated.Dispose();

        //UnsafeUtility.Free(RootNode, Allocator.Persistent); // only works if there is one node
    }

    #endregion

    #region Tilemap Utility Functions
    // Chunk index is an index that would be valid in ChunksGenerated, a block index is an index that would be valid in TilemapArray
    // Local block index is basically a block inside a chunk, I think? So like say a chunk holds 10 by 10 blocks, then local block index of 2 would be local chunk coords of x1,y0 I think?

    // world pos from chunk index plus local pos from local block index gives up the correct world pos! I think so atleast

    public static int ChunkIndexFromWorldPos(int2 WorldPos, int ChunkGridWidth)
    {
        return WorldPos.y * ChunkGridWidth + WorldPos.x;
    }

    public static int GetChunkGridWidth(int GridWidth, int ChunkWidth)
    {
        return GridWidth / ChunkWidth;
    }

    public static int BlockIndexFromChunkIndex(int ChunkIndex, int ChunkWidthSquared)
    {
        return ChunkIndex * ChunkWidthSquared;
    }

    public static int2 LocalPosFromLocalBlockIndex(int LocalBlockIndex, int ChunkWidth)
    {
        return new int2(LocalBlockIndex % ChunkWidth, LocalBlockIndex / ChunkWidth);
    }

    public static int2 WorldPosFromChunkIndex(int ChunkIndex, int BlockGridWidth)
    {
        return new int2(ChunkIndex % BlockGridWidth, ChunkIndex / BlockGridWidth);
    }

    #endregion

    #region Generation Functions and Jobs

    public static int GenerateBlock(int2 Pos, Random Rand)
    {
        return Rand.NextInt();
    }

    [BurstCompile]
    public JobHandle GenerateChunk(int ChunkIndex, JobHandle Dependency = new())
    {
        ChunksGenerated[ChunkIndex] = true;

        var ChunkGeneratorJob = new ChunkGenerator
        {
            Tilemap = TilemapArray,
            StartingIndex = BlockIndexFromChunkIndex(ChunkIndex, ChunkWidthSquared),
            GridWidth = BlockGridWidth,
            ChunkWidth = ChunkWidth,
            ChunkWorldPos = WorldPosFromChunkIndex(ChunkIndex, BlockGridWidth),
            Seed = Seed
        };
        return ChunkGeneratorJob.ScheduleParallel(ChunkWidthSquared, 64, Dependency);
    }

    [BurstCompile]
    public struct ChunkGenerator : IJobFor
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<int> Tilemap;

        public int StartingIndex;

        public int GridWidth;

        public int ChunkWidth;

        public int2 ChunkWorldPos;

        public uint Seed;

        public void Execute(int i)
        {
            int2 BlockLocalPos = new int2(i % ChunkWidth, i / ChunkWidth);
            int TrueIndex = StartingIndex + i;

            if (TrueIndex > Tilemap.Length || TrueIndex < 0)
            {
                return;
            }


            Random ParallelRandom = Random.CreateFromIndex((uint)(Seed + i));
            Tilemap[TrueIndex] = GenerateBlock(BlockLocalPos + ChunkWorldPos, ParallelRandom);
        }
    }

    #endregion
}