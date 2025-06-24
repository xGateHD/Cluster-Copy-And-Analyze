namespace ClustersCopyAndAnalyze.Services.Clusters;

public class InvalidBootSectorSignatureException : Exception
{
    public InvalidBootSectorSignatureException(string? message)
        : base(message) { }
}
