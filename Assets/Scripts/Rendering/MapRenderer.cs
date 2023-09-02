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
//[UpdateInGroup(typeof(PresentationSystemGroup))]
//public partial struct TilemapSystem : ISystem, ISystemStartStop
//{
//    NativeArray<VertexAttributeDescriptor> VertexAttributes;

//    float2 SpriteSize;

//    [BurstCompile]
//    public void OnCreate(ref SystemState state)
//    {
//        state.RequireForUpdate<MapContainers>();
//        state.RequireForUpdate<Stats>();
//    }

//    [BurstCompile]
//    public void OnStartRunning(ref SystemState state)
//    {
//        VertexAttributes = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Persistent);
//        VertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
//        VertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);
//    }

//    [BurstCompile]
//    public void OnUpdate(ref SystemState state) // todo, split into patterns. 1 mesh per pattern, then merge
//    {
//        PlayerData PlayerInfo = SystemAPI.GetSingleton<PlayerData>();
//        MapData MapInfo = SystemAPI.GetSingleton<MapData>();

//        int ScreenWidth = (int)(PlayerInfo.TopRightPosOfScreen.x - PlayerInfo.BottomLeftPosOfScreen.x);
//        int ScreenHeight = (int)(PlayerInfo.TopRightPosOfScreen.y - PlayerInfo.BottomLeftPosOfScreen.y);

//        int BlocksOnScreen = ScreenWidth * ScreenHeight;

//        SimpleMesh<Vertex, uint> GridMesh = new SimpleMesh<Vertex, uint>((uint)BlocksOnScreen * 4 + 4, (uint)BlocksOnScreen * 6 + 6, Allocator.Temp);

//        RenderPlayer(PlayerInfo, GridMesh, BlocksOnScreen, MapInfo.SpriteSize);

//        var GridMapToMeshJob = new GridMapToMesh()
//        {
//            GridMesh = GridMesh,
//            BlockTypes = BlockTypes,
//            Vertices = Vertices,
//            Indices = Indices,
//            SpriteWidth = SpriteWidth,
//            SpriteHeight = SpriteHeight,
//            ScreenWidth = ScreenWidth,
//            BottomLeftOfScreen = BottomLeftPos
//        };

//        JobHandle TilemapToMeshHandle = TilemapToMeshJob.ScheduleParallel(BlocksOnScreen, 64, new JobHandle());

//        TilemapToMeshHandle.Complete(); // do the system handle nonsense sooner rather than later!!!!!!!!!

//        Bounds SubMeshBounds = new Bounds()
//        {
//            center = PlayerCam.transform.position,
//            extents = new float3(ScreenWidth, ScreenHeight, 50) / 2 // 50 should be enough
//        };

//        SubMeshDescriptor SubMeshInfo = new()
//        {
//            baseVertex = 0, // for now this is correct, but will be an issue eventually
//            bounds = SubMeshBounds,
//            firstVertex = 0,
//            indexCount = 6 * BlocksOnScreen + 6, // 2 triangles with each triangle needing 3 then that for every block.
//            indexStart = 0, //potentially lol
//            topology = MeshTopology.Triangles, // 3 indices per face
//            vertexCount = 4 * BlocksOnScreen + 4
//        };

//        MeshData.SetSubMesh(0, SubMeshInfo, MeshUpdateFlags.Default);

//        MeshUpdateFlags MeshFlags = MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices; // very minor performance save lol

//        Mesh.ApplyAndDisposeWritableMeshData(MeshArray, TilemapMeshManaged, MeshFlags);

//        ref var TilemapBounds = ref SystemAPI.GetComponentRW<RenderBounds>(TilemapEntity).ValueRW;

//        TilemapBounds.Value = new AABB()
//        {
//            Center = SubMeshInfo.bounds.center,
//            Extents = SubMeshInfo.bounds.extents
//        };

//        mesh.Dispose();
//    }

//    [BurstCompile]
//    public void OnStopRunning(ref SystemState state)
//    {
//        VertexAttributes.Dispose();
//    }

//    #region Mesh Functions, Jobs, and structs

//    public void RenderPlayer(PlayerData PlayerInfo, SimpleMesh<Vertex, uint> mesh, int BlocksOnScreen, float2 SpriteSize)
//    {
//        int VertexStart = BlocksOnScreen * 4; // if every tile takes up 4 vertices then we use i * 4 to get the correct starting vertex
//        int IndexStart = BlocksOnScreen * 6; // read above and replace some words, and you might understand my nonsense

//        mesh.Vertices.RefElementAt(VertexStart).Pos = new float3(PlayerInfo.Pos, 0) + new float3(0.5f * PlayerInfo.Stats.RenderSize.x, 0.5f * PlayerInfo.Stats.RenderSize.y, 0); // top right
//        mesh.Vertices.RefElementAt(VertexStart).UV = SpriteSize;

//        mesh.Vertices.RefElementAt(VertexStart + 1).Pos = new float3(PlayerInfo.Pos, 0) + new float3(0.5f * PlayerInfo.Stats.RenderSize.x, -0.5f * PlayerInfo.Stats.RenderSize.y, 0); // top left
//        mesh.Vertices.RefElementAt(VertexStart + 1).UV = new float2(0, SpriteSize.y);

//        mesh.Vertices.RefElementAt(VertexStart + 2).Pos = new float3(PlayerInfo.Pos, 0) + new float3(-0.5f * PlayerInfo.Stats.RenderSize.x, 0.5f * PlayerInfo.Stats.RenderSize.y, 0); // bottom right
//        mesh.Vertices.RefElementAt(VertexStart + 2).UV = new float2(SpriteSize.x, 0);

//        mesh.Vertices.RefElementAt(VertexStart + 3).Pos = new float3(PlayerInfo.Pos, 0) + new float3(-0.5f * PlayerInfo.Stats.RenderSize.x, -0.5f * PlayerInfo.Stats.RenderSize.y, 0); // bottom left
//        mesh.Vertices.RefElementAt(VertexStart + 3).UV = 0;

//        uint UVertexStart = (uint)VertexStart;

//        mesh.Indices[IndexStart] = UVertexStart;
//        mesh.Indices[IndexStart + 1] = UVertexStart + 1;
//        mesh.Indices[IndexStart + 2] = UVertexStart + 2;

//        mesh.Indices[IndexStart + 3] = UVertexStart + 1;
//        mesh.Indices[IndexStart + 4] = UVertexStart + 3;
//        mesh.Indices[IndexStart + 5] = UVertexStart + 2;
//    }

//    [BurstCompile]
//    struct TilemapToMesh : IJobFor
//    {
//        [ReadOnly]
//        public Chunked2DArray<byte> TilemapArray;

//        [ReadOnly]
//        public BlobAssetReference<BlobArray<BlockType>> BlockTypes;

//        [NativeDisableContainerSafetyRestriction]
//        [WriteOnly]
//        public NativeArray<Vertex> Vertices;

//        [NativeDisableContainerSafetyRestriction]
//        [WriteOnly]
//        public NativeArray<uint> Indices;

//        [ReadOnly]
//        public float SpriteWidth;

//        [ReadOnly]
//        public float SpriteHeight;

//        [ReadOnly]
//        public int ScreenWidth;

//        [ReadOnly]
//        public int2 BottomLeftOfScreen;

//        public void Execute(int i)
//        {
//            int2 iWorldPos = StorageMethods.PosFromIndex(i, ScreenWidth);

//            int2 TrueWorldPos = iWorldPos + BottomLeftOfScreen;

//            if ((TrueWorldPos.x < 0 || TrueWorldPos.y < 0) || (TrueWorldPos.x > TilemapArray.FullGridWidth || TrueWorldPos.y > TilemapArray.FullGridWidth))
//            {
//                return;
//            }

//            int2 ChunkPos = TilemapArray.ChunkPosFromFullPos(TrueWorldPos);
//            int ChunkIndex = StorageMethods.IndexFromPos(ChunkPos, TilemapArray.ChunkGridWidth);
//            int BlockIndexStart = ChunkIndex * TilemapArray.ChunkWidthSquared;

//            int2 WorldPosStart = TilemapArray.FullPosFromChunkPos(ChunkPos);

//            int2 LocalPos = TrueWorldPos - WorldPosStart;

//            int LocalIndex = StorageMethods.IndexFromPos(LocalPos, TilemapArray.ChunkWidth);

//            int BlockIndex = BlockIndexStart + LocalIndex;

//            byte BlockTypeIndex = TilemapArray.FullArray[BlockIndex];

//            if (BlockTypeIndex == 0)
//            {
//                return;
//            }

//            BlockType BlockInfo = BlockTypes.Value[BlockTypeIndex];

//            int VertexStart = i * 4; // if every tile takes up 4 vertices then we use i * 4 to get the correct starting vertex
//            int IndexStart = i * 6; // read above and replace some words, and you might understand my nonsense

//            UnsafeElementAt(Vertices, VertexStart).Pos = new float3(TrueWorldPos, BlockInfo.Depth) + new float3(0.5f * BlockInfo.RenderingSize.x, 0.5f * BlockInfo.RenderingSize.y, 0); // top right
//            UnsafeElementAt(Vertices, VertexStart).UV = BlockInfo.UV + new float2(SpriteWidth, SpriteHeight);

//            UnsafeElementAt(Vertices, VertexStart + 1).Pos = new float3(TrueWorldPos, BlockInfo.Depth) + new float3(0.5f * BlockInfo.RenderingSize.x, -0.5f * BlockInfo.RenderingSize.y, 0); // bottom right
//            UnsafeElementAt(Vertices, VertexStart + 1).UV = BlockInfo.UV + new float2(SpriteWidth, 0);

//            UnsafeElementAt(Vertices, VertexStart + 2).Pos = new float3(TrueWorldPos, BlockInfo.Depth) + new float3(-0.5f * BlockInfo.RenderingSize.x, 0.5f * BlockInfo.RenderingSize.y, 0); // top left
//            UnsafeElementAt(Vertices, VertexStart + 2).UV = BlockInfo.UV + new float2(0, SpriteHeight);

//            UnsafeElementAt(Vertices, VertexStart + 3).Pos = new float3(TrueWorldPos, BlockInfo.Depth) + new float3(-0.5f * BlockInfo.RenderingSize.x, -0.5f * BlockInfo.RenderingSize.y, 0); // bottom left
//            UnsafeElementAt(Vertices, VertexStart + 3).UV = BlockInfo.UV;

//            uint UVertexStart = (uint)VertexStart;

//            Indices[IndexStart] = UVertexStart;
//            Indices[IndexStart + 1] = UVertexStart + 1;
//            Indices[IndexStart + 2] = UVertexStart + 2;

//            Indices[IndexStart + 3] = UVertexStart + 1;
//            Indices[IndexStart + 4] = UVertexStart + 3;
//            Indices[IndexStart + 5] = UVertexStart + 2;
//        }
//    }

//    #endregion
//}