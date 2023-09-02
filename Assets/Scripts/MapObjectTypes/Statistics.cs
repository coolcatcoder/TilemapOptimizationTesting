using Unity.Mathematics;
using Unity.Entities;

[System.Serializable]
public struct Statistics // the only stats that are allowed here are stats used by absolutely everything. So no speed stat, cause not all things move, etc.
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