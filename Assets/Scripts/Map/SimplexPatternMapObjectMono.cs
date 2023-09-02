using UnityEngine;
using Unity.Mathematics;

public class SimplexPatternMapObjectMono : MonoBehaviour
{
    public float2 RenderingSize;
    public float2 CollisionSize;

    public float PercentChance;

    public float MinNoise;
    public float MaxNoise;

    public uint Seed; // Creating a Random from this number should give a consistent seed for each number. So for blocks that want the same simplex pattern as eachother, you use the same seed. If you don't want them to have to the same pattern, use a different seed.

    public float Scale; // this will get chucked into an array hence why for the non-mono it is a uint, cause it is an index into the array! Highly experimental!
}
