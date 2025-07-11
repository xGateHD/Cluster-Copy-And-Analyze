namespace ClusterAnalyzer;

public readonly struct Fat32Entry
{
    public readonly required uint Value { get; init; }

    /// <summary>
    /// Проверка свободного кластера
    /// </summary>
    public readonly bool IsFreeCluster => (Value & 0x0FFFFFFF) == 0x00000000;
    /// <summary>
    /// Проверка конца цепочки
    /// </summary>
    public readonly bool IsEndOfChain => (Value & 0x0FFFFFFF) >= 0x0FFFFFF8;
    /// <summary>
    /// Проверка на сломаный кластер
    /// </summary>
    public readonly bool IsBadCluster => (Value & 0x0FFFFFFF) == 0x0FFFFFF7;

    public override readonly string ToString()
    {
        if (IsBadCluster) return "Bad Cluster";
        if (IsEndOfChain) return "Last Cluster in chain";
        return Value.ToString();
    }

    public readonly string ToString16() => $"0x{Value:X8}";
}
