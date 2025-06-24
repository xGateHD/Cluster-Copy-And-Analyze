namespace ClustersCopyAndAnalyze.Services.Clusters;

public struct Fat32Entry
{
    public uint Value;

    public Fat32Entry(uint value)
    {
        Value = value;
    }


    public readonly bool IsEndOfChain => (Value & 0x0FFFFFFF) >= 0x0FFFFFF8; // Проверка конца цепочки
    public readonly bool IsBadCluster => (Value & 0x0FFFFFFF) == 0x0FFFFFF7;

    public override string ToString()
    {
        if (IsBadCluster) return "Bad Cluster";
        if (IsEndOfChain) return "Last Cluster in chain";
        return Value.ToString();
    }

    public readonly string ToString16() => $"0x{Value:X8}";
}
