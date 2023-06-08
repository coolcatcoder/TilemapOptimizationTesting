using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;

public partial struct PlayerSystem : ISystem, ISystemStartStop
{
    //[BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<InputData>();
        state.RequireForUpdate<Stats>();
    }

    public void OnStartRunning(ref SystemState state)
    {
        Object.FindObjectOfType<Camera>().transparencySortMode = TransparencySortMode.CustomAxis;
        Object.FindObjectOfType<Camera>().transparencySortAxis = new float3(0, 0, -1);
    }

    public void OnUpdate(ref SystemState state)
    {
        InputData InputInfo = SystemAPI.GetSingleton<InputData>();
        ref Stats PlayerStats = ref SystemAPI.GetSingletonRW<Stats>().ValueRW;

        float MaxSpeed;

        if (PlayerStats.Sprinting)
        {
            MaxSpeed = PlayerStats.SprintSpeed;
        }
        else
        {
            MaxSpeed = PlayerStats.WalkSpeed;
        }

        PlayerStats.Speed = math.lerp(0, MaxSpeed, math.clamp(InputInfo.TimeHeldFor, 0, 1));

        if (PlayerStats.Speed == 0)
        {
            return;
        }

        PlayerStats.HasMoved = true; // I think?

        PlayerStats.Pos += InputInfo.Movement * PlayerStats.Speed;

        PlayerStats.Pos = math.clamp(PlayerStats.Pos, 0, float.MaxValue);

        Object.FindObjectOfType<Camera>().transform.position = new float3(PlayerStats.Pos, -10);
    }

    public void OnStopRunning(ref SystemState state)
    {

    }
}