using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

public class PlayerSettings : MonoBehaviour
{
    public Statistics StartingStats;
}

public class PlayerSettingsBaker : Baker<PlayerSettings>
{
    public override void Bake(PlayerSettings authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new PlayerData()
        {
            Stats = authoring.StartingStats
        });
    }
}

[System.Serializable]
public struct PlayerData : IComponentData
{
    public float2 Pos; // Not float 3 cause not including depth. I think this is good?
    public float2 PreviousPos; // Position on the previous frame

    public bool Sprinting;
    public float Speed;
    public float SprintSpeed;
    public float WalkSpeed;

    public Statistics Stats;

    public float3 BottomLeftPosOfScreen;
    public float3 TopRightPosOfScreen;
}