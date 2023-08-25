using Unity.Mathematics;

public struct StorageMethods
{
    public static int IndexFromPos(int2 Pos, int GridWidth) // dangerous
    {
        return Pos.y * GridWidth + Pos.x;
    }

    public static int2 PosFromIndex(int Index, int GridWidth) // dangerous
    {
        return new int2(Index % GridWidth, Index / GridWidth);
    }
}