using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

public struct MarchingSquares // todo: stop saddle points from being pain and suffering
{
    public static readonly int2[][] VertexTable =
    {
        new int2[]{}, // done
        new int2[]{new int2(-1,0), new int2(0,-1), new int2(-1,-1)}, // done
        new int2[]{new int2(1,0), new int2(1,-1), new int2(0,-1)}, // done
        new int2[]{new int2(-1,0), new int2(-1,-1), new int2(1,0), new int2(1,-1)}, //done
        new int2[]{new int2(0,1), new int2(1,1), new int2(1,0)}, // done
        new int2[]{new int2(),}, // ambiguous
        new int2[]{new int2(0,1), new int2(0,-1), new int2(1,1), new int2(1,-1)}, // done
        new int2[]{new int2(-1,0), new int2(-1,-1), new int2(0,1), new int2(1,1), new int2(1,-1)}, //done
        new int2[]{new int2(-1,1), new int2(0,1), new int2(-1,0)}, //done
        new int2[]{new int2(-1,1), new int2(-1,-1), new int2(0,1), new int2(0,-1)}, //done
        new int2[]{}, // ambiguous
        new int2[]{new int2(-1,1), new int2(-1, -1), new int2(0,1), new int2(1,0), new int2(1,-1)}, //done
        new int2[]{new int2(-1,1), new int2(-1,0), new int2(1,1), new int2(1,0)}, // done
        new int2[]{new int2(-1,1), new int2(-1,-1), new int2(0,-1), new int2(1,1), new int2(1,0)}, //done
        new int2[]{new int2(-1,1), new int2(-1,0), new int2(0,-1), new int2(1,1), new int2(1,-1)}, //done
        new int2[]{new int2(-1,1), new int2(-1,-1), new int2(1,1), new int2(1,-1)}// done
    };

    public static readonly uint[][] IndexTable = // each 3 uints is a triangle
    {
        new uint[]{}, //done
        new uint[]{0,1,2}, //done
        new uint[]{0,1,2}, //done
        new uint[]{0,2,1, 1,2,3}, //done
        new uint[]{0,1,2}, //done
        new uint[]{}, // ambiguous
        new uint[]{0,2,4, 0,3,1}, //done
        new uint[]{0,4,1, 0,2,4, 2,3,4}, //done
        new uint[]{0,1,2}, //done
        new uint[]{0,2,3, 0,3,1}, //done
        new uint[]{}, // ambiguous
        new uint[]{0,2,1, 2,3,1, 3,4,1}, //done
        new uint[]{0,3,1, 0,2,3}, //done
        new uint[]{0,2,1, 0,4,2, 0,3,4}, //done
        new uint[]{0,3,1, 1,3,2, 2,3,4}, //done
        new uint[]{0,2,1, 2,3,1} //done
    };

    public static byte CalculateSquareIndex(bool4 Corners)
    {
        return (byte)math.csum(math.select(0, new int4(1, 2, 4, 8), Corners)); // stolen from https://eetumaenpaa.fi/blog/marching-cubes-optimizations-in-unity
    }

    [BurstCompile]
    public struct MarchSquares1 : IJobFor
    {
        [ReadOnly]
        public NativeList<bool4> Squares;

        [WriteOnly]
        public NativeList<byte>.ParallelWriter SquareIndices;

        public void Execute(int i)
        {
            SquareIndices.AddNoResize(CalculateSquareIndex(Squares[i]));
        }
    }

    [BurstCompile]
    public struct MarchSquares2 : IJobFor
    {
        [ReadOnly]
        public NativeList<bool4> Squares;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> SquareIndices;

        public void Execute(int i)
        {
            SquareIndices[i] = CalculateSquareIndex(Squares[i]);
        }
    }

    [BurstCompile]
    public struct TablesToMesh : IJobFor // terrible name! This just converts from the square indices to an actual mesh
    {
        [ReadOnly]
        public NativeArray<byte> SquareIndices;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public SimpleMesh<Vertex,uint> Mesh;

        [ReadOnly]
        public float HalfSize;

        public uint Counter;

        public void Execute(int i) // doesn't work, do the math with i, and the adding of stuff, this is all nonsense!
        {
            byte SquareIndex = SquareIndices[i];

            for (byte vi = 0; vi < VertexTable[SquareIndex].Length; vi++)
            {
                Vertex Vert = new Vertex(); // no uv for now, and no depth for now
                Vert.Pos = new float3(VertexTable[SquareIndex][vi], 0) * HalfSize;
                Mesh.Vertices[i + vi] = Vert;
            }
        }
    }

    public static SimpleMesh<Vertex,uint> BetterTri(NativeArray<byte> SquareIndices, NativeArray<int3> Positions, float HalfWidth)
    {
        uint AmountOfVertices = 0;
        uint AmountOfIndices = 0;

        for (int i = 0; i < SquareIndices.Length; i++)
        {
            AmountOfVertices += (uint)VertexTable[SquareIndices[i]].Length;
            AmountOfIndices += (uint)IndexTable[SquareIndices[i]].Length;
        }

        SimpleMesh<Vertex, uint> Mesh = new SimpleMesh<Vertex, uint>(Allocator.Persistent, AmountOfVertices, Allocator.Persistent, AmountOfIndices);

        int VertIndex = 0;
        uint IndexIndex = 0; // The worst name of any variable ever!

        for (int i = 0; i < SquareIndices.Length; i++)
        {
            byte SquareIndex = SquareIndices[i];
            int2[] Vertices = VertexTable[SquareIndex];
            uint[] Indices = IndexTable[SquareIndex];

            for (int vi = 0; vi < Vertices.Length; vi++)
            {
                Vertex CurrentVertex = new();
                CurrentVertex.Pos = new float3(Vertices[vi], 0) * HalfWidth + Positions[i];
                Mesh.Vertices[VertIndex+vi] = CurrentVertex;
            }

            for (int ii = 0; ii < Indices.Length; ii++)
            {
                Mesh.Indices[ii + (int)IndexIndex] = Indices[ii] + IndexIndex;
            }

            VertIndex += Vertices.Length;
            IndexIndex += (uint)Indices.Length;
        }

        return Mesh;
    }
}