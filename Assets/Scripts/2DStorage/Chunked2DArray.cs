using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public struct Chunked2DArray<T> : IDisposable where T : unmanaged
{
    public NativeArray<T> FullArray;

    public readonly int ChunkWidth;
    public readonly int ChunkWidthLog2;
    public readonly int ChunkWidthSquared;
    public readonly int ChunkGridWidth;
    public readonly int ChunkGridWidthSquared;

    public readonly int FullGridWidth;
    public readonly int FullGridWidthSquared; // aka length of FullArray

    // implement square root fast stuff asap

    public Chunked2DArray(int FullGridWidth, int ChunkWidth, Allocator allocator)
    {
        int IsPowerOf2 = ChunkWidth & (ChunkWidth - 1);

        if (IsPowerOf2 != 0)
        {
            throw new ArgumentException("ChunkWidth is not a power of 2!");
        }

        this.FullGridWidth = FullGridWidth;
        FullGridWidthSquared = FullGridWidth * FullGridWidth;
        FullArray = new NativeArray<T>(FullGridWidthSquared, allocator);

        this.ChunkWidth = ChunkWidth;
        ChunkWidthLog2 = (int)math.log2(ChunkWidth); // fast apparently?
        ChunkWidthSquared = ChunkWidth * ChunkWidth;
        ChunkGridWidth = FullGridWidth / ChunkWidth;
        ChunkGridWidthSquared = ChunkGridWidth * ChunkGridWidth;
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
        int ChunkIndex = StorageMethods.IndexFromPos(ChunkPos, ChunkGridWidth);
        int FullIndexStart = ChunkIndex * ChunkWidthSquared;

        int2 LocalPos = FullPos % ChunkWidth;

        int LocalIndex = StorageMethods.IndexFromPos(LocalPos, ChunkWidth);

        int BlockIndex = FullIndexStart + LocalIndex;

        return BlockIndex;
    }

    public int FullIndexFromChunkIndex(int ChunkIndex)
    {
        return ChunkIndex * ChunkWidthSquared;
    }

    public int2 FullPosFromChunkPos(int2 ChunkPos)
    {
        return ChunkPos * ChunkWidth;
    }

    public int2 ChunkPosFromFullPos(int2 ChunkPos)
    {
        return ChunkPos / ChunkWidth;
    }

    public void Dispose()
    {
        FullArray.Dispose();
    }
}