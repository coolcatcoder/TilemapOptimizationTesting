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
    public float2 Size; // controls rendering size and collision size, for now

    public float2 Pos; // Not float 3 cause not including depth. I think this is good?
    public float2 PreviousPos; // Position on the previous frame

    public int Health;
    public int Stamina;
    public int Strength;

    public bool Sprinting;
    public float Speed;
    public float SprintSpeed;
    public float WalkSpeed;
    public bool HasMoved;

    public bool ForceUpdate;

    // implement an addition operator
    public static Stats operator +(Stats x, Stats y)
    {
        return new Stats()
        {
            Pos = x.Pos,
            PreviousPos = x.PreviousPos,
            Sprinting = x.Sprinting,
            HasMoved = x.HasMoved,
            ForceUpdate = x.ForceUpdate,

            Size = math.clamp(x.Size + y.Size, 0.1f, float.MaxValue),
            Health = math.clamp(x.Health + y.Health, 0, int.MaxValue),
            Stamina = x.Stamina + y.Stamina,
            Strength = math.clamp(x.Strength + y.Strength, 1, int.MaxValue),
            Speed = x.Speed + y.Speed,
            SprintSpeed = x.SprintSpeed + y.SprintSpeed,
            WalkSpeed = x.WalkSpeed + y.WalkSpeed
        };
    }
}