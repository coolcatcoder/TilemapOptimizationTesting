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
public partial struct TilemapSystem : ISystem, ISystemStartStop
{
    NativeArray<byte> TilemapArray; // should never need more than 200 block types, at least I hope so
    NativeArray<bool> ChunksGenerated;

    NativeList<BlockMeshElement> BlocksToRender;
    NativeArray<VertexAttributeDescriptor> VertexAttributes;

    BlobAssetReference<BlobArray<BlockType>> BlockTypes;

    bool ReplaceMesh; // basically just renders the blocks when this is true (not needed every frame, only when stuff changes)

    int ChunkWidth;
    int ChunkWidthSquared;

    int BlockGridWidth;
    int ChunkGridWidth;

    uint Seed;

    float SpriteWidth;
    float SpriteHeight;

    #region ISystem Methods

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TilemapSettingsData>();
        state.RequireForUpdate<Stats>();
    }

    //[BurstCompile]
    public void OnStartRunning(ref SystemState state)
    {
        ref var TilemapSettingsInfo = ref SystemAPI.GetSingletonRW<TilemapSettingsData>().ValueRW;

        BlockTypes = TilemapSettingsInfo.BlockTypes;

        ChunkWidth = TilemapSettingsInfo.ChunkWidth;
        ChunkWidthSquared = ChunkWidth * ChunkWidth;
        BlockGridWidth = TilemapSettingsInfo.TilemapSize; // why is it called tilemap size???? ughhhhh
        Seed = (uint)SystemAPI.Time.ElapsedTime; // so sketchy
        SpriteWidth = TilemapSettingsInfo.SpriteWidth;
        SpriteHeight = TilemapSettingsInfo.SpriteHeight;

        ChunkGridWidth = TilemapSettingsInfo.TilemapSize / TilemapSettingsInfo.ChunkWidth;

        TilemapArray = new NativeArray<byte>(TilemapSettingsInfo.TilemapSize * TilemapSettingsInfo.TilemapSize, Allocator.Persistent);
        ChunksGenerated = new NativeArray<bool>(ChunkGridWidth * ChunkGridWidth, Allocator.Persistent);
        //Debug.Log(ChunksGenerated.Length);
        BlocksToRender = new NativeList<BlockMeshElement>(TilemapSettingsInfo.MaxBlocksToRender, Allocator.Persistent);

        VertexAttributes = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Persistent);
        VertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
        VertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);

        //System.Diagnostics.Stopwatch Watch = new();
        //Watch.Start();
        //for (int i = 0; i < TilemapSettingsInfo.Trials; i++)
        //{
        //    GenerateChunk(new int2(0,0)).Complete();
        //}
        //Watch.Stop();
        //Debug.Log("Weird Chunks: " + Watch.Elapsed / TilemapSettingsInfo.Trials);

        SystemAPI.GetSingletonRW<Stats>().ValueRW.Pos = FindSafePos();

        GenerateChunk(new int2(0, 0)).Complete();
        GenerateChunk(new int2(1, 0)).Complete();
        GenerateChunk(new int2(1, 1)).Complete();

        //Debug.Log(FindSafePos());

        ReplaceMesh = true;

        //int2 TestingWorldPos = new int2(5, 3);
        //int2 TestingChunkPos = ChunkPosFromBlockPos(TestingWorldPos, ChunkWidth);

        //Debug.Log($"WorldPos {TestingWorldPos} is in ChunkPos {TestingChunkPos} which is ChunkIndex {IndexFromPos(TestingChunkPos, ChunkGridWidth)}");
    }

    public void OnUpdate(ref SystemState state)
    {
        ref var PlayerStats = ref SystemAPI.GetSingletonRW<Stats>().ValueRW;

        CheckForCollisions(ref PlayerStats);

        if (PlayerStats.HasMoved)
        {
            ReplaceMesh = true; // this is very slow, instead just grab the read only mesh data, and the write only mesh data, then set the write only mesh data equal to the read only mesh data, but with updated player pos
            BlocksToRender.Add(new BlockMeshElement
            {
                Position = new float3(PlayerStats.Pos, 0),
                UV = new float2(0,0), // player is always first sprite? This is a bad idea
                Size = PlayerStats.Size
            });
        }

        if (!ReplaceMesh)
        {
            return;
        }

        //int PlayerChunk = ChunkIndexFromWorldPos((int2)PlayerStats.Pos, ChunkWidth); // is casting to int2 a good idea? Should we round instead?
        //int PlayerChunk = ChunkIndexFromBlockIndex(IndexFromPos((int2)PlayerStats.Pos, BlockGridWidth), ChunkWidthSquared);
        int2 PlayerChunkPos = ChunkPosFromBlockPos((int2)PlayerStats.Pos, ChunkWidth);
        int PlayerChunkIndex = IndexFromPos(PlayerChunkPos, ChunkGridWidth);


        if (!ChunksGenerated[PlayerChunkIndex])
        {
            Debug.Log("Something has gone terribly wrong!");
            Debug.Log($"ChunkPos {PlayerChunkPos} , ChunkIndex {PlayerChunkIndex}");
            return;
        }

        ReplaceMesh = false;

        Entity TilemapEntity = SystemAPI.GetSingletonEntity<TilemapSettingsData>();

        var TilemapMeshManaged = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(TilemapEntity).Meshes[0];

        ref var TilemapBounds = ref SystemAPI.GetComponentRW<RenderBounds>(TilemapEntity).ValueRW;

        MeshUpdateFlags MeshFlags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices; // not tested yet

        Mesh.ApplyAndDisposeWritableMeshData(GenerateMesh(), TilemapMeshManaged, MeshUpdateFlags.Default);

        var SubMeshInfo = TilemapMeshManaged.GetSubMesh(0);
        TilemapBounds.Value = new AABB()
        {
            Center = SubMeshInfo.bounds.center,
            Extents = SubMeshInfo.bounds.extents
        };
    }

    public void OnStopRunning(ref SystemState state)
    {
        TilemapArray.Dispose();
        ChunksGenerated.Dispose();
        BlocksToRender.Dispose();
        VertexAttributes.Dispose();

        //UnsafeUtility.Free(RootNode, Allocator.Persistent); // only works if there is one node
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

    // add functions to go from chunk pos to world pos, and from world pos to chunk pos

    // add function to go from chunk index to block index, but not the other way round yet, until my math improves

    public static unsafe ref T UnsafeElementAt<T>(NativeArray<T> array, int index) where T : struct
    {
        return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
    }

    // the 2 functions below cannot exist as far as I know

    //public static int BlockIndexFromChunkIndex(int ChunkIndex, int ChunkWidthSquared)
    //{
    //    return ChunkIndex * ChunkWidthSquared;
    //}

    //public static int ChunkIndexFromBlockIndex(int BlockIndex, int ChunkWidthSquared)
    //{
    //    return BlockIndex / ChunkWidthSquared;
    //}

    //// below are bad functions, very very scary

    //[System.Obsolete]
    //public static int2 ChunkPosFromWorldPos(int2 WorldPos, int ChunkWidth) // confirmed correct
    //{
    //    return WorldPos/ChunkWidth;
    //}

    //[System.Obsolete]
    //public static int ChunkIndexFromChunkPos(int2 ChunkPos, int ChunkGridWidth)
    //{
    //    return ChunkPos.y * ChunkGridWidth + ChunkPos.x;
    //}

    //[System.Obsolete]
    //public static int ChunkIndexFromWorldPos(int2 WorldPos, int ChunkGridWidth) // wrong
    //{
    //    return WorldPos.y * ChunkGridWidth + WorldPos.x;
    //}

    //[System.Obsolete]
    //public static int GetChunkGridWidth(int GridWidth, int ChunkWidth)
    //{
    //    return GridWidth / ChunkWidth;
    //}

    //[System.Obsolete]
    //public static int2 LocalPosFromLocalBlockIndex(int LocalBlockIndex, int ChunkWidth)
    //{
    //    return new int2(LocalBlockIndex % ChunkWidth, LocalBlockIndex / ChunkWidth);
    //}

    //[System.Obsolete]
    //public static int2 WorldPosFromChunkIndex(int ChunkIndex, int BlockGridWidth)
    //{
    //    return new int2(ChunkIndex % BlockGridWidth, ChunkIndex / BlockGridWidth);
    //}

    #endregion

    #region Generation Functions and Jobs

    public int2 FindSafePos()
    {
        Random Rand = Random.CreateFromIndex(Seed);

        int2 ChunkPos = (int2)Rand.NextUInt2((uint)ChunkGridWidth);
        Debug.Log(ChunkPos);

        int ChunkIndex = IndexFromPos(ChunkPos, ChunkGridWidth);

        GenerateChunk(ChunkPos).Complete();

        int BlockIndexStart = ChunkIndex * ChunkWidthSquared;

        //int2 WorldPosStart = PosFromIndex(BlockIndexStart, BlockGridWidth);

        int2 WorldPosStart = ChunkPos * ChunkWidth;

        Debug.Log(WorldPosStart);

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

    public static byte GenerateBlock(int2 Pos, Random Rand, byte AmountOfBlockTypes)
    {
        return (byte)Rand.NextInt(0,AmountOfBlockTypes);
    }

    //[BurstCompile]
    public JobHandle GenerateChunk(int2 ChunkPos, JobHandle Dependency = new())
    {
        Debug.Log($"starting generation of chunk {ChunkPos.x}, {ChunkPos.y}");

        int ChunkIndex = IndexFromPos(ChunkPos, ChunkGridWidth);
        Debug.Log($"chunk index {ChunkIndex}");

        int2 BlockPos = BlockPosFromChunkPos(ChunkPos, ChunkWidth);
        Debug.Log($"block pos {BlockPos.x}, {BlockPos.y}");

        int BlockIndex = ChunkIndex * ChunkWidthSquared; // this works?????

        ChunksGenerated[ChunkIndex] = true;

        //int BlockIndex = BlockIndexFromChunkIndex(ChunkIndex, ChunkWidthSquared);

        var ChunkGeneratorJob = new ChunkGenerator
        {
            Tilemap = TilemapArray,
            StartingIndex = BlockIndex,
            GridWidth = BlockGridWidth,
            ChunkWidth = ChunkWidth,
            ChunkWorldPos = BlockPos,
            //ChunkWorldPos = PosFromIndex(BlockIndex, BlockGridWidth),
            //ChunkWorldPos = WorldPosFromChunkIndex(ChunkIndex, BlockGridWidth),
            Seed = Seed,
            AmountOfBlockTypes = (byte)BlockTypes.Value.Length
        };

        Debug.Log($"stopping generation of chunk {ChunkPos.x}, {ChunkPos.y}");

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

        public byte AmountOfBlockTypes;

        public void Execute(int i)
        {
            //int2 BlockLocalPos = new int2(i % ChunkWidth, i / ChunkWidth);
            int2 BlockLocalPos = PosFromIndex(i, ChunkWidth);
            int TrueIndex = StartingIndex + i;

            if (TrueIndex > Tilemap.Length || TrueIndex < 0)
            {
                Debug.Log("Outside of map, what the hell???");
                return;
            }


            Random ParallelRandom = Random.CreateFromIndex((uint)(Seed + i));
            Tilemap[TrueIndex] = GenerateBlock(BlockLocalPos + ChunkWorldPos, ParallelRandom, AmountOfBlockTypes);
        }
    }

    #endregion

    #region Mesh Functions, Jobs, and structs

    [BurstCompile]
    public Mesh.MeshDataArray GenerateMesh() // needs to be redone to only render 1 chunk at a time, or render a number of chunks based on players render distance setting
    {
        Mesh.MeshDataArray TilemapMeshArray = Mesh.AllocateWritableMeshData(1); // future optimization by allocating more than 1???
        Mesh.MeshData TilemapMeshData = TilemapMeshArray[0];

        int ChunksToRender = 0;

        for (int CI = 0; CI < ChunksGenerated.Length; CI++)
        {
            if (ChunksGenerated[CI])
            {
                ChunksToRender++;

                int2 ChunkPos = PosFromIndex(CI, ChunkGridWidth);

                //Debug.Log(WorldPosFromChunkIndex(CI, BlockGridWidth));

                //int BlockIndexStart = BlockIndexFromChunkIndex(CI, ChunkWidthSquared); // bad naming scheme
                int BlockIndexStart = CI * ChunkWidthSquared; // I don't get this at all

                //int2 WorldChunkPos = WorldPosFromChunkIndex(CI, BlockGridWidth);
                //int2 WorldPosStart = PosFromIndex(BlockIndexStart, BlockGridWidth); // OH HELL OH HELL THIS IS NOT GOOD also bad naming scheme

                int2 WorldPosStart = ChunkPos * ChunkWidth;

                for (int BI = 0; BI < ChunkWidthSquared; BI++)
                {
                    //int2 WorldPos = WorldPosStart + LocalPosFromLocalBlockIndex(BI, ChunkWidth);
                    int2 WorldPos = WorldPosStart + PosFromIndex(BI, ChunkWidth); // important lesson here, the chunk is our grid, so we use chunk width instead of chunk grid width
                    int BlockIndex = BlockIndexStart + BI;

                    byte TypeIndex = TilemapArray[BlockIndex];

                    if (TypeIndex == 0) // 0 is empty space, don't try to render it, note not -1 because byte's min value is 0
                    {
                        continue;
                    }

                    BlockType TypeInfo = BlockTypes.Value[TypeIndex];

                    //Debug.Log(WorldBlockPos);

                    BlocksToRender.Add(new BlockMeshElement
                    {
                        UV = TypeInfo.UV,
                        Position = new int3(WorldPos, TypeInfo.Depth), // z is depth
                        Size = new float2(1,1)
                    });
                }
            }
        }

        //int RenderAmount = ChunksToRender * ChunkWidthSquared;
        int RenderAmount = BlocksToRender.Length;

        TilemapMeshData.SetVertexBufferParams(RenderAmount * 4, VertexAttributes);
        TilemapMeshData.SetIndexBufferParams(RenderAmount * 6, IndexFormat.UInt32);

        TilemapMeshData.subMeshCount = 1; // temporary, needs to be replaced by something good that works out how many use what material, but that is pain

        var ProcessMeshData = new ProcessMeshDataJob()
        {
            BlockMeshInfo = BlocksToRender,
            Vertices = TilemapMeshData.GetVertexData<Vertex>(),
            Indices = TilemapMeshData.GetIndexData<int>(), // why not uint??????????
            SpriteWidth = SpriteWidth,
            SpriteHeight = SpriteHeight
        };

        ProcessMeshData.ScheduleParallel(RenderAmount, 64, new JobHandle()).Complete(); // pass out of function somehow please?

        //Bounds SubMeshBounds = new Bounds() // very bad
        //{
        //    center = new float3(0, 0, 0), // not good and not true, same for everything below
        //    extents = new float3(1000, 1000, 10),
        //    max = new float3(1000, 1000, 10),
        //    min = new float3(-1000, -1000, -10),
        //    size = new float3(2000, 2000, 20)
        //};

        SubMeshDescriptor SubMeshInfo = new()
        {
            baseVertex = 0, // for now this is correct, but will be an issue eventually
            //bounds = SubMeshBounds,
            firstVertex = 0,
            indexCount = 6 * RenderAmount, // 2 triangles with each triangle needing 3 then that for every block.
            indexStart = 0, //potentially lol
            topology = MeshTopology.Triangles, // 3 indices per face
            vertexCount = 4 * RenderAmount
        };

        TilemapMeshData.SetSubMesh(0, SubMeshInfo, MeshUpdateFlags.Default);

        BlocksToRender.Clear(); // clear it so it can be used again, I think

        return TilemapMeshArray;
    }

    public struct BlockMeshElement
    {
        public float3 Position;
        public float2 UV;
        public float2 Size;
    }

    struct Vertex // this has to match the VertexAttributes
    {
        public float3 Pos;
        public float2 UV; // is this precise enough?
    }

    [BurstCompile]
    struct ProcessMeshDataJob : IJobFor // doesn't work well and uses old naming scheme of THINGJob instead of just being THING for later use as THINGJob and THINGHandle
    {
        [ReadOnly]
        public NativeList<BlockMeshElement> BlockMeshInfo; // don't replace, rather just populate this list from the for loop that checks which chunks exist. Get the uv data and everything from a blob array containing the block types

        [WriteOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Vertex> Vertices;

        [NativeDisableContainerSafetyRestriction]
        [WriteOnly]
        public NativeArray<int> Indices; // why is this not uint????

        //[ReadOnly]
        //public float2 UVTileHalfSize; // basically x = 1 / UVWidth / 2 , y = 1 / UVHeight / 2     What the hell does this mean?

        [ReadOnly]
        public float SpriteWidth;

        [ReadOnly]
        public float SpriteHeight;

        public void Execute(int i)
        {
            //var Vertices = OutputMesh.GetVertexData<Vertex>(); Kept so I can understand how to populate

            BlockMeshElement BlockInfo = BlockMeshInfo[i];

            int VertexStart = i * 4; // if every tile takes up 4 vertices then we use i * 4 to get the correct starting vertex
            int IndexStart = i * 6; // read above and replace some words, and you might understand my nonsense

            UnsafeElementAt(Vertices, VertexStart).Pos = BlockInfo.Position + new float3(0.5f * BlockInfo.Size.x, 0.5f * BlockInfo.Size.y, 0); // top right
            UnsafeElementAt(Vertices, VertexStart).UV = BlockInfo.UV + new float2(SpriteWidth, SpriteHeight);

            UnsafeElementAt(Vertices, VertexStart + 1).Pos = BlockInfo.Position + new float3(0.5f * BlockInfo.Size.x, -0.5f * BlockInfo.Size.y, 0); // top left
            UnsafeElementAt(Vertices, VertexStart + 1).UV = BlockInfo.UV + new float2(0, SpriteHeight);

            UnsafeElementAt(Vertices, VertexStart + 2).Pos = BlockInfo.Position + new float3(-0.5f * BlockInfo.Size.x, 0.5f * BlockInfo.Size.y, 0); // bottom right
            UnsafeElementAt(Vertices, VertexStart + 2).UV = BlockInfo.UV + new float2(SpriteWidth, 0);

            UnsafeElementAt(Vertices, VertexStart + 3).Pos = BlockInfo.Position + new float3(-0.5f * BlockInfo.Size.x, -0.5f * BlockInfo.Size.y, 0); // bottom left
            UnsafeElementAt(Vertices, VertexStart + 3).UV = BlockInfo.UV;

            //var Indices = OutputMesh.GetIndexData<int>(); // shouldn't this be uint???

            Indices[IndexStart] = VertexStart;
            Indices[IndexStart + 1] = VertexStart + 1;
            Indices[IndexStart + 2] = VertexStart + 2;

            Indices[IndexStart + 3] = VertexStart + 1;
            Indices[IndexStart + 4] = VertexStart + 3;
            Indices[IndexStart + 5] = VertexStart + 2;
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

        bool3x3 HasCollided3x3 = new(
            CheckForCollision(PlayerPos, PlayerSize, ClosestBlockPos + new int2(-1, -1)), CheckForCollision(PlayerPos, PlayerSize, ClosestBlockPos + new int2(-1, 0)), CheckForCollision(PlayerPos, PlayerSize, ClosestBlockPos + new int2(-1, 1)),
            CheckForCollision(PlayerPos, PlayerSize, ClosestBlockPos + new int2(0, -1)), CheckForCollision(PlayerPos, PlayerSize, ClosestBlockPos + new int2(0, 0)), CheckForCollision(PlayerPos, PlayerSize, ClosestBlockPos + new int2(0, 1)),
            CheckForCollision(PlayerPos, PlayerSize, ClosestBlockPos + new int2(1, -1)), CheckForCollision(PlayerPos, PlayerSize, ClosestBlockPos + new int2(1, 0)), CheckForCollision(PlayerPos, PlayerSize, ClosestBlockPos + new int2(1, 1))
            );

        bool HasCollided = math.any(new bool3(math.any(HasCollided3x3.c0), math.any(HasCollided3x3.c1), math.any(HasCollided3x3.c2))); // math.any does not support 3x3 sadly

        if (HasCollided)
        {
            PlayerStats.Pos = PlayerStats.PreviousPos; // should undo collisions
        }
    }

    public bool CheckForCollision(float2 PlayerPos, float2 PlayerSize, int2 BlockPos) // highly likely this isn't correct
    {
        BlockPos = math.clamp(BlockPos, 0, int.MaxValue);

        int2 ChunkPos = ChunkPosFromBlockPos(BlockPos, ChunkWidth);
        int ChunkIndex = IndexFromPos(ChunkPos, ChunkGridWidth);
        int BlockIndexStart = ChunkIndex * ChunkWidthSquared;

        //int2 WorldPosStart = PosFromIndex(BlockIndexStart, BlockGridWidth);
        int2 WorldPosStart = ChunkPos * ChunkWidth;

        int2 LocalPos = BlockPos - WorldPosStart;

        int LocalIndex = IndexFromPos(LocalPos, ChunkWidth);
        
        int BlockIndex = BlockIndexStart + LocalIndex;

        byte BlockTypeIndex = TilemapArray[BlockIndex];

        if (BlockTypeIndex == 0)
        {
            return false;
        }

        // this will change to check if the player has enough strength and stuff, but for now, if the block is not nothing, then the block is solid

        //PlayerPos += PlayerSize / 8;
        PlayerPos += PlayerSize * -0.5f + 0.5f;

        if ( // https://developer.mozilla.org/en-US/docs/Games/Techniques/2D_collision_detection so good
            PlayerPos.x < BlockPos.x + 1 &&
            PlayerPos.x + PlayerSize.x > BlockPos.x &&
            PlayerPos.y < BlockPos.y + 1 &&
            PlayerPos.y + PlayerSize.y > BlockPos.y
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