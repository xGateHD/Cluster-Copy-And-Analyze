using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClustersCopyAndAnalyze.Services.Clusters;

public struct Fat32Entry
{
    public ulong Value;

    public Fat32Entry(ulong value)
    {
        Value = value;
    }


    public readonly bool IsEndOfChain => (Value & 0x0FFFFFFF) >= 0x0FFFFFF8; // Проверка конца цепочки
    public readonly bool IsBadCluster => (Value & 0x0FFFFFFF) == 0x0FFFFFF7;

    public readonly override string ToString() => Value.ToString();
    public readonly string ToString16() => $"0x{Value:X8}";
}
