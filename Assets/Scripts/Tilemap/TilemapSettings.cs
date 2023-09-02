using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System;

public class TilemapSettings : MonoBehaviour
{
    public int TilemapSize;
    public int Trials;
    public int2 Pos;
    public int ChunkWidth;

    public float2 BiomeScale;

    public float TerrainNoiseScale;
    public float AdditionToTerrainNoise;
    public float PostTerrainNoiseScale;

    public bool DebugChunk0;
    public bool DebugBiomeDraw;

    public int SpriteGridWidth;
    public int SpriteGridHeight;

    public BlockTypeMono[] Blocktypes;
}

public class TilemapBaker : Baker<TilemapSettings>
{
    public override void Bake(TilemapSettings authoring)
    {
        if (authoring.Blocktypes.Length <= 0)
        {
            Debug.LogError("Can't bake tilemap due to lack of BlockTypes.");
            return;
        }

        if (authoring.SpriteGridHeight <= 0 || authoring.SpriteGridWidth <= 0)
        {
            Debug.LogError("Can't bake tilemap because SpriteGridHeight or SpriteGridWidth is 0 or below.");
            return;
        }

        var BlockTypesBuilder = new BlobBuilder(Allocator.Temp);
        ref var BlockTypesArray = ref BlockTypesBuilder.ConstructRoot<BlobArray<BlockType>>();

        BlobBuilderArray<BlockType> BlockTypesArrayBuilder = BlockTypesBuilder.Allocate(ref BlockTypesArray, authoring.Blocktypes.Length);

        float SpriteWidth = 1f / authoring.SpriteGridWidth;

        for (int i = 0; i < authoring.Blocktypes.Length; i++)
        {
            var BT = authoring.Blocktypes[i];

            BlockTypesArrayBuilder[i] = new BlockType
            {
                UV = new float2(SpriteWidth*BT.BlockSprite,0), // bottom left hand corner should be (1/NumSprites*Sprite, 0)
                //BlockMat = BT.BlockMat,
                Depth = BT.Depth,
                RenderingSize = BT.RenderingSize,
                CollisionSize = BT.CollisionSize,
                Behaviour = BT.Behaviour,
                StrengthToCross = BT.StrengthToCross,
                //StatsChange = new Stats()
                //{
                //    Size = BT.StatsChange.Size,
                //    Health = BT.StatsChange.Health,
                //    Stamina = BT.StatsChange.Stamina,
                //    Strength = BT.StatsChange.Strength,
                //    Speed = BT.StatsChange.Speed,
                //    SprintSpeed = BT.StatsChange.SprintSpeed,
                //    WalkSpeed = BT.StatsChange.WalkSpeed
                //},
                MinNoise = BT.MinNoise,
                MaxNoise = BT.MaxNoise,
                Chance = BT.PercentChance/100
            };
        }

        var entity = GetEntity(TransformUsageFlags.Renderable);

        AddComponent(entity, new TilemapSettingsData
        {
            TilemapSize = authoring.TilemapSize,
            Trials = authoring.Trials,
            Pos = authoring.Pos,
            ChunkWidth = authoring.ChunkWidth,
            BiomeScale = authoring.BiomeScale,
            TerrainNoiseScale = authoring.TerrainNoiseScale,
            AdditionToTerrainNoise = authoring.AdditionToTerrainNoise,
            PostTerrainNoiseScale = authoring.PostTerrainNoiseScale,
            DebugChunk0 = authoring.DebugChunk0,
            DebugBiomeDraw = authoring.DebugBiomeDraw,
            SpriteWidth = SpriteWidth,
            SpriteHeight = 1f/authoring.SpriteGridHeight,
            BlockTypes = BlockTypesBuilder.CreateBlobAssetReference<BlobArray<BlockType>>(Allocator.Persistent),
        });

        BlockTypesBuilder.Dispose();
    }
}

public struct TilemapSettingsData : IComponentData
{
    public int TilemapSize;
    public int Trials;
    public int2 Pos;
    public int ChunkWidth;

    public float2 BiomeScale;

    public float TerrainNoiseScale;
    public float AdditionToTerrainNoise;
    public float PostTerrainNoiseScale;

    public bool DebugChunk0;
    public bool DebugBiomeDraw;

    public float SpriteWidth;
    public float SpriteHeight;

    public BlobAssetReference<BlobArray<BlockType>> BlockTypes;
}

[System.Serializable]
public struct BlockTypeMono // no id int needed, just use position in array
{
    public string BlockName; // makes it easier to tell which blocks are what in the array
    public int BlockSprite;
    //public BlockMaterial BlockMat;
    public int Depth;

    public float2 RenderingSize;
    public float2 CollisionSize;

    public CollisionBehaviour Behaviour;

    public uint StrengthToCross;

    public StatsMono StatsChange;

    public float MinNoise;
    public float MaxNoise;

    public float PercentChance;
}

public struct BlockType
{
    public float2 UV;
    //public BlockMaterial BlockMat; removed for now
    public int Depth;

    public float2 RenderingSize;
    public float2 CollisionSize;

    public CollisionBehaviour Behaviour;

    public uint StrengthToCross;

    //public Stats StatsChange;

    public float MinNoise;
    public float MaxNoise;

    public float Chance;
}

[System.Serializable]
public struct StatsMono // makes it easier to create block types
{
    public float2 Size;

    public int Health;
    public int Stamina;
    public int Strength;

    public float Speed;
    public float SprintSpeed;
    public float WalkSpeed;
}

public enum BlockMaterial // do we actually plan to ever use this?
{
    Transparent = 0,
    Opaque = 1,
    TransparentAnimated = 2,
    OpaqueAnimated = 3
}

public enum CollisionBehaviour
{
    None = 0,
    Consume = 1
}