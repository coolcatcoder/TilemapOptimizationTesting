using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System;
using UnityEditor;

public class BakeBiomes : MonoBehaviour
{
    public int2 SpriteSheetSize = 1;

    public bool Bake;

    public float2 DebugPos;

    // anything beyond this line is read only, don't edit in the inspector

    public Biome[] Biomes;

    public RandomPatternMapObject[] RandomPatternMapObjects;
    public SimplexPatternMapObject[] SimplexPatternMapObjects;
    public SimplexSmoothedPatternMapObject[] SimplexSmoothedPatternMapObjects;
}

[CustomEditor(typeof(BakeBiomes))]
public class BakeBiomesEditor : Editor
{
    public void OnSceneGUI()
    {
        var t = target as BakeBiomes;

        Handles.color = Color.blue;
        Handles.DrawWireCube(new float3(5, 5, 0), new float3(10, 10, 0.1f));

        GUI.color = Color.black;
        Handles.Label(new float3(0, 0, 0), "0%");
        Handles.Label(new float3(10, 0, 0), "100%");
        Handles.Label(new float3(0, 10, 0), "100%");

        var Biomes = FindObjectsOfType<BiomeMono>();

        for (int i = 0; i < Biomes.Length; i++)
        {
            var BiomeInfo = Biomes[i];

            if ( // https://developer.mozilla.org/en-US/docs/Games/Techniques/2D_collision_detection so good
                t.DebugPos.x < BiomeInfo.Pos.x + BiomeInfo.Size.x &&
                t.DebugPos.x + 0 > BiomeInfo.Pos.x &&
                t.DebugPos.y < BiomeInfo.Pos.y + BiomeInfo.Size.y &&
                t.DebugPos.y + 0 > BiomeInfo.Pos.y
                )
            {
                Handles.color = Color.red;
            }
            else
            {
                Handles.color = BiomeInfo.ReferenceColour;
            }

            Rect RectangleInfo = new(BiomeInfo.Pos, BiomeInfo.Size);

            Handles.DrawSolidRectangleWithOutline(RectangleInfo, BiomeInfo.ReferenceColour, Color.black);

            Handles.Label(new float3(BiomeInfo.Pos + 0.5f, 0), math.lerp(0, 100, BiomeInfo.Pos.x / 10).ToString());
        }

        GUI.color = Color.magenta;

        Handles.Label(new float3(t.DebugPos, 0), "o");
    }
}

public class BiomesBaker : Baker<BakeBiomes>
{
    public override void Bake(BakeBiomes authoring)
    {
        var BiomesMono = UnityEngine.Object.FindObjectsOfType<BiomeMono>(); // I can't work out why I need the full name here... Oh well...

        if (BiomesMono.Length <= 0)
        {
            Debug.LogError("Can't bake biomes due to lack of biomes.");
            return;
        }

        var Builder = new BlobBuilder(Allocator.Temp);
        
        ref var Root = ref Builder.ConstructRoot<BiomesAndMapObjectsBlob>();

        var Biomes = Builder.Allocate(ref Root.Biomes, BiomesMono.Length);
        
        var RandomPatternMapObjects = Builder.Allocate(ref Root.RandomPatternMapObjects, UnityEngine.Object.FindObjectsOfType<RandomPatternMapObjectMono>().Length);
        var SimplexPatternMapObjects = Builder.Allocate(ref Root.SimplexPatternMapObjects, UnityEngine.Object.FindObjectsOfType<SimplexPatternMapObjectMono>().Length);
        var SimplexSmoothedPatternMapObjects = Builder.Allocate(ref Root.SimplexSmoothedPatternMapObjects, UnityEngine.Object.FindObjectsOfType<SimplexSmoothedPatternMapObjectMono>().Length);

        byte RandomPatternMapObjectIndex = 0;
        byte SimplexPatternMapObjectIndex = 0;
        byte SimplexSmoothedPatternMapObjectIndex = 0;

        for (int i = 0; i < BiomesMono.Length; i++)
        {
            var Children = BiomesMono[i].GetComponentsInChildren<MapObjectMono>();  // Gets all children as mono map objects

            Biome CurrentBiome = new();
            CurrentBiome.Size = BiomesMono[i].Size;
            CurrentBiome.Pos = BiomesMono[i].Pos;

            CurrentBiome.RandomPatternStartingIndex = RandomPatternMapObjectIndex;
            CurrentBiome.RandomPatternLength = BiomesMono[i].GetComponentsInChildren<RandomPatternMapObjectMono>().Length;

            CurrentBiome.SimplexPatternStartingIndex = SimplexPatternMapObjectIndex;
            CurrentBiome.SimplexPatternLength = BiomesMono[i].GetComponentsInChildren<SimplexPatternMapObjectMono>().Length;

            CurrentBiome.SimplexSmoothedPatternStartingIndex = SimplexSmoothedPatternMapObjectIndex;
            CurrentBiome.SimplexSmoothedPatternLength = BiomesMono[i].GetComponentsInChildren<SimplexSmoothedPatternMapObjectMono>().Length;

            for (int ci = 0; ci < Children.Length; ci++)
            {
                var Child = Children[ci];

                switch (Child.Pattern)
                {
                    case MapObjectPattern.Random:
                        var RandomPatternMapObjectInfo = Child.GetComponent<RandomPatternMapObjectMono>();
                        RandomPatternMapObjects[RandomPatternMapObjectIndex] = new RandomPatternMapObject()
                        {
                            Data = new MapObject()
                            {
                                UV = new float2(1f/authoring.SpriteSheetSize.x * (Child.MapObjectSprite-1),0), // experimental fix? Basically if the entire sprite sheet is 1 unit long, then we do 1 / LengthOfSpriteSheet to get the width of 1 sprite. We then multiply that by the sprite number - 1 to account for starting from 0
                                Depth = Child.Depth,
                                Pattern = Child.Pattern,
                                Behaviour = Child.Behaviour,
                                StrengthToCross = Child.StrengthToCross,
                                Stats = Child.Stats
                            },

                            RenderingSize = RandomPatternMapObjectInfo.RenderingSize,
                            CollisionSize = RandomPatternMapObjectInfo.CollisionSize,

                            Chance = RandomPatternMapObjectInfo.PercentChance / 100
                        };

                        RandomPatternMapObjectIndex++;
                        break;

                    case MapObjectPattern.Simplex:
                        var SimplexPatternMapObjectInfo = Child.GetComponent<SimplexPatternMapObjectMono>();
                        SimplexPatternMapObjects[SimplexPatternMapObjectIndex] = new SimplexPatternMapObject()
                        {
                            Data = new MapObject()
                            {
                                UV = 0, // fix asap
                                Depth = Child.Depth,
                                Pattern = Child.Pattern,
                                Behaviour = Child.Behaviour,
                                StrengthToCross = Child.StrengthToCross,
                                Stats = Child.Stats
                            },

                            RenderingSize = SimplexPatternMapObjectInfo.RenderingSize,
                            CollisionSize = SimplexPatternMapObjectInfo.CollisionSize,

                            Chance = SimplexPatternMapObjectInfo.PercentChance / 100,

                            MinNoise = SimplexPatternMapObjectInfo.MinNoise,
                            MaxNoise = SimplexPatternMapObjectInfo.MaxNoise,

                            Seed = Unity.Mathematics.Random.CreateFromIndex(SimplexPatternMapObjectInfo.Seed).NextUInt(),

                            Scale = SimplexPatternMapObjectInfo.Scale // there has to be a better way...
                        };

                        SimplexPatternMapObjectIndex++;
                        break;

                    case MapObjectPattern.SimplexSmoothed:
                        var SimplexSmoothedPatternMapObjectInfo = Child.GetComponent<SimplexSmoothedPatternMapObjectMono>();
                        SimplexSmoothedPatternMapObjects[SimplexSmoothedPatternMapObjectIndex] = new SimplexSmoothedPatternMapObject()
                        {
                            Data = new MapObject()
                            {
                                UV = 0, // fix asap
                                Depth = Child.Depth,
                                Pattern = Child.Pattern,
                                Behaviour = Child.Behaviour,
                                StrengthToCross = Child.StrengthToCross,
                                Stats = Child.Stats
                            },

                            Chance = SimplexSmoothedPatternMapObjectInfo.PercentChance / 100,

                            MinNoise = SimplexSmoothedPatternMapObjectInfo.MinNoise,
                            MaxNoise = SimplexSmoothedPatternMapObjectInfo.MaxNoise,

                            Seed = Unity.Mathematics.Random.CreateFromIndex(SimplexSmoothedPatternMapObjectInfo.Seed).NextUInt(),

                            Scale = SimplexSmoothedPatternMapObjectInfo.Scale
                        };

                        SimplexSmoothedPatternMapObjectIndex++;
                        break;
                }
            }

            Biomes[i] = CurrentBiome;
        }

        // debug authoring stuff
        authoring.Biomes = new Biome[Biomes.Length];
        authoring.RandomPatternMapObjects = new RandomPatternMapObject[RandomPatternMapObjects.Length];
        authoring.SimplexPatternMapObjects = new SimplexPatternMapObject[SimplexPatternMapObjects.Length];
        authoring.SimplexSmoothedPatternMapObjects = new SimplexSmoothedPatternMapObject[SimplexSmoothedPatternMapObjects.Length];

        for (int i = 0; i < Biomes.Length; i++)
        {
            authoring.Biomes[i] = Biomes[i];
        }

        for (int i = 0; i < RandomPatternMapObjects.Length; i++)
        {
            authoring.RandomPatternMapObjects[i] = RandomPatternMapObjects[i];
        }

        for (int i = 0; i < SimplexPatternMapObjects.Length; i++)
        {
            authoring.SimplexPatternMapObjects[i] = SimplexPatternMapObjects[i];
        }

        for (int i = 0; i < SimplexSmoothedPatternMapObjects.Length; i++)
        {
            authoring.SimplexSmoothedPatternMapObjects[i] = SimplexSmoothedPatternMapObjects[i];
        }

        var entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new BiomesAndMapObjects
        {
            DataBlob = Builder.CreateBlobAssetReference<BiomesAndMapObjectsBlob>(Allocator.Persistent)
        });
    }
}

public struct BiomesAndMapObjects : IComponentData // potentially split into 2 seperate components?
{
    public BlobAssetReference<BiomesAndMapObjectsBlob> DataBlob; // bad name?
}

public struct BiomesAndMapObjectsBlob
{
    public BlobArray<Biome> Biomes;

    //public BlobArray<MapObject> MapObjects; This can't exist. Each other struct should rather just contain a MapObject field. There isn't a better way from what I can tell.

    public BlobArray<RandomPatternMapObject> RandomPatternMapObjects;
    public BlobArray<SimplexPatternMapObject> SimplexPatternMapObjects;
    public BlobArray<SimplexSmoothedPatternMapObject> SimplexSmoothedPatternMapObjects;
}

[System.Serializable]
public struct Biome // having different simplex scales could be nice, should we want quite large fields, but quite small circles of flowers in the same biome
{
    public float2 Size;
    public float2 Pos;

    public byte RandomPatternStartingIndex;
    public int RandomPatternLength;

    // simplex stuff will have to be looped through due to each one having a different scale and seed...

    public byte SimplexPatternStartingIndex;
    public int SimplexPatternLength;

    public byte SimplexSmoothedPatternStartingIndex;
    public int SimplexSmoothedPatternLength;
}

[System.Serializable]
public struct MapObject // we may want priority? Check physical note on desk for more info.
{
    public float2 UV;

    public int Depth;

    public MapObjectPattern Pattern; // do we really need this? Isn't this just a helper during baking... Seems we could just remove it without anything breaking?

    public CollisionBehaviour Behaviour;

    public uint StrengthToCross;

    public Statistics Stats;

    //consider moving chance to here as so far every pattern uses it...
}

[System.Serializable]
public struct RandomPatternMapObject
{
    public MapObject Data; // bad name

    public float2 RenderingSize;
    public float2 CollisionSize;

    public float Chance;
}

[System.Serializable]
public struct SimplexPatternMapObject
{
    public MapObject Data;

    public float2 RenderingSize;
    public float2 CollisionSize;

    public float Chance;

    public float MinNoise;
    public float MaxNoise;

    public uint Seed; // this should be some insane uint created during baking from the boring seed in the mono

    public float Scale;
}

[System.Serializable]
public struct SimplexSmoothedPatternMapObject
{
    public MapObject Data;

    public float Chance; // usually you don't want holes in your smoothed simplex patterns, but could be useful in some weird cases

    public float MinNoise;
    public float MaxNoise;

    public uint Seed; // Creating a Random from this number should give a consistent seed for each number. So for blocks that want the same simplex pattern as eachother, you use the same seed. If you don't want them to have to the same pattern, use a different seed.

    public float Scale; // index into the scale array
}