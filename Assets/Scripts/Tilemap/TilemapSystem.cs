using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using Unity.Rendering;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
[UpdateAfter(typeof(PlayerSystem))]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct TilemapSystem : ISystem, ISystemStartStop
{
    NativeArray<byte> TilemapArray; // should never need more than 200 block types, at least I hope so
    NativeArray<bool> ChunksGenerated;

    NativeArray<VertexAttributeDescriptor> VertexAttributes;

    BlobAssetReference<BlobArray<BlockType>> BlockTypes;

    BlobAssetReference<BlobArray<Biome>> Biomes;

    int ChunkWidth;
    int ChunkWidthSquared;

    int BlockGridWidth;
    int ChunkGridWidth;

    uint Seed;

    float2 BiomeSeed;

    float2 BiomeScale;

    float TerrainNoiseScale;
    float AdditionToTerrainNoise;
    float PostTerrainNoiseScale;

    float SpriteWidth;
    float SpriteHeight;

    Random SafePosRandom;

    bool DebugChunk0;

    #region ISystem Methods

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TilemapSettingsData>();
        state.RequireForUpdate<Stats>();
    }

    [BurstCompile]
    public void OnStartRunning(ref SystemState state)
    {
        ref var TilemapSettingsInfo = ref SystemAPI.GetSingletonRW<TilemapSettingsData>().ValueRW;
        ref var BiomeSettingsInfo = ref SystemAPI.GetSingletonRW<BiomeSettingsData>().ValueRW;

        //float LowestValue = 100f;
        //float HighestValue = -100f;

        //for (int i = 0; i < TilemapSettingsInfo.Trials; i++)
        //{
        //    float NoiseValue = noise.snoise(TilemapSettingsInfo.TerrainNoiseScale * (float2)Random.CreateFromIndex((uint)i).NextInt2(0, int.MaxValue));

        //    if (NoiseValue > HighestValue)
        //    {
        //        HighestValue = NoiseValue;
        //    }

        //    if (NoiseValue < LowestValue)
        //    {
        //        LowestValue = NoiseValue;
        //    }
        //}

        //Debug.Log(HighestValue);
        //Debug.Log(LowestValue);

        //for (int i = 0; i < TilemapSettingsInfo.Trials; i++)
        //{
        //    float NoiseValue = noise.snoise(TilemapSettingsInfo.TerrainNoiseScale * (float2)Random.CreateFromIndex((uint)i).NextInt2(0, int.MaxValue));

        //    Debug.Log((int)math.round((NoiseValue + 0.696) * 2 + 1));
        //}

        BiomeScale = TilemapSettingsInfo.BiomeScale;

        TerrainNoiseScale = TilemapSettingsInfo.TerrainNoiseScale;
        PostTerrainNoiseScale = TilemapSettingsInfo.PostTerrainNoiseScale;
        AdditionToTerrainNoise = TilemapSettingsInfo.AdditionToTerrainNoise;

        DebugChunk0 = TilemapSettingsInfo.DebugChunk0;

        BlockTypes = TilemapSettingsInfo.BlockTypes;
        Biomes = BiomeSettingsInfo.Biomes;

        ChunkWidth = TilemapSettingsInfo.ChunkWidth;
        ChunkWidthSquared = ChunkWidth * ChunkWidth;

        BlockGridWidth = TilemapSettingsInfo.TilemapSize; // why is it called tilemap size???? ughhhhh

        Seed = (uint)SystemAPI.Time.ElapsedTime; // so sketchy
        SafePosRandom = Random.CreateFromIndex(Seed);

        BiomeSeed = SafePosRandom.NextFloat2(-5000, 5000); // SUPER SKETCHY

        SpriteWidth = TilemapSettingsInfo.SpriteWidth;
        SpriteHeight = TilemapSettingsInfo.SpriteHeight;

        ChunkGridWidth = TilemapSettingsInfo.TilemapSize / TilemapSettingsInfo.ChunkWidth;

        TilemapArray = new NativeArray<byte>(TilemapSettingsInfo.TilemapSize * TilemapSettingsInfo.TilemapSize, Allocator.Persistent);
        ChunksGenerated = new NativeArray<bool>(ChunkGridWidth * ChunkGridWidth, Allocator.Persistent);

        VertexAttributes = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Persistent);
        VertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
        VertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);

        ref Stats PlayerStats = ref SystemAPI.GetSingletonRW<Stats>().ValueRW;

        PlayerStats.Pos = FindSafePos();
        PlayerStats.ForceUpdate = true;
    }

    public void OnUpdate(ref SystemState state)
    {
        Entity TilemapEntity = SystemAPI.GetSingletonEntity<TilemapSettingsData>();

        var TilemapMeshManaged = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(TilemapEntity).Meshes[0];

        var MeshArray = Mesh.AllocateWritableMeshData(1);
        var MeshData = MeshArray[0];

        var PlayerCam = Object.FindObjectOfType<Camera>();

        int2 BottomLeftPos = (int2)((float3)PlayerCam.ScreenToWorldPoint(new float3(0, 0, 0))).xy; // z is 0 cause depth shouldn't matter due to orthographic camera

        int2 TopRightPos = (int2)((float3)PlayerCam.ScreenToWorldPoint(new float3(PlayerCam.pixelWidth, PlayerCam.pixelHeight, 0))).xy + new int2(5,5); // add 5,5 to make sure everything is rendered on the edge of the screen

        int ScreenWidth = TopRightPos.x - BottomLeftPos.x;
        int ScreenHeight = TopRightPos.y - BottomLeftPos.y;

        int RenderAmount = ScreenWidth * ScreenHeight;

        MeshData.SetVertexBufferParams(RenderAmount * 4 + 4, VertexAttributes); //4 vertices per block, plus 4 more for player
        MeshData.SetIndexBufferParams(RenderAmount * 6 + 6, IndexFormat.UInt32);

        MeshData.subMeshCount = 1; // temporary, needs to be replaced by something good that works out how many use what material, but that is pain

        var Vertices = MeshData.GetVertexData<Vertex>();
        var Indices = MeshData.GetIndexData<uint>();

        unsafe
        {
            UnsafeUtility.MemClear(Vertices.GetUnsafePtr(), Vertices.Length * sizeof(Vertex)); // native vertices and indices buffers ain't initialized
            UnsafeUtility.MemClear(Indices.GetUnsafePtr(), Indices.Length * sizeof(uint));
        }

        BurstUpdate(ref state, Vertices, Indices, RenderAmount);

        var TilemapToMeshJob = new TilemapToMesh()
        {
            TilemapArray = TilemapArray,
            BlockTypes = BlockTypes,
            Vertices = Vertices,
            Indices = Indices,
            SpriteWidth = SpriteWidth,
            SpriteHeight = SpriteHeight,
            ScreenWidth = ScreenWidth,
            BlockGridWidth = BlockGridWidth,
            ChunkWidth = ChunkWidth,
            ChunkWidthSquared = ChunkWidthSquared,
            ChunkGridWidth = ChunkGridWidth,
            BottomLeftOfScreen = BottomLeftPos
        };

        JobHandle TilemapToMeshHandle = TilemapToMeshJob.ScheduleParallel(RenderAmount, 64, new JobHandle());

        TilemapToMeshHandle.Complete(); // do the system handle nonsense sooner rather than later!!!!!!!!!

        Bounds SubMeshBounds = new Bounds()
        {
            center = PlayerCam.transform.position,
            extents = new float3(ScreenWidth, ScreenHeight, 50) / 2 // 50 should be enough
        };

        SubMeshDescriptor SubMeshInfo = new()
        {
            baseVertex = 0, // for now this is correct, but will be an issue eventually
            bounds = SubMeshBounds,
            firstVertex = 0,
            indexCount = 6 * RenderAmount + 6, // 2 triangles with each triangle needing 3 then that for every block.
            indexStart = 0, //potentially lol
            topology = MeshTopology.Triangles, // 3 indices per face
            vertexCount = 4 * RenderAmount + 4
        };

        MeshData.SetSubMesh(0, SubMeshInfo, MeshUpdateFlags.Default);

        MeshUpdateFlags MeshFlags = MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices; // very minor performance save lol

        Mesh.ApplyAndDisposeWritableMeshData(MeshArray, TilemapMeshManaged, MeshFlags);

        ref var TilemapBounds = ref SystemAPI.GetComponentRW<RenderBounds>(TilemapEntity).ValueRW;

        TilemapBounds.Value = new AABB()
        {
            Center = SubMeshInfo.bounds.center,
            Extents = SubMeshInfo.bounds.extents
        };
    }

    [BurstCompile]
    public void BurstUpdate(ref SystemState state, NativeArray<Vertex> Vertices, NativeArray<uint> Indices, int RenderAmount)
    {
        //if (DebugChunk0)
        //{
        //    ref var TilemapSettingsInfo = ref SystemAPI.GetSingletonRW<TilemapSettingsData>().ValueRW;

        //    TerrainNoiseScale = TilemapSettingsInfo.TerrainNoiseScale;
        //    PostTerrainNoiseScale = TilemapSettingsInfo.PostTerrainNoiseScale;
        //    AdditionToTerrainNoise = TilemapSettingsInfo.AdditionToTerrainNoise;

        //    DebugChunk0 = TilemapSettingsInfo.DebugChunk0;

        //    BlockTypes = TilemapSettingsInfo.BlockTypes;

        //    GenerateChunkOld(0).Complete();
        //}

        ref var PlayerStats = ref SystemAPI.GetSingletonRW<Stats>().ValueRW;

        int2 PlayerChunkPos = ChunkPosFromBlockPos((int2)PlayerStats.Pos, ChunkWidth);
        int PlayerChunkIndex = IndexFromPos(PlayerChunkPos, ChunkGridWidth);

        if (PlayerStats.HasMoved)
        {
            PlayerStats.HasMoved = false;

            CheckForCollisions(ref PlayerStats);

            if (!ChunksGenerated[PlayerChunkIndex])
            {
                GenerateChunk(PlayerChunkPos).Complete();
            }

            GenerateChunksAroundPlayer((int2)PlayerStats.Pos);
        }

        RenderPlayer(ref PlayerStats, Vertices, Indices, RenderAmount);

        if (!ChunksGenerated[PlayerChunkIndex])
        {
            Debug.Log("Something has gone terribly wrong!");
            Debug.Log($"ChunkPos {PlayerChunkPos} , ChunkIndex {PlayerChunkIndex}");
            return;
        }
    }

    [BurstCompile]
    public void OnStopRunning(ref SystemState state)
    {
        TilemapArray.Dispose();
        ChunksGenerated.Dispose();
        VertexAttributes.Dispose();
    }

    #endregion

    #region Tilemap Utility Functions
    // Chunk index is an index that would be valid in ChunksGenerated, a block index is an index that would be valid in TilemapArray
    // Local block index is basically a block inside a chunk, I think? So like say a chunk holds 10 by 10 blocks, then local block index of 2 would be local chunk coords of x1,y0 I think?

    // world pos from chunk index plus local pos from local block index gives up the correct world pos! I think so atleast

    // Everything commented above might be right, might be wrong... Trust the next comment more.

    // ChunkPos and ChunkIndex are valid for the ChunkGrid
    // BlockPos (aka WorldPos) and BlockIndex are valid for the BlockGrid.... but I'm lying as BlockIndex is usually never valid for the BlockGrid. Good luck!

    public static int IndexFromPos(int2 Pos, int GridWidth) // dangerous
    {
        return Pos.y * GridWidth + Pos.x;
    }

    public static int2 PosFromIndex(int Index, int GridWidth) // dangerous
    {
        return new int2(Index % GridWidth, Index / GridWidth);
    }

    // probably should inline this?
    public static int2 ChunkPosFromBlockPos(int2 BlockPos, int ChunkWidth)
    {
        return BlockPos / ChunkWidth;
    }

    public static int2 BlockPosFromChunkPos(int2 ChunkPos, int ChunkWidth)
    {
        return ChunkPos * ChunkWidth;
    }

    // add function to get chunk index from block index

    public static int BlockIndexFromChunkIndex(int ChunkIndex, int ChunkWidthSquared)
    {
        return ChunkIndex * ChunkWidthSquared;
    }

    public static unsafe ref T UnsafeElementAt<T>(NativeArray<T> array, int index) where T : struct
    {
        return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
    }

    #endregion

    #region Generation Functions, Jobs, and structs

    public static int FindClosestBiome(float2 BlockConditions, BlobAssetReference<BlobArray<Biome>> Biomes)
    {
        for (int i = 0; i < Biomes.Value.Length; i++)
        {
            if (IsColliding(BlockConditions, 0, Biomes.Value[i].Pos, Biomes.Value[i].Size))
            {
                return i;
            }
        }

        Debug.Log("biome not found");
        return 0; // fail safe biome
    }

    [BurstCompile]
    public void GenerateChunksAroundPlayer(int2 PlayerPos)
    {
        int2 PlayerChunkPos = ChunkPosFromBlockPos(PlayerPos, ChunkWidth);

        for (int i = 0; i < 9; i++)
        {
            int2 OffsetPos = PosFromIndex(i, 3) - 1;

            int UnsafeChunkIndex = IndexFromPos(OffsetPos + PlayerChunkPos, ChunkGridWidth);

            if (UnsafeChunkIndex <= 0 || UnsafeChunkIndex > ChunksGenerated.Length)
            {
                continue;
            }

            if (!ChunksGenerated[UnsafeChunkIndex])
            {
                GenerateChunk(math.clamp(PlayerChunkPos + OffsetPos, 0, ChunkGridWidth)).Complete();
            }
        }
    }

    public int2 FindSafePos()
    {
        int2 ChunkPos = SafePosRandom.NextInt2(ChunkGridWidth);

        int ChunkIndex = IndexFromPos(ChunkPos, ChunkGridWidth);

        GenerateChunk(ChunkPos).Complete();

        int BlockIndexStart = BlockIndexFromChunkIndex(ChunkIndex, ChunkWidthSquared);
        int2 WorldPosStart = BlockPosFromChunkPos(ChunkPos, ChunkWidth);

        for (int BI = 0; BI < ChunkWidthSquared; BI++)
        {
            int2 WorldPos = WorldPosStart + PosFromIndex(BI, ChunkWidth); // important lesson here, the chunk is our grid, so we use chunk width instead of chunk grid width
            int BlockIndex = BlockIndexStart + BI;

            byte TypeIndex = TilemapArray[BlockIndex];

            if (TypeIndex == 0) // make better soon
            {
                return WorldPos;
            }
        }

        return WorldPosStart; // fail deadly
    }

    public static byte GenerateBlock(int2 Pos, uint Seed, float2 BiomeSeed, float2 BiomeScale, float Scale, BlobAssetReference<BlobArray<Biome>> Biomes, BlobAssetReference<BlobArray<BlockType>> BlockTypes, int TrueIndex) // extremely bad, don't like
    {
        float2 BlockConditions = new float2(
            noise.snoise(new float2(Pos.x, Pos.y + BiomeSeed.x) * BiomeScale.x),
            noise.snoise(new float2(Pos.x, Pos.y + BiomeSeed.y) * BiomeScale.y)
            );

        BlockConditions = (BlockConditions + 1) * 0.5f; //make it so block conditions is in range of 0-1

        int BiomeIndex = FindClosestBiome(BlockConditions, Biomes);

        Biome BiomeInfo = Biomes.Value[BiomeIndex];

        float BlockNoise = noise.snoise(new float2(Pos.x + Seed, Pos.y) * Scale); // replace with per biome stuff

        for (int i = BiomeInfo.StartingBlockIndex; i < BiomeInfo.BlockLength + BiomeInfo.StartingBlockIndex; i++)
        {
            BlockType BlockInfo = BlockTypes.Value[i];

            if ((BlockNoise >= BlockInfo.MinNoise) && (BlockNoise < BlockInfo.MaxNoise))
            {
                return (byte)i;
            }
        }

        Random ParallelRandom = Random.CreateFromIndex((uint)(Seed + TrueIndex));

        for (int i = BiomeInfo.StartingPlantIndex; i < BiomeInfo.PlantLength + BiomeInfo.StartingPlantIndex; i++)
        {
            BlockType BlockInfo = BlockTypes.Value[i];

            if (ParallelRandom.NextFloat() < BlockInfo.Chance)
            {
                return (byte)i;
            }
        }

        return 0;

        //return Biomes.Value[BiomeIndex].StartingBlockIndex;

        //return (byte)math.clamp(math.round((noise.snoise(new float2(Pos.x + Seed, Pos.y) * Scale) + AdditionToNoise) * PostScale), 0, AmountOfBlockTypes - 1);
    }

    public JobHandle GenerateChunk(int2 ChunkPos, JobHandle Dependency = new()) // do we want to burst compile this?
    {
        int ChunkIndex = IndexFromPos(ChunkPos, ChunkGridWidth);

        int2 BlockPos = BlockPosFromChunkPos(ChunkPos, ChunkWidth);

        int BlockIndex = BlockIndexFromChunkIndex(ChunkIndex, ChunkWidthSquared);

        ChunksGenerated[ChunkIndex] = true;

        var ChunkGeneratorJob = new ChunkGenerator
        {
            Tilemap = TilemapArray,
            StartingIndex = BlockIndex,
            GridWidth = BlockGridWidth,
            ChunkWidth = ChunkWidth,
            ChunkWorldPos = BlockPos,
            Seed = Seed,
            BiomeSeed = BiomeSeed,
            BiomeScale = BiomeScale,
            TerrainNoiseScale = TerrainNoiseScale,
            AdditionToNoise = AdditionToTerrainNoise,
            PostScale = PostTerrainNoiseScale,
            AmountOfBlockTypes = (byte)BlockTypes.Value.Length,
            Biomes = Biomes,
            BlockTypes = BlockTypes
        };

        return ChunkGeneratorJob.ScheduleParallel(ChunkWidthSquared, 64, Dependency);
    }

    [BurstCompile]
    public struct ChunkGenerator : IJobFor
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> Tilemap;

        public int StartingIndex;

        public int GridWidth;

        public int ChunkWidth;

        public int2 ChunkWorldPos;

        public uint Seed;

        public float2 BiomeSeed;

        public float2 BiomeScale;

        public float TerrainNoiseScale;

        public float AdditionToNoise;

        public float PostScale;

        public byte AmountOfBlockTypes;

        [ReadOnly]
        public BlobAssetReference<BlobArray<Biome>> Biomes;

        [ReadOnly]
        public BlobAssetReference<BlobArray<BlockType>> BlockTypes;

        public void Execute(int i)
        {
            int2 BlockLocalPos = PosFromIndex(i, ChunkWidth);
            int TrueIndex = StartingIndex + i;

            if (TrueIndex > Tilemap.Length || TrueIndex < 0)
            {
                Debug.Log("Outside of map, what the hell???");
                return;
            }

            int2 BlockWorldPos = BlockLocalPos + ChunkWorldPos;

            byte BlockTypeIndex = GenerateBlock(BlockWorldPos, Seed, BiomeSeed, BiomeScale, TerrainNoiseScale, Biomes, BlockTypes, TrueIndex);

            Tilemap[TrueIndex] = BlockTypeIndex;
        }
    }

    #endregion

    #region Mesh Functions, Jobs, and structs

    public void RenderPlayer(ref Stats PlayerStats, NativeArray<Vertex> Vertices, NativeArray<uint> Indices, int RenderAmount)
    {
        int VertexStart = RenderAmount * 4; // if every tile takes up 4 vertices then we use i * 4 to get the correct starting vertex
        int IndexStart = RenderAmount * 6; // read above and replace some words, and you might understand my nonsense

        UnsafeElementAt(Vertices, VertexStart).Pos = new float3(PlayerStats.Pos, 0) + new float3(0.5f * PlayerStats.Size.x, 0.5f * PlayerStats.Size.y, 0); // top right
        UnsafeElementAt(Vertices, VertexStart).UV = new float2(SpriteWidth, SpriteHeight);

        UnsafeElementAt(Vertices, VertexStart + 1).Pos = new float3(PlayerStats.Pos, 0) + new float3(0.5f * PlayerStats.Size.x, -0.5f * PlayerStats.Size.y, 0); // top left
        UnsafeElementAt(Vertices, VertexStart + 1).UV = new float2(0, SpriteHeight);

        UnsafeElementAt(Vertices, VertexStart + 2).Pos = new float3(PlayerStats.Pos, 0) + new float3(-0.5f * PlayerStats.Size.x, 0.5f * PlayerStats.Size.y, 0); // bottom right
        UnsafeElementAt(Vertices, VertexStart + 2).UV = new float2(SpriteWidth, 0);

        UnsafeElementAt(Vertices, VertexStart + 3).Pos = new float3(PlayerStats.Pos, 0) + new float3(-0.5f * PlayerStats.Size.x, -0.5f * PlayerStats.Size.y, 0); // bottom left
        UnsafeElementAt(Vertices, VertexStart + 3).UV = 0;

        uint UVertexStart = (uint)VertexStart;

        Indices[IndexStart] = UVertexStart;
        Indices[IndexStart + 1] = UVertexStart + 1;
        Indices[IndexStart + 2] = UVertexStart + 2;

        Indices[IndexStart + 3] = UVertexStart + 1;
        Indices[IndexStart + 4] = UVertexStart + 3;
        Indices[IndexStart + 5] = UVertexStart + 2;
    }

    public struct Vertex // this has to match the VertexAttributes
    {
        public float3 Pos;
        public float2 UV; // is this precise enough?
    }

    [BurstCompile]
    struct TilemapToMesh : IJobFor
    {
        [ReadOnly]
        public NativeArray<byte> TilemapArray;

        [ReadOnly]
        public BlobAssetReference<BlobArray<BlockType>> BlockTypes;

        [NativeDisableContainerSafetyRestriction]
        [WriteOnly]
        public NativeArray<Vertex> Vertices;

        [NativeDisableContainerSafetyRestriction]
        [WriteOnly]
        public NativeArray<uint> Indices;

        [ReadOnly]
        public float SpriteWidth;

        [ReadOnly]
        public float SpriteHeight;

        [ReadOnly]
        public int ScreenWidth;

        [ReadOnly]
        public int BlockGridWidth;

        [ReadOnly]
        public int ChunkWidth;

        [ReadOnly]
        public int ChunkWidthSquared;

        [ReadOnly]
        public int ChunkGridWidth;

        [ReadOnly]
        public int2 BottomLeftOfScreen;

        public void Execute(int i)
        {
            int2 iWorldPos = PosFromIndex(i, ScreenWidth);

            int2 TrueWorldPos = iWorldPos + BottomLeftOfScreen;

            if ((TrueWorldPos.x < 0 || TrueWorldPos.y < 0) || (TrueWorldPos.x > BlockGridWidth || TrueWorldPos.y > BlockGridWidth))
            {
                return;
            }

            int2 ChunkPos = ChunkPosFromBlockPos(TrueWorldPos, ChunkWidth);
            int ChunkIndex = IndexFromPos(ChunkPos, ChunkGridWidth);
            int BlockIndexStart = ChunkIndex * ChunkWidthSquared;

            int2 WorldPosStart = BlockPosFromChunkPos(ChunkPos, ChunkWidth);

            int2 LocalPos = TrueWorldPos - WorldPosStart;

            int LocalIndex = IndexFromPos(LocalPos, ChunkWidth);

            int BlockIndex = BlockIndexStart + LocalIndex;

            byte BlockTypeIndex = TilemapArray[BlockIndex];

            if (BlockTypeIndex == 0)
            {
                return;
            }

            BlockType BlockInfo = BlockTypes.Value[BlockTypeIndex];

            int VertexStart = i * 4; // if every tile takes up 4 vertices then we use i * 4 to get the correct starting vertex
            int IndexStart = i * 6; // read above and replace some words, and you might understand my nonsense

            UnsafeElementAt(Vertices, VertexStart).Pos = new float3(TrueWorldPos, BlockInfo.Depth) + new float3(0.5f * BlockInfo.RenderingSize.x, 0.5f * BlockInfo.RenderingSize.y, 0); // top right
            UnsafeElementAt(Vertices, VertexStart).UV = BlockInfo.UV + new float2(SpriteWidth, SpriteHeight);

            UnsafeElementAt(Vertices, VertexStart + 1).Pos = new float3(TrueWorldPos, BlockInfo.Depth) + new float3(0.5f * BlockInfo.RenderingSize.x, -0.5f * BlockInfo.RenderingSize.y, 0); // bottom right
            UnsafeElementAt(Vertices, VertexStart + 1).UV = BlockInfo.UV + new float2(SpriteWidth, 0);

            UnsafeElementAt(Vertices, VertexStart + 2).Pos = new float3(TrueWorldPos, BlockInfo.Depth) + new float3(-0.5f * BlockInfo.RenderingSize.x, 0.5f * BlockInfo.RenderingSize.y, 0); // top left
            UnsafeElementAt(Vertices, VertexStart + 2).UV = BlockInfo.UV + new float2(0, SpriteHeight);

            UnsafeElementAt(Vertices, VertexStart + 3).Pos = new float3(TrueWorldPos, BlockInfo.Depth) + new float3(-0.5f * BlockInfo.RenderingSize.x, -0.5f * BlockInfo.RenderingSize.y, 0); // bottom left
            UnsafeElementAt(Vertices, VertexStart + 3).UV = BlockInfo.UV;

            uint UVertexStart = (uint)VertexStart;

            Indices[IndexStart] = UVertexStart;
            Indices[IndexStart + 1] = UVertexStart + 1;
            Indices[IndexStart + 2] = UVertexStart + 2;

            Indices[IndexStart + 3] = UVertexStart + 1;
            Indices[IndexStart + 4] = UVertexStart + 3;
            Indices[IndexStart + 5] = UVertexStart + 2;
        }
    }

    #endregion

    #region Collision Stuff

    [BurstCompile]
    public void CheckForCollisions(ref Stats PlayerStats)
    {
        float2 PlayerPos = PlayerStats.Pos;
        float2 PlayerSize = PlayerStats.Size;

        int2 ClosestBlockPos = (int2)math.round(PlayerPos);

        for (int i = 0; i < 9; i++)
        {
            int2 OffsetPos = PosFromIndex(i, 3) - 1;

            int2 BlockPos = ClosestBlockPos + OffsetPos;

            BlockPos = math.clamp(BlockPos, 0, int.MaxValue);

            int2 ChunkPos = ChunkPosFromBlockPos(BlockPos, ChunkWidth);
            int ChunkIndex = IndexFromPos(ChunkPos, ChunkGridWidth);
            int BlockIndexStart = ChunkIndex * ChunkWidthSquared;

            int2 WorldPosStart = BlockPosFromChunkPos(ChunkPos, ChunkWidth);

            int2 LocalPos = BlockPos - WorldPosStart;

            int LocalIndex = IndexFromPos(LocalPos, ChunkWidth);

            int BlockIndex = BlockIndexStart + LocalIndex;

            byte BlockTypeIndex = TilemapArray[BlockIndex];

            if (BlockTypeIndex == 0) // you can't collide with nothing
            {
                continue;
            }

            BlockType BlockInfo = BlockTypes.Value[BlockTypeIndex];

            if (IsColliding(PlayerPos + PlayerSize * -0.5f + 0.5f, PlayerSize, BlockPos + BlockInfo.CollisionSize * -0.5f + 0.5f, BlockInfo.CollisionSize)) // aabb usually expect pos to be the bottom left corner of the sprite, which is not true for us, hence the weird multiplication of positions and stuff
            {
                if (BlockInfo.StrengthToCross < PlayerStats.Strength)
                {
                    if (BlockInfo.Behaviour.HasFlag(CollisionBehaviour.Consume))
                    {
                        TilemapArray[BlockIndex] = 0;
                    }

                    PlayerStats += BlockInfo.StatsChange;

                    continue;
                }

                PlayerStats.Pos = PlayerStats.PreviousPos;
                return;
            }
        }
    }

    public static bool IsColliding(float2 Pos1, float2 Size1, float2 Pos2, float2 Size2)
    {
        if ( // https://developer.mozilla.org/en-US/docs/Games/Techniques/2D_collision_detection so good
            Pos1.x < Pos2.x + Size2.x &&
            Pos1.x + Size1.x > Pos2.x &&
            Pos1.y < Pos2.y + Size2.y &&
            Pos1.y + Size1.y > Pos2.y
            )
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    #endregion
}