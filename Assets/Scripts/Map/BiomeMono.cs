using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System;
using UnityEditor;

public class BiomeMono : MonoBehaviour // any map objects can be done as children with a bunch of mono behaviours on them for what is needed
{
    public float2 Size;
    public float2 Pos;

    public Color ReferenceColour; // discarded
}

[CustomEditor(typeof(BiomeMono))]
public class BiomeMonoEditor : Editor
{
    public void OnSceneGUI()
    {
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
            
            Handles.color = BiomeInfo.ReferenceColour;

            Rect RectangleInfo = new(BiomeInfo.Pos, BiomeInfo.Size);

            Handles.DrawSolidRectangleWithOutline(RectangleInfo, BiomeInfo.ReferenceColour, Color.black);

            Handles.Label(new float3(BiomeInfo.Pos + 0.5f, 0), math.lerp(0, 100, BiomeInfo.Pos.x / 10).ToString());
        }

        GUI.color = Color.magenta;
    }
}