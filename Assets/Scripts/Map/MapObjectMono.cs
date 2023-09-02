using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class MapObjectMono : MonoBehaviour // stuff that is the same for all map objects no matter what!
{
    public int MapObjectSprite;

    public int Depth;

    public MapObjectPattern Pattern;

    public CollisionBehaviour Behaviour;

    public uint StrengthToCross;

    public Statistics Stats;
}

public enum MapObjectPattern // no powers of 2
{
    Random = 1,
    Simplex = 2,
    SimplexSmoothed = 3,
}

public enum CollisionBehaviour // powers of 2 are powerful!
{
    None = 0,
    Consume = 1
}

[System.Serializable]
public struct Statistics // I don't know anymore! This should be player only... Right??? So should this go in the player assembly? I don't know! This is scary!
{
    public float2 RenderSize;
    public float2 CollisionSize;

    public int Health;
    public int Stamina;
    public int Strength;

    public static Statistics operator +(Statistics x, Statistics y)
    {
        return new Statistics()
        {
            RenderSize = math.clamp(x.RenderSize + y.RenderSize, 0.1f, float.MaxValue),
            CollisionSize = math.clamp(x.CollisionSize + y.CollisionSize, 0.1f, float.MaxValue),
            Health = math.clamp(x.Health + y.Health, 0, int.MaxValue),
            Stamina = x.Stamina + y.Stamina,
            Strength = math.clamp(x.Strength + y.Strength, 1, int.MaxValue),
        };
    }
}
