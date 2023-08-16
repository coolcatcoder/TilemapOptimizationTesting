using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public struct Chunked2DArray<T> : IDisposable where T : unmanaged
{
    NativeArray<T> FullArray;
    NativeArray<bool> ChunkArray; // not sure

    readonly int ChunkWidth;
    readonly int ChunkWidthLog2;
    readonly int ChunkWidthSquared;
    readonly int ChunkGridWidth;

    readonly int FullWidth;
    readonly int FullWidthSquared; // aka length of FullArray

    // implement square root fast stuff asap

    public Chunked2DArray(int FullWidth, int ChunkWidth, Allocator allocator)
    {
        int IsPowerOf2 = ChunkWidth & (ChunkWidth - 1);

        if (IsPowerOf2 != 0)
        {
            throw new ArgumentException("ChunkWidth is not a power of 2!");
        }

        this.FullWidth = FullWidth;
        FullWidthSquared = FullWidth * FullWidth;
        FullArray = new NativeArray<T>(FullWidthSquared, allocator);

        this.ChunkWidth = ChunkWidth;
        ChunkWidthLog2 = (int)math.log2(ChunkWidth); // fast apparently?
        ChunkWidthSquared = ChunkWidth * ChunkWidth;
        ChunkGridWidth = FullWidth / ChunkWidth;
        ChunkArray = new NativeArray<bool>(ChunkWidthSquared, allocator);
    }

    public T Get(int2 Pos)
    {
        return FullArray[FullIndexFromFullPos(Pos)];
    }

    public unsafe ref T GetRW(int2 Pos)
    {
        return ref UnsafeUtility.ArrayElementAsRef<T>(FullArray.GetUnsafePtr(), FullIndexFromFullPos(Pos));
    }

    public void Set(int2 Pos, T Value)
    {
        FullArray[FullIndexFromFullPos(Pos)] = Value;
    }

    public int FullIndexFromFullPos(int2 FullPos)
    {
        int2 ChunkPos = FullPos >> ChunkWidthLog2;
        int ChunkIndex = IndexFromPos(ChunkPos, ChunkGridWidth);
        int FullIndexStart = ChunkIndex * ChunkWidthSquared;

        int2 LocalPos = FullPos % ChunkWidth;

        int LocalIndex = IndexFromPos(LocalPos, ChunkWidth);

        int BlockIndex = FullIndexStart + LocalIndex;

        return BlockIndex;
    }

    internal static int IndexFromPos(int2 Pos, int GridWidth) // dangerous
    {
        return Pos.y * GridWidth + Pos.x;
    }

    internal static int2 PosFromIndex(int Index, int GridWidth) // dangerous
    {
        return new int2(Index % GridWidth, Index / GridWidth);
    }

    public void Dispose()
    {
        FullArray.Dispose();
        ChunkArray.Dispose();
    }
}