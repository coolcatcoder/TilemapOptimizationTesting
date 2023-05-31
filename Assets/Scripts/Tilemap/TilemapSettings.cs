using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class TilemapSettings : MonoBehaviour
{
    public int TilemapSize;
    public int Trials;
    public int2 Pos;
    public int GenerationWidth;
    public int ChunkWidth;
}

public class TilemapBaker : Baker<TilemapSettings>
{
    public override void Bake(TilemapSettings authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Renderable);

        AddComponent(entity, new TilemapSettingsData
        {
            TilemapSize = authoring.TilemapSize,
            Trials = authoring.Trials,
            Pos = authoring.Pos,
            GenerationWidth = authoring.GenerationWidth,
            ChunkWidth = authoring.ChunkWidth
        });
    }
}

public struct TilemapSettingsData : IComponentData
{
    public int TilemapSize;
    public int Trials;
    public int2 Pos;
    public int GenerationWidth;
    public int ChunkWidth;
}
