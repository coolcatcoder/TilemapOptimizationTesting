using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

public class TilemapSettings : MonoBehaviour
{
    public int TilemapSize;
    public int Trials;
}

public class TilemapBaker : Baker<TilemapSettings>
{
    public override void Bake(TilemapSettings authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new TilemapSettingsData
        {
            TilemapSize = authoring.TilemapSize,
            Trials = authoring.Trials
        });
    }
}

public struct TilemapSettingsData : IComponentData
{
    public int TilemapSize;
    public int Trials;
}

[BurstCompile]
public partial struct TileMapSystem : ISystem, ISystemStartStop
{
    NativeArray<int> TilemapArray;

    public void OnStartRunning(ref SystemState state)
    {
        ref var TilemapSettingsInfo = ref SystemAPI.GetSingletonRW<TilemapSettingsData>().ValueRW;

        TilemapArray = new NativeArray<int>(TilemapSettingsInfo.TilemapSize*TilemapSettingsInfo.TilemapSize, Allocator.Persistent);

        System.Diagnostics.Stopwatch Watch = new();
        Watch.Start();
        for (int i = 0; i < TilemapSettingsInfo.Trials; i++)
        {
            GenerateBlocksWithRegularArray();
        }
        Watch.Stop();
        Debug.Log("Regular Array: " + Watch.Elapsed/TilemapSettingsInfo.Trials);
    }

    public void OnStopRunning(ref SystemState state)
    {
        TilemapArray.Dispose();
    }

    [BurstCompile]
    public void GenerateBlocksWithRegularArray()
    {
        var RegularArrayGeneratorJob = new RegularArrayGenerator
        {
            Tilemap = TilemapArray,
            Seed = 3, // lol
        };
        RegularArrayGeneratorJob.ScheduleParallel(TilemapArray.Length, 64, new JobHandle()).Complete();
    }

    [BurstCompile]
    public struct RegularArrayGenerator : IJobFor
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<int> Tilemap;

        public uint Seed;

        public void Execute(int i)
        {
            Random ParallelRandom = Random.CreateFromIndex((uint)(Seed + i));
            Tilemap[i] = ParallelRandom.NextInt(0, 10);
        }
    }
}
