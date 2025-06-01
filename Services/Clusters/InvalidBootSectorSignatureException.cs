using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClustersCopyAndAnalyze.Services.Clusters;

public class InvalidBootSectorSignatureException : Exception
{
    public InvalidBootSectorSignatureException(string? message)
        : base(message) { }
}
