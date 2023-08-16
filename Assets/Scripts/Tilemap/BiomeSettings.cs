using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System;
using UnityEditor;

public class BiomeSettings : MonoBehaviour
{
    public float2 DebugPos;

    public BiomeMono[] Biomes;
}

[System.Serializable]
public struct BiomeMono
{
    public string BiomeName; //discarded

    public Color ReferenceColour; //discarded

    public float2 Size;
    public float2 Pos;

    public byte StartingPlantIndex;
    public int PlantLength;

    public byte StartingBlockIndex;
    public int BlockLength;

    // add terrain generation scale and stuff later
}

[System.Serializable]
public struct Biome
{
    public float2 Size;
    public float2 Pos;

    public byte StartingPlantIndex;
    public int PlantLength;

    public byte StartingBlockIndex;
    public int BlockLength;

    // add terrain generation scale and stuff later
}

[CustomEditor(typeof(BiomeSettings))]
public class BiomeEditor : Editor
{
    public void OnSceneGUI()
    {
        var t = target as BiomeSettings;

        Handles.color = Color.blue;
        Handles.DrawWireCube(new float3(5, 5, 0), new float3(10, 10, 0.1f));

        GUI.color = Color.black;
        Handles.Label(new float3(0, 0, 0), "0%");
        Handles.Label(new float3(10, 0, 0), "100%");
        Handles.Label(new float3(0, 10, 0), "100%");

        for (int i = 0; i < t.Biomes.Length; i++)
        {
            var BiomeInfo = t.Biomes[i];

            //PlayerPos += PlayerSize * -0.5f + 0.5f; // why the hell do these 2 lines exist???????
            //BlockPos += BlockSize * -0.5f + 0.5f;

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

            Handles.Label(new float3(BiomeInfo.Pos + 0.5f, 0), math.lerp(0, 100, BiomeInfo.Pos.x/10).ToString());
        }

        GUI.color = Color.magenta;

        Handles.Label(new float3(t.DebugPos, 0), "o");
    }
}

public class BiomeSettingsBaker : Baker<BiomeSettings>
{
    public override void Bake(BiomeSettings authoring)
    {
        if (authoring.Biomes.Length <= 0)
        {
            Debug.LogError("Can't bake biomes due to lack of biomes.");
            return;
        }

        var BiomesBuilder = new BlobBuilder(Allocator.Temp);
        ref var BiomesArray = ref BiomesBuilder.ConstructRoot<BlobArray<Biome>>();

        BlobBuilderArray<Biome> BiomesArrayBuilder = BiomesBuilder.Allocate(ref BiomesArray, authoring.Biomes.Length);

        for (int i = 0; i < authoring.Biomes.Length; i++)
        {
            var BiomeInfo = authoring.Biomes[i];

            BiomesArrayBuilder[i] = new Biome()
            {
                Size = BiomeInfo.Size / 10, // divide 10 to put it in range of 0-1
                Pos = BiomeInfo.Pos / 10,
                StartingPlantIndex = BiomeInfo.StartingPlantIndex,
                PlantLength = BiomeInfo.PlantLength,
                StartingBlockIndex = BiomeInfo.StartingBlockIndex,
                BlockLength = BiomeInfo.BlockLength
            };
        }

        var entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new BiomeSettingsData
        {
            Biomes = BiomesBuilder.CreateBlobAssetReference<BlobArray<Biome>>(Allocator.Persistent)
        });

        BiomesBuilder.Dispose();
    }
}

public struct BiomeSettingsData : IComponentData
{
    public BlobAssetReference<BlobArray<Biome>> Biomes;
}