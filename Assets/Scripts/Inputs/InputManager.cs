using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

public partial class InputManager : SystemBase // Stolen from SmallSight hence the weird commented out stuff, and slightly different naming scheme. This should be updated at some point.
{
    protected override void OnStartRunning()
    {
        EntityManager.AddComponent<InputData>(EntityManager.CreateEntity());
        UnityEngine.Object.FindObjectOfType<PlayerInput>().actionEvents[0].AddListener(ThrowMovement);
        UnityEngine.Object.FindObjectOfType<PlayerInput>().actionEvents[13].AddListener(ThrowSprint);
        //UnityEngine.Object.FindObjectOfType<PlayerInput>().actionEvents[11].AddListener(ThrowTeleport);
        //UnityEngine.Object.FindObjectOfType<PlayerInput>().actionEvents[12].AddListener(ThrowCamera);
        //UnityEngine.Object.FindObjectOfType<PlayerInput>().actionEvents[13].AddListener(ThrowYMove);

        //var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(11380664438141642328);
        //var type = TypeManager.GetType(typeIndex);
        //Debug.Log(type);
    }

    protected override void OnUpdate()
    {
        ref var InputInfo = ref SystemAPI.GetSingletonRW<InputData>().ValueRW;

        if (InputInfo.MovementInputHeld)
        {
            InputInfo.TimeMovementInputHeldFor += SystemAPI.Time.DeltaTime;
        }
    }

    public void ThrowMovement(InputAction.CallbackContext context)
    {
        ref var InputInfo = ref SystemAPI.GetSingletonRW<InputData>().ValueRW;

        if (context.canceled)
        {
            InputInfo.TimeMovementInputHeldFor = 0;
        }

        InputInfo.MovementInputHeld = !context.canceled;
        InputInfo.Movement = context.ReadValue<Vector2>();
    }

    public void ThrowSprint(InputAction.CallbackContext context)
    {
        ref var InputInfo = ref SystemAPI.GetSingletonRW<InputData>().ValueRW;

        InputInfo.SprintPressed = !context.canceled;
    }
}

public struct InputData : IComponentData
{
    public bool MovementInputHeld;
    public float TimeMovementInputHeldFor;
    public float2 Movement;

    public bool SprintPressed;
}