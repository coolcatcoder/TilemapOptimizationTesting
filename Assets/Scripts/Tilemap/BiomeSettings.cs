using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System;
using UnityEditor;

public class BiomeSettings : MonoBehaviour
{
    public bool ButtonTest;

    public Mesh RenderLocation;

    public BiomeMonoNew[] Biomes;
}

[System.Serializable]
public struct BiomeMonoNew
{
    public string BiomeName; //discarded

    public Color ReferenceColour;

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

            Handles.color = BiomeInfo.ReferenceColour;

            Rect RectangleInfo = new(BiomeInfo.Pos, BiomeInfo.Size);

            Handles.DrawSolidRectangleWithOutline(RectangleInfo, BiomeInfo.ReferenceColour, Color.black);

            Handles.Label(new float3(BiomeInfo.Pos + 0.5f, 0), math.lerp(0, 100, BiomeInfo.Pos.x/10).ToString());
        }
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

        if (authoring.ButtonTest)
        {
            authoring.ButtonTest = false;
            Debug.Log("pressed");
        }

        var BiomesBuilder = new BlobBuilder(Allocator.Temp);
        ref var BiomesArray = ref BiomesBuilder.ConstructRoot<BlobArray<TilemapSystem.Biome>>();

        BlobBuilderArray<TilemapSystem.Biome> BiomesArrayBuilder = BiomesBuilder.Allocate(ref BiomesArray, authoring.Biomes.Length);

        for (int i = 0; i < authoring.Biomes.Length; i++)
        {
            var BiomeInfo = authoring.Biomes[i];

            BiomesArrayBuilder[i] = new TilemapSystem.Biome()
            {
                //IdealConditions = BiomeInfo.IdealConditions,
                StartingPlantIndex = BiomeInfo.StartingPlantIndex,
                PlantLength = BiomeInfo.PlantLength,
                StartingBlockIndex = BiomeInfo.StartingBlockIndex,
                BlockLength = BiomeInfo.BlockLength
            };
        }

        var entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new BiomeSettingsData
        {
            Biomes = BiomesBuilder.CreateBlobAssetReference<BlobArray<TilemapSystem.Biome>>(Allocator.Persistent)
        });

        BiomesBuilder.Dispose();
    }
}

public struct BiomeSettingsData : IComponentData
{
    public BlobAssetReference<BlobArray<TilemapSystem.Biome>> Biomes;
}