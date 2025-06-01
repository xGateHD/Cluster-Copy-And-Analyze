using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClustersCopyAndAnalyze.Services.Clusters;

public class ClusterData
{
    public Fat32Entry CurrentCluster;
    public Fat32Entry NextCluster;
}
