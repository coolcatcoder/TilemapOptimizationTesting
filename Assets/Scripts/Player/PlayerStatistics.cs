using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

public class PlayerStatistics : MonoBehaviour
{
    public Stats StartingStats;
}

public class PlayerStatisticsBaker : Baker<PlayerStatistics>
{
    public override void Bake(PlayerStatistics authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, authoring.StartingStats);
    }
}

[System.Serializable]
public struct Stats : IComponentData
{
    public float2 Pos; // Not float 3 cause not including depth. I think this is good?
    public uint Health; // perhaps should be int?
    public int Stamina;
    public bool Sprinting;
    public float Speed;
    public float SprintSpeed;
    public float WalkSpeed;
    public bool HasMoved;
}