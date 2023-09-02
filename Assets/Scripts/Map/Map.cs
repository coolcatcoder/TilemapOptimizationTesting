using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class MapMono : MonoBehaviour
{
    public int MapWidth;
    public int ChunkWidth;

    public bool DebugChunk0;

    public int SpriteGridWidth;
    public int SpriteGridHeight;
}

public class MapBaker : Baker<MapMono>
{
    public override void Bake(MapMono authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        //AddComponent(entity, )
    }
}

public struct Map : IComponentData
{
    public float2 SpriteSize;
}

public struct MapContainers : IComponentData
{
    public Chunked2DArray<byte> Tilemap; // Tilemap patterned data belongs in this array
}