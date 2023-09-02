using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct PlayerSystem : ISystem//, ISystemStartStop
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<InputData>();
        //state.RequireForUpdate<Stats>();
    }

    //public void OnStartRunning(ref SystemState state)
    //{
    //    Object.FindObjectOfType<Camera>().transparencySortMode = TransparencySortMode.CustomAxis;
    //    Object.FindObjectOfType<Camera>().transparencySortAxis = new float3(0, 0, -1);
    //}

    public void OnUpdate(ref SystemState state)
    {
        //ref InputData InputInfo = ref SystemAPI.GetSingletonRW<InputData>().ValueRW;
        //ref Stats PlayerStats = ref SystemAPI.GetSingletonRW<Stats>().ValueRW;

        //if (PlayerStats.ForceUpdate)
        //{
        //    PlayerStats.ForceUpdate = false;
        //    Object.FindObjectOfType<Camera>().transform.position = new float3(PlayerStats.Pos, -10);
        //}

        //PlayerStats.PreviousPos = PlayerStats.Pos;

        //if (InputInfo.SprintPressed)
        //{
        //    InputInfo.SprintPressed = false;
        //    PlayerStats.Sprinting = !PlayerStats.Sprinting; // is there a more efficient way of writing this lol?
        //}

        //float MaxSpeed;

        //if (PlayerStats.Sprinting)
        //{
        //    MaxSpeed = PlayerStats.SprintSpeed;
        //}
        //else
        //{
        //    MaxSpeed = PlayerStats.WalkSpeed;
        //}

        //PlayerStats.Speed = math.lerp(0, MaxSpeed, math.clamp(InputInfo.TimeMovementInputHeldFor, 0, 1));

        //if (PlayerStats.Speed == 0)
        //{
        //    return;
        //}

        //PlayerStats.HasMoved = true; // I think?

        //PlayerStats.Pos += InputInfo.Movement * PlayerStats.Speed * SystemAPI.Time.DeltaTime;

        //PlayerStats.Pos = math.clamp(PlayerStats.Pos, 0, float.MaxValue);

        //Object.FindObjectOfType<Camera>().transform.position = new float3(PlayerStats.Pos, -10);
    }

    //public void OnStopRunning(ref SystemState state)
    //{

    //}
}