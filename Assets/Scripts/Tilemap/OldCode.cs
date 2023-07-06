//using Unity.Burst;
//using Unity.Entities;
//using Unity.Mathematics;
//using Unity.Collections;
//using Unity.Jobs;
//using UnityEngine;
//using Random = Unity.Mathematics.Random;
//using Unity.Rendering;
//using UnityEngine.Rendering;
//using Unity.Collections.LowLevel.Unsafe;

//[BurstCompile]
//[UpdateAfter(typeof(PlayerSystem))]
//[UpdateInGroup(typeof(SimulationSystemGroup))]
//public partial struct TilemapSystemOld : ISystem, ISystemStartStop
//{
//    NativeArray<byte> TilemapArray; // should never need more than 200 block types, at least I hope so
//    NativeArray<bool> ChunksGenerated;

//    NativeList<BlockMeshElement> BlocksToRender;
//    NativeArray<VertexAttributeDescriptor> VertexAttributes;

//    BlobAssetReference<BlobArray<BlockType>> BlockTypes;

//    [System.Obsolete]
//    bool ReplaceMesh; // basically just renders the blocks when this is true (not needed every frame, only when stuff changes)

//    int ChunkWidth;
//    int ChunkWidthSquared;

//    int BlockGridWidth;
//    int ChunkGridWidth;

//    uint Seed;

//    float TerrainNoiseScale;
//    float AdditionToTerrainNoise;
//    float PostTerrainNoiseScale;

//    float SpriteWidth;
//    float SpriteHeight;

//    Random SafePosRandom;

//    bool DebugChunk0;

//    Mesh.MeshDataArray MeshArray;
//    Mesh.MeshData MeshData;

//    NativeArray<Vertex> Vertices;
//    NativeArray<uint> Indices;

//    #region ISystem Methods

//    [BurstCompile]
//    public void OnCreate(ref SystemState state)
//    {
//        state.RequireForUpdate<TilemapSettingsData>();
//        state.RequireForUpdate<Stats>();
//    }

//    [BurstCompile]
//    public void OnStartRunning(ref SystemState state)
//    {
//        ref var TilemapSettingsInfo = ref SystemAPI.GetSingletonRW<TilemapSettingsData>().ValueRW;

//        //float LowestValue = 100f;
//        //float HighestValue = -100f;

//        //for (int i = 0; i < TilemapSettingsInfo.Trials; i++)
//        //{
//        //    float NoiseValue = noise.snoise(TilemapSettingsInfo.TerrainNoiseScale * (float2)Random.CreateFromIndex((uint)i).NextInt2(0, int.MaxValue));

//        //    if (NoiseValue > HighestValue)
//        //    {
//        //        HighestValue = NoiseValue;
//        //    }

//        //    if (NoiseValue < LowestValue)
//        //    {
//        //        LowestValue = NoiseValue;
//        //    }
//        //}

//        //Debug.Log(HighestValue);
//        //Debug.Log(LowestValue);

//        //for (int i = 0; i < TilemapSettingsInfo.Trials; i++)
//        //{
//        //    float NoiseValue = noise.snoise(TilemapSettingsInfo.TerrainNoiseScale * (float2)Random.CreateFromIndex((uint)i).NextInt2(0, int.MaxValue));

//        //    Debug.Log((int)math.round((NoiseValue + 0.696) * 2 + 1));
//        //}

//        TerrainNoiseScale = TilemapSettingsInfo.TerrainNoiseScale;
//        PostTerrainNoiseScale = TilemapSettingsInfo.PostTerrainNoiseScale;
//        AdditionToTerrainNoise = TilemapSettingsInfo.AdditionToTerrainNoise;

//        DebugChunk0 = TilemapSettingsInfo.DebugChunk0;

//        BlockTypes = TilemapSettingsInfo.BlockTypes;

//        ChunkWidth = TilemapSettingsInfo.ChunkWidth;
//        ChunkWidthSquared = ChunkWidth * ChunkWidth;

//        BlockGridWidth = TilemapSettingsInfo.TilemapSize; // why is it called tilemap size???? ughhhhh

//        Seed = (uint)SystemAPI.Time.ElapsedTime; // so sketchy
//        SafePosRandom = Random.CreateFromIndex(Seed);

//        SpriteWidth = TilemapSettingsInfo.SpriteWidth;
//        SpriteHeight = TilemapSettingsInfo.SpriteHeight;

//        ChunkGridWidth = TilemapSettingsInfo.TilemapSize / TilemapSettingsInfo.ChunkWidth;

//        TilemapArray = new NativeArray<byte>(TilemapSettingsInfo.TilemapSize * TilemapSettingsInfo.TilemapSize, Allocator.Persistent);
//        ChunksGenerated = new NativeArray<bool>(ChunkGridWidth * ChunkGridWidth, Allocator.Persistent);
//        BlocksToRender = new NativeList<BlockMeshElement>(TilemapSettingsInfo.MaxBlocksToRender, Allocator.Persistent);

//        VertexAttributes = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Persistent);
//        VertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
//        VertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);

//        ref Stats PlayerStats = ref SystemAPI.GetSingletonRW<Stats>().ValueRW;

//        PlayerStats.Pos = FindSafePos();
//        PlayerStats.ForceUpdate = true;

//        ReplaceMesh = true;
//    }

//    public void OnUpdate(ref SystemState state)
//    {
//        Entity TilemapEntity = SystemAPI.GetSingletonEntity<TilemapSettingsData>();

//        var TilemapMeshManaged = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(TilemapEntity).Meshes[0];

//        MeshArray = Mesh.AllocateWritableMeshData(1);
//        MeshData = MeshArray[0];

//        int BlockAmount = ChunkWidthSquared * 9;

//        MeshData.SetVertexBufferParams(BlockAmount * 4 + 4, VertexAttributes); //4 vertices per block, plus 4 more for player
//        MeshData.SetIndexBufferParams(BlockAmount * 6 + 6, IndexFormat.UInt32);

//        MeshData.subMeshCount = 1; // temporary, needs to be replaced by something good that works out how many use what material, but that is pain

//        Vertices = MeshData.GetVertexData<Vertex>();
//        Indices = MeshData.GetIndexData<uint>();

//        var ReadOnlyMeshArray = Mesh.AcquireReadOnlyMeshData(TilemapMeshManaged);
//        var ReadOnlyMeshData = ReadOnlyMeshArray[0];

//        unsafe
//        {
//            UnsafeUtility.MemCpy(Vertices.GetUnsafePtr(), ReadOnlyMeshData.GetVertexData<Vertex>().GetUnsafePtr(), Vertices.Length);
//            UnsafeUtility.MemCpy(Indices.GetUnsafePtr(), ReadOnlyMeshData.GetIndexData<uint>().GetUnsafePtr(), Indices.Length);
//        }

//        BurstUpdate(ref state);

//        ref var TilemapBounds = ref SystemAPI.GetComponentRW<RenderBounds>(TilemapEntity).ValueRW;

//        MeshUpdateFlags MeshFlags = MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices; // very minor performance save lol

//        Mesh.ApplyAndDisposeWritableMeshData(MeshArray, TilemapMeshManaged, MeshFlags);

//        var SubMeshInfo = TilemapMeshManaged.GetSubMesh(0);
//        TilemapBounds.Value = new AABB()
//        {
//            Center = SubMeshInfo.bounds.center,
//            Extents = SubMeshInfo.bounds.extents
//        };


//        // to do:
//        // Player movement shouldn't trigger full mesh redo
//        // Only render 9 closest chunks!!! This will save so much performance!!!!!
//        // don't use a list, instead treat the vertex array as a 2d grid (convert to index using utility methods) this means we can grab the vertex array from the mesh, modify only what is needed, then set the mesh to have that modified vertex array, nice and simple, and SUPER PERFORMANCE. Player and other offgrid beings can be stored in a list. Plants are not offgrid beings, they belong to the grid and simply have an pos offset. Further thought: How the hell do we plan to deal with empty vertices???? Potentially draw nothing as a sprite with full transparency


//        //if (DebugChunk0)
//        //{
//        //    ref var TilemapSettingsInfo = ref SystemAPI.GetSingletonRW<TilemapSettingsData>().ValueRW;

//        //    TerrainNoiseScale = TilemapSettingsInfo.TerrainNoiseScale;
//        //    PostTerrainNoiseScale = TilemapSettingsInfo.PostTerrainNoiseScale;
//        //    AdditionToTerrainNoise = TilemapSettingsInfo.AdditionToTerrainNoise;

//        //    DebugChunk0 = TilemapSettingsInfo.DebugChunk0;

//        //    BlockTypes = TilemapSettingsInfo.BlockTypes;

//        //    //ChunkWidth = TilemapSettingsInfo.ChunkWidth;
//        //    //ChunkWidthSquared = ChunkWidth * ChunkWidth;

//        //    //BlockGridWidth = TilemapSettingsInfo.TilemapSize;

//        //    //Seed = (uint)SystemAPI.Time.ElapsedTime; // so sketchy
//        //    //SafePosRandom = Random.CreateFromIndex(Seed);

//        //    //SpriteWidth = TilemapSettingsInfo.SpriteWidth;
//        //    //SpriteHeight = TilemapSettingsInfo.SpriteHeight;

//        //    //ChunkGridWidth = TilemapSettingsInfo.TilemapSize / TilemapSettingsInfo.ChunkWidth;

//        //    GenerateChunk(0).Complete(); // replace with custom chunk generator that accounts for updates to component
//        //    ReplaceMesh = true;
//        //}

//        //ref var PlayerStats = ref SystemAPI.GetSingletonRW<Stats>().ValueRW;

//        //int2 PlayerChunkPos = ChunkPosFromBlockPos((int2)PlayerStats.Pos, ChunkWidth);
//        //int PlayerChunkIndex = IndexFromPos(PlayerChunkPos, ChunkGridWidth);

//        ////Debug.Log(PlayerStats.HasMoved);

//        //if (PlayerStats.HasMoved)
//        //{
//        //    PlayerStats.HasMoved = false;

//        //    CheckForCollisions(ref PlayerStats);

//        //    if (!ChunksGenerated[PlayerChunkIndex])
//        //    {
//        //        GenerateChunk(PlayerChunkPos).Complete();
//        //    }

//        //    GenerateChunksAroundPlayer((int2)PlayerStats.Pos);

//        //    RenderPlayer(ref PlayerStats);
//        //}

//        //if (!ReplaceMesh)
//        //{
//        //    return;
//        //}

//        //if (!ChunksGenerated[PlayerChunkIndex])
//        //{
//        //    Debug.Log("Something has gone terribly wrong!");
//        //    Debug.Log($"ChunkPos {PlayerChunkPos} , ChunkIndex {PlayerChunkIndex}");
//        //    return;
//        //}

//        //ReplaceMesh = false;



//        //Entity TilemapEntity = SystemAPI.GetSingletonEntity<TilemapSettingsData>();

//        //var TilemapMeshManaged = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(TilemapEntity).Meshes[0];

//        //ref var TilemapBounds = ref SystemAPI.GetComponentRW<RenderBounds>(TilemapEntity).ValueRW;

//        //MeshUpdateFlags MeshFlags = MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices; // very minor performance save lol

//        //Mesh.ApplyAndDisposeWritableMeshData(GenerateMesh((int2)PlayerStats.Pos), TilemapMeshManaged, MeshFlags);

//        //var SubMeshInfo = TilemapMeshManaged.GetSubMesh(0);
//        //TilemapBounds.Value = new AABB()
//        //{
//        //    Center = SubMeshInfo.bounds.center,
//        //    Extents = SubMeshInfo.bounds.extents
//        //};
//    }

//    [BurstCompile]
//    public void BurstUpdate(ref SystemState state)
//    {
//        if (DebugChunk0)
//        {
//            ref var TilemapSettingsInfo = ref SystemAPI.GetSingletonRW<TilemapSettingsData>().ValueRW;

//            TerrainNoiseScale = TilemapSettingsInfo.TerrainNoiseScale;
//            PostTerrainNoiseScale = TilemapSettingsInfo.PostTerrainNoiseScale;
//            AdditionToTerrainNoise = TilemapSettingsInfo.AdditionToTerrainNoise;

//            DebugChunk0 = TilemapSettingsInfo.DebugChunk0;

//            BlockTypes = TilemapSettingsInfo.BlockTypes;

//            GenerateChunkOld(0).Complete();
//        }

//        ref var PlayerStats = ref SystemAPI.GetSingletonRW<Stats>().ValueRW;

//        int2 PlayerChunkPos = ChunkPosFromBlockPos((int2)PlayerStats.Pos, ChunkWidth);
//        int PlayerChunkIndex = IndexFromPos(PlayerChunkPos, ChunkGridWidth);

//        if (PlayerStats.HasMoved)
//        {
//            PlayerStats.HasMoved = false;

//            CheckForCollisions(ref PlayerStats);

//            if (!ChunksGenerated[PlayerChunkIndex])
//            {
//                GenerateChunkOld(PlayerChunkPos).Complete();
//            }

//            GenerateChunksAroundPlayer((int2)PlayerStats.Pos);

//            RenderPlayer(ref PlayerStats);
//        }

//        if (!ReplaceMesh)
//        {
//            return;
//        }

//        if (!ChunksGenerated[PlayerChunkIndex])
//        {
//            Debug.Log("Something has gone terribly wrong!");
//            Debug.Log($"ChunkPos {PlayerChunkPos} , ChunkIndex {PlayerChunkIndex}");
//            return;
//        }

//        ReplaceMesh = false;

//        Entity TilemapEntity = SystemAPI.GetSingletonEntity<TilemapSettingsData>();

//        var TilemapMeshManaged = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(TilemapEntity).Meshes[0];

//        ref var TilemapBounds = ref SystemAPI.GetComponentRW<RenderBounds>(TilemapEntity).ValueRW;

//        MeshUpdateFlags MeshFlags = MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices; // very minor performance save lol

//        Mesh.ApplyAndDisposeWritableMeshData(GenerateMesh((int2)PlayerStats.Pos), TilemapMeshManaged, MeshFlags);

//        var SubMeshInfo = TilemapMeshManaged.GetSubMesh(0);
//        TilemapBounds.Value = new AABB()
//        {
//            Center = SubMeshInfo.bounds.center,
//            Extents = SubMeshInfo.bounds.extents
//        };
//    }

//    [BurstCompile]
//    public void OnStopRunning(ref SystemState state)
//    {
//        TilemapArray.Dispose();
//        ChunksGenerated.Dispose();
//        BlocksToRender.Dispose();
//        VertexAttributes.Dispose();
//    }

//    #endregion

//    #region Tilemap Utility Functions
//    // Chunk index is an index that would be valid in ChunksGenerated, a block index is an index that would be valid in TilemapArray
//    // Local block index is basically a block inside a chunk, I think? So like say a chunk holds 10 by 10 blocks, then local block index of 2 would be local chunk coords of x1,y0 I think?

//    // world pos from chunk index plus local pos from local block index gives up the correct world pos! I think so atleast

//    // Everything commented above might be right, might be wrong... Trust the next comment more.

//    // ChunkPos and ChunkIndex are valid for the ChunkGrid
//    // BlockPos (aka WorldPos) and BlockIndex are valid for the BlockGrid.... but I'm lying as BlockIndex is usually never valid for the BlockGrid. Good luck!

//    public static int IndexFromPos(int2 Pos, int GridWidth) // dangerous
//    {
//        return Pos.y * GridWidth + Pos.x;
//    }

//    public static int2 PosFromIndex(int Index, int GridWidth) // dangerous
//    {
//        return new int2(Index % GridWidth, Index / GridWidth);
//    }

//    // probably should inline this?
//    public static int2 ChunkPosFromBlockPos(int2 BlockPos, int ChunkWidth)
//    {
//        return BlockPos / ChunkWidth;
//    }

//    public static int2 BlockPosFromChunkPos(int2 ChunkPos, int ChunkWidth)
//    {
//        return ChunkPos * ChunkWidth;
//    }

//    // add function to get chunk index from block index

//    public static int BlockIndexFromChunkIndex(int ChunkIndex, int ChunkWidthSquared)
//    {
//        return ChunkIndex * ChunkWidthSquared;
//    }

//    public static unsafe ref T UnsafeElementAt<T>(NativeArray<T> array, int index) where T : struct
//    {
//        return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
//    }

//    public void SetBlockInMesh(float2 UV, float2 Size, float3 WorldPos, int BlockIndex) // float3 for now
//    {
//        int VertexStart = BlockIndex * 4; // if every tile takes up 4 vertices then we use i * 4 to get the correct starting vertex
//        int IndexStart = BlockIndex * 6; // read above and replace some words, and you might understand my nonsense

//        UnsafeElementAt(Vertices, VertexStart).Pos = WorldPos + new float3(0.5f * Size.x, 0.5f * Size.y, 0); // top right
//        UnsafeElementAt(Vertices, VertexStart).UV = UV + new float2(SpriteWidth, SpriteHeight);

//        UnsafeElementAt(Vertices, VertexStart + 1).Pos = WorldPos + new float3(0.5f * Size.x, -0.5f * Size.y, 0); // top left
//        UnsafeElementAt(Vertices, VertexStart + 1).UV = UV + new float2(0, SpriteHeight);

//        UnsafeElementAt(Vertices, VertexStart + 2).Pos = WorldPos + new float3(-0.5f * Size.x, 0.5f * Size.y, 0); // bottom right
//        UnsafeElementAt(Vertices, VertexStart + 2).UV = UV + new float2(SpriteWidth, 0);

//        UnsafeElementAt(Vertices, VertexStart + 3).Pos = WorldPos + new float3(-0.5f * Size.x, -0.5f * Size.y, 0); // bottom left
//        UnsafeElementAt(Vertices, VertexStart + 3).UV = UV;

//        uint UVertexStart = (uint)VertexStart;

//        Indices[IndexStart] = UVertexStart;
//        Indices[IndexStart + 1] = UVertexStart + 1;
//        Indices[IndexStart + 2] = UVertexStart + 2;

//        Indices[IndexStart + 3] = UVertexStart + 1;
//        Indices[IndexStart + 4] = UVertexStart + 3;
//        Indices[IndexStart + 5] = UVertexStart + 2;
//    }

//    public static void SetBlockInMesh(float2 UV, float2 Size, float3 WorldPos, int BlockIndex, float SpriteWidth, float SpriteHeight, NativeArray<Vertex> Vertices, NativeArray<uint> Indices) // float3 for now
//    {
//        int VertexStart = BlockIndex * 4; // if every tile takes up 4 vertices then we use i * 4 to get the correct starting vertex
//        int IndexStart = BlockIndex * 6; // read above and replace some words, and you might understand my nonsense

//        UnsafeElementAt(Vertices, VertexStart).Pos = WorldPos + new float3(0.5f * Size.x, 0.5f * Size.y, 0); // top right
//        UnsafeElementAt(Vertices, VertexStart).UV = UV + new float2(SpriteWidth, SpriteHeight);

//        UnsafeElementAt(Vertices, VertexStart + 1).Pos = WorldPos + new float3(0.5f * Size.x, -0.5f * Size.y, 0); // top left
//        UnsafeElementAt(Vertices, VertexStart + 1).UV = UV + new float2(0, SpriteHeight);

//        UnsafeElementAt(Vertices, VertexStart + 2).Pos = WorldPos + new float3(-0.5f * Size.x, 0.5f * Size.y, 0); // bottom right
//        UnsafeElementAt(Vertices, VertexStart + 2).UV = UV + new float2(SpriteWidth, 0);

//        UnsafeElementAt(Vertices, VertexStart + 3).Pos = WorldPos + new float3(-0.5f * Size.x, -0.5f * Size.y, 0); // bottom left
//        UnsafeElementAt(Vertices, VertexStart + 3).UV = UV;

//        uint UVertexStart = (uint)VertexStart;

//        Indices[IndexStart] = UVertexStart;
//        Indices[IndexStart + 1] = UVertexStart + 1;
//        Indices[IndexStart + 2] = UVertexStart + 2;

//        Indices[IndexStart + 3] = UVertexStart + 1;
//        Indices[IndexStart + 4] = UVertexStart + 3;
//        Indices[IndexStart + 5] = UVertexStart + 2;
//    }

//    #endregion

//    #region Generation Functions and Jobs

//    [BurstCompile]
//    public void GenerateChunksAroundPlayer(int2 PlayerPos)
//    {
//        int2 PlayerChunkPos = ChunkPosFromBlockPos(PlayerPos, ChunkWidth);

//        for (int i = 0; i < 9; i++)
//        {
//            int2 OffsetPos = PosFromIndex(i, 3) - 1;

//            int SafeChunkIndex = math.clamp(IndexFromPos(OffsetPos + PlayerChunkPos, ChunkGridWidth), 0, int.MaxValue);

//            if (!ChunksGenerated[SafeChunkIndex])
//            {
//                GenerateChunkOld(math.clamp(PlayerChunkPos + OffsetPos, 0, int.MaxValue)).Complete();
//            }
//        }
//    }

//    public int2 FindSafePos()
//    {
//        int2 ChunkPos = SafePosRandom.NextInt2(ChunkGridWidth);

//        int ChunkIndex = IndexFromPos(ChunkPos, ChunkGridWidth);

//        GenerateChunkOld(ChunkPos).Complete();

//        int BlockIndexStart = BlockIndexFromChunkIndex(ChunkIndex, ChunkWidthSquared);
//        int2 WorldPosStart = BlockPosFromChunkPos(ChunkPos, ChunkWidth);

//        for (int BI = 0; BI < ChunkWidthSquared; BI++)
//        {
//            int2 WorldPos = WorldPosStart + PosFromIndex(BI, ChunkWidth); // important lesson here, the chunk is our grid, so we use chunk width instead of chunk grid width
//            int BlockIndex = BlockIndexStart + BI;

//            byte TypeIndex = TilemapArray[BlockIndex];

//            if (TypeIndex == 0) // make better soon
//            {
//                return WorldPos;
//            }
//        }

//        return WorldPosStart; // fail deadly
//    }

//    public static byte GenerateBlock(int2 Pos, uint Seed, float Scale, float AdditionToNoise, float PostScale, byte AmountOfBlockTypes) // extremely bad, don't like
//    {
//        return (byte)math.clamp(math.round((noise.snoise(new float2(Pos.x + Seed, Pos.y) * Scale) + AdditionToNoise) * PostScale), 0, AmountOfBlockTypes - 1);
//    }

//    [System.Obsolete]
//    public JobHandle GenerateChunkOld(int2 ChunkPos, JobHandle Dependency = new()) // do we want to burst compile this?
//    {
//        int ChunkIndex = IndexFromPos(ChunkPos, ChunkGridWidth);

//        int2 BlockPos = BlockPosFromChunkPos(ChunkPos, ChunkWidth);

//        int BlockIndex = BlockIndexFromChunkIndex(ChunkIndex, ChunkWidthSquared);

//        ChunksGenerated[ChunkIndex] = true;

//        var ChunkGeneratorJob = new ChunkGeneratorOld
//        {
//            Tilemap = TilemapArray,
//            StartingIndex = BlockIndex,
//            GridWidth = BlockGridWidth,
//            ChunkWidth = ChunkWidth,
//            ChunkWorldPos = BlockPos,
//            Seed = Seed,
//            TerrainNoiseScale = TerrainNoiseScale,
//            AdditionToNoise = AdditionToTerrainNoise,
//            PostScale = PostTerrainNoiseScale,
//            AmountOfBlockTypes = (byte)BlockTypes.Value.Length
//        };

//        return ChunkGeneratorJob.ScheduleParallel(ChunkWidthSquared, 64, Dependency);
//    }

//    [System.Obsolete]
//    [BurstCompile]
//    public struct ChunkGeneratorOld : IJobFor
//    {
//        [NativeDisableParallelForRestriction]
//        public NativeArray<byte> Tilemap;

//        public int StartingIndex;

//        public int GridWidth;

//        public int ChunkWidth;

//        public int2 ChunkWorldPos;

//        public uint Seed;

//        public float TerrainNoiseScale;

//        public float AdditionToNoise;

//        public float PostScale;

//        public byte AmountOfBlockTypes;

//        public void Execute(int i)
//        {
//            int2 BlockLocalPos = PosFromIndex(i, ChunkWidth);
//            int TrueIndex = StartingIndex + i;

//            if (TrueIndex > Tilemap.Length || TrueIndex < 0)
//            {
//                Debug.Log("Outside of map, what the hell???");
//                return;
//            }


//            Random ParallelRandom = Random.CreateFromIndex((uint)(Seed + i));
//            //Tilemap[TrueIndex] = GenerateBlockOld(BlockLocalPos + ChunkWorldPos, ParallelRandom, AmountOfBlockTypes);
//            Tilemap[TrueIndex] = GenerateBlock(BlockLocalPos + ChunkWorldPos, Seed, TerrainNoiseScale, AdditionToNoise, PostScale, AmountOfBlockTypes);
//        }
//    }

//    public JobHandle GenerateChunk(int2 ChunkPos, JobHandle Dependency = new()) // do we want to burst compile this?
//    {
//        int ChunkIndex = IndexFromPos(ChunkPos, ChunkGridWidth);

//        int2 BlockPos = BlockPosFromChunkPos(ChunkPos, ChunkWidth);

//        int BlockIndex = BlockIndexFromChunkIndex(ChunkIndex, ChunkWidthSquared);

//        ChunksGenerated[ChunkIndex] = true;

//        var ChunkGeneratorJob = new ChunkGenerator
//        {
//            Tilemap = TilemapArray,
//            StartingIndex = BlockIndex,
//            GridWidth = BlockGridWidth,
//            ChunkWidth = ChunkWidth,
//            ChunkWorldPos = BlockPos,
//            Seed = Seed,
//            TerrainNoiseScale = TerrainNoiseScale,
//            AdditionToNoise = AdditionToTerrainNoise,
//            PostScale = PostTerrainNoiseScale,
//            AmountOfBlockTypes = (byte)BlockTypes.Value.Length
//        };

//        return ChunkGeneratorJob.ScheduleParallel(ChunkWidthSquared, 64, Dependency);
//    }

//    [System.Obsolete]
//    [BurstCompile]
//    public struct ChunkGenerator : IJobFor
//    {
//        [NativeDisableParallelForRestriction]
//        public NativeArray<byte> Tilemap;

//        [WriteOnly]
//        [NativeDisableParallelForRestriction]
//        public NativeArray<Vertex> Vertices;

//        [WriteOnly]
//        [NativeDisableParallelForRestriction]
//        public NativeArray<uint> Indices;

//        [ReadOnly]
//        [NativeDisableParallelForRestriction]
//        BlobAssetReference<BlobArray<BlockType>> BlockTypes;

//        public int StartingIndex;

//        public int GridWidth;

//        public int ChunkWidth;

//        public int2 ChunkWorldPos;

//        public uint Seed;

//        public float TerrainNoiseScale;

//        public float AdditionToNoise;

//        public float PostScale;

//        public byte AmountOfBlockTypes;

//        public float SpriteWidth;
//        public float SpriteHeight;

//        public void Execute(int i)
//        {
//            int2 BlockLocalPos = PosFromIndex(i, ChunkWidth);
//            int TrueIndex = StartingIndex + i;

//            if (TrueIndex > Tilemap.Length || TrueIndex < 0)
//            {
//                Debug.Log("Outside of map, what the hell???");
//                return;
//            }


//            Random ParallelRandom = Random.CreateFromIndex((uint)(Seed + i));
//            //Tilemap[TrueIndex] = GenerateBlockOld(BlockLocalPos + ChunkWorldPos, ParallelRandom, AmountOfBlockTypes);

//            int2 BlockWorldPos = BlockLocalPos + ChunkWorldPos;

//            byte BlockTypeIndex = GenerateBlock(BlockWorldPos, Seed, TerrainNoiseScale, AdditionToNoise, PostScale, AmountOfBlockTypes);

//            Tilemap[TrueIndex] = BlockTypeIndex;

//            BlockType BType = BlockTypes.Value[BlockTypeIndex];

//            SetBlockInMesh(BType.UV, 1, new float3(BlockWorldPos, BType.Depth), TrueIndex, SpriteWidth, SpriteHeight, Vertices, Indices);
//        }
//    }

//    #endregion

//    #region Mesh Functions, Jobs, and structs

//    [BurstCompile]
//    public void RenderPlayer(ref Stats PlayerStats)
//    {
//        ReplaceMesh = true; // this is very slow, instead just grab the read only mesh data, and the write only mesh data, then set the write only mesh data equal to the read only mesh data, but with updated player pos
//        BlocksToRender.Add(new BlockMeshElement
//        {
//            Position = new float3(PlayerStats.Pos, 0),
//            UV = new float2(0, 0), // player is always first sprite? This is a bad idea
//            Size = PlayerStats.Size
//        });
//    }

//    [System.Obsolete]
//    [BurstCompile]
//    public Mesh.MeshDataArray GenerateMesh(int2 PlayerPos) // needs to be redone to only render 1 chunk at a time, or render a number of chunks based on players render distance setting
//    {
//        Mesh.MeshDataArray TilemapMeshArray = Mesh.AllocateWritableMeshData(1); // future optimization by allocating more than 1???
//        Mesh.MeshData TilemapMeshData = TilemapMeshArray[0];

//        int2 PlayerChunkPos = ChunkPosFromBlockPos(PlayerPos, ChunkWidth);

//        for (int i = 0; i < 9; i++)
//        {
//            int2 OffsetPos = PosFromIndex(i, 3) - 1;

//            int2 ChunkPos = PlayerChunkPos + OffsetPos;

//            int ChunkIndex = math.clamp(IndexFromPos(ChunkPos, ChunkGridWidth), 0, int.MaxValue);

//            if (ChunksGenerated[ChunkIndex])
//            {
//                int BlockIndexStart = BlockIndexFromChunkIndex(ChunkIndex, ChunkWidthSquared); // bad naming scheme

//                int2 WorldPosStart = BlockPosFromChunkPos(ChunkPos, ChunkWidth); // bad naming scheme

//                for (int BI = 0; BI < ChunkWidthSquared; BI++)
//                {
//                    int2 WorldPos = WorldPosStart + PosFromIndex(BI, ChunkWidth); // important lesson here, the chunk is our grid, so we use chunk width instead of chunk grid width
//                    int BlockIndex = BlockIndexStart + BI;

//                    byte TypeIndex = TilemapArray[BlockIndex];

//                    if (TypeIndex == 0) // 0 is empty space, don't try to render it, note not -1 because byte's min value is 0
//                    {
//                        continue;
//                    }

//                    BlockType TypeInfo = BlockTypes.Value[TypeIndex];

//                    BlocksToRender.Add(new BlockMeshElement
//                    {
//                        UV = TypeInfo.UV,
//                        Position = new int3(WorldPos, TypeInfo.Depth), // z is depth
//                        Size = new float2(1, 1)
//                    });
//                }
//            }
//        }

//        int RenderAmount = BlocksToRender.Length;

//        TilemapMeshData.SetVertexBufferParams(RenderAmount * 4, VertexAttributes);
//        TilemapMeshData.SetIndexBufferParams(RenderAmount * 6, IndexFormat.UInt32);

//        TilemapMeshData.subMeshCount = 1; // temporary, needs to be replaced by something good that works out how many use what material, but that is pain

//        var ProcessMeshDataJob = new ProcessMeshData()
//        {
//            BlockMeshInfo = BlocksToRender,
//            Vertices = TilemapMeshData.GetVertexData<Vertex>(),
//            Indices = TilemapMeshData.GetIndexData<int>(), // why not uint??????????
//            SpriteWidth = SpriteWidth,
//            SpriteHeight = SpriteHeight
//        };

//        ProcessMeshDataJob.ScheduleParallel(RenderAmount, 64, new JobHandle()).Complete(); // pass out of function somehow please?

//        SubMeshDescriptor SubMeshInfo = new()
//        {
//            baseVertex = 0, // for now this is correct, but will be an issue eventually
//            //bounds = SubMeshBounds,
//            firstVertex = 0,
//            indexCount = 6 * RenderAmount, // 2 triangles with each triangle needing 3 then that for every block.
//            indexStart = 0, //potentially lol
//            topology = MeshTopology.Triangles, // 3 indices per face
//            vertexCount = 4 * RenderAmount
//        };

//        TilemapMeshData.SetSubMesh(0, SubMeshInfo, MeshUpdateFlags.Default);

//        BlocksToRender.Clear(); // clear it so it can be used again, I think

//        return TilemapMeshArray;
//    }

//    public struct BlockMeshElement
//    {
//        public float3 Position;
//        public float2 UV;
//        public float2 Size;
//    }

//    public struct Vertex // this has to match the VertexAttributes
//    {
//        public float3 Pos;
//        public float2 UV; // is this precise enough?
//    }

//    [System.Obsolete]
//    [BurstCompile]
//    struct ProcessMeshData : IJobFor // doesn't work well
//    {
//        [ReadOnly]
//        public NativeList<BlockMeshElement> BlockMeshInfo; // don't replace, rather just populate this list from the for loop that checks which chunks exist. Get the uv data and everything from a blob array containing the block types

//        [WriteOnly]
//        [NativeDisableContainerSafetyRestriction]
//        public NativeArray<Vertex> Vertices;

//        [NativeDisableContainerSafetyRestriction]
//        [WriteOnly]
//        public NativeArray<int> Indices; // why is this not uint????

//        [ReadOnly]
//        public float SpriteWidth;

//        [ReadOnly]
//        public float SpriteHeight;

//        public void Execute(int i)
//        {
//            BlockMeshElement BlockInfo = BlockMeshInfo[i];

//            int VertexStart = i * 4; // if every tile takes up 4 vertices then we use i * 4 to get the correct starting vertex
//            int IndexStart = i * 6; // read above and replace some words, and you might understand my nonsense

//            UnsafeElementAt(Vertices, VertexStart).Pos = BlockInfo.Position + new float3(0.5f * BlockInfo.Size.x, 0.5f * BlockInfo.Size.y, 0); // top right
//            UnsafeElementAt(Vertices, VertexStart).UV = BlockInfo.UV + new float2(SpriteWidth, SpriteHeight);

//            UnsafeElementAt(Vertices, VertexStart + 1).Pos = BlockInfo.Position + new float3(0.5f * BlockInfo.Size.x, -0.5f * BlockInfo.Size.y, 0); // top left
//            UnsafeElementAt(Vertices, VertexStart + 1).UV = BlockInfo.UV + new float2(0, SpriteHeight);

//            UnsafeElementAt(Vertices, VertexStart + 2).Pos = BlockInfo.Position + new float3(-0.5f * BlockInfo.Size.x, 0.5f * BlockInfo.Size.y, 0); // bottom right
//            UnsafeElementAt(Vertices, VertexStart + 2).UV = BlockInfo.UV + new float2(SpriteWidth, 0);

//            UnsafeElementAt(Vertices, VertexStart + 3).Pos = BlockInfo.Position + new float3(-0.5f * BlockInfo.Size.x, -0.5f * BlockInfo.Size.y, 0); // bottom left
//            UnsafeElementAt(Vertices, VertexStart + 3).UV = BlockInfo.UV;

//            Indices[IndexStart] = VertexStart;
//            Indices[IndexStart + 1] = VertexStart + 1;
//            Indices[IndexStart + 2] = VertexStart + 2;

//            Indices[IndexStart + 3] = VertexStart + 1;
//            Indices[IndexStart + 4] = VertexStart + 3;
//            Indices[IndexStart + 5] = VertexStart + 2;
//        }
//    }

//    #endregion

//    #region Collision Stuff

//    [BurstCompile]
//    public void CheckForCollisions(ref Stats PlayerStats)
//    {
//        float2 PlayerPos = PlayerStats.Pos;
//        float2 PlayerSize = PlayerStats.Size;

//        int2 ClosestBlockPos = (int2)math.round(PlayerPos);

//        for (int i = 0; i < 9; i++)
//        {
//            int2 OffsetPos = PosFromIndex(i, 3) - 1;

//            int2 BlockPos = ClosestBlockPos + OffsetPos;

//            BlockPos = math.clamp(BlockPos, 0, int.MaxValue);

//            int2 ChunkPos = ChunkPosFromBlockPos(BlockPos, ChunkWidth);
//            int ChunkIndex = IndexFromPos(ChunkPos, ChunkGridWidth);
//            int BlockIndexStart = ChunkIndex * ChunkWidthSquared;

//            int2 WorldPosStart = BlockPosFromChunkPos(ChunkPos, ChunkWidth);

//            int2 LocalPos = BlockPos - WorldPosStart;

//            int LocalIndex = IndexFromPos(LocalPos, ChunkWidth);

//            int BlockIndex = BlockIndexStart + LocalIndex;

//            byte BlockTypeIndex = TilemapArray[BlockIndex];

//            if (BlockTypeIndex == 0) // you can't collide with nothing
//            {
//                continue;
//            }

//            if (CheckForCollision(PlayerPos, PlayerSize, BlockPos))
//            {
//                BlockType BlockInfo = BlockTypes.Value[BlockTypeIndex];

//                if (BlockInfo.StatsChange.Strength < PlayerStats.Strength)
//                {
//                    TilemapArray[BlockIndex] = 0;

//                    PlayerStats += BlockInfo.StatsChange;

//                    continue;
//                }

//                PlayerStats.Pos = PlayerStats.PreviousPos;
//                return;
//            }
//        }
//    }

//    public bool CheckForCollision(float2 PlayerPos, float2 PlayerSize, int2 BlockPos)
//    {
//        PlayerPos += PlayerSize * -0.5f + 0.5f;

//        if ( // https://developer.mozilla.org/en-US/docs/Games/Techniques/2D_collision_detection so good
//            PlayerPos.x < BlockPos.x + 1 &&
//            PlayerPos.x + PlayerSize.x > BlockPos.x &&
//            PlayerPos.y < BlockPos.y + 1 &&
//            PlayerPos.y + PlayerSize.y > BlockPos.y
//            )
//        {
//            return true;
//        }
//        else
//        {
//            return false;
//        }
//    }

//    #endregion
//}