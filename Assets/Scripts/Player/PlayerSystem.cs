using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct PlayerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<InputData>();
        state.RequireForUpdate<PlayerData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ref InputData InputInfo = ref SystemAPI.GetSingletonRW<InputData>().ValueRW;
        ref PlayerData PlayerInfo = ref SystemAPI.GetSingletonRW<PlayerData>().ValueRW;

        PlayerInfo.PreviousPos = PlayerInfo.Pos;

        if (InputInfo.SprintPressed)
        {
            InputInfo.SprintPressed = false;
            PlayerInfo.Sprinting = !PlayerInfo.Sprinting; // is there a more efficient way of writing this lol?
        }

        float MaxSpeed;

        if (PlayerInfo.Sprinting)
        {
            MaxSpeed = PlayerInfo.SprintSpeed;
        }
        else
        {
            MaxSpeed = PlayerInfo.WalkSpeed;
        }

        PlayerInfo.Speed = math.lerp(0, MaxSpeed, math.clamp(InputInfo.TimeMovementInputHeldFor, 0, 1));

        if (PlayerInfo.Speed == 0)
        {
            return;
        }

        PlayerInfo.Pos += InputInfo.Movement * PlayerInfo.Speed * SystemAPI.Time.DeltaTime;

        PlayerInfo.Pos = math.clamp(PlayerInfo.Pos, 0, float.MaxValue);
    }
}