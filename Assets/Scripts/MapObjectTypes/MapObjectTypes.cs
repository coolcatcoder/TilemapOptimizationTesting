using Unity.Entities;
using Unity.Mathematics;

public struct MapObjectTypes : IComponentData
{
    // 1 array for the random pattern, 1 for simplex, 1 for etc

    public BlobAssetReference<BlobMapObjectTypes> BlockTypes;

}

public struct BlobMapObjectTypes
{
    BlobArray<RandomPatternMapObject> RandomPatternMapObjects;
}

public struct RandomPatternMapObject
{
    public float2 UV;

    public int Depth;

    public CollisionBehaviour Behaviour;

    public uint StrengthToCross;

    public Statistics StatsChange;

    public float Chance;
}

public enum CollisionBehaviour // follow the powers of 2!
{
    None = 0,
    Consume = 1
}