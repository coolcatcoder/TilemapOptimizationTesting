//using UnityEngine;
//using Unity.Entities;
//using Unity.Collections;

//public class MapObjectTypesMono : MonoBehaviour
//{
//    public bool ForceBake;
//}

//public class MapObjectTypesBaker : Baker<MapObjectTypesMono>
//{
//    public override void Bake(MapObjectTypesMono authoring)
//    {
//        Debug.Log("Baked map object types with force!");

//        int RandomPatternMapObjectsAmount = -37; // temp

//        var Biomes = Object.FindObjectsOfType<Biome>();

//        for (int i = 0; i < Biomes.Length; i++)
//        {
//            RandomPatternMapObjectsAmount += Biomes[i].RandomPatternMapObjects.Length;
//        }

//        var entity = GetEntity(TransformUsageFlags.None);

//        var Builder = new BlobBuilder(Allocator.Temp);
//        ref var TypesBlob = ref Builder.ConstructRoot<BlobMapObjectTypes>();

//        ref BlobArray<RandomPatternMapObject> RandomPatternMapObjects = ref TypesBlob.Allocate(ref TypesBlob, RandomPatternMapObjectsAmount);

//        for (int i = 0; i < Biomes.Length; i++)
//        {
//            BlockTypesArrayBuilder[i] = new BlockType
//            {
//                UV = new float2(SpriteWidth * BT.BlockSprite, 0), // bottom left hand corner should be (1/NumSprites*Sprite, 0)
//                //BlockMat = BT.BlockMat,
//                Depth = BT.Depth,
//                RenderingSize = BT.RenderingSize,
//                CollisionSize = BT.CollisionSize,
//                Behaviour = BT.Behaviour,
//                StrengthToCross = BT.StrengthToCross,
//                StatsChange = new Stats()
//                {
//                    Size = BT.StatsChange.Size,
//                    Health = BT.StatsChange.Health,
//                    Stamina = BT.StatsChange.Stamina,
//                    Strength = BT.StatsChange.Strength,
//                    Speed = BT.StatsChange.Speed,
//                    SprintSpeed = BT.StatsChange.SprintSpeed,
//                    WalkSpeed = BT.StatsChange.WalkSpeed
//                },
//                MinNoise = BT.MinNoise,
//                MaxNoise = BT.MaxNoise,
//                Chance = BT.PercentChance / 100
//            };
//        }
//    }
//}